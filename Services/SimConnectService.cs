// =============================================================================
// Services/SimConnectService.cs — SimConnect integration with multi-definition
//                                  support for aircraft, autopilot, and traffic
// =============================================================================

using Microsoft.FlightSimulator.SimConnect;
using MSFSCompanionBridge.Telemetry;

namespace MSFSCompanionBridge.Services;

/// <summary>
/// Identifiers for SimConnect data definitions.
/// </summary>
public enum SimDefinition
{
    Aircraft,
    Autopilot,
    Traffic
}

/// <summary>
/// Identifiers for SimConnect data requests.
/// </summary>
public enum SimRequest
{
    Aircraft,
    Autopilot,
    Traffic
}

/// <summary>
/// Manages the SimConnect lifecycle: connection, data subscriptions for
/// aircraft / autopilot / traffic, receive loop, automatic reconnect,
/// and graceful shutdown.
/// </summary>
public sealed class SimConnectService : IDisposable
{
    private SimConnect? _simConnect;
    private bool _connected;
    private bool _disposed;

    private int _autopilotTickCounter;
    private int _trafficTickCounter;

    // Aircraft: every tick (10 Hz at 100 ms interval)
    // Autopilot: every 5 ticks (2 Hz)
    // Traffic: every 10 ticks (1 Hz)
    private const int AutopilotTickInterval = 5;
    private const int TrafficTickInterval = 10;

    /// <summary>Raised when new aircraft state is received.</summary>
    public event Action<AircraftState>? AircraftStateReceived;

    /// <summary>Raised when new autopilot state is received.</summary>
    public event Action<AutopilotState>? AutopilotStateReceived;

    /// <summary>Raised when traffic data is received.</summary>
    public event Action<TrafficAircraft>? TrafficAircraftReceived;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------

    /// <summary>
    /// Starts the connection + polling loop. Keeps retrying until cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_connected)
            {
                TryConnect();
            }

            if (_connected)
            {
                PollMessages();
            }

            try
            {
                await Task.Delay(Config.TelemetryIntervalMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Connection management
    // ---------------------------------------------------------------------

    private void TryConnect()
    {
        try
        {
            Emit("Connecting to SimConnect...");

            _simConnect = new SimConnect(
                Config.SimConnectAppName,
                IntPtr.Zero,
                0x0402,       // WM_USER_SIMCONNECT
                null,
                0);

            _simConnect.OnRecvOpen += OnRecvOpen;
            _simConnect.OnRecvQuit += OnRecvQuit;
            _simConnect.OnRecvException += OnRecvException;
            _simConnect.OnRecvSimobjectDataBytype += OnRecvSimobjectDataBytype;

            RegisterAircraftDefinition();
            RegisterAutopilotDefinition();
            RegisterTrafficDefinition();

            _connected = true;
        }
        catch (Exception ex)
        {
            Emit($"SimConnect connection failed: {ex.Message}");
            Emit($"Retrying in {Config.ReconnectDelayMs / 1000} seconds...");
            CleanupConnection();
            Thread.Sleep(Config.ReconnectDelayMs);
        }
    }

    // ---------------------------------------------------------------------
    // Data definition registration
    // ---------------------------------------------------------------------

    private void RegisterAircraftDefinition()
    {
        if (_simConnect is null) return;

        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "PLANE LATITUDE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "PLANE LONGITUDE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "PLANE ALTITUDE", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "PLANE HEADING DEGREES TRUE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "PLANE PITCH DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "PLANE BANK DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "GROUND VELOCITY", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "AIRSPEED INDICATED", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "VERTICAL SPEED", "feet per minute",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<AircraftStateStruct>(SimDefinition.Aircraft);
    }

    private void RegisterAutopilotDefinition()
    {
        if (_simConnect is null) return;

        _simConnect.AddToDataDefinition(SimDefinition.Autopilot,
            "AUTOPILOT MASTER", "bool",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Autopilot,
            "AUTOPILOT ALTITUDE LOCK", "bool",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Autopilot,
            "AUTOPILOT HEADING LOCK", "bool",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Autopilot,
            "AUTOPILOT HEADING LOCK DIR", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Autopilot,
            "AUTOPILOT ALTITUDE LOCK VAR", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<AutopilotStateStruct>(SimDefinition.Autopilot);
    }

    private void RegisterTrafficDefinition()
    {
        if (_simConnect is null) return;

        _simConnect.AddToDataDefinition(SimDefinition.Traffic,
            "TITLE", null,
            SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Traffic,
            "PLANE LATITUDE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Traffic,
            "PLANE LONGITUDE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Traffic,
            "PLANE ALTITUDE", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Traffic,
            "PLANE HEADING DEGREES TRUE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Traffic,
            "GROUND VELOCITY", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<TrafficAircraftStruct>(SimDefinition.Traffic);
    }

    // ---------------------------------------------------------------------
    // Polling — issues requests at different rates per data type
    // ---------------------------------------------------------------------

    private void PollMessages()
    {
        if (_simConnect is null) return;

        try
        {
            _simConnect.ReceiveMessage();

            // Aircraft: every tick (10 Hz)
            _simConnect.RequestDataOnSimObjectType(
                SimRequest.Aircraft,
                SimDefinition.Aircraft,
                0,
                SIMCONNECT_SIMOBJECT_TYPE.USER);

            // Autopilot: every 5 ticks (2 Hz)
            _autopilotTickCounter++;
            if (_autopilotTickCounter >= AutopilotTickInterval)
            {
                _autopilotTickCounter = 0;
                _simConnect.RequestDataOnSimObjectType(
                    SimRequest.Autopilot,
                    SimDefinition.Autopilot,
                    0,
                    SIMCONNECT_SIMOBJECT_TYPE.USER);
            }

            // Traffic: every 10 ticks (1 Hz)
            _trafficTickCounter++;
            if (_trafficTickCounter >= TrafficTickInterval)
            {
                _trafficTickCounter = 0;
                _simConnect.RequestDataOnSimObjectType(
                    SimRequest.Traffic,
                    SimDefinition.Traffic,
                    200000,  // 200 km radius for nearby aircraft
                    SIMCONNECT_SIMOBJECT_TYPE.AIRCRAFT);
            }
        }
        catch (Exception ex)
        {
            Emit($"SimConnect error during poll: {ex.Message}");
            HandleDisconnect();
        }
    }

    // ---------------------------------------------------------------------
    // SimConnect event handlers
    // ---------------------------------------------------------------------

    private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Emit("Connected to MSFS");
    }

    private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV_QUIT data)
    {
        Emit("MSFS has closed");
        HandleDisconnect();
    }

    private void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        Emit($"SimConnect exception received: {data.dwException}");
    }

    private void OnRecvSimobjectDataBytype(
        SimConnect sender,
        SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
    {
        try
        {
            var requestId = (SimRequest)data.dwRequestID;

            switch (requestId)
            {
                case SimRequest.Aircraft when data.dwData is AircraftStateStruct aircraftStruct:
                    AircraftStateReceived?.Invoke(AircraftState.FromStruct(aircraftStruct));
                    break;

                case SimRequest.Autopilot when data.dwData is AutopilotStateStruct apStruct:
                    AutopilotStateReceived?.Invoke(AutopilotState.FromStruct(apStruct));
                    break;

                case SimRequest.Traffic when data.dwData is TrafficAircraftStruct trafficStruct:
                    TrafficAircraftReceived?.Invoke(TrafficAircraft.FromStruct(trafficStruct));
                    break;
            }
        }
        catch (Exception ex)
        {
            Emit($"Error processing telemetry: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void HandleDisconnect()
    {
        _connected = false;
        CleanupConnection();
        Emit($"Disconnected. Will retry in {Config.ReconnectDelayMs / 1000} seconds...");
    }

    private void CleanupConnection()
    {
        if (_simConnect is not null)
        {
            try { _simConnect.Dispose(); } catch { /* best-effort */ }
            _simConnect = null;
        }
        _connected = false;
    }

    private void Emit(string message)
    {
        Log?.Invoke(message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupConnection();
    }
}
