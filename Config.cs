// =============================================================================
// Config.cs — Application-wide configuration constants
// =============================================================================

namespace SimpleFlightTracker;

/// <summary>
/// Central configuration for the Simple Flight Tracker application.
/// </summary>
public static class Config
{
    /// <summary>WebSocket server host address.</summary>
    public const string WebSocketHost = "0.0.0.0";

    /// <summary>WebSocket server port (matches simpleflightplanner.com protocol).</summary>
    public const int WebSocketPort = 29112;

    /// <summary>Full WebSocket server URI used by Fleck.</summary>
    public static string WebSocketUri => $"ws://{WebSocketHost}:{WebSocketPort}";

    /// <summary>REST API server port.</summary>
    public const int ApiPort = 5555;

    /// <summary>
    /// Base polling interval in milliseconds.
    /// 100 ms = 10 Hz for aircraft data.
    /// Autopilot and traffic use multiples of this interval.
    /// </summary>
    public const int TelemetryIntervalMs = 100;

    /// <summary>
    /// Delay in milliseconds before retrying a SimConnect connection after failure.
    /// </summary>
    public const int ReconnectDelayMs = 5000;

    /// <summary>Display name passed to the SimConnect Open call.</summary>
    public const string SimConnectAppName = "Simple Flight Tracker";
}
