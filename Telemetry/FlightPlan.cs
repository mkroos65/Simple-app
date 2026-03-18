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

        // Find departure and destination waypoints for LLA fields
        var depWp = Waypoints.FirstOrDefault(w => w.Name == Departure);
        var arrWp = Waypoints.FirstOrDefault(w => w.Name == Arrival);
        var depLla = depWp != null ? ToWorldPosition(depWp.Lat, depWp.Lng, 0) : "";
        var arrLla = arrWp != null ? ToWorldPosition(arrWp.Lat, arrWp.Lng, 0) : "";

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<SimBase.Document Type=\"AceXML\" version=\"1,0\">");
        sb.AppendLine("    <Descr>AceXML Document</Descr>");
        sb.AppendLine("    <FlightPlan.FlightPlan>");
        sb.AppendLine($"        <Title>{Departure} to {Arrival}</Title>");
        sb.AppendLine($"        <Descr>{Departure} to {Arrival}</Descr>");
        sb.AppendLine("        <FPType>VFR</FPType>");
        sb.AppendLine("        <RouteType>Direct</RouteType>");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "        <CruisingAlt>{0:F0}</CruisingAlt>", CruisingAltitude));
        sb.AppendLine($"        <DepartureID>{Departure}</DepartureID>");
        sb.AppendLine($"        <DepartureLLA>{depLla}</DepartureLLA>");
        sb.AppendLine($"        <DestinationID>{Arrival}</DestinationID>");
        sb.AppendLine($"        <DestinationLLA>{arrLla}</DestinationLLA>");
        sb.AppendLine("        <AppVersion>");
        sb.AppendLine("            <AppVersionMajor>11</AppVersionMajor>");
        sb.AppendLine("            <AppVersionBuild>282174</AppVersionBuild>");
        sb.AppendLine("        </AppVersion>");

        foreach (var wp in Waypoints)
        {
            var isAirport = wp.Name == Departure || wp.Name == Arrival;
            var wpType = isAirport ? "Airport" : "User";
            // Airport waypoints get altitude 0, en-route waypoints get cruising altitude
            var wpAlt = isAirport ? 0.0 : CruisingAltitude;
            var worldPos = ToWorldPosition(wp.Lat, wp.Lng, wpAlt);

            sb.AppendLine($"        <ATCWaypoint id=\"{wp.Name}\">");
            sb.AppendLine($"            <ATCWaypointType>{wpType}</ATCWaypointType>");
            sb.AppendLine($"            <WorldPosition>{worldPos}</WorldPosition>");
            if (isAirport)
            {
                sb.AppendLine("            <ICAO>");
                sb.AppendLine($"                <ICAOIdent>{wp.Name}</ICAOIdent>");
                sb.AppendLine("            </ICAO>");
            }
            sb.AppendLine("        </ATCWaypoint>");
        }

        sb.AppendLine("    </FlightPlan.FlightPlan>");
        sb.AppendLine("</SimBase.Document>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    /// <summary>
    /// Converts decimal lat/lng/alt to MSFS DMS WorldPosition format:
    /// N47° 27' 0.00",W122° 18' 36.00",+008000.00
    /// </summary>
    private static string ToWorldPosition(double lat, double lng, double altFeet)
    {
        var latDir = lat >= 0 ? "N" : "S";
        var lngDir = lng >= 0 ? "E" : "W";

        var absLat = Math.Abs(lat);
        var absLng = Math.Abs(lng);

        int latDeg = (int)absLat;
        double latMinFull = (absLat - latDeg) * 60.0;
        int latMin = (int)latMinFull;
        double latSec = (latMinFull - latMin) * 60.0;

        int lngDeg = (int)absLng;
        double lngMinFull = (absLng - lngDeg) * 60.0;
        int lngMin = (int)lngMinFull;
        double lngSec = (lngMinFull - lngMin) * 60.0;

        var altSign = altFeet >= 0 ? "+" : "-";
        var altStr = string.Format(CultureInfo.InvariantCulture,
            "{0}{1:000000.00}", altSign, Math.Abs(altFeet));

        return string.Format(CultureInfo.InvariantCulture,
            "{0}{1}\u00b0 {2}' {3:F2}\",{4}{5}\u00b0 {6}' {7:F2}\",{8}",
            latDir, latDeg, latMin, latSec,
            lngDir, lngDeg, lngMin, lngSec,
            altStr);
    }
}
