// =============================================================================
// Telemetry/WaypointStatus.cs — Per-waypoint ETA and fuel prediction model
// =============================================================================

using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Computed status for a single waypoint: ETA and predicted fuel remaining.
/// </summary>
public sealed class WaypointStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    /// <summary>Distance from current position in nautical miles.</summary>
    [JsonPropertyName("distanceNm")]
    public double DistanceNm { get; set; }

    /// <summary>Cumulative distance along the route from current position in NM.</summary>
    [JsonPropertyName("cumulativeDistanceNm")]
    public double CumulativeDistanceNm { get; set; }

    /// <summary>Estimated time to reach this waypoint in seconds.</summary>
    [JsonPropertyName("etaSeconds")]
    public double EtaSeconds { get; set; }

    /// <summary>Estimated time of arrival as UTC ISO 8601 string.</summary>
    [JsonPropertyName("etaUtc")]
    public string EtaUtc { get; set; } = string.Empty;

    /// <summary>Predicted fuel remaining at this waypoint in gallons.</summary>
    [JsonPropertyName("fuelRemainingGallons")]
    public double FuelRemainingGallons { get; set; }

    /// <summary>Predicted fuel remaining at this waypoint as percent of capacity.</summary>
    [JsonPropertyName("fuelRemainingPercent")]
    public double FuelRemainingPercent { get; set; }

    /// <summary>Whether this waypoint has already been passed.</summary>
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    /// <summary>Index of this waypoint in the flight plan.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }
}

/// <summary>
/// Alert raised when the aircraft deviates from the planned route.
/// </summary>
public sealed class DeviationAlert
{
    /// <summary>Cross-track distance from the planned route in nautical miles.</summary>
    [JsonPropertyName("deviationNm")]
    public double DeviationNm { get; set; }

    /// <summary>Severity: "none", "warning", "critical".</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "none";

    /// <summary>Human-readable description of the deviation.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Name of the waypoint the aircraft is currently heading toward.</summary>
    [JsonPropertyName("nextWaypoint")]
    public string NextWaypoint { get; set; } = string.Empty;

    /// <summary>Bearing to the next waypoint in degrees true.</summary>
    [JsonPropertyName("bearingToNext")]
    public double BearingToNext { get; set; }
}

/// <summary>
/// Combined flight plan progress message with per-waypoint ETA,
/// fuel predictions, and deviation status.
/// </summary>
public sealed class FlightPlanProgress
{
    [JsonPropertyName("departure")]
    public string Departure { get; set; } = string.Empty;

    [JsonPropertyName("arrival")]
    public string Arrival { get; set; } = string.Empty;

    [JsonPropertyName("activeWaypointIndex")]
    public int ActiveWaypointIndex { get; set; }

    [JsonPropertyName("waypoints")]
    public List<WaypointStatus> Waypoints { get; set; } = new();

    [JsonPropertyName("deviation")]
    public DeviationAlert Deviation { get; set; } = new();

    [JsonPropertyName("totalDistanceRemainingNm")]
    public double TotalDistanceRemainingNm { get; set; }

    [JsonPropertyName("totalEtaSeconds")]
    public double TotalEtaSeconds { get; set; }

    [JsonPropertyName("totalEtaUtc")]
    public string TotalEtaUtc { get; set; } = string.Empty;
}
