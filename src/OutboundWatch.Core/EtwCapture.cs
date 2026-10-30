using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Strongthium.OutboundWatch;

/// <summary>What the kernel's network events and the DNS client's events tell the watcher.</summary>
public interface INetworkEvents
{
    /// <summary>Bytes sent or received on a connection. Which end is local isn't certain from the event alone: the
    /// watcher checks it against this PC's addresses.</summary>
    void Traffic(FlowKey key, int pid, long sent, long received, DateTime at);

    /// <summary>A TCP connection made from here (<paramref name="outbound"/>) or accepted from outside.</summary>
    void Opened(FlowKey key, int pid, bool outbound, DateTime at);

    void Closed(FlowKey key, int pid, DateTime at);

    /// <summary>A name the PC looked up, and the addresses it got.</summary>
    void Resolved(string name, IReadOnlyList<UInt128> addresses, DateTime at);
}

public enum NetEventKind { Send, Receive, Connect, Accept, Disconnect }

public readonly record struct NetEvent(NetEventKind Kind, FlowKey Key, int Pid, long Size);

/// <summary>
/// An ETW session of its own that reads two of Windows' providers as they happen: Microsoft-Windows-Kernel-Network
/// (every TCP and UDP send and receive, with its process and size, and every TCP connect, accept and disconnect) and
/// Microsoft-Windows-DNS-Client (every name looked up, and the addresses it gave). Starting a session needs
/// administrator rights. The session is stopped when this is disposed, and one left behind by a crash is stopped and
/// started again.
/// </summary>
public sealed unsafe class EtwCapture : IDisposable
{
    public const string SessionName = "Strongthium outbound-watch";
    public static readonly Guid KernelNetwork = new("7dd42a49-5329-4832-8dfd-43d979153a88");
    public static readonly Guid DnsClient = new("1c95126e-7eea-49a9-a3fe-a378b03ddb4d");
    // KERNEL_NETWORK_KEYWORD_IPV4 and _IPV6.
    private const ulong NetworkKeywords = 0x10 | 0x20;
    private const ushort DnsQueryCompleted = 3008;

    private readonly INetworkEvents sink;
    private readonly GCHandle self;
    private ulong session;
    private ulong trace = Native.InvalidProcessTraceHandle;
    private Thread? thread;
    private bool disposed;

    /// <summary>Why it couldn't start, when <see cref="Start"/> returned false.</summary>
    public string Error { get; private set; } = "";

    public EtwCapture(INetworkEvents sink)
    {
        this.sink = sink;
        self = GCHandle.Alloc(this);
    }

    public bool Start()
    {
        if (IntPtr.Size != 8)
        {
            Error = "Reading the network events needs the 64-bit program.";
            return false;
        }
        var result = StartSession(out session);
        if (result == Native.ErrorAlreadyExists)
        {
            StopSession();
            result = StartSession(out session);
        }
        if (result != Native.NoError)
        {
            Error = result == 5 ? "The network events need administrator rights." : $"The network events couldn't be started (error {result}).";
            return false;
        }

        Native.EnableTraceEx2(session, KernelNetwork, Native.EventControlCodeEnableProvider, 5, NetworkKeywords, 0, 0, 0);
        Native.EnableTraceEx2(session, DnsClient, Native.EventControlCodeEnableProvider, 5, ulong.MaxValue, 0, 0, 0);

        var logfile = new Native.EventTraceLogfile
        {
            ProcessTraceMode = Native.ProcessTraceModeRealTime | Native.ProcessTraceModeEventRecord,
            EventRecordCallback = &OnEvent,
            Context = GCHandle.ToIntPtr(self),
        };
        fixed (char* name = SessionName)
        {
            logfile.LoggerName = name;
            trace = Native.OpenTrace(&logfile);
        }
        if (trace == Native.InvalidProcessTraceHandle)
        {
            Error = "The network events couldn't be opened (error " + Marshal.GetLastPInvokeError() + ").";
            StopSession();
            return false;
        }

        thread = new Thread(() =>
        {
            var handle = trace;
            Native.ProcessTrace(&handle, 1, 0, 0);
        }) { IsBackground = true, Name = "outbound-watch ETW" };
        thread.Start();
        return true;
    }

    private static uint StartSession(out ulong handle)
    {
        var properties = Properties();
        try
        {
            fixed (char* name = SessionName) return Native.StartTrace(out handle, name, properties);
        }
        finally { NativeMemory.Free(properties); }
    }

    private static void StopSession()
    {
        var properties = Properties();
        try
        {
            fixed (char* name = SessionName) Native.ControlTrace(0, name, properties, Native.EventTraceControlStop);
        }
        finally { NativeMemory.Free(properties); }
    }

    /// <summary>A real-time session's properties, with room after them for its name, which Windows copies there.</summary>
    private static Native.EventTraceProperties* Properties()
    {
        var size = (uint)(sizeof(Native.EventTraceProperties) + (SessionName.Length + 1) * 2 + 1024);
        var block = NativeMemory.AllocZeroed(size);
        var p = (Native.EventTraceProperties*)block;
        p->WnodeBufferSize = size;
        p->Flags = Native.WnodeFlagTracedGuid;
        p->ClientContext = 1;
        p->Guid = Guid.NewGuid();
        p->BufferSize = 64;
        p->MinimumBuffers = 16;
        p->MaximumBuffers = 128;
        p->LogFileMode = Native.EventTraceRealTimeMode;
        p->FlushTimer = 1;
        p->LoggerNameOffset = (uint)sizeof(Native.EventTraceProperties);
        return p;
    }

    [UnmanagedCallersOnly]
    private static void OnEvent(Native.EventRecord* record)
    {
        try
        {
            if (GCHandle.FromIntPtr(record->UserContext).Target is EtwCapture capture) capture.Handle(record);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine("outbound-watch: event: " + exception.Message);
        }
    }

    private void Handle(Native.EventRecord* record)
    {
        var data = new ReadOnlySpan<byte>(record->UserData, record->UserDataLength);
        var at = Time(record->TimeStamp);
        if (record->ProviderId == KernelNetwork)
        {
            if (!TryParseNetwork(record->Id, data, out var e)) return;
            switch (e.Kind)
            {
                case NetEventKind.Send: sink.Traffic(e.Key, e.Pid, e.Size, 0, at); break;
                case NetEventKind.Receive: sink.Traffic(e.Key, e.Pid, 0, e.Size, at); break;
                case NetEventKind.Connect: sink.Opened(e.Key, e.Pid, true, at); break;
                case NetEventKind.Accept: sink.Opened(e.Key, e.Pid, false, at); break;
                case NetEventKind.Disconnect: sink.Closed(e.Key, e.Pid, at); break;
            }
        }
        else if (record->ProviderId == DnsClient && record->Id == DnsQueryCompleted)
        {
            if (TryParseDnsQuery(data, out var name, out var addresses)) sink.Resolved(name, addresses, at);
        }
    }

    /// <summary>The event's time, which ProcessTrace gives as a FILETIME; now, if it's far off.</summary>
    private static DateTime Time(long fileTime)
    {
        var now = DateTime.UtcNow;
        if (fileTime <= 0) return now;
        var at = DateTime.FromFileTimeUtc(fileTime);
        return Math.Abs((at - now).TotalMinutes) < 10 ? at : now;
    }

    /// <summary>
    /// A Microsoft-Windows-Kernel-Network event. Every one this reads starts the same way: the process (4 bytes), the
    /// size (4), the destination and source addresses (4 bytes each for IPv4, 16 for IPv6, in network order), and the
    /// destination and source ports (2 bytes each, network order). The destination is the far end, whether the event is
    /// a send or a receive.
    /// </summary>
    internal static bool TryParseNetwork(ushort id, ReadOnlySpan<byte> data, out NetEvent e)
    {
        e = default;
        (Protocol protocol, bool v6, NetEventKind kind) = id switch
        {
            10 => (Protocol.Tcp, false, NetEventKind.Send),
            11 => (Protocol.Tcp, false, NetEventKind.Receive),
            12 => (Protocol.Tcp, false, NetEventKind.Connect),
            13 => (Protocol.Tcp, false, NetEventKind.Disconnect),
            15 => (Protocol.Tcp, false, NetEventKind.Accept),
            26 => (Protocol.Tcp, true, NetEventKind.Send),
            27 => (Protocol.Tcp, true, NetEventKind.Receive),
            28 => (Protocol.Tcp, true, NetEventKind.Connect),
            29 => (Protocol.Tcp, true, NetEventKind.Disconnect),
            31 => (Protocol.Tcp, true, NetEventKind.Accept),
            42 => (Protocol.Udp, false, NetEventKind.Send),
            43 => (Protocol.Udp, false, NetEventKind.Receive),
            58 => (Protocol.Udp, true, NetEventKind.Send),
            59 => (Protocol.Udp, true, NetEventKind.Receive),
            _ => (Protocol.Tcp, false, (NetEventKind)(-1)),
        };
        if ((int)kind < 0 || data.Length < (v6 ? 44 : 20)) return false;
        var pid = BinaryPrimitives.ReadInt32LittleEndian(data);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        UInt128 remote, local;
        int portAt;
        if (v6)
        {
            remote = Addresses.FromV6(data.Slice(8, 16));
            local = Addresses.FromV6(data.Slice(24, 16));
            portAt = 40;
        }
        else
        {
            remote = Addresses.FromV4(BinaryPrimitives.ReadUInt32LittleEndian(data[8..]));
            local = Addresses.FromV4(BinaryPrimitives.ReadUInt32LittleEndian(data[12..]));
            portAt = 16;
        }
        var remotePort = BinaryPrimitives.ReadUInt16BigEndian(data[portAt..]);
        var localPort = BinaryPrimitives.ReadUInt16BigEndian(data[(portAt + 2)..]);
        e = new NetEvent(kind, new FlowKey(protocol, local, localPort, remote, remotePort), pid, size);
        return true;
    }

    /// <summary>
    /// Microsoft-Windows-DNS-Client's event 3008, a query finished: the name (UTF-16, ended by a 0), its type (4 bytes),
    /// options (8), status (4) and the results, as text: <c>type:  5 cdn.example.net;::ffff:192.0.2.10;</c>, a CNAME
    /// written with its type, addresses as they are, IPv4 mapped into IPv6.
    /// </summary>
    internal static bool TryParseDnsQuery(ReadOnlySpan<byte> data, out string name, out IReadOnlyList<UInt128> addresses)
    {
        addresses = [];
        name = ReadString(ref data);
        if (name.Length == 0 || data.Length < 16) return false;
        var status = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
        data = data[16..];
        if (status != 0) return false;
        addresses = ParseDnsResults(ReadString(ref data));
        return addresses.Count > 0;
    }

    internal static List<UInt128> ParseDnsResults(string results)
    {
        var list = new List<UInt128>();
        foreach (var part in results.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("type:", StringComparison.Ordinal)) continue;
            if (Addresses.TryParse(part, out var address) && !list.Contains(address)) list.Add(address);
        }
        return list;
    }

    private static string ReadString(ref ReadOnlySpan<byte> data)
    {
        var chars = MemoryMarshal.Cast<byte, char>(data[..(data.Length & ~1)]);
        var end = chars.IndexOf('\0');
        if (end < 0) end = chars.Length;
        var text = new string(chars[..end]);
        data = data[Math.Min(data.Length, (end + 1) * 2)..];
        return text;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (session != 0) StopSession();
        if (trace != Native.InvalidProcessTraceHandle) Native.CloseTrace(trace);
        thread?.Join(2000);
        self.Free();
    }
}
