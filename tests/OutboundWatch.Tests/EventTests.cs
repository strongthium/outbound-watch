using System.Buffers.Binary;
using System.Text;

namespace Strongthium.OutboundWatch.Tests;

/// <summary>The bytes of Windows' tables and events, made here as Windows lays them out.</summary>
public sealed class EventTests
{
    private static UInt128 A(string text) => Addresses.TryParse(text, out var a) ? a : throw new ArgumentException(text);

    [Fact]
    public void A_TCP_send_over_IPv4()
    {
        var data = new byte[36];
        BinaryPrimitives.WriteInt32LittleEndian(data, 4120);              // PID
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 1460);   // size
        new byte[] { 203, 0, 113, 5 }.CopyTo(data, 8);                    // daddr
        new byte[] { 192, 168, 1, 20 }.CopyTo(data, 12);                  // saddr
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(16), 443);      // dport
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(18), 51000);    // sport
        Assert.True(EtwCapture.TryParseNetwork(10, data, out var e));
        Assert.Equal(NetEventKind.Send, e.Kind);
        Assert.Equal(4120, e.Pid);
        Assert.Equal(1460, e.Size);
        Assert.Equal(new FlowKey(Protocol.Tcp, A("192.168.1.20"), 51000, A("203.0.113.5"), 443), e.Key);
    }

    [Fact]
    public void A_UDP_receive_over_IPv6()
    {
        var data = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(data, 77);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 1200);
        System.Net.IPAddress.Parse("2001:db8::5").GetAddressBytes().CopyTo(data, 8);
        System.Net.IPAddress.Parse("2001:db8:ffff::20").GetAddressBytes().CopyTo(data, 24);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(40), 443);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(42), 60000);
        Assert.True(EtwCapture.TryParseNetwork(59, data, out var e));
        Assert.Equal(NetEventKind.Receive, e.Kind);
        Assert.Equal(new FlowKey(Protocol.Udp, A("2001:db8:ffff::20"), 60000, A("2001:db8::5"), 443), e.Key);
    }

    [Fact]
    public void Other_and_short_events_are_left()
    {
        Assert.False(EtwCapture.TryParseNetwork(14, new byte[40], out _));
        Assert.False(EtwCapture.TryParseNetwork(10, new byte[10], out _));
        Assert.False(EtwCapture.TryParseNetwork(26, new byte[30], out _));
    }

    [Fact]
    public void A_DNS_query_and_its_addresses()
    {
        var data = new List<byte>();
        data.AddRange(Encoding.Unicode.GetBytes("www.example.org\0"));
        data.AddRange(BitConverter.GetBytes(1u));     // type
        data.AddRange(BitConverter.GetBytes(0ul));    // options
        data.AddRange(BitConverter.GetBytes(0u));     // status
        data.AddRange(Encoding.Unicode.GetBytes("type:  5 cdn.example.net;::ffff:192.0.2.10;2001:db8::10;::ffff:192.0.2.10;\0"));
        Assert.True(EtwCapture.TryParseDnsQuery(data.ToArray(), out var name, out var addresses));
        Assert.Equal("www.example.org", name);
        Assert.Equal([A("192.0.2.10"), A("2001:db8::10")], addresses);
    }

    [Fact]
    public void A_failed_DNS_query_gives_nothing()
    {
        var data = new List<byte>();
        data.AddRange(Encoding.Unicode.GetBytes("nowhere.example\0"));
        data.AddRange(new byte[12]);
        data.AddRange(BitConverter.GetBytes(9003u));
        data.AddRange(Encoding.Unicode.GetBytes("\0"));
        Assert.False(EtwCapture.TryParseDnsQuery(data.ToArray(), out _, out _));
    }

    [Fact]
    public void The_TCP_tables()
    {
        var v4 = new byte[4 + 24];
        BinaryPrimitives.WriteInt32LittleEndian(v4, 1);
        BinaryPrimitives.WriteInt32LittleEndian(v4.AsSpan(4), (int)TcpState.Established);
        new byte[] { 192, 168, 1, 20 }.CopyTo(v4, 8);
        v4[12] = 0xC7; v4[13] = 0x38;                                     // 51000, in network order
        new byte[] { 203, 0, 113, 5 }.CopyTo(v4, 16);
        v4[20] = 0x01; v4[21] = 0xBB;                                     // 443
        BinaryPrimitives.WriteInt32LittleEndian(v4.AsSpan(24), 4120);
        var rows = new List<TcpRow>();
        ConnectionTable.ParseV4(v4, rows);
        Assert.Equal(new TcpRow(new FlowKey(Protocol.Tcp, A("192.168.1.20"), 51000, A("203.0.113.5"), 443), 4120, TcpState.Established), rows[0]);
        Assert.True(rows[0].IsOpen);

        var v6 = new byte[4 + 56];
        BinaryPrimitives.WriteInt32LittleEndian(v6, 1);
        System.Net.IPAddress.Parse("2001:db8:ffff::20").GetAddressBytes().CopyTo(v6, 4);
        v6[24] = 0xC7; v6[25] = 0x38;
        System.Net.IPAddress.Parse("2001:db8::5").GetAddressBytes().CopyTo(v6, 28);
        v6[48] = 0x01; v6[49] = 0xBB;
        BinaryPrimitives.WriteInt32LittleEndian(v6.AsSpan(52), (int)TcpState.TimeWait);
        BinaryPrimitives.WriteInt32LittleEndian(v6.AsSpan(56), 0);
        rows.Clear();
        ConnectionTable.ParseV6(v6, rows);
        Assert.Equal(new FlowKey(Protocol.Tcp, A("2001:db8:ffff::20"), 51000, A("2001:db8::5"), 443), rows[0].Key);
        Assert.False(rows[0].IsOpen);
    }

    [Theory]
    [InlineData(@"C:\Windows\system32\svchost.exe -k NetworkService -p", @"C:\Windows\system32\svchost.exe")]
    [InlineData(@"""C:\Program Files\Example\service.exe"" --run", @"C:\Program Files\Example\service.exe")]
    [InlineData(@"C:\Program Files\Example App\app.exe /service", @"C:\Program Files\Example App\app.exe")]
    public void A_service_program(string commandLine, string program) => Assert.Equal(program, Processes.ProgramOf(commandLine));
}
