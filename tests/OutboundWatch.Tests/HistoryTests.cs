namespace Strongthium.OutboundWatch.Tests;

/// <summary>The history in SQLite, and the totals made from it.</summary>
public sealed class HistoryTests
{
    private static readonly DateTime T0 = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static Connection Ended(string remote, string process, string host, DateTime started, TimeSpan length, long sent, long received, Place? place = null)
    {
        var c = RuleTests.Make(remote: remote, process: process, host: host, place: place);
        c.Started = started;
        c.LastSeen = started + length;
        c.Ended = started + length;
        c.Sent = sent;
        c.Received = received;
        c.Measured = true;
        return c;
    }

    [Fact]
    public void Connections_are_found_by_time_and_by_search()
    {
        using var database = Database.InMemory();
        database.Save(
        [
            Ended("203.0.113.5", "firefox.exe", "www.example.org", T0, TimeSpan.FromMinutes(5), 10, 100),
            Ended("203.0.113.6", "thunderbird.exe", "mail.example.com", T0.AddHours(2), TimeSpan.FromMinutes(1), 20, 200),
            Ended("198.51.100.7", "git.exe", "", T0.AddDays(-3), TimeSpan.FromSeconds(4), 30, 300),
        ], new Dictionary<long, (long, long)>());

        Assert.Equal(2, database.Connections(T0.AddHours(-1), T0.AddHours(3)).Count);
        // Open at any time in the range, not only started in it.
        Assert.Single(database.Connections(T0.AddMinutes(4), T0.AddMinutes(10)));
        Assert.Equal("thunderbird.exe", Assert.Single(database.Connections(T0.AddDays(-7), T0.AddDays(1), "mail.")).Process);
        Assert.Single(database.Connections(T0.AddDays(-7), T0.AddDays(1), "198.51"));
        Assert.Equal(3, database.Connections(T0.AddDays(-7), T0.AddDays(1), "AS64500").Count);
        // Latest first.
        Assert.Equal("thunderbird.exe", database.Connections(T0.AddDays(-7), T0.AddDays(1))[0].Process);

        database.Prune(T0.AddDays(-1));
        Assert.Equal(2, database.Connections(T0.AddDays(-7), T0.AddDays(1)).Count);
    }

    [Fact]
    public void A_connection_reads_back_as_it_was_saved()
    {
        using var database = Database.InMemory();
        var c = Ended("2001:db8::5", "svchost.exe", "update.example.com", T0, TimeSpan.FromSeconds(90), 1234, 567_890, new Place("NL", 64503, "Example Mirrors B.V."));
        c.Service = "wuauserv";
        c.Verdict = Verdict.Expected;
        c.ExpectedBy = "Windows";
        c.OpenAtStart = true;
        database.Save([c], new Dictionary<long, (long, long)>());
        Assert.NotEqual(0, c.Id);
        var back = Assert.Single(database.Connections(T0, T0.AddHours(1)));
        Assert.Equal(c.Key with { Local = back.Key.Local }, back.Key);
        Assert.Equal((c.Process, c.Service, c.Host, c.HostSource, c.Place), (back.Process, back.Service, back.Host, back.HostSource, back.Place));
        Assert.Equal((c.Started, c.Ended, c.Sent, c.Received), (back.Started, back.Ended, back.Sent, back.Received));
        Assert.Equal((Verdict.Expected, "Windows", true, true), (back.Verdict, back.ExpectedBy, back.OpenAtStart, back.Measured));
    }

    [Fact]
    public void Connections_left_open_by_a_crash_are_closed()
    {
        using var database = Database.InMemory();
        var c = Ended("203.0.113.5", "firefox.exe", "", T0, TimeSpan.FromMinutes(1), 0, 0);
        c.Ended = null;
        database.Save([c], new Dictionary<long, (long, long)>());
        database.CloseLeftOpen();
        Assert.Equal(T0.AddMinutes(1), database.Connections(T0, T0.AddHours(1))[0].Ended);
    }

    [Fact]
    public void Totals_by_program_organization_country_domain_and_port()
    {
        using var database = Database.InMemory();
        var cloud = new Place("DE", 64501, "Example Cloud GmbH");
        database.Save(
        [
            Ended("203.0.113.5", "firefox.exe", "www.example.org", T0, TimeSpan.FromMinutes(10), 10, 1000),
            Ended("203.0.113.5", "firefox.exe", "www.example.org", T0.AddMinutes(20), TimeSpan.FromMinutes(10), 10, 1000),
            Ended("203.0.113.6", "firefox.exe", "cdn.example.org", T0.AddMinutes(6), TimeSpan.FromMinutes(1), 5, 500),
            Ended("198.51.100.7", "updater.exe", "telemetry.example.net", T0.AddMinutes(30), TimeSpan.FromMinutes(2), 4000, 40, cloud),
        ], new Dictionary<long, (long, long)>());
        // From 5 minutes in: the first connection's time counts from there.
        var destinations = database.Destinations(T0.AddMinutes(5), T0.AddHours(1));
        Assert.Equal(3, destinations.Count);

        var lists = new List<ExpectedList> { new("browsing", "process firefox.exe") };
        var programs = Totals.Group(destinations, GroupBy.Program, lists);
        Assert.Equal(["updater.exe", "firefox.exe"], programs.Select(g => g.Name));
        var firefox = programs[1];
        Assert.Equal(3, firefox.Connections);
        Assert.Equal(2525, firefox.Bytes);
        Assert.Equal(TimeSpan.FromMinutes(16), firefox.Open);
        Assert.Equal(0, firefox.Unexpected);
        Assert.Equal(1, programs[0].Unexpected);

        Assert.Equal(["Example Cloud GmbH", "Example Cloud Inc."], Totals.Group(destinations, GroupBy.Organization, lists).Select(g => g.Name));
        Assert.Equal(["DE", "CA"], Totals.Group(destinations, GroupBy.Country, lists).Select(g => g.Key));
        var domains = Totals.Group(destinations, GroupBy.Domain, lists);
        Assert.Equal(["example.net", "example.org"], domains.Select(g => g.Name));
        Assert.Equal("2 names", domains[1].Detail);
        Assert.Equal("TCP 443 (HTTPS)", Assert.Single(Totals.Group(destinations, GroupBy.Port, lists)).Name);
    }

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("cdn.eu.example.com", "example.com")]
    [InlineData("www.example.co.uk", "example.co.uk")]
    [InlineData("a.b.example.de", "example.de")]
    public void Domains(string host, string domain) => Assert.Equal(domain, Totals.Domain(host));

    [Fact]
    public void Traffic_by_the_hour()
    {
        using var database = Database.InMemory();
        var minute = Database.Ms(T0) / 60_000;
        database.Save([], new Dictionary<long, (long, long)> { [minute] = (1, 10), [minute + 30] = (2, 20), [minute + 61] = (4, 40) });
        database.Save([], new Dictionary<long, (long, long)> { [minute] = (1, 10) });
        var hours = database.Traffic(T0, T0.AddHours(3), TimeSpan.FromHours(1));
        Assert.Equal([(T0, 4L, 40L), (T0.AddHours(1), 4L, 40L)], hours);
    }

    [Fact]
    public void Lists_and_settings_are_kept()
    {
        using var database = Database.InMemory();
        var list = new ExpectedList("mail", "host *.example.com");
        database.SaveList(list);
        database.SaveList(new ExpectedList("off", "", enabled: false));
        list.SetText("host *.example.org");
        list.Name = "web";
        database.SaveList(list);
        var lists = database.Lists();
        Assert.Equal(["web", "off"], lists.Select(l => l.Name));
        Assert.Equal("host *.example.org", lists[0].Text);
        Assert.False(lists[1].Enabled);
        database.DeleteList(list.Id);
        Assert.Single(database.Lists());

        Assert.Null(database.Setting("keep_days"));
        database.SetSetting("keep_days", "30");
        database.SetSetting("keep_days", "60");
        Assert.Equal("60", database.Setting("keep_days"));
    }

    [Fact]
    public void Sizes_and_durations()
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal("512 bytes", Sizes.Format(512, c));
        Assert.Equal("1.5 MB", Sizes.Format(1_572_864, c));
        Assert.Equal("0.4 s", Sizes.Duration(TimeSpan.FromSeconds(0.4), c));
        Assert.Equal("42 s", Sizes.Duration(TimeSpan.FromSeconds(42), c));
        Assert.Equal("3 min 05 s", Sizes.Duration(TimeSpan.FromSeconds(185), c));
        Assert.Equal("2 h 07 min", Sizes.Duration(TimeSpan.FromMinutes(127), c));
        Assert.Equal("3 d 4 h", Sizes.Duration(TimeSpan.FromHours(76), c));
    }

    [Fact]
    public void The_sample_week_fills_the_history()
    {
        using var database = Database.InMemory();
        var lists = SampleData.Lists();
        SampleData.Fill(database, lists, T0);
        var week = database.Connections(T0.AddDays(-8), T0);
        Assert.True(week.Count > 500);
        Assert.Contains(week, c => c.Verdict == Verdict.Unexpected && c.Process == "larkspur-update.exe");
        Assert.DoesNotContain(week, c => c.Verdict == Verdict.Unexpected && c.Process == "firefox.exe");
    }
}
