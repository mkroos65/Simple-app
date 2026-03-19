// =============================================================================
// Program.cs — Entry point for Simple Flight Tracker
// =============================================================================
//
// Wires together the SimConnectService, TelemetryEngine, WebSocket server,
// and REST API server. Waits for Ctrl+C (SIGINT) for graceful shutdown.
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using SimpleFlightTracker.Servers;
using SimpleFlightTracker.Services;

namespace SimpleFlightTracker;

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

        // --- TLS certificate ----------------------------------------------------
        X509Certificate2? tlsCert = null;
        var useTls = !args.Contains("--no-tls");

        if (useTls)
        {
            try
            {
                tlsCert = CertificateHelper.GetOrCreateCertificate(Log);
                Log($"TLS enabled — WebSocket server will use wss:// on port {Config.WebSocketPort}");
                Log("NOTE: Clients connecting from other devices must accept the self-signed certificate.");
            }
            catch (Exception ex)
            {
                Log($"TLS certificate generation failed: {ex.Message}");
                Log("Falling back to plain ws:// (no TLS)");
                tlsCert = null;
            }
        }
        else
        {
            Log("TLS disabled (--no-tls flag). Using plain ws://");
        }

        // --- WebSocket server ---------------------------------------------------
        using var wsServer = new TelemetryWebSocketServer(tlsCert);
        wsServer.Log += Log;
        wsServer.Start();

        // --- SimConnect service -------------------------------------------------
        using var simService = new SimConnectService();
        simService.Log += Log;

        // --- Telemetry engine ---------------------------------------------------
        var engine = new TelemetryEngine(simService);
        engine.Log += Log;

        // Connect engine output to WebSocket broadcast
        engine.MessageReady += json => wsServer.Broadcast(json);

        // Route incoming WebSocket messages (e.g. flight plans) to the engine
        wsServer.MessageReceived += json => engine.HandleIncomingMessage(json);

        // When a flight plan is received from the planner, load it into MSFS
        engine.FlightPlanReceived += plan =>
        {
            Log($"Flight plan received: {plan.Departure} -> {plan.Arrival} " +
                $"({plan.Waypoints.Count} waypoints, cruise {plan.CruisingAltitude} ft)");
            simService.LoadFlightPlan(plan);
        };

        // Send flight plan responses (ack/error) back to the planner via WebSocket
        simService.FlightPlanResponse += json => wsServer.Broadcast(json);

        // --- REST API server ----------------------------------------------------
        using var apiServer = new ApiServer(engine, tlsCert);
        apiServer.Log += Log;

        // Start the API server in the background (do NOT pass cts.Token to
        // Task.Run — only pass it to StartAsync so the server stays alive
        // until we explicitly cancel).
        var apiTask = Task.Run(async () =>
        {
            try
            {
                await apiServer.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                Log($"REST API error: {ex.Message}");
            }
        });

        // --- SimConnect polling loop (blocks until cancellation) ----------------
        try
        {
            await simService.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }
        catch (Exception ex)
        {
            Log($"Fatal error: {ex.Message}");
        }

        // Signal shutdown and wait briefly for the API server
        if (!cts.IsCancellationRequested) cts.Cancel();
        try
        {
            await apiTask.WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // Best-effort shutdown
        }

        Log("Simple Flight Tracker stopped. Goodbye!");
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================");
        Console.WriteLine("       Simple Flight Tracker v2.0");
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
