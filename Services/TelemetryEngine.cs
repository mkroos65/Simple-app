// =============================================================================
// Services/TelemetryEngine.cs — Central telemetry hub: caches state, emits
//                                 events to WebSocket clients, feeds REST API
// =============================================================================

using System.Collections.Concurrent;
using MSFSCompanionBridge.Telemetry;

namespace MSFSCompanionBridge.Services;

/// <summary>
/// Collects data from <see cref="SimConnectService"/>, caches the latest
/// state for each data source, and emits structured messages to subscribers
/// (WebSocket server, REST API).
/// </summary>
public sealed class TelemetryEngine
{
    private readonly SimConnectService _simConnect;
    private readonly object _trafficLock = new();

    // Cached latest state — read by the REST API
    private AircraftState? _latestAircraft;
    private AutopilotState? _latestAutopilot;
    private List<TrafficAircraft> _latestTraffic = new();
    private List<TrafficAircraft> _trafficBuffer = new();

    /// <summary>
    /// Raised whenever a structured telemetry message is ready to broadcast.
    /// The string payload is a pre-serialised JSON message.
    /// </summary>
    public event Action<string>? MessageReady;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    public TelemetryEngine(SimConnectService simConnect)
    {
        _simConnect = simConnect;

        _simConnect.AircraftStateReceived += OnAircraftState;
        _simConnect.AutopilotStateReceived += OnAutopilotState;
        _simConnect.TrafficAircraftReceived += OnTrafficAircraft;
    }

    // ---------------------------------------------------------------------
    // Cached state accessors (used by REST API)
    // ---------------------------------------------------------------------

    public AircraftState? LatestAircraft => _latestAircraft;
    public AutopilotState? LatestAutopilot => _latestAutopilot;

    public List<TrafficAircraft> LatestTraffic
    {
        get
        {
            lock (_trafficLock)
            {
                return new List<TrafficAircraft>(_latestTraffic);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Event handlers from SimConnectService
    // ---------------------------------------------------------------------

    private void OnAircraftState(AircraftState state)
    {
        _latestAircraft = state;

        var message = TelemetryMessage<AircraftState>.Create(
            MessageTypes.AircraftUpdate, state);
        EmitMessage(message);
    }

    private void OnAutopilotState(AutopilotState state)
    {
        _latestAutopilot = state;

        var message = TelemetryMessage<AutopilotState>.Create(
            MessageTypes.AutopilotUpdate, state);
        EmitMessage(message);
    }

    private void OnTrafficAircraft(TrafficAircraft aircraft)
    {
        // Traffic arrives one aircraft at a time per request cycle.
        // We buffer them and flush on next aircraft update (which runs at
        // a higher rate). A simpler approach: just add to the list and
        // emit the full list each time a traffic response completes.
        lock (_trafficLock)
        {
            _trafficBuffer.Add(aircraft);
        }

        // Flush the buffer periodically via the dedicated method
        FlushTrafficBuffer();
    }

    /// <summary>
    /// Publishes the buffered traffic list and resets the buffer.
    /// Called after each traffic polling cycle.
    /// </summary>
    private void FlushTrafficBuffer()
    {
        List<TrafficAircraft> snapshot;
        lock (_trafficLock)
        {
            if (_trafficBuffer.Count == 0) return;
            snapshot = new List<TrafficAircraft>(_trafficBuffer);
            _latestTraffic = snapshot;
            _trafficBuffer = new List<TrafficAircraft>();
        }

        var message = TelemetryMessage<List<TrafficAircraft>>.Create(
            MessageTypes.TrafficUpdate, snapshot);
        EmitMessage(message);
    }

    private void EmitMessage<T>(TelemetryMessage<T> message)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(message);
            MessageReady?.Invoke(json);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Error serialising telemetry message: {ex.Message}");
        }
    }
}
