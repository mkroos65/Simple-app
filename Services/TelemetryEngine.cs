// =============================================================================
// Services/TelemetryEngine.cs — Central telemetry hub: caches state, emits
//                                 events to WebSocket clients, feeds REST API
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using SimpleFlightTracker.Telemetry;

namespace SimpleFlightTracker.Services;

/// <summary>
/// Collects data from <see cref="SimConnectService"/>, caches the latest
/// state for each data source, and emits structured messages to subscribers
/// (WebSocket server, REST API).
///
/// Also emits flat "telemetry" messages at ~1 Hz matching the
/// simpleflightplanner.com WebSocket protocol.
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

    // Planner telemetry throttle: emit at ~1 Hz (every ~10 aircraft ticks)
    private int _plannerTickCounter;
    private const int PlannerTickInterval = 10;  // 10 ticks × 100 ms = 1 Hz

    /// <summary>
    /// Raised whenever a structured telemetry message is ready to broadcast.
    /// The string payload is a pre-serialised JSON message.
    /// </summary>
    public event Action<string>? MessageReady;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// Raised when a flight plan is received from the web planner.
    /// </summary>
    public event Action<FlightPlan>? FlightPlanReceived;

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

        // Emit the flat "telemetry" message for the planner at ~1 Hz
        _plannerTickCounter++;
        if (_plannerTickCounter >= PlannerTickInterval)
        {
            _plannerTickCounter = 0;
            EmitPlannerTelemetry(state);
        }
    }

    private void OnAutopilotState(AutopilotState state)
    {
        _latestAutopilot = state;
    }

    private void OnTrafficAircraft(TrafficAircraft aircraft)
    {
        lock (_trafficLock)
        {
            _trafficBuffer.Add(aircraft);
        }

        FlushTrafficBuffer();
    }

    /// <summary>
    /// Publishes the buffered traffic list and resets the buffer.
    /// </summary>
    private void FlushTrafficBuffer()
    {
        lock (_trafficLock)
        {
            if (_trafficBuffer.Count == 0) return;
            _latestTraffic = new List<TrafficAircraft>(_trafficBuffer);
            _trafficBuffer = new List<TrafficAircraft>();
        }
    }

    // ---------------------------------------------------------------------
    // Planner telemetry (flat JSON, ~1 Hz)
    // ---------------------------------------------------------------------

    private void EmitPlannerTelemetry(AircraftState state)
    {
        try
        {
            var msg = PlannerTelemetryMessage.FromAircraftState(state);
            var json = JsonSerializer.Serialize(msg);
            MessageReady?.Invoke(json);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Error serialising planner telemetry: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------
    // Incoming messages from WebSocket clients
    // ---------------------------------------------------------------------

    /// <summary>
    /// Processes an incoming WebSocket message from a client (e.g. flight plan).
    /// </summary>
    public void HandleIncomingMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var type = typeProp.GetString();

            switch (type)
            {
                case "flightplan":
                    var plan = JsonSerializer.Deserialize<FlightPlan>(json);
                    if (plan is not null)
                    {
                        Log?.Invoke($"Flight plan received: {plan.Departure} -> {plan.Arrival} ({plan.Waypoints.Count} waypoints)");
                        FlightPlanReceived?.Invoke(plan);
                    }
                    break;

                default:
                    Log?.Invoke($"Unknown incoming message type: {type}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            Log?.Invoke($"Error parsing incoming message: {ex.Message}");
        }
    }
}
