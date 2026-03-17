# MSFS Companion Bridge

A professional Windows desktop companion application that bridges **Microsoft Flight Simulator** telemetry to **WebSocket** and **REST API** servers, enabling any web application to consume live aircraft data in real time.

Designed to integrate with [simpleflightplanner.com/planner](https://www.simpleflightplanner.com/planner) — providing live aircraft position on the map, telemetry display, and the ability to send flight plans from the planner directly into MSFS.

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
                                 ws://localhost:29112
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

[12:00:00] WebSocket server running on port 29112
[12:00:00] REST API server running on port 5555
[12:00:00] Connecting to SimConnect...
[12:00:01] Connected to MSFS
```

### 4. Publish a standalone executable

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

The output executable will be in `bin/Release/net8.0/win-x64/publish/`.

## simpleflightplanner.com Integration

This bridge is designed to work with [simpleflightplanner.com/planner](https://www.simpleflightplanner.com/planner). The planner connects to this bridge via WebSocket to:

1. **Display live aircraft position** on the map with heading indicator
2. **Show telemetry panel** (altitude, airspeed, groundspeed, heading, vertical speed)
3. **Send flight plans** from the planner directly into MSFS

### How it works

1. Start the companion bridge (`dotnet run`)
2. Open [simpleflightplanner.com/planner](https://www.simpleflightplanner.com/planner)
3. The planner auto-connects to `ws://localhost:29112`
4. Your aircraft appears on the map in real time
5. Click "Send to Sim" in the planner to load a flight plan into MSFS

## WebSocket Protocol

### Endpoint

```
ws://localhost:29112
```

### Telemetry Message (Bridge -> Planner, ~1 Hz)

Flat JSON format matching the simpleflightplanner.com protocol:

```json
{
  "type": "telemetry",
  "lat": 47.4502,
  "lng": -122.3088,
  "altitude": 3500,
  "heading": 270,
  "airspeed": 120,
  "groundspeed": 135,
  "verticalSpeed": -200,
  "onGround": false,
  "timestamp": 1710600000
}
```

| Field | Type | Unit | Description |
|---|---|---|---|
| `type` | `string` | — | Always `"telemetry"` |
| `lat` | `number` | degrees | Plane latitude |
| `lng` | `number` | degrees | Plane longitude |
| `altitude` | `number` | feet | Plane altitude |
| `heading` | `number` | degrees | True heading |
| `airspeed` | `number` | knots | Indicated airspeed |
| `groundspeed` | `number` | knots | Ground speed |
| `verticalSpeed` | `number` | ft/min | Vertical speed |
| `onGround` | `boolean` | — | Whether aircraft is on the ground |
| `timestamp` | `number` | unix seconds | UTC timestamp |

### Flight Plan Message (Planner -> Bridge)

Sent when user clicks "Send to Sim" in the planner:

```json
{
  "type": "flightplan",
  "departure": "KSEA",
  "arrival": "KPDX",
  "waypoints": [
    { "name": "KSEA", "lat": 47.45, "lng": -122.31 },
    { "name": "KPDX", "lat": 45.59, "lng": -122.60 }
  ],
  "cruisingAltitude": 8000
}
```

| Field | Type | Description |
|---|---|---|
| `type` | `string` | Always `"flightplan"` |
| `departure` | `string` | Departure airport ICAO code |
| `arrival` | `string` | Arrival airport ICAO code |
| `waypoints` | `array` | Array of waypoints with name/lat/lng |
| `cruisingAltitude` | `number` | Cruising altitude in feet |

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

### WebSocket (streaming telemetry)

```javascript
const ws = new WebSocket("ws://localhost:29112");

ws.onopen = () => {
  console.log("Connected to MSFS Companion Bridge");
};

ws.onmessage = (event) => {
  const data = JSON.parse(event.data);

  if (data.type === "telemetry") {
    console.log(`Position: ${data.lat}, ${data.lng}`);
    console.log(`Alt: ${data.altitude} ft | AS: ${data.airspeed} kts | GS: ${data.groundspeed} kts`);
    console.log(`Heading: ${data.heading} | VS: ${data.verticalSpeed} fpm | Ground: ${data.onGround}`);
  }
};

ws.onclose = () => {
  console.log("Disconnected from MSFS Companion Bridge");
};
```

### Sending a Flight Plan

```javascript
const flightPlan = {
  type: "flightplan",
  departure: "KSEA",
  arrival: "KPDX",
  waypoints: [
    { name: "KSEA", lat: 47.45, lng: -122.31 },
    { name: "KPDX", lat: 45.59, lng: -122.60 }
  ],
  cruisingAltitude: 8000
};

ws.send(JSON.stringify(flightPlan));
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
| Aircraft State | 10 Hz (1 Hz to planner) | `PLANE LATITUDE`, `PLANE LONGITUDE`, `PLANE ALTITUDE`, `PLANE HEADING DEGREES TRUE`, `PLANE PITCH DEGREES`, `PLANE BANK DEGREES`, `GROUND VELOCITY`, `AIRSPEED INDICATED`, `VERTICAL SPEED`, `SIM ON GROUND` |
| Autopilot State | 2 Hz | `AUTOPILOT MASTER`, `AUTOPILOT ALTITUDE LOCK`, `AUTOPILOT HEADING LOCK`, `AUTOPILOT HEADING LOCK DIR`, `AUTOPILOT ALTITUDE LOCK VAR` |
| Traffic | 1 Hz | `TITLE`, `PLANE LATITUDE`, `PLANE LONGITUDE`, `PLANE ALTITUDE`, `PLANE HEADING DEGREES TRUE`, `GROUND VELOCITY` |

## Features

- **simpleflightplanner.com integration**: Live aircraft marker on the planner map + send flight plans to MSFS.
- **Flat telemetry protocol**: Simple JSON messages at ~1 Hz matching the planner's WebSocket spec.
- **Bidirectional WebSocket**: Receives flight plans from the planner, sends telemetry back.
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
