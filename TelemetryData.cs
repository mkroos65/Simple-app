// =============================================================================
// TelemetryData.cs — Aircraft telemetry model
// =============================================================================

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace MSFSCompanionBridge;

/// <summary>
/// Matches the SimConnect data definition layout.
/// Fields MUST be in the same order as they are registered
/// via <c>AddToDataDefinition</c> in <see cref="SimConnectManager"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct TelemetryStruct
{
    public double Latitude;
    public double Longitude;
    public double Altitude;          // feet
    public double Heading;           // degrees true
    public double Pitch;             // degrees
    public double Bank;              // degrees
    public double GroundVelocity;    // knots
    public double IndicatedAirspeed; // knots
    public double VerticalSpeed;     // feet per minute
}

/// <summary>
/// JSON-serialisable telemetry payload sent to WebSocket clients.
/// Property names match the specification.
/// </summary>
public sealed class TelemetryData
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("alt")]
    public double Alt { get; set; }

    [JsonPropertyName("heading")]
    public double Heading { get; set; }

    [JsonPropertyName("pitch")]
    public double Pitch { get; set; }

    [JsonPropertyName("bank")]
    public double Bank { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("vs")]
    public double Vs { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Creates a <see cref="TelemetryData"/> instance from the raw SimConnect struct.
    /// </summary>
    public static TelemetryData FromStruct(TelemetryStruct s)
    {
        return new TelemetryData
        {
            Lat       = Math.Round(s.Latitude, 6),
            Lon       = Math.Round(s.Longitude, 6),
            Alt       = Math.Round(s.Altitude, 1),
            Heading   = Math.Round(s.Heading, 1),
            Pitch     = Math.Round(s.Pitch, 2),
            Bank      = Math.Round(s.Bank, 2),
            Speed     = Math.Round(s.IndicatedAirspeed, 1),
            Vs        = Math.Round(s.VerticalSpeed, 1),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
