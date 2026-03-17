// =============================================================================
// Telemetry/AircraftState.cs — Structured aircraft telemetry model
// =============================================================================

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Raw SimConnect data definition layout for aircraft state.
/// Fields MUST match the order registered in SimConnectService.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct AircraftStateStruct
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
    public double SimOnGround;       // bool (0.0 or 1.0)
}

/// <summary>
/// JSON-serialisable aircraft state payload.
/// </summary>
public sealed class AircraftState
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; }

    [JsonPropertyName("heading")]
    public double Heading { get; set; }

    [JsonPropertyName("pitch")]
    public double Pitch { get; set; }

    [JsonPropertyName("bank")]
    public double Bank { get; set; }

    [JsonPropertyName("airspeed")]
    public double Airspeed { get; set; }

    [JsonPropertyName("groundspeed")]
    public double Groundspeed { get; set; }

    [JsonPropertyName("verticalSpeed")]
    public double VerticalSpeed { get; set; }

    [JsonPropertyName("onGround")]
    public bool OnGround { get; set; }

    public static AircraftState FromStruct(AircraftStateStruct s)
    {
        return new AircraftState
        {
            Lat           = Math.Round(s.Latitude, 6),
            Lng           = Math.Round(s.Longitude, 6),
            Altitude      = Math.Round(s.Altitude, 1),
            Heading       = Math.Round(s.Heading, 1),
            Pitch         = Math.Round(s.Pitch, 2),
            Bank          = Math.Round(s.Bank, 2),
            Airspeed      = Math.Round(s.IndicatedAirspeed, 1),
            Groundspeed   = Math.Round(s.GroundVelocity, 1),
            VerticalSpeed = Math.Round(s.VerticalSpeed, 1),
            OnGround      = s.SimOnGround > 0.5
        };
    }
}
