using System.Runtime.InteropServices;

namespace Strongthium.OutboundWatch;

/// <summary>The Windows calls: the connection tables (iphlpapi), the processes and services (kernel32, advapi32), the
/// DNS cache (dnsapi) and an ETW session of its own (advapi32).</summary>
internal static unsafe partial class Native
{
    public const uint NoError = 0, ErrorInsufficientBuffer = 122, ErrorMoreData = 234, ErrorAlreadyExists = 183;
    public const uint AfInet = 2, AfInet6 = 23;
    public const int TcpTableOwnerPidAll = 5;

    [LibraryImport("iphlpapi.dll")]
    public static partial uint GetExtendedTcpTable(void* table, ref uint size, [MarshalAs(UnmanagedType.Bool)] bool order, uint af, int tableClass, uint reserved);

    // ---- Processes -------------------------------------------------------------------------------------------------

    public const uint ProcessQueryLimitedInformation = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageName(nint process, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetProcessTimes(nint process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    // ---- Services --------------------------------------------------------------------------------------------------

    public const uint ScManagerEnumerateService = 0x4, ServiceWin32 = 0x30, ServiceActive = 0x1;

    [LibraryImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true)]
    public static partial nint OpenSCManager(char* machine, char* database, uint access);

    [LibraryImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumServicesStatusEx(nint manager, int infoLevel, uint serviceType, uint state, byte* services, uint size,
        out uint needed, out uint returned, ref uint resume, char* group);

    public const uint ServiceQueryConfig = 0x1;

    [LibraryImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true)]
    public static partial nint OpenService(nint manager, char* name, uint access);

    /// <summary>QUERY_SERVICE_CONFIGW: three 4-byte fields, then the program's command line (a pointer).</summary>
    [LibraryImport("advapi32.dll", EntryPoint = "QueryServiceConfigW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryServiceConfig(nint service, byte* config, uint size, out uint needed);

    [LibraryImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseServiceHandle(nint handle);

    // ---- DNS cache -------------------------------------------------------------------------------------------------

    public const ushort DnsTypeA = 1, DnsTypeCname = 5, DnsTypeAaaa = 28;
    /// <summary>DNS_QUERY_NO_WIRE_QUERY: only the cache, nothing sent.</summary>
    public const uint DnsQueryNoWireQuery = 0x10;

    /// <summary>The names in the DNS client's cache. Not in Windows' headers, but in dnsapi.dll since Windows 2000 and
    /// what <c>ipconfig /displaydns</c> starts from.</summary>
    [LibraryImport("dnsapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DnsGetCacheDataTable(out DnsCacheEntry* first);

    [LibraryImport("dnsapi.dll", EntryPoint = "DnsQuery_W")]
    public static partial int DnsQuery(char* name, ushort type, uint options, nint extra, out DnsRecord* results, nint reserved);

    [LibraryImport("dnsapi.dll")]
    public static partial void DnsRecordListFree(DnsRecord* list, int freeType);

    [LibraryImport("dnsapi.dll")]
    public static partial void DnsFree(void* data, int freeType);

    [StructLayout(LayoutKind.Sequential)]
    public struct DnsCacheEntry
    {
        public DnsCacheEntry* Next;
        public char* Name;
        public ushort Type;
        public ushort DataLength;
        public uint Flags;
    }

    /// <summary>DNS_RECORDW up to its data: an A record's address (4 bytes), an AAAA record's (16), or a CNAME's
    /// name (a pointer).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DnsRecord
    {
        public DnsRecord* Next;
        public char* Name;
        public ushort Type;
        public ushort DataLength;
        public uint Flags;
        public uint Ttl;
        public uint Reserved;
        public fixed byte Data[16];
    }

    // ---- ETW -------------------------------------------------------------------------------------------------------

    public const uint WnodeFlagTracedGuid = 0x20000, EventTraceRealTimeMode = 0x100;
    public const uint EventTraceControlStop = 1;
    public const uint EventControlCodeEnableProvider = 1;
    public const uint ProcessTraceModeRealTime = 0x100, ProcessTraceModeEventRecord = 0x10000000;
    public const ulong InvalidProcessTraceHandle = ulong.MaxValue;

    /// <summary>EVENT_TRACE_PROPERTIES, followed in the same block by the session's name.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EventTraceProperties
    {
        // WNODE_HEADER
        public uint WnodeBufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public long TimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
        // The rest
        public uint BufferSize;
        public uint MinimumBuffers;
        public uint MaximumBuffers;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint FlushTimer;
        public uint EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers;
        public uint FreeBuffers;
        public uint EventsLost;
        public uint BuffersWritten;
        public uint LogBuffersLost;
        public uint RealTimeBuffersLost;
        public nint LoggerThreadId;
        public uint LogFileNameOffset;
        public uint LoggerNameOffset;
    }

    /// <summary>EVENT_TRACE_LOGFILEW on x64 (448 bytes), with only the fields that are set.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 448)]
    public struct EventTraceLogfile
    {
        [FieldOffset(8)] public char* LoggerName;
        [FieldOffset(28)] public uint ProcessTraceMode;
        [FieldOffset(424)] public delegate* unmanaged<EventRecord*, void> EventRecordCallback;
        [FieldOffset(440)] public nint Context;
    }

    /// <summary>EVENT_RECORD on x64, with EVENT_HEADER and its EVENT_DESCRIPTOR laid out in it.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct EventRecord
    {
        [FieldOffset(4)] public ushort Flags;
        [FieldOffset(12)] public uint ProcessId;
        [FieldOffset(16)] public long TimeStamp;
        [FieldOffset(24)] public Guid ProviderId;
        [FieldOffset(40)] public ushort Id;
        [FieldOffset(42)] public byte Version;
        [FieldOffset(45)] public byte Opcode;
        [FieldOffset(86)] public ushort UserDataLength;
        [FieldOffset(96)] public byte* UserData;
        [FieldOffset(104)] public nint UserContext;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "StartTraceW")]
    public static partial uint StartTrace(out ulong session, char* name, EventTraceProperties* properties);

    [LibraryImport("advapi32.dll", EntryPoint = "ControlTraceW")]
    public static partial uint ControlTrace(ulong session, char* name, EventTraceProperties* properties, uint control);

    [LibraryImport("advapi32.dll")]
    public static partial uint EnableTraceEx2(ulong session, in Guid provider, uint control, byte level, ulong matchAny, ulong matchAll, uint timeout, nint parameters);

    [LibraryImport("advapi32.dll", EntryPoint = "OpenTraceW")]
    public static partial ulong OpenTrace(EventTraceLogfile* logfile);

    [LibraryImport("advapi32.dll")]
    public static partial uint ProcessTrace(ulong* handles, uint count, nint start, nint end);

    [LibraryImport("advapi32.dll")]
    public static partial uint CloseTrace(ulong handle);
}
