// =============================================================================
// Telemetry/FlightPlan.cs — Flight plan model
// =============================================================================

using System.Text.Json.Serialization;

namespace MSFSCompanionBridge.Telemetry;

/// <summary>
/// Represents a single waypoint in a flight plan.
/// </summary>
public sealed class FlightPlanWaypoint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; }
}

/// <summary>
/// JSON-serialisable flight plan payload.
/// </summary>
public sealed class FlightPlan
{
    [JsonPropertyName("waypoints")]
    public List<FlightPlanWaypoint> Waypoints { get; set; } = new();

    [JsonPropertyName("activeLegIndex")]
    public int ActiveLegIndex { get; set; }
}
