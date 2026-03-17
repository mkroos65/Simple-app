// =============================================================================
// Telemetry/FlightPlan.cs — Flight plan model (matches simpleflightplanner.com)
// =============================================================================

using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Represents a single waypoint in a flight plan received from the planner.
/// </summary>
public sealed class FlightPlanWaypoint
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

/// <summary>
/// Flight plan received from the web planner via WebSocket.
/// Protocol: { "type": "flightplan", "departure": "KSEA", "arrival": "KPDX",
///             "waypoints": [...], "cruisingAltitude": 8000 }
/// </summary>
public sealed class FlightPlan
{
    [JsonPropertyName("departure")]
    public string Departure { get; set; } = string.Empty;

    [JsonPropertyName("arrival")]
    public string Arrival { get; set; } = string.Empty;

    [JsonPropertyName("waypoints")]
    public List<FlightPlanWaypoint> Waypoints { get; set; } = new();

    [JsonPropertyName("cruisingAltitude")]
    public double CruisingAltitude { get; set; }
}
