// =============================================================================
// Servers/WebSocketServer.cs — Fleck-based WebSocket server for structured
//                                telemetry broadcast (event-based messaging)
// =============================================================================

using System.Collections.Concurrent;
using Fleck;

namespace SimpleFlightTracker.Servers;

/// <summary>
/// Manages the WebSocket server lifecycle, client tracking, broadcast
/// of pre-serialised JSON telemetry messages, and routing of incoming
/// messages (e.g. flight plans from the web planner).
/// </summary>
public sealed class TelemetryWebSocketServer : IDisposable
{
    private readonly WebSocketServer _server;
    private readonly ConcurrentDictionary<Guid, IWebSocketConnection> _clients = new();
    private bool _disposed;

    /// <summary>Raised to surface log messages to the console.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// Raised when a message is received from a WebSocket client.
    /// Used to route flight plan messages to the TelemetryEngine.
    /// </summary>
    public event Action<string>? MessageReceived;

    public TelemetryWebSocketServer()
    {
        _server = new WebSocketServer(Config.WebSocketUri);

        // Suppress Fleck's built-in logging to keep console clean
        FleckLog.LogAction = (level, message, ex) =>
        {
            if (level == Fleck.LogLevel.Error)
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

            socket.OnMessage = message =>
            {
                Emit($"Incoming WebSocket message ({message.Length} chars)");
                // Route incoming messages (e.g. flight plans) to the engine
                MessageReceived?.Invoke(message);
            };

            socket.OnError = ex =>
            {
                Emit($"WebSocket error: {ex.Message}");
            };
        });

        Emit($"WebSocket server running on port {Config.WebSocketPort} (ws://)");
    }

    /// <summary>
    /// Broadcasts a pre-serialised JSON string to every connected client.
    /// </summary>
    public void Broadcast(string json)
    {
        if (_clients.IsEmpty) return;

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
