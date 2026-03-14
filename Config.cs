// =============================================================================
// Config.cs — Application-wide configuration constants
// =============================================================================

namespace MSFSCompanionBridge;

/// <summary>
/// Central configuration for the MSFS Companion Bridge application.
/// </summary>
public static class Config
{
    /// <summary>WebSocket server host address.</summary>
    public const string WebSocketHost = "0.0.0.0";

    /// <summary>WebSocket server port.</summary>
    public const int WebSocketPort = 8765;

    /// <summary>Full WebSocket server URI used by Fleck.</summary>
    public static string WebSocketUri => $"ws://{WebSocketHost}:{WebSocketPort}";

    /// <summary>
    /// Interval in milliseconds between SimConnect data requests.
    /// 100 ms = 10 updates per second.
    /// </summary>
    public const int TelemetryIntervalMs = 100;

    /// <summary>
    /// Delay in milliseconds before retrying a SimConnect connection after failure.
    /// </summary>
    public const int ReconnectDelayMs = 5000;

    /// <summary>Display name passed to the SimConnect Open call.</summary>
    public const string SimConnectAppName = "MSFS Companion Bridge";
}
