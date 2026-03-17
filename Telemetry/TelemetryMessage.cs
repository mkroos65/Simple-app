// =============================================================================
// Telemetry/TelemetryMessage.cs — Envelope for event-based WebSocket messages
// =============================================================================

using System.Text.Json.Serialization;

namespace MSFSCompanionBridge.Telemetry;

/// <summary>
/// Generic wrapper that adds a "type" and "timestamp" field to every
/// WebSocket message, enabling event-based messaging.
/// </summary>
public sealed class TelemetryMessage<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    public static TelemetryMessage<T> Create(string type, T data)
    {
        return new TelemetryMessage<T>
        {
            Type = type,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}

/// <summary>Well-known message type constants.</summary>
public static class MessageTypes
{
    public const string AircraftUpdate  = "aircraft:update";
    public const string AutopilotUpdate = "autopilot:update";
    public const string TrafficUpdate   = "traffic:update";
    public const string FlightPlanUpdate = "flightplan:update";

    /// <summary>Flat telemetry type used by simpleflightplanner.com protocol.</summary>
    public const string PlannerTelemetry = "telemetry";
}

/// <summary>
/// Flat telemetry message matching the simpleflightplanner.com WebSocket protocol.
/// Sent at ~1 Hz so the planner can display a live aircraft marker.
/// </summary>
public sealed class PlannerTelemetryMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = MessageTypes.PlannerTelemetry;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; }

    [JsonPropertyName("heading")]
    public double Heading { get; set; }

    [JsonPropertyName("airspeed")]
    public double Airspeed { get; set; }

    [JsonPropertyName("groundspeed")]
    public double Groundspeed { get; set; }

    [JsonPropertyName("verticalSpeed")]
    public double VerticalSpeed { get; set; }

    [JsonPropertyName("onGround")]
    public bool OnGround { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    public static PlannerTelemetryMessage FromAircraftState(AircraftState state)
    {
        return new PlannerTelemetryMessage
        {
            Lat           = state.Lat,
            Lng           = state.Lng,
            Altitude      = state.Altitude,
            Heading       = state.Heading,
            Airspeed      = state.Airspeed,
            Groundspeed   = state.Groundspeed,
            VerticalSpeed = state.VerticalSpeed,
            OnGround      = state.OnGround,
            Timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
