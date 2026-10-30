using System.Buffers.Binary;

namespace Strongthium.OutboundWatch;

/// <summary>The TCP states of Windows' table (MIB_TCP_STATE).</summary>
public enum TcpState
{
    Closed = 1, Listen, SynSent, SynReceived, Established, FinWait1, FinWait2, CloseWait, Closing, LastAck, TimeWait, DeleteTcb,
}

/// <summary>A line of Windows' TCP table: a connection, which process has it, and its state.</summary>
public readonly record struct TcpRow(FlowKey Key, int Pid, TcpState State)
{
    /// <summary>Still a connection: not listening, and not closed or waiting out its close (TIME_WAIT, which has no
    /// process any more).</summary>
    public bool IsOpen => State is >= TcpState.SynSent and <= TcpState.LastAck;
}

/// <summary>
/// Windows' table of TCP connections, IPv4 and IPv6, each with its process (GetExtendedTcpTable). Anyone can read it:
/// this is how connections are seen without administrator rights. It has no byte counts and, read once a second,
/// misses connections that open and close in between.
/// </summary>
public static unsafe class ConnectionTable
{
    public static List<TcpRow> Read()
    {
        var rows = new List<TcpRow>(256);
        ReadFamily(Native.AfInet, rows);
        ReadFamily(Native.AfInet6, rows);
        return rows;
    }

    private static void ReadFamily(uint family, List<TcpRow> rows)
    {
        uint size = 64 * 1024;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var buffer = new byte[size];
            uint result;
            fixed (byte* p = buffer) result = Native.GetExtendedTcpTable(p, ref size, false, family, Native.TcpTableOwnerPidAll, 0);
            if (result == Native.ErrorInsufficientBuffer)
            {
                size += 16 * 1024;
                continue;
            }
            if (result != Native.NoError) return;
            if (family == Native.AfInet) ParseV4(buffer, rows);
            else ParseV6(buffer, rows);
            return;
        }
    }

    /// <summary>MIB_TCPTABLE_OWNER_PID: a count, then rows of state, local address, local port, remote address,
    /// remote port and process, each 4 bytes; addresses and ports in network order.</summary>
    internal static void ParseV4(ReadOnlySpan<byte> table, List<TcpRow> rows)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(table);
        for (var i = 0; i < count; i++)
        {
            var row = table.Slice(4 + i * 24, 24);
            var state = (TcpState)BinaryPrimitives.ReadInt32LittleEndian(row);
            var key = new FlowKey(Protocol.Tcp,
                Addresses.FromV4(BinaryPrimitives.ReadUInt32LittleEndian(row[4..])), Port(row[8..]),
                Addresses.FromV4(BinaryPrimitives.ReadUInt32LittleEndian(row[12..])), Port(row[16..]));
            rows.Add(new TcpRow(key, BinaryPrimitives.ReadInt32LittleEndian(row[20..]), state));
        }
    }

    /// <summary>MIB_TCP6TABLE_OWNER_PID: a count, then rows of local address (16 bytes), scope, port, remote address,
    /// scope, port, state and process.</summary>
    internal static void ParseV6(ReadOnlySpan<byte> table, List<TcpRow> rows)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(table);
        for (var i = 0; i < count; i++)
        {
            var row = table.Slice(4 + i * 56, 56);
            var key = new FlowKey(Protocol.Tcp,
                Addresses.FromV6(row[..16]), Port(row[20..]),
                Addresses.FromV6(row[24..40]), Port(row[44..]));
            rows.Add(new TcpRow(key, BinaryPrimitives.ReadInt32LittleEndian(row[52..]), (TcpState)BinaryPrimitives.ReadInt32LittleEndian(row[48..])));
        }
    }

    /// <summary>The port, kept in network order in the low two bytes of a 4-byte field.</summary>
    private static int Port(ReadOnlySpan<byte> field) => field[0] << 8 | field[1];
}
