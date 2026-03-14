// =============================================================================
// Program.cs — Entry point for MSFS Companion Bridge
// =============================================================================
//
// Launches the WebSocket server and SimConnect manager, then waits for
// Ctrl+C (SIGINT) to perform a graceful shutdown.
// =============================================================================

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

        // --- WebSocket server ---------------------------------------------------
        using var wsServer = new TelemetryWebSocketServer();
        wsServer.Log += Log;
        wsServer.Start();

        // --- SimConnect manager -------------------------------------------------
        using var simManager = new SimConnectManager();
        simManager.Log += Log;
        simManager.TelemetryReceived += telemetry =>
        {
            wsServer.Broadcast(telemetry);
        };

        // Run the SimConnect polling loop until cancellation
        try
        {
            await simManager.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }

        Log("MSFS Companion Bridge stopped. Goodbye!");
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================");
        Console.WriteLine("       MSFS Companion Bridge v1.0");
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
