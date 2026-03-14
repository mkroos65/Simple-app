# MSFS Companion Bridge

A lightweight Windows desktop companion application that bridges **Microsoft Flight Simulator** telemetry to a **WebSocket server**, enabling any web application to consume live aircraft data in real time.

## Architecture

```
┌──────────────┐  SimConnect   ┌─────────────────────┐  WebSocket   ┌──────────────┐
│  MS Flight   │──────────────▶│  MSFS Companion     │─────────────▶│  Web App /   │
│  Simulator   │               │  Bridge             │   (JSON)     │  React Client│
└──────────────┘               └─────────────────────┘              └──────────────┘
                                ws://localhost:8765
```

## Requirements

| Requirement | Version |
|---|---|
| Microsoft Flight Simulator | 2020 / 2024 |
| SimConnect SDK | Installed with MSFS SDK |
| .NET SDK | 8.0+ |
| OS | Windows 10/11 (x64) |

## Quick Start

### 1. Set up the SimConnect DLL

Copy the **managed** SimConnect assembly into the `lib/` folder:

```bash
# Typical MSFS SDK location:
copy "C:\MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll" lib\
```

> **Note:** The repository ships with a build-time stub so the project compiles without the SDK.
> You **must** replace it with the real DLL before running the application.

### 2. Build

```bash
dotnet build
```

### 3. Run

```bash
dotnet run
```

You should see:

```
=========================================
       MSFS Companion Bridge v1.0
=========================================

[12:00:00] WebSocket server running on port 8765
[12:00:00] Connecting to SimConnect...
[12:00:01] Connected to MSFS
```

### 4. Publish a standalone executable

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

The output executable will be in `bin/Release/net8.0/win-x64/publish/`.

## WebSocket API

### Endpoint

```
ws://localhost:8765
```

### JSON Payload

Each message is a JSON object with the following fields:

| Field | Type | Unit | Description |
|---|---|---|---|
| `lat` | `number` | degrees | Plane latitude |
| `lon` | `number` | degrees | Plane longitude |
| `alt` | `number` | feet | Plane altitude |
| `heading` | `number` | degrees | True heading |
| `pitch` | `number` | degrees | Pitch angle |
| `bank` | `number` | degrees | Bank angle |
| `speed` | `number` | knots | Indicated airspeed |
| `vs` | `number` | ft/min | Vertical speed |
| `timestamp` | `number` | epoch sec | Unix timestamp |

### Example Payload

```json
{
  "lat": 45.5017,
  "lon": -73.5673,
  "alt": 3500.0,
  "heading": 180.0,
  "pitch": 2.1,
  "bank": -1.2,
  "speed": 120.0,
  "vs": 500.0,
  "timestamp": 1710000000
}
```

## Example JavaScript Client

```javascript
const ws = new WebSocket("ws://localhost:8765");

ws.onopen = () => {
  console.log("Connected to MSFS Companion Bridge");
};

ws.onmessage = (event) => {
  const data = JSON.parse(event.data);
  console.log(`Position: ${data.lat}, ${data.lon}`);
  console.log(`Altitude: ${data.alt} ft | Speed: ${data.speed} kts`);
  console.log(`Heading: ${data.heading}° | VS: ${data.vs} fpm`);
};

ws.onclose = () => {
  console.log("Disconnected from MSFS Companion Bridge");
};

ws.onerror = (error) => {
  console.error("WebSocket error:", error);
};
```

A ready-to-use HTML test page is provided in [`example-client.html`](example-client.html).

## Project Structure

```
MSFS-Companion-Bridge/
├── Program.cs              # Application entry point
├── SimConnectManager.cs    # SimConnect lifecycle & telemetry polling
├── TelemetryData.cs        # Data model (struct + JSON DTO)
├── WebSocketServer.cs      # Fleck WebSocket server & broadcast
├── Config.cs               # Configuration constants
├── MSFSCompanionBridge.csproj
├── lib/                    # SimConnect managed DLL (stub or real)
├── stubs/                  # Build-time SimConnect stub source
├── example-client.html     # Browser-based WebSocket test client
└── README.md
```

## Telemetry Variables

The following SimConnect simulation variables are subscribed to:

| SimConnect Variable | JSON Field |
|---|---|
| `PLANE LATITUDE` | `lat` |
| `PLANE LONGITUDE` | `lon` |
| `PLANE ALTITUDE` | `alt` |
| `PLANE HEADING DEGREES TRUE` | `heading` |
| `PLANE PITCH DEGREES` | `pitch` |
| `PLANE BANK DEGREES` | `bank` |
| `AIRSPEED INDICATED` | `speed` |
| `VERTICAL SPEED` | `vs` |

Data is requested at **10 Hz** (100 ms interval) and broadcast to all connected WebSocket clients.

## Features

- **Automatic reconnect**: If MSFS closes or SimConnect drops, the bridge retries every 5 seconds.
- **Multiple clients**: Any number of WebSocket clients can connect simultaneously.
- **Graceful shutdown**: Press `Ctrl+C` to cleanly close SimConnect and the WebSocket server.
- **Async architecture**: Non-blocking polling loop keeps CPU usage minimal.

## Error Handling

| Scenario | Behaviour |
|---|---|
| MSFS not running | Retries connection every 5 seconds |
| MSFS restarts | Detects disconnect, reconnects automatically |
| Client disconnects | Removed from broadcast list, no error |
| Malformed telemetry | Logged and skipped, does not crash |

## License

MIT
