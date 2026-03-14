// =============================================================================
// Program.cs — Entry point for MSFS Companion Bridge v2.0
// =============================================================================
//
// Wires together the SimConnectService, TelemetryEngine, WebSocket server,
// and REST API server. Waits for Ctrl+C (SIGINT) for graceful shutdown.
// =============================================================================

using MSFSCompanionBridge.Servers;
using MSFSCompanionBridge.Services;

namespace MSFSCompanionBridge;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.Title = Config.SimConnectAppName;
        PrintBanner();

        using var cts = new CancellationTokenSource();

        // Ctrl+C / SIGINT handler
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;          // prevent abrupt termination
            Log("Shutdown requested...");
            cts.Cancel();
        };

        // --- SimConnect service -------------------------------------------------
        using var simService = new SimConnectService();
        simService.Log += Log;

        // --- Telemetry engine ---------------------------------------------------
        var engine = new TelemetryEngine(simService);
        engine.Log += Log;

        // --- WebSocket server ---------------------------------------------------
        using var wsServer = new TelemetryWebSocketServer();
        wsServer.Log += Log;
        wsServer.Start();

        // Connect engine output to WebSocket broadcast
        engine.MessageReady += json => wsServer.Broadcast(json);

        // --- REST API server ----------------------------------------------------
        using var apiServer = new ApiServer(engine);
        apiServer.Log += Log;

        // Start the API server in the background
        var apiTask = Task.Run(() => apiServer.StartAsync(cts.Token), cts.Token);

        // --- SimConnect polling loop (blocks until cancellation) ----------------
        try
        {
            await simService.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }

        // Wait briefly for the API server to shut down
        try
        {
            await apiTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort shutdown
        }

        Log("MSFS Companion Bridge stopped. Goodbye!");
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================");
        Console.WriteLine("       MSFS Companion Bridge v2.0");
        Console.WriteLine("=========================================");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] {message}");
    }
}
