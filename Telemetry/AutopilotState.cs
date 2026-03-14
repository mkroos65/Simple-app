// =============================================================================
// Telemetry/AutopilotState.cs — Autopilot telemetry model
// =============================================================================

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace MSFSCompanionBridge.Telemetry;

/// <summary>
/// Raw SimConnect data definition layout for autopilot state.
/// Fields MUST match the order registered in SimConnectService.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct AutopilotStateStruct
{
    public double AutopilotMaster;      // bool (0 or 1)
    public double AltitudeLock;         // bool (0 or 1)
    public double HeadingLock;          // bool (0 or 1)
    public double HeadingLockDir;       // degrees
    public double AltitudeLockVar;      // feet
}

/// <summary>
/// JSON-serialisable autopilot state payload.
/// </summary>
public sealed class AutopilotState
{
    [JsonPropertyName("master")]
    public bool Master { get; set; }

    [JsonPropertyName("altitudeLock")]
    public bool AltitudeLock { get; set; }

    [JsonPropertyName("headingLock")]
    public bool HeadingLock { get; set; }

    [JsonPropertyName("headingLockDir")]
    public double HeadingLockDir { get; set; }

    [JsonPropertyName("altitudeLockVar")]
    public double AltitudeLockVar { get; set; }

    public static AutopilotState FromStruct(AutopilotStateStruct s)
    {
        return new AutopilotState
        {
            Master         = s.AutopilotMaster > 0.5,
            AltitudeLock   = s.AltitudeLock > 0.5,
            HeadingLock    = s.HeadingLock > 0.5,
            HeadingLockDir = Math.Round(s.HeadingLockDir, 1),
            AltitudeLockVar = Math.Round(s.AltitudeLockVar, 0)
        };
    }
}
