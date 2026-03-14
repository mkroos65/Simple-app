// =============================================================================
// WebSocketServer.cs — Fleck-based WebSocket server for telemetry broadcast
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Fleck;

namespace MSFSCompanionBridge;

/// <summary>
/// Manages the WebSocket server lifecycle, client tracking, and telemetry
/// broadcasting to all connected clients.
/// </summary>
public sealed class TelemetryWebSocketServer : IDisposable
{
    private readonly WebSocketServer _server;
    private readonly ConcurrentDictionary<Guid, IWebSocketConnection> _clients = new();
    private bool _disposed;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    public TelemetryWebSocketServer()
    {
        _server = new WebSocketServer(Config.WebSocketUri);

        // Suppress Fleck's built-in logging to keep console clean
        FleckLog.LogAction = (level, message, ex) =>
        {
            if (level == LogLevel.Error)
                Emit($"[Fleck] {message} {ex?.Message}");
        };
    }

    /// <summary>
    /// Starts the WebSocket server and begins accepting connections.
    /// </summary>
    public void Start()
    {
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                var id = Guid.NewGuid();
                _clients.TryAdd(id, socket);

                // Store the id on the connection for lookup on close
                socket.ConnectionInfo.Headers["X-Client-Id"] = id.ToString();

                Emit($"Client connected ({_clients.Count} total)");
            };

            socket.OnClose = () =>
            {
                if (socket.ConnectionInfo.Headers.TryGetValue("X-Client-Id", out var idStr)
                    && Guid.TryParse(idStr, out var id))
                {
                    _clients.TryRemove(id, out _);
                }

                Emit($"Client disconnected ({_clients.Count} total)");
            };

            socket.OnError = ex =>
            {
                Emit($"WebSocket error: {ex.Message}");
            };
        });

        Emit($"WebSocket server running on port {Config.WebSocketPort}");
    }

    /// <summary>
    /// Serialises the telemetry payload to JSON and sends it to every
    /// connected client. Failed sends are silently ignored (the client
    /// will be cleaned up via OnClose).
    /// </summary>
    public void Broadcast(TelemetryData telemetry)
    {
        if (_clients.IsEmpty) return;

        var json = JsonSerializer.Serialize(telemetry);

        foreach (var kvp in _clients)
        {
            try
            {
                if (kvp.Value.IsAvailable)
                {
                    kvp.Value.Send(json);
                }
            }
            catch
            {
                // Connection will be cleaned up by OnClose/OnError
            }
        }
    }

    /// <summary>Number of currently connected clients.</summary>
    public int ClientCount => _clients.Count;

    private void Emit(string message)
    {
        Log?.Invoke(message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _clients)
        {
            try { kvp.Value.Close(); } catch { /* best-effort */ }
        }
        _clients.Clear();

        try { _server.Dispose(); } catch { /* best-effort */ }

        Emit("WebSocket server stopped");
    }
}
