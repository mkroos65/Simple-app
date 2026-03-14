// =============================================================================
// SimConnect Stub Library
// =============================================================================
// This is a build-time stub that provides the minimal SimConnect type
// definitions needed to compile the MSFS Companion Bridge project on machines
// that do not have the Microsoft Flight Simulator SDK installed.
//
// At runtime on Windows, the real Microsoft.FlightSimulator.SimConnect.dll
// (from the MSFS SDK) must replace this stub. See README.md for details.
// =============================================================================

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.FlightSimulator.SimConnect
{
    // -------------------------------------------------------------------------
    // Enumerations
    // -------------------------------------------------------------------------

    public enum SIMCONNECT_DATATYPE
    {
        INVALID,
        INT32,
        INT64,
        FLOAT32,
        FLOAT64,
        STRING8,
        STRING32,
        STRING64,
        STRING128,
        STRING256,
        STRING260,
        STRINGV,
        INITPOSITION,
        MARKERSTATE,
        WAYPOINT,
        LATLONALT,
        XYZ
    }

    public enum SIMCONNECT_PERIOD
    {
        NEVER,
        ONCE,
        VISUAL_FRAME,
        SIM_FRAME,
        SECOND
    }

    public enum SIMCONNECT_SIMOBJECT_TYPE
    {
        USER,
        ALL,
        AIRCRAFT,
        HELICOPTER,
        BOAT,
        GROUND
    }

    public enum SIMCONNECT_DATA_REQUEST_FLAG : uint
    {
        DEFAULT = 0,
        CHANGED = 1,
        TAGGED = 2
    }

    // -------------------------------------------------------------------------
    // Receive data structures
    // -------------------------------------------------------------------------

    public class SIMCONNECT_RECV
    {
        public uint dwSize;
        public uint dwVersion;
        public uint dwID;
    }

    public class SIMCONNECT_RECV_OPEN : SIMCONNECT_RECV
    {
        public string szApplicationName = string.Empty;
        public uint dwApplicationVersionMajor;
        public uint dwApplicationVersionMinor;
        public uint dwApplicationBuildMajor;
        public uint dwApplicationBuildMinor;
        public uint dwSimConnectVersionMajor;
        public uint dwSimConnectVersionMinor;
        public uint dwSimConnectBuildMajor;
        public uint dwSimConnectBuildMinor;
        public uint dwReserved1;
        public uint dwReserved2;
    }

    public class SIMCONNECT_RECV_QUIT : SIMCONNECT_RECV
    {
    }

    public class SIMCONNECT_RECV_EXCEPTION : SIMCONNECT_RECV
    {
        public uint dwException;
        public uint dwSendID;
        public uint dwIndex;
    }

    public class SIMCONNECT_RECV_SIMOBJECT_DATA : SIMCONNECT_RECV
    {
        public uint dwRequestID;
        public uint dwObjectID;
        public uint dwDefineID;
        public uint dwFlags;
        public uint dwentrynumber;
        public uint dwoutof;
        public uint dwDefineCount;
        public object? dwData;
    }

    public class SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE : SIMCONNECT_RECV_SIMOBJECT_DATA
    {
    }

    // -------------------------------------------------------------------------
    // Delegates
    // -------------------------------------------------------------------------

    public delegate void SignalProcDelegate();
    public delegate void RecvOpenEventHandler(SimConnect sender, SIMCONNECT_RECV_OPEN data);
    public delegate void RecvQuitEventHandler(SimConnect sender, SIMCONNECT_RECV_QUIT data);
    public delegate void RecvExceptionEventHandler(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data);
    public delegate void RecvSimobjectDataBytypeEventHandler(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data);

    // -------------------------------------------------------------------------
    // SimConnect class (stub)
    // -------------------------------------------------------------------------

    public class SimConnect : IDisposable
    {
        public const uint SIMCONNECT_UNUSED = 0xFFFFFFFF;

        public event RecvOpenEventHandler? OnRecvOpen;
        public event RecvQuitEventHandler? OnRecvQuit;
        public event RecvExceptionEventHandler? OnRecvException;
        public event RecvSimobjectDataBytypeEventHandler? OnRecvSimobjectDataBytype;

        public SimConnect(string name, IntPtr hWnd, uint userMsg, WaitHandle? eventHandle, uint configIndex)
        {
            throw new NotImplementedException(
                "This is a build-time stub. Replace with the real SimConnect assembly from the MSFS SDK.");
        }

        public void AddToDataDefinition(
            Enum defineId,
            string datumName,
            string? unitsName,
            SIMCONNECT_DATATYPE datumType,
            float epsilon,
            uint datumId)
        {
            throw new NotImplementedException();
        }

        public void RegisterDataDefineStruct<T>(Enum defineId)
        {
            throw new NotImplementedException();
        }

        public void RequestDataOnSimObjectType(
            Enum requestId,
            Enum defineId,
            uint radiusMeters,
            SIMCONNECT_SIMOBJECT_TYPE type)
        {
            throw new NotImplementedException();
        }

        public void ReceiveMessage()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
