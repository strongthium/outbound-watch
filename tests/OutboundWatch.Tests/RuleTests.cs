namespace Strongthium.OutboundWatch.Tests;

public sealed class RuleTests
{
    internal static Connection Make(string remote = "203.0.113.5", int port = 443, string process = "firefox.exe", string host = "",
        HostSource source = HostSource.Dns, Place? place = null, Protocol protocol = Protocol.Tcp, string service = "", string path = "")
    {
        Addresses.TryParse(remote, out var address);
        return new Connection
        {
            Key = new FlowKey(protocol, Addresses.FromV4Number(0xC0A80114), 50000, address, port),
            Process = process,
            ProcessPath = path.Length > 0 ? path : $@"C:\Program Files\Example\{process}",
            Service = service,
            Host = host,
            HostSource = host.Length > 0 ? source : HostSource.None,
            Place = place ?? new Place("CA", 64500, "Example Cloud Inc."),
        };
    }

    private static bool Matches(string rule, Connection c) => new ExpectedList("test", rule).Matches(c);

    [Fact]
    public void Host_wildcard_matches_the_domain_and_names_under_it()
    {
        Assert.True(Matches("host *.example.com", Make(host: "example.com")));
        Assert.True(Matches("host *.example.com", Make(host: "cdn.eu.example.com")));
        Assert.False(Matches("host *.example.com", Make(host: "example.com.evil.example")));
        Assert.False(Matches("host *.example.com", Make(host: "notexample.com")));
        Assert.True(Matches("host www.example.org", Make(host: "WWW.Example.org")));
    }

    [Fact]
    public void A_reverse_name_is_not_trusted()
    {
        Assert.False(Matches("host *.example.com", Make(host: "a.example.com", source: HostSource.Reverse)));
    }

    [Fact]
    public void Every_word_on_a_line_must_match()
    {
        var rule = "asn 64500,AS64501 process firefox.exe";
        Assert.True(Matches(rule, Make()));
        Assert.False(Matches(rule, Make(process: "other.exe")));
        Assert.False(Matches(rule, Make(place: new Place("CA", 64999, "x"))));
    }

    [Fact]
    public void Networks_ports_countries_and_protocols()
    {
        Assert.True(Matches("ip 203.0.113.0/24", Make()));
        Assert.False(Matches("ip 203.0.114.0/24", Make()));
        Assert.True(Matches("ip 203.0.113.5", Make()));
        Assert.True(Matches("ip 2001:db8::/32", Make("2001:db8::1")));
        Assert.False(Matches("ip 2001:db8::/32", Make()));
        Assert.True(Matches("port 8000-8100,443", Make()));
        Assert.False(Matches("port 80", Make()));
        Assert.True(Matches("country ca,us", Make()));
        Assert.True(Matches("proto udp", Make(protocol: Protocol.Udp)));
        Assert.False(Matches("proto udp", Make()));
    }

    [Fact]
    public void Org_is_a_part_of_the_name_or_a_pattern()
    {
        Assert.True(Matches("org \"example cloud\"", Make()));
        Assert.True(Matches("org Example*Inc.", Make()));
        Assert.False(Matches("org Example*Ltd", Make()));
    }

    [Fact]
    public void Process_by_name_or_by_path_and_service()
    {
        Assert.True(Matches("process FIREFOX.EXE", Make()));
        Assert.True(Matches(@"process ""C:\Program Files\Example\*""", Make()));
        Assert.False(Matches(@"process ""C:\Program Files\Other\*""", Make()));
        Assert.True(Matches("service Dnscache", Make(process: "svchost.exe", service: "Dnscache, NlaSvc")));
        Assert.False(Matches("service wuauserv", Make(process: "svchost.exe", service: "Dnscache, NlaSvc")));
    }

    [Fact]
    public void Notes_and_blank_lines_are_not_rules()
    {
        var list = new ExpectedList("test", "# Everyday\n\nhost *.example.com   # the mail\n");
        Assert.Single(list.Rules);
        Assert.Empty(list.Errors);
    }

    [Fact]
    public void Mistakes_are_reported_by_line()
    {
        var list = new ExpectedList("test", "host *.example.com\nhots example.com\nport 99999\nip nonsense\ncountry Canada\nasn\norg \"open");
        Assert.Single(list.Rules);
        Assert.Equal(6, list.Errors.Count);
        Assert.StartsWith("Line 2:", list.Errors[0]);
        Assert.StartsWith("Line 7:", list.Errors[5]);
    }

    [Fact]
    public void Judged_by_the_lists_that_are_on()
    {
        var c = Make(host: "a.example.com");
        Assert.Equal(Verdict.NotChecked, ExpectedList.Judge(c, []).Verdict);
        var off = new ExpectedList("off", "host *.example.com", enabled: false);
        Assert.Equal(Verdict.NotChecked, ExpectedList.Judge(c, [off]).Verdict);
        var other = new ExpectedList("other", "host *.example.org");
        Assert.Equal(Verdict.Unexpected, ExpectedList.Judge(c, [off, other]).Verdict);
        var on = new ExpectedList("mail", "host *.example.com");
        Assert.Equal((Verdict.Expected, "mail"), ExpectedList.Judge(c, [off, other, on]));
    }

    [Theory]
    [InlineData("*", "anything", true)]
    [InlineData("a*c", "abbbc", true)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "abbc", false)]
    [InlineData("*.exe", "FIREFOX.EXE", true)]
    [InlineData("fire*", "ice", false)]
    public void Wildcards(string pattern, string text, bool matches) => Assert.Equal(matches, Wildcard.Matches(pattern, text));
}
