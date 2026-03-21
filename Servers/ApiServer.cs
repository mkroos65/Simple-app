// =============================================================================
// Servers/ApiServer.cs — ASP.NET Minimal API for cached telemetry data
// =============================================================================

using System.Text.Json;
using SimpleFlightTracker.Services;
using SimpleFlightTracker.Telemetry;

namespace SimpleFlightTracker.Servers;

/// <summary>
/// Lightweight HTTP REST API server that exposes the latest cached telemetry
/// from <see cref="TelemetryEngine"/> via ASP.NET minimal APIs.
/// </summary>
public sealed class ApiServer : IDisposable
{
    private readonly TelemetryEngine _engine;
    private WebApplication? _app;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    public ApiServer(TelemetryEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Builds and starts the HTTP server on the configured port.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();

        // Suppress default ASP.NET logging noise
        builder.Logging.ClearProviders();

        builder.WebHost.UseUrls($"http://0.0.0.0:{Config.ApiPort}");

        _app = builder.Build();

        // Enable CORS for browser-based clients
        _app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                return;
            }

            await next();
        });

        MapEndpoints(_app);

        Emit($"REST API server running on port {Config.ApiPort}");

        await _app.RunAsync(cancellationToken);
    }

    private void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/aircraft", () =>
        {
            var state = _engine.LatestAircraft;
            if (state is null)
                return Results.Json(new { error = "No aircraft data available" }, statusCode: 503);

            var message = TelemetryMessage<AircraftState>.Create(
                MessageTypes.AircraftUpdate, state);
            return Results.Json(message);
        });

        app.MapGet("/api/autopilot", () =>
        {
            var state = _engine.LatestAutopilot;
            if (state is null)
                return Results.Json(new { error = "No autopilot data available" }, statusCode: 503);

            var message = TelemetryMessage<AutopilotState>.Create(
                MessageTypes.AutopilotUpdate, state);
            return Results.Json(message);
        });

        app.MapGet("/api/traffic", () =>
        {
            var traffic = _engine.LatestTraffic;

            var message = TelemetryMessage<List<TrafficAircraft>>.Create(
                MessageTypes.TrafficUpdate, traffic);
            return Results.Json(message);
        });

        app.MapGet("/api/fuel", () =>
        {
            var state = _engine.LatestFuel;
            if (state is null)
                return Results.Json(new { error = "No fuel data available" }, statusCode: 503);

            var message = TelemetryMessage<FuelState>.Create(
                MessageTypes.FuelUpdate, state);
            return Results.Json(message);
        });

        app.MapGet("/api/flightplan/progress", () =>
        {
            var progress = _engine.LatestFlightPlanProgress;
            if (progress is null)
                return Results.Json(new { error = "No active flight plan" }, statusCode: 503);

            var message = TelemetryMessage<FlightPlanProgress>.Create(
                MessageTypes.FlightPlanProgress, progress);
            return Results.Json(message);
        });
    }

    private void Emit(string message)
    {
        Log?.Invoke(message);
    }

    public void Dispose()
    {
        if (_app is not null)
        {
            try { _app.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2)); }
            catch { /* best-effort */ }
        }

        Emit("REST API server stopped");
    }
}
