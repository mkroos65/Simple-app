// =============================================================================
// Services/FlightPlanTracker.cs — Computes ETA per waypoint, fuel predictions,
//                                   and route deviation alerts
// =============================================================================

using SimpleFlightTracker.Telemetry;

namespace SimpleFlightTracker.Services;

/// <summary>
/// Tracks progress along an active flight plan. Computes:
/// - ETA per waypoint (based on groundspeed and cumulative distance)
/// - Fuel remaining prediction per waypoint (based on fuel flow rate)
/// - Cross-track deviation from the planned route with alerts
/// </summary>
public sealed class FlightPlanTracker
{
    private FlightPlan? _activePlan;
    private int _activeWaypointIndex;
    private FlightPlanProgress? _latestProgress;

    private const double EarthRadiusNm = 3440.065;

    // Deviation thresholds in nautical miles
    private const double WarningDeviationNm = 2.0;
    private const double CriticalDeviationNm = 5.0;

    // Minimum groundspeed to compute ETA (avoids division by near-zero)
    private const double MinGroundspeedKts = 5.0;

    // Distance threshold to consider a waypoint "passed" (NM)
    private const double WaypointPassedThresholdNm = 2.0;

    private readonly object _lock = new();

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// Sets the active flight plan and resets tracking state.
    /// </summary>
    public void SetFlightPlan(FlightPlan plan)
    {
        lock (_lock)
        {
            _activePlan = plan;
            _activeWaypointIndex = 0;
            _latestProgress = null;
            Log?.Invoke($"FlightPlanTracker: tracking {plan.Departure} -> {plan.Arrival} ({plan.Waypoints.Count} waypoints)");
        }
    }

    /// <summary>
    /// Returns the latest computed progress, or null if no flight plan is active.
    /// </summary>
    public FlightPlanProgress? LatestProgress
    {
        get
        {
            lock (_lock)
            {
                return _latestProgress;
            }
        }
    }

    /// <summary>
    /// Returns true if a flight plan is currently being tracked.
    /// </summary>
    public bool HasActivePlan
    {
        get
        {
            lock (_lock)
            {
                return _activePlan is not null;
            }
        }
    }

    /// <summary>
    /// Updates progress calculations based on the current aircraft and fuel state.
    /// Called on each telemetry tick (~1 Hz from the engine).
    /// </summary>
    public FlightPlanProgress? Update(AircraftState aircraft, FuelState? fuel)
    {
        lock (_lock)
        {
            if (_activePlan is null || _activePlan.Waypoints.Count == 0)
                return null;

            var waypoints = _activePlan.Waypoints;
            var progress = new FlightPlanProgress
            {
                Departure = _activePlan.Departure,
                Arrival = _activePlan.Arrival
            };

            // Advance the active waypoint index if we've passed it
            AdvanceActiveWaypoint(aircraft, waypoints);

            progress.ActiveWaypointIndex = _activeWaypointIndex;

            // Compute per-waypoint status
            double cumulativeDistance = 0.0;
            double prevLat = aircraft.Lat;
            double prevLng = aircraft.Lng;

            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                var status = new WaypointStatus
                {
                    Name = wp.Name,
                    Lat = wp.Lat,
                    Lng = wp.Lng,
                    Index = i,
                    Passed = i < _activeWaypointIndex
                };

                if (i < _activeWaypointIndex)
                {
                    // Already passed
                    status.DistanceNm = 0;
                    status.CumulativeDistanceNm = 0;
                    status.EtaSeconds = 0;
                    status.EtaUtc = "";
                    status.FuelRemainingGallons = 0;
                    status.FuelRemainingPercent = 0;
                }
                else
                {
                    double legDistance;
                    if (i == _activeWaypointIndex)
                    {
                        // Distance from current position to this waypoint
                        legDistance = HaversineDistanceNm(
                            aircraft.Lat, aircraft.Lng, wp.Lat, wp.Lng);
                    }
                    else
                    {
                        // Distance from previous waypoint to this one
                        legDistance = HaversineDistanceNm(
                            prevLat, prevLng, wp.Lat, wp.Lng);
                    }

                    cumulativeDistance += legDistance;

                    status.DistanceNm = Math.Round(legDistance, 1);
                    status.CumulativeDistanceNm = Math.Round(cumulativeDistance, 1);

                    // ETA calculation
                    if (aircraft.Groundspeed >= MinGroundspeedKts)
                    {
                        double hoursToWaypoint = cumulativeDistance / aircraft.Groundspeed;
                        double secondsToWaypoint = hoursToWaypoint * 3600.0;
                        status.EtaSeconds = Math.Round(secondsToWaypoint, 0);

                        var etaTime = DateTimeOffset.UtcNow.AddSeconds(secondsToWaypoint);
                        status.EtaUtc = etaTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

                        // Fuel prediction
                        if (fuel is not null && fuel.FuelFlowGPH > 0)
                        {
                            double fuelBurned = fuel.FuelFlowGPH * hoursToWaypoint;
                            double fuelRemaining = Math.Max(0, fuel.FuelQuantityGallons - fuelBurned);
                            status.FuelRemainingGallons = Math.Round(fuelRemaining, 2);
                            status.FuelRemainingPercent = fuel.FuelCapacityGallons > 0
                                ? Math.Round((fuelRemaining / fuel.FuelCapacityGallons) * 100.0, 1)
                                : 0.0;
                        }
                    }
                }

                progress.Waypoints.Add(status);

                // Track previous waypoint position for leg distance calc
                prevLat = wp.Lat;
                prevLng = wp.Lng;
            }

            // Total remaining distance and ETA
            progress.TotalDistanceRemainingNm = Math.Round(cumulativeDistance, 1);
            if (aircraft.Groundspeed >= MinGroundspeedKts)
            {
                double totalHours = cumulativeDistance / aircraft.Groundspeed;
                progress.TotalEtaSeconds = Math.Round(totalHours * 3600.0, 0);
                progress.TotalEtaUtc = DateTimeOffset.UtcNow
                    .AddSeconds(totalHours * 3600.0)
                    .ToString("yyyy-MM-ddTHH:mm:ssZ");
            }

            // Deviation calculation
            progress.Deviation = ComputeDeviation(aircraft, waypoints);

            _latestProgress = progress;
            return progress;
        }
    }

    // -----------------------------------------------------------------
    // Waypoint advancement
    // -----------------------------------------------------------------

    private void AdvanceActiveWaypoint(AircraftState aircraft, List<FlightPlanWaypoint> waypoints)
    {
        while (_activeWaypointIndex < waypoints.Count)
        {
            var wp = waypoints[_activeWaypointIndex];
            double dist = HaversineDistanceNm(aircraft.Lat, aircraft.Lng, wp.Lat, wp.Lng);

            if (dist <= WaypointPassedThresholdNm)
            {
                Log?.Invoke($"FlightPlanTracker: passed waypoint {wp.Name} ({dist:F1} NM)");
                _activeWaypointIndex++;
            }
            else
            {
                break;
            }
        }
    }

    // -----------------------------------------------------------------
    // Deviation calculation
    // -----------------------------------------------------------------

    private DeviationAlert ComputeDeviation(AircraftState aircraft, List<FlightPlanWaypoint> waypoints)
    {
        var alert = new DeviationAlert();

        if (_activeWaypointIndex >= waypoints.Count)
        {
            alert.Severity = "none";
            alert.Message = "Flight plan complete";
            return alert;
        }

        var nextWp = waypoints[_activeWaypointIndex];
        alert.NextWaypoint = nextWp.Name;
        alert.BearingToNext = Math.Round(
            BearingDegrees(aircraft.Lat, aircraft.Lng, nextWp.Lat, nextWp.Lng), 1);

        // Compute cross-track distance from the active leg
        double crossTrackNm;
        if (_activeWaypointIndex > 0)
        {
            var prevWp = waypoints[_activeWaypointIndex - 1];
            crossTrackNm = CrossTrackDistanceNm(
                aircraft.Lat, aircraft.Lng,
                prevWp.Lat, prevWp.Lng,
                nextWp.Lat, nextWp.Lng);
        }
        else
        {
            // No previous waypoint — use direct distance to the line from
            // current position to next waypoint (deviation is just lateral offset)
            crossTrackNm = 0.0;
        }

        alert.DeviationNm = Math.Round(Math.Abs(crossTrackNm), 2);

        if (alert.DeviationNm >= CriticalDeviationNm)
        {
            alert.Severity = "critical";
            alert.Message = $"CRITICAL: {alert.DeviationNm:F1} NM off course toward {nextWp.Name}";
        }
        else if (alert.DeviationNm >= WarningDeviationNm)
        {
            alert.Severity = "warning";
            alert.Message = $"WARNING: {alert.DeviationNm:F1} NM off course toward {nextWp.Name}";
        }
        else
        {
            alert.Severity = "none";
            alert.Message = "On course";
        }

        return alert;
    }

    // -----------------------------------------------------------------
    // Geodesic helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// Haversine distance between two points in nautical miles.
    /// </summary>
    private static double HaversineDistanceNm(double lat1, double lng1, double lat2, double lng2)
    {
        double lat1Rad = DegreesToRadians(lat1);
        double lat2Rad = DegreesToRadians(lat2);
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLng = DegreesToRadians(lng2 - lng1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusNm * c;
    }

    /// <summary>
    /// Cross-track distance from a point to a great circle defined by two points.
    /// Returns signed distance (positive = right of track, negative = left).
    /// </summary>
    private static double CrossTrackDistanceNm(
        double lat, double lng,
        double lat1, double lng1,
        double lat2, double lng2)
    {
        double d13 = HaversineDistanceNm(lat1, lng1, lat, lng) / EarthRadiusNm;
        double bearing13 = DegreesToRadians(BearingDegrees(lat1, lng1, lat, lng));
        double bearing12 = DegreesToRadians(BearingDegrees(lat1, lng1, lat2, lng2));

        return Math.Asin(Math.Sin(d13) * Math.Sin(bearing13 - bearing12)) * EarthRadiusNm;
    }

    /// <summary>
    /// Initial bearing from point 1 to point 2 in degrees (0-360).
    /// </summary>
    private static double BearingDegrees(double lat1, double lng1, double lat2, double lng2)
    {
        double lat1Rad = DegreesToRadians(lat1);
        double lat2Rad = DegreesToRadians(lat2);
        double dLng = DegreesToRadians(lng2 - lng1);

        double y = Math.Sin(dLng) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                   Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLng);

        double bearing = Math.Atan2(y, x);
        return (RadiansToDegrees(bearing) + 360.0) % 360.0;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
