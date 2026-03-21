// =============================================================================
// Telemetry/FuelState.cs — Fuel telemetry model for fuel prediction
// =============================================================================

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SimpleFlightTracker.Telemetry;

/// <summary>
/// Raw SimConnect data definition layout for fuel state.
/// Fields MUST match the order registered in SimConnectService.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct FuelStateStruct
{
    public double FuelTotalQuantity;   // gallons
    public double FuelTotalCapacity;   // gallons
    public double FuelFlowGPH;         // gallons per hour (all engines combined)
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

        return new FuelState
        {
            FuelQuantityGallons  = Math.Round(s.FuelTotalQuantity, 2),
            FuelCapacityGallons  = Math.Round(s.FuelTotalCapacity, 2),
            FuelFlowGPH          = Math.Round(s.FuelFlowGPH, 2),
            FuelRemainingPercent = percent
        };
    }
}
