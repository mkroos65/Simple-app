// =============================================================================
// SimConnectManager.cs — SimConnect integration with automatic reconnect
// =============================================================================

using Microsoft.FlightSimulator.SimConnect;

namespace MSFSCompanionBridge;

/// <summary>
/// Identifiers used when registering data definitions and requests with SimConnect.
/// </summary>
public enum SimDataDefinition
{
    TelemetryDefinition
}

public enum SimDataRequest
{
    TelemetryRequest
}

/// <summary>
/// Manages the SimConnect lifecycle: connection, data subscription,
/// receive loop, automatic reconnect, and graceful shutdown.
/// </summary>
public sealed class SimConnectManager : IDisposable
{
    private SimConnect? _simConnect;
    private bool _connected;
    private bool _disposed;

    /// <summary>Raised whenever a new telemetry frame is received.</summary>
    public event Action<TelemetryData>? TelemetryReceived;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------

    /// <summary>
    /// Starts the connection loop. Keeps retrying until cancelled.
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

            RegisterTelemetryDefinition();
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

    /// <summary>
    /// Registers each telemetry variable with SimConnect.
    /// The order here MUST match <see cref="TelemetryStruct"/>.
    /// </summary>
    private void RegisterTelemetryDefinition()
    {
        if (_simConnect is null) return;

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "PLANE LATITUDE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "PLANE LONGITUDE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "PLANE ALTITUDE", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "PLANE HEADING DEGREES TRUE", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "PLANE PITCH DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "PLANE BANK DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "GROUND VELOCITY", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "AIRSPEED INDICATED", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            SimDataDefinition.TelemetryDefinition,
            "VERTICAL SPEED", "feet per minute",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<TelemetryStruct>(
            SimDataDefinition.TelemetryDefinition);
    }

    /// <summary>
    /// Calls ReceiveMessage to dispatch pending SimConnect callbacks,
    /// then issues a new data request for the next frame.
    /// </summary>
    private void PollMessages()
    {
        if (_simConnect is null) return;

        try
        {
            _simConnect.ReceiveMessage();
            _simConnect.RequestDataOnSimObjectType(
                SimDataRequest.TelemetryRequest,
                SimDataDefinition.TelemetryDefinition,
                0,
                SIMCONNECT_SIMOBJECT_TYPE.USER);
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
            if (data.dwData is TelemetryStruct telemetryStruct)
            {
                var telemetry = TelemetryData.FromStruct(telemetryStruct);
                TelemetryReceived?.Invoke(telemetry);
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
