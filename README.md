# MSFS Companion Bridge

A professional Windows desktop companion application that bridges **Microsoft Flight Simulator** telemetry to **WebSocket** and **REST API** servers, enabling any web application to consume live aircraft, autopilot, and traffic data in real time.

## Architecture

```
┌──────────────┐  SimConnect   ┌─────────────────────┐  WebSocket    ┌──────────────┐
│  MS Flight   │──────────────▶│  MSFS Companion     │──────────────▶│  Web App /   │
│  Simulator   │               │  Bridge v2.0        │   (JSON)      │  React Client│
└──────────────┘               │                     │               └──────────────┘
                               │  SimConnectService  │  REST API     ┌──────────────┐
                               │  TelemetryEngine    │──────────────▶│  HTTP Client │
                               │  WebSocketServer    │               └──────────────┘
                               │  ApiServer          │
                               └─────────────────────┘
                                ws://localhost:8765
                                http://localhost:5555
```

## Requirements

| Requirement | Version |
|---|---|
| Microsoft Flight Simulator | 2020 / 2024 |
| SimConnect SDK | Installed with MSFS SDK |
| .NET SDK | 8.0+ |
| OS | Windows 10/11 (x64) |

## Quick Start

### 1. Set up the SimConnect DLLs

You need **two** DLLs from the MSFS SDK in the `lib/` folder:

```bash
# 1) Managed wrapper (.NET assembly)
copy "C:\MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll" lib\

# 2) Native library (unmanaged C++ DLL)
copy "C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll" lib\
```

> **Note:** The repository ships with a build-time stub so the project compiles without the SDK.
> You **must** replace it with the real DLLs before running the application.
> The managed DLL is a .NET wrapper that calls into the native `SimConnect.dll` via P/Invoke — both are required at runtime.

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
       MSFS Companion Bridge v2.0
=========================================

[12:00:00] WebSocket server running on port 8765
[12:00:00] REST API server running on port 5555
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

All WebSocket messages use **event-based messaging** with a `type` field, a `data` payload, and a `timestamp`.

### Message Types

#### `aircraft:update` (10 Hz)

```json
{
  "type": "aircraft:update",
  "data": {
    "lat": 45.5017,
    "lon": -73.5673,
    "altitude": 3500.0,
    "heading": 180.0,
    "pitch": 2.1,
    "bank": -1.2,
    "groundSpeed": 120.0,
    "verticalSpeed": 500.0
  },
  "timestamp": 1710000000
}
```

| Field | Type | Unit | Description |
|---|---|---|---|
| `lat` | `number` | degrees | Plane latitude |
| `lon` | `number` | degrees | Plane longitude |
| `altitude` | `number` | feet | Plane altitude |
| `heading` | `number` | degrees | True heading |
| `pitch` | `number` | degrees | Pitch angle |
| `bank` | `number` | degrees | Bank angle |
| `groundSpeed` | `number` | knots | Ground speed |
| `verticalSpeed` | `number` | ft/min | Vertical speed |

#### `autopilot:update` (2 Hz)

```json
{
  "type": "autopilot:update",
  "data": {
    "master": true,
    "altitudeLock": true,
    "headingLock": false,
    "headingLockDir": 270.0,
    "altitudeLockVar": 35000
  },
  "timestamp": 1710000000
}
```

| Field | Type | Unit | Description |
|---|---|---|---|
| `master` | `boolean` | — | Autopilot master switch |
| `altitudeLock` | `boolean` | — | Altitude hold active |
| `headingLock` | `boolean` | — | Heading hold active |
| `headingLockDir` | `number` | degrees | Selected heading |
| `altitudeLockVar` | `number` | feet | Selected altitude |

#### `traffic:update` (1 Hz)

```json
{
  "type": "traffic:update",
  "data": [
    {
      "callsign": "UAL123",
      "lat": 45.51,
      "lon": -73.55,
      "altitude": 36000,
      "heading": 90.0,
      "speed": 450.0
    }
  ],
  "timestamp": 1710000000
}
```

| Field | Type | Unit | Description |
|---|---|---|---|
| `callsign` | `string` | — | Aircraft title / callsign |
| `lat` | `number` | degrees | Latitude |
| `lon` | `number` | degrees | Longitude |
| `altitude` | `number` | feet | Altitude |
| `heading` | `number` | degrees | True heading |
| `speed` | `number` | knots | Ground speed |

## REST API

### Base URL

```
http://localhost:5555
```

All endpoints return the same structured JSON format used by WebSocket messages. CORS is enabled for browser-based clients.

### Endpoints

#### `GET /api/aircraft`

Returns the latest aircraft state.

```bash
curl http://localhost:5555/api/aircraft
```

```json
{
  "type": "aircraft:update",
  "data": {
    "lat": 45.5017,
    "lon": -73.5673,
    "altitude": 3500.0,
    "heading": 180.0,
    "pitch": 2.1,
    "bank": -1.2,
    "groundSpeed": 120.0,
    "verticalSpeed": 500.0
  },
  "timestamp": 1710000000
}
```

#### `GET /api/autopilot`

Returns the latest autopilot state.

```bash
curl http://localhost:5555/api/autopilot
```

#### `GET /api/traffic`

Returns a list of nearby AI / multiplayer aircraft.

```bash
curl http://localhost:5555/api/traffic
```

> **Note:** If no data is available yet (e.g. MSFS not connected), aircraft and autopilot endpoints return HTTP 503 with `{"error": "No ... data available"}`.

## Example JavaScript Client

### WebSocket (streaming)

```javascript
const ws = new WebSocket("ws://localhost:8765");

ws.onopen = () => {
  console.log("Connected to MSFS Companion Bridge");
};

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);

  switch (msg.type) {
    case "aircraft:update":
      console.log(`Position: ${msg.data.lat}, ${msg.data.lon}`);
      console.log(`Altitude: ${msg.data.altitude} ft | Speed: ${msg.data.groundSpeed} kts`);
      break;

    case "autopilot:update":
      console.log(`AP Master: ${msg.data.master} | Alt Lock: ${msg.data.altitudeLock}`);
      break;

    case "traffic:update":
      console.log(`Nearby aircraft: ${msg.data.length}`);
      msg.data.forEach(t => console.log(`  ${t.callsign} at ${t.altitude} ft`));
      break;
  }
};

ws.onclose = () => {
  console.log("Disconnected from MSFS Companion Bridge");
};
```

### REST API (polling)

```javascript
async function getAircraftState() {
  const res = await fetch("http://localhost:5555/api/aircraft");
  const msg = await res.json();
  console.log(msg.data);
}

// Poll every second
setInterval(getAircraftState, 1000);
```

A ready-to-use HTML test page is provided in [`example-client.html`](example-client.html).

## Project Structure

```
MSFS-Companion-Bridge/
├── Program.cs                  # Application entry point (v2.0)
├── Config.cs                   # Configuration constants
├── Telemetry/
│   ├── AircraftState.cs        # Aircraft telemetry model
│   ├── AutopilotState.cs       # Autopilot state model
│   ├── TrafficAircraft.cs      # AI / multiplayer traffic model
│   ├── FlightPlan.cs           # Flight plan model
│   └── TelemetryMessage.cs     # Event-based message envelope
├── Services/
│   ├── SimConnectService.cs    # SimConnect lifecycle & multi-definition polling
│   └── TelemetryEngine.cs      # Central telemetry hub: cache, events, REST feed
├── Servers/
│   ├── WebSocketServer.cs      # Fleck WebSocket server & structured broadcast
│   └── ApiServer.cs            # ASP.NET minimal API (REST endpoints)
├── MSFSCompanionBridge.csproj
├── lib/                        # SimConnect managed DLL (stub or real)
├── stubs/                      # Build-time SimConnect stub source
├── example-client.html         # Browser-based WebSocket test client
└── README.md
```

## Data Sources & Update Frequencies

| Data Source | Frequency | SimConnect Variables |
|---|---|---|
| Aircraft State | 10 Hz | `PLANE LATITUDE`, `PLANE LONGITUDE`, `PLANE ALTITUDE`, `PLANE HEADING DEGREES TRUE`, `PLANE PITCH DEGREES`, `PLANE BANK DEGREES`, `GROUND VELOCITY`, `AIRSPEED INDICATED`, `VERTICAL SPEED` |
| Autopilot State | 2 Hz | `AUTOPILOT MASTER`, `AUTOPILOT ALTITUDE LOCK`, `AUTOPILOT HEADING LOCK`, `AUTOPILOT HEADING LOCK DIR`, `AUTOPILOT ALTITUDE LOCK VAR` |
| Traffic | 1 Hz | `TITLE`, `PLANE LATITUDE`, `PLANE LONGITUDE`, `PLANE ALTITUDE`, `PLANE HEADING DEGREES TRUE`, `GROUND VELOCITY` |

## Features

- **Structured telemetry**: Event-based messaging with `type`, `data`, and `timestamp` fields.
- **Multiple data sources**: Aircraft state, autopilot, and AI/multiplayer traffic.
- **REST API**: Cached telemetry available via HTTP GET endpoints with CORS support.
- **Automatic reconnect**: If MSFS closes or SimConnect drops, the bridge retries every 5 seconds.
- **Multiple clients**: Any number of WebSocket clients can connect simultaneously.
- **Graceful shutdown**: Press `Ctrl+C` to cleanly close all services.
- **Async architecture**: Non-blocking polling loop keeps CPU usage minimal.

## Error Handling

| Scenario | Behaviour |
|---|---|
| MSFS not running | Retries connection every 5 seconds |
| MSFS restarts | Detects disconnect, reconnects automatically |
| Client disconnects | Removed from broadcast list, no error |
| Malformed telemetry | Logged and skipped, does not crash |
| REST API before data ready | Returns HTTP 503 with error message |

## License

MIT
