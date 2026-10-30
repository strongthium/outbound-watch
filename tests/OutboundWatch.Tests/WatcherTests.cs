namespace Strongthium.OutboundWatch.Tests;

/// <summary>The watcher, fed readings of the TCP table and network events made up here.</summary>
public sealed class WatcherTests
{
    private static readonly DateTime T0 = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UInt128 Me = A("192.168.1.20");

    private static UInt128 A(string text) => Addresses.TryParse(text, out var a) ? a : throw new ArgumentException(text);

    private static FlowKey Key(string remote, int port = 443, int localPort = 51000, Protocol protocol = Protocol.Tcp) =>
        new(protocol, Me, localPort, A(remote), port);

    private static TcpRow Row(FlowKey key, int pid = 4120, TcpState state = TcpState.Established) => new(key, pid, state);

    private static Watcher Make(Database? database = null)
    {
        var watcher = new Watcher(database, pid => new ProcessInfo(pid == 4120 ? "firefox.exe" : $"p{pid}.exe", "", "")) { ReverseLookups = false };
        watcher.SetLocalAddresses([Me]);
        return watcher;
    }

    [Fact]
    public void A_connection_from_the_table_starts_and_ends()
    {
        var w = Make();
        var key = Key("203.0.113.5");
        w.Apply([Row(key)], T0);
        var c = Assert.Single(w.Snapshot());
        Assert.Equal("firefox.exe", c.Process);
        Assert.True(c.OpenAtStart);
        Assert.True(c.IsOpen);

        w.Apply([], T0.AddSeconds(1));
        Assert.Single(w.Snapshot()); // missing once: the table may have been read as it changed
        w.Apply([Row(key)], T0.AddSeconds(2));
        w.Apply([], T0.AddSeconds(3));
        w.Apply([], T0.AddSeconds(4));
        Assert.Empty(w.Snapshot());
    }

    [Fact]
    public void A_new_connection_after_the_first_reading_started_while_watching()
    {
        var w = Make();
        w.Apply([], T0);
        w.Apply([Row(Key("203.0.113.5"))], T0.AddSeconds(1));
        var c = Assert.Single(w.Snapshot());
        Assert.False(c.OpenAtStart);
        Assert.Equal(T0.AddSeconds(1), c.Started);
    }

    [Fact]
    public void Incoming_local_loopback_and_closed_are_left_out()
    {
        var w = Make();
        w.Apply(
        [
            Row(new FlowKey(Protocol.Tcp, Me, 3389, 0, 0), state: TcpState.Listen),
            Row(Key("203.0.113.9", port: 60000, localPort: 3389)),           // someone connected in to Remote Desktop
            Row(Key("192.168.1.4", port: 445)),                            // the NAS
            Row(new FlowKey(Protocol.Tcp, A("127.0.0.1"), 5000, A("127.0.0.1"), 5001)),
            Row(Key("203.0.113.6"), state: TcpState.TimeWait),
            Row(Key("203.0.113.7")),
        ], T0);
        var c = Assert.Single(w.Snapshot());
        Assert.Equal(A("203.0.113.7"), c.Remote);

        w.WatchLocalNetwork = true;
        w.Apply([Row(Key("192.168.1.4", port: 445), pid: 4)], T0.AddSeconds(1));
        Assert.Contains(w.Snapshot(), x => x.Place == Place.LocalNetwork);
    }

    [Fact]
    public void Events_count_bytes_either_way_round()
    {
        var w = Make();
        var key = Key("203.0.113.5");
        w.Opened(key, 4120, outbound: true, T0);
        w.Traffic(key, 4120, 500, 0, T0.AddSeconds(1));
        // The same connection, written from the far end.
        var turned = new FlowKey(Protocol.Tcp, key.Remote, key.RemotePort, key.Local, key.LocalPort);
        w.Traffic(turned, 4120, 0, 7000, T0.AddSeconds(2));
        var c = Assert.Single(w.Snapshot());
        Assert.Equal(500, c.Sent);
        Assert.Equal(7000, c.Received);
        Assert.True(c.Measured);
        Assert.Equal(T0.AddSeconds(2), c.LastSeen);

        w.Closed(key, 4120, T0.AddSeconds(5));
        Assert.Empty(w.Snapshot());
    }

    [Fact]
    public void Udp_goes_out_when_this_end_sends_first_and_ends_when_quiet()
    {
        var w = Make();
        var dns = Key("203.0.113.53", port: 53, localPort: 60000, protocol: Protocol.Udp);
        var inbound = Key("203.0.113.99", port: 40000, localPort: 3478, protocol: Protocol.Udp);
        w.Traffic(dns, 1460, 60, 0, T0);
        w.Traffic(dns, 1460, 0, 180, T0.AddSeconds(0.1));
        w.Traffic(inbound, 1460, 0, 100, T0);
        w.Traffic(inbound, 1460, 100, 0, T0.AddSeconds(1));
        var c = Assert.Single(w.Snapshot());
        Assert.Equal(Protocol.Udp, c.Protocol);
        Assert.Equal(180, c.Received);

        w.Housekeep(T0.AddMinutes(1));
        Assert.Single(w.Snapshot());
        w.Housekeep(T0.AddMinutes(3));
        Assert.Empty(w.Snapshot());
    }

    [Fact]
    public void Names_and_places_come_to_open_connections()
    {
        var w = Make();
        w.SetPlaces(SampleData.Lookup);
        var key = Key("192.0.2.10");
        w.Opened(key, 4120, true, T0);
        Assert.Equal("", w.Snapshot()[0].Host);
        Assert.Equal(64500, w.Snapshot()[0].Place.Asn);
        w.Resolved("www.example.org", [key.Remote], T0.AddSeconds(1));
        var c = w.Snapshot()[0];
        Assert.Equal("www.example.org", c.Host);
        Assert.Equal(HostSource.Dns, c.HostSource);

        // Known before it opens.
        var other = Key("192.0.2.24", localPort: 51001);
        w.Resolved("cdn.example.net", [other.Remote], T0);
        w.Opened(other, 4120, true, T0.AddSeconds(2));
        Assert.Contains(w.Snapshot(), x => x.Host == "cdn.example.net");
    }

    [Fact]
    public void Unexpected_connections_are_told_once_their_names_have_had_time()
    {
        var w = Make();
        w.SetLists([new ExpectedList("mail", "host *.example.com")]);
        var told = new List<Connection>();
        w.Unexpected += list => told.AddRange(list);

        var expected = Key("203.0.113.12", localPort: 51010);
        var unexpected = Key("203.0.113.36", localPort: 51011);
        w.Opened(expected, 5232, true, T0);
        w.Opened(unexpected, 8808, true, T0);
        w.Housekeep(T0.AddSeconds(1));
        Assert.Empty(told);
        // The name came after the connection: it's expected after all.
        w.Resolved("mail.example.com", [expected.Remote], T0.AddSeconds(1));
        w.Housekeep(T0.AddSeconds(4));
        var c = Assert.Single(told);
        Assert.Equal(A("203.0.113.36"), c.Remote);
        w.Housekeep(T0.AddSeconds(5));
        Assert.Single(told);

        Assert.Equal(Verdict.Expected, w.Snapshot().Single(x => x.Remote == expected.Remote).Verdict);
        w.SetLists([]);
        Assert.All(w.Snapshot(), x => Assert.Equal(Verdict.NotChecked, x.Verdict));
    }

    [Fact]
    public void Saved_as_it_goes_and_ended_when_disposed()
    {
        using var database = Database.InMemory();
        var w = Make(database);
        var key = Key("203.0.113.5");
        w.Opened(key, 4120, true, T0);
        w.Traffic(key, 4120, 100, 2000, T0.AddSeconds(30));
        w.Save();
        var saved = Assert.Single(database.Connections(T0.AddHours(-1), T0.AddHours(1)));
        Assert.Null(saved.Ended);
        Assert.Equal(2000, saved.Received);

        w.Traffic(key, 4120, 0, 1000, T0.AddSeconds(40));
        w.Closed(key, 4120, T0.AddSeconds(41));
        w.Save();
        saved = Assert.Single(database.Connections(T0.AddHours(-1), T0.AddHours(1)));
        Assert.Equal(3000, saved.Received);
        Assert.Equal(TimeSpan.FromSeconds(41), saved.Duration);
        var traffic = database.Traffic(T0, T0.AddMinutes(5), TimeSpan.FromMinutes(1));
        Assert.Equal(3000, traffic.Sum(t => t.Received));
        w.Dispose();
    }
}
