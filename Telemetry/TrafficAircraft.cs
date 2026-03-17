// =============================================================================
// Telemetry/TrafficAircraft.cs — AI / Multiplayer traffic model
// =============================================================================

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Raw SimConnect data definition layout for traffic aircraft.
/// Fields MUST match the order registered in SimConnectService.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct TrafficAircraftStruct
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Title;
    public double Latitude;
    public double Longitude;
    public double Altitude;      // feet
    public double Heading;       // degrees true
    public double GroundVelocity; // knots
}

/// <summary>
/// JSON-serialisable traffic aircraft payload.
/// </summary>
public sealed class TrafficAircraft
{
    [JsonPropertyName("callsign")]
    public string Callsign { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; }

    [JsonPropertyName("heading")]
    public double Heading { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    public static TrafficAircraft FromStruct(TrafficAircraftStruct s)
    {
        return new TrafficAircraft
        {
            Callsign = s.Title ?? "Unknown",
            Lat      = Math.Round(s.Latitude, 6),
            Lon      = Math.Round(s.Longitude, 6),
            Altitude = Math.Round(s.Altitude, 1),
            Heading  = Math.Round(s.Heading, 1),
            Speed    = Math.Round(s.GroundVelocity, 1)
        };
    }
}
