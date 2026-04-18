// =============================================================================
// Telemetry/FuelState.cs — Fuel telemetry model for fuel prediction
// =============================================================================

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Raw SimConnect data definition layout for fuel state.
/// Fields MUST match the order registered in SimConnectService.
/// Registers fuel flow for engines 1-4 individually; FromStruct sums them.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct FuelStateStruct
{
    public double FuelTotalQuantity;   // gallons
    public double FuelTotalCapacity;   // gallons
    public double FuelFlowGPH1;        // gallons per hour, engine 1
    public double FuelFlowGPH2;        // gallons per hour, engine 2
    public double FuelFlowGPH3;        // gallons per hour, engine 3
    public double FuelFlowGPH4;        // gallons per hour, engine 4
}

/// <summary>
/// JSON-serialisable fuel state payload.
/// </summary>
public sealed class FuelState
{
    [JsonPropertyName("fuelQuantityGallons")]
    public double FuelQuantityGallons { get; set; }

    [JsonPropertyName("fuelCapacityGallons")]
    public double FuelCapacityGallons { get; set; }

    [JsonPropertyName("fuelFlowGPH")]
    public double FuelFlowGPH { get; set; }

    [JsonPropertyName("fuelRemainingPercent")]
    public double FuelRemainingPercent { get; set; }

    public static FuelState FromStruct(FuelStateStruct s)
    {
        var percent = s.FuelTotalCapacity > 0
            ? Math.Round((s.FuelTotalQuantity / s.FuelTotalCapacity) * 100.0, 1)
            : 0.0;

        // Sum fuel flow across all engines (unused engines report 0)
        var totalFlow = s.FuelFlowGPH1 + s.FuelFlowGPH2 + s.FuelFlowGPH3 + s.FuelFlowGPH4;

        return new FuelState
        {
            FuelQuantityGallons  = Math.Round(s.FuelTotalQuantity, 2),
            FuelCapacityGallons  = Math.Round(s.FuelTotalCapacity, 2),
            FuelFlowGPH          = Math.Round(totalFlow, 2),
            FuelRemainingPercent = percent
        };
    }
}
