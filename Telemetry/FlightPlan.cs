// =============================================================================
// Telemetry/FlightPlan.cs — Flight plan model (matches simpleflightplanner.com)
// =============================================================================

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Represents a single waypoint in a flight plan received from the planner.
/// </summary>
public sealed class FlightPlanWaypoint
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

/// <summary>
/// Flight plan received from the web planner via WebSocket.
/// Protocol: { "type": "flightplan", "departure": "KSEA", "arrival": "KPDX",
///             "waypoints": [...], "cruisingAltitude": 8000 }
/// </summary>
public sealed class FlightPlan
{
    [JsonPropertyName("departure")]
    public string Departure { get; set; } = string.Empty;

    [JsonPropertyName("arrival")]
    public string Arrival { get; set; } = string.Empty;

    [JsonPropertyName("waypoints")]
    public List<FlightPlanWaypoint> Waypoints { get; set; } = new();

    [JsonPropertyName("cruisingAltitude")]
    public double CruisingAltitude { get; set; }

    /// <summary>
    /// Writes this flight plan as an MSFS-compatible .pln XML file.
    /// Returns the full path to the written file.
    /// </summary>
    public string WritePln(string directory)
    {
        Directory.CreateDirectory(directory);

        var fileName = $"{Departure}_{Arrival}.pln";
        var filePath = Path.Combine(directory, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<SimBase.Document Type=\"AceXML\" version=\"1,0\">");
        sb.AppendLine("    <Descr>AceXML Document</Descr>");
        sb.AppendLine("    <FlightPlan.FlightPlan>");
        sb.AppendLine(CultureInvariant($"        <Title>{Departure} to {Arrival}</Title>"));
        sb.AppendLine("        <FPType>VFR</FPType>");
        sb.AppendLine(CultureInvariant($"        <CruisingAlt>{CruisingAltitude:F0}</CruisingAlt>"));
        sb.AppendLine(CultureInvariant($"        <DepartureID>{Departure}</DepartureID>"));
        sb.AppendLine(CultureInvariant($"        <DestinationID>{Arrival}</DestinationID>"));
        sb.AppendLine(CultureInvariant($"        <Descr>{Departure} to {Arrival}</Descr>"));
        sb.AppendLine("        <AppVersion>");
        sb.AppendLine("            <AppVersionMajor>11</AppVersionMajor>");
        sb.AppendLine("            <AppVersionBuild>0</AppVersionBuild>");
        sb.AppendLine("        </AppVersion>");

        foreach (var wp in Waypoints)
        {
            // Determine waypoint type: first/last are airports, middle are user waypoints
            var isAirport = wp.Name == Departure || wp.Name == Arrival;
            var wpType = isAirport ? "Airport" : "User";
            var lla = string.Format(CultureInfo.InvariantCulture,
                "{0:F6},{1:F6},{2:F6}", wp.Lat, wp.Lng, CruisingAltitude);

            sb.AppendLine(CultureInvariant($"        <ATCWaypoint id=\"{wp.Name}\">"));
            sb.AppendLine(CultureInvariant($"            <ATCWaypointType>{wpType}</ATCWaypointType>"));
            sb.AppendLine(CultureInvariant($"            <WorldPosition>{lla}</WorldPosition>"));
            if (isAirport)
            {
                sb.AppendLine("            <ICAO>");
                sb.AppendLine(CultureInvariant($"                <ICAOIdent>{wp.Name}</ICAOIdent>"));
                sb.AppendLine("            </ICAO>");
            }
            sb.AppendLine("        </ATCWaypoint>");
        }

        sb.AppendLine("    </FlightPlan.FlightPlan>");
        sb.AppendLine("</SimBase.Document>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    private static string CultureInvariant(FormattableString formattable)
    {
        return formattable.ToString(CultureInfo.InvariantCulture);
    }
}
