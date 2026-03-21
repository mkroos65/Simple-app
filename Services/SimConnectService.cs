// =============================================================================
// Services/SimConnectService.cs — SimConnect integration with multi-definition
//                                  support for aircraft, autopilot, and traffic
// =============================================================================

using System.Runtime.CompilerServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleFlightTracker.Telemetry;

namespace SimpleFlightTracker.Services;

/// <summary>
/// Identifiers for SimConnect data definitions.
/// </summary>
public enum SimDefinition
{
    Aircraft,
    Autopilot,
    Traffic,
    Fuel
}

/// <summary>
/// Identifiers for SimConnect data requests.
/// </summary>
public enum SimRequest
{
    Aircraft,
    Autopilot,
    Traffic,
    Fuel
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
    private int _fuelTickCounter;

    // Aircraft: every tick (10 Hz at 100 ms interval)
    // Autopilot: every 5 ticks (2 Hz)
    // Traffic: every 10 ticks (1 Hz)
    // Fuel: every 10 ticks (1 Hz)
    private const int AutopilotTickInterval = 5;
    private const int TrafficTickInterval = 10;
    private const int FuelTickInterval = 10;

    /// <summary>Raised when new aircraft state is received.</summary>
    public event Action<AircraftState>? AircraftStateReceived;

    /// <summary>Raised when new autopilot state is received.</summary>
    public event Action<AutopilotState>? AutopilotStateReceived;

    /// <summary>Raised when traffic data is received.</summary>
    public event Action<TrafficAircraft>? TrafficAircraftReceived;

    /// <summary>Raised when fuel state is received.</summary>
    public event Action<FuelState>? FuelStateReceived;

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
                try
                {
                    TryConnect();
                }
                catch (FileNotFoundException ex)
                {
                    // The SimConnect assembly itself could not be loaded.
                    Emit("ERROR: SimConnect DLL not found or failed to load.");
                    Emit(ex.Message);
                    Emit("");
                    Emit("You need TWO DLLs from your MSFS SDK in the lib/ folder:");
                    Emit("  1) Managed:  copy 'C:\\MSFS SDK\\SimConnect SDK\\lib\\managed\\Microsoft.FlightSimulator.SimConnect.dll'");
                    Emit("  2) Native:   copy 'C:\\MSFS SDK\\SimConnect SDK\\lib\\SimConnect.dll'");
                    Emit("Then rebuild with 'dotnet build'.");
                    Emit($"Retrying in {Config.ReconnectDelayMs / 1000} seconds...");
                    try { await Task.Delay(Config.ReconnectDelayMs, cancellationToken); }
                    catch (TaskCanceledException) { break; }
                    continue;
                }
                catch (FileLoadException ex)
                {
                    Emit("ERROR: SimConnect DLL failed to load.");
                    Emit(ex.Message);
                    Emit($"Retrying in {Config.ReconnectDelayMs / 1000} seconds...");
                    try { await Task.Delay(Config.ReconnectDelayMs, cancellationToken); }
                    catch (TaskCanceledException) { break; }
                    continue;
                }
            }

            if (_connected)
            {
                try
                {
                    PollMessages();
                }
                catch (FileNotFoundException)
                {
                    // Assembly became unavailable mid-run
                    HandleDisconnect();
                    continue;
                }
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

    /// <summary>
    /// Attempts to connect to SimConnect. This method is marked NoInlining
    /// so the JIT does not try to resolve SimConnect types until this
    /// method is actually called (allowing callers to catch
    /// FileNotFoundException at a higher level).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
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
            RegisterFuelDefinition();

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
        _simConnect.AddToDataDefinition(SimDefinition.Aircraft,
            "SIM ON GROUND", "bool",
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

    private void RegisterFuelDefinition()
    {
        if (_simConnect is null) return;

        _simConnect.AddToDataDefinition(SimDefinition.Fuel,
            "FUEL TOTAL QUANTITY", "gallons",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Fuel,
            "FUEL TOTAL CAPACITY", "gallons",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(SimDefinition.Fuel,
            "ENG FUEL FLOW GPH:1", "gallons per hour",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<FuelStateStruct>(SimDefinition.Fuel);
    }

    // ---------------------------------------------------------------------
    // Polling — issues requests at different rates per data type
    // ---------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
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

            // Fuel: every 10 ticks (1 Hz)
            _fuelTickCounter++;
            if (_fuelTickCounter >= FuelTickInterval)
            {
                _fuelTickCounter = 0;
                _simConnect.RequestDataOnSimObjectType(
                    SimRequest.Fuel,
                    SimDefinition.Fuel,
                    0,
                    SIMCONNECT_SIMOBJECT_TYPE.USER);
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

    private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
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

            // dwData is object[] in the real SimConnect SDK; element [0] holds the struct.
            var dwData = data.dwData;
            object? payload = dwData is object[] arr && arr.Length > 0 ? arr[0] : dwData;

            switch (requestId)
            {
                case SimRequest.Aircraft when payload is AircraftStateStruct aircraftStruct:
                    AircraftStateReceived?.Invoke(AircraftState.FromStruct(aircraftStruct));
                    break;

                case SimRequest.Autopilot when payload is AutopilotStateStruct apStruct:
                    AutopilotStateReceived?.Invoke(AutopilotState.FromStruct(apStruct));
                    break;

                case SimRequest.Traffic when payload is TrafficAircraftStruct trafficStruct:
                    TrafficAircraftReceived?.Invoke(TrafficAircraft.FromStruct(trafficStruct));
                    break;

                case SimRequest.Fuel when payload is FuelStateStruct fuelStruct:
                    FuelStateReceived?.Invoke(FuelState.FromStruct(fuelStruct));
                    break;
            }
        }
        catch (Exception ex)
        {
            Emit($"Error processing telemetry: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------
    // Flight plan loading
    // ---------------------------------------------------------------------

    /// <summary>
    /// Writes the flight plan to a .pln file and loads it into MSFS
    /// using SimConnect.FlightPlanLoad.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LoadFlightPlan(FlightPlan plan)
    {
        if (_simConnect is null || !_connected)
        {
            Emit("Cannot load flight plan: not connected to MSFS");
            return;
        }

        try
        {
            // Write PLN file to a temp directory next to the executable
            var plnDir = Path.Combine(AppContext.BaseDirectory, "flightplans");
            var plnPath = plan.WritePln(plnDir);

            Emit($"Loading flight plan into MSFS: {plnPath}");

            // SimConnect_FlightPlanLoad expects path without .pln extension
            var pathWithoutExtension = Path.Combine(
                Path.GetDirectoryName(plnPath) ?? plnDir,
                Path.GetFileNameWithoutExtension(plnPath));

            _simConnect.FlightPlanLoad(pathWithoutExtension);

            Emit($"Flight plan loaded: {plan.Departure} -> {plan.Arrival}");
        }
        catch (Exception ex)
        {
            Emit($"Error loading flight plan: {ex.Message}");
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

    [MethodImpl(MethodImplOptions.NoInlining)]
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
        try
        {
            CleanupConnection();
        }
        catch (FileNotFoundException)
        {
            // SimConnect DLL not available — nothing to clean up
        }
        catch (FileLoadException)
        {
            // SimConnect DLL failed to load — nothing to clean up
        }
        catch (Exception)
        {
            // Best-effort cleanup
        }
    }
}
