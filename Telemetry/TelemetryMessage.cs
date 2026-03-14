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
}
