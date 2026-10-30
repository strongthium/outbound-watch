namespace Strongthium.OutboundWatch;

/// <summary>
/// A made-up PC for <c>outbound-watch --sample</c> and the README's pictures: a week of history and a few connections
/// open now, from open-source programs, Windows' services and one made-up updater. Every address is from the ranges
/// set aside for documentation (192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24, 2001:db8::/32), every network number
/// from those set aside for documentation (64496 to 64511), and every name is under example.com, .net or .org.
/// </summary>
public static class SampleData
{
    private sealed record Program(int Pid, string Name, string Path, string Services = "");

    private sealed record Site(string Host, string Address, int Port, Protocol Protocol = Protocol.Tcp);

    private static readonly Program Firefox = new(4120, "firefox.exe", @"C:\Program Files\Mozilla Firefox\firefox.exe");
    private static readonly Program Thunderbird = new(5232, "thunderbird.exe", @"C:\Program Files\Mozilla Thunderbird\thunderbird.exe");
    private static readonly Program Syncthing = new(3388, "syncthing.exe", @"C:\Users\Alex\AppData\Local\Programs\Syncthing\syncthing.exe");
    private static readonly Program Git = new(9012, "git-remote-https.exe", @"C:\Program Files\Git\mingw64\libexec\git-core\git-remote-https.exe");
    private static readonly Program Vlc = new(7740, "vlc.exe", @"C:\Program Files\VideoLAN\VLC\vlc.exe");
    private static readonly Program KeePass = new(6150, "KeePassXC.exe", @"C:\Program Files\KeePassXC\KeePassXC.exe");
    private static readonly Program Updates = new(1844, "svchost.exe", @"C:\Windows\System32\svchost.exe", "wuauserv");
    private static readonly Program Time = new(1460, "svchost.exe", @"C:\Windows\System32\svchost.exe", "W32Time");
    private static readonly Program Larkspur = new(8808, "larkspur-update.exe", @"C:\Program Files (x86)\Larkspur\larkspur-update.exe");

    private static readonly Program[] Programs = [Firefox, Thunderbird, Syncthing, Git, Vlc, KeePass, Updates, Time, Larkspur];

    private static readonly (string Prefix, Place Place)[] Places =
    [
        ("192.0.2.", new Place("US", 64500, "Example Content Network")),
        ("198.51.100.", new Place("DE", 64501, "Example Cloud GmbH")),
        ("203.0.113.1", new Place("CA", 64502, "Example Mail Inc.")),
        ("203.0.113.2", new Place("NL", 64503, "Example Mirrors B.V.")),
        ("203.0.113.3", new Place("SG", 64504, "Example Hosting Pte. Ltd.")),
        ("203.0.113.4", new Place("BR", 64505, "Example Telecom S.A.")),
        ("203.0.113.5", new Place("JP", 64506, "Example Time Service")),
        ("2001:db8:1:", new Place("US", 64500, "Example Content Network")),
        ("2001:db8:2:", new Place("DE", 64501, "Example Cloud GmbH")),
    ];

    private static readonly Dictionary<Program, Site[]> Sites = new()
    {
        [Firefox] =
        [
            new("www.example.org", "192.0.2.10", 443), new("cdn.example.net", "192.0.2.24", 443), new("cdn.example.net", "192.0.2.24", 443, Protocol.Udp),
            new("video.example.com", "2001:db8:1::40", 443), new("search.example.com", "198.51.100.7", 443), new("news.example.org", "198.51.100.33", 443),
            new("fonts.example.net", "192.0.2.61", 443),
        ],
        [Thunderbird] = [new("mail.example.com", "203.0.113.12", 993), new("smtp.example.com", "203.0.113.13", 587)],
        [Syncthing] = [new("relay.example.net", "198.51.100.90", 22067), new("discovery.example.net", "2001:db8:2::5", 443)],
        [Git] = [new("git.example.org", "203.0.113.21", 443)],
        [Vlc] = [new("", "203.0.113.44", 8080)],
        [KeePass] = [new("icons.example.com", "192.0.2.80", 443)],
        [Updates] = [new("update.example.com", "192.0.2.120", 443), new("dl.example.com", "203.0.113.25", 80)],
        [Time] = [new("time.example.com", "203.0.113.51", 123, Protocol.Udp)],
        [Larkspur] = [new("telemetry.example.net", "203.0.113.36", 443)],
    };

    /// <summary>Where the sample's addresses are.</summary>
    public static IPlaces Lookup { get; } = new SamplePlaces();

    private sealed class SamplePlaces : IPlaces
    {
        public Place Lookup(UInt128 address)
        {
            var text = Addresses.Format(address);
            foreach (var (prefix, place) in Places)
            {
                if (text.StartsWith(prefix, StringComparison.Ordinal)) return place;
            }
            return Addresses.IsLocalNetwork(address) ? Place.LocalNetwork : Place.Unknown;
        }
    }

    public static ProcessInfo ProcessOf(int pid) =>
        Programs.FirstOrDefault(p => p.Pid == pid) is { } program ? new(program.Name, program.Path, program.Services) : new($"PID {pid}", "", "");

    /// <summary>The sample's expected lists: the everyday programs and Windows, and one for the NAS that's turned off.</summary>
    public static List<ExpectedList> Lists() =>
    [
        new ExpectedList("Everyday", """
            # Browsing and mail
            process firefox.exe port 80,443
            process thunderbird.exe host *.example.com
            # Sync, passwords and code
            process syncthing.exe
            process KeePassXC.exe host icons.example.com
            process git-remote-https.exe host *.example.org
            """),
        new ExpectedList("Windows", """
            service wuauserv host *.example.com
            service W32Time proto udp port 123
            """),
        new ExpectedList("Media server", "process vlc.exe ip 203.0.113.44 port 8080", enabled: false),
    ];

    private static readonly UInt128 Local = Addresses.FromV4Number(0xC0A8_0114); // 192.168.1.20
    private static readonly UInt128 Local6 = Addresses.From(System.Net.IPAddress.Parse("2001:db8:ffff::20"));

    private static FlowKey KeyOf(Site site, int localPort)
    {
        Addresses.TryParse(site.Address, out var remote);
        return new FlowKey(site.Protocol, Addresses.IsV4(remote) ? Local : Local6, localPort, remote, site.Port);
    }

    /// <summary>A week of history before <paramref name="now"/>, the same every time.</summary>
    public static void Fill(Database database, IReadOnlyList<ExpectedList> lists, DateTime now)
    {
        var random = new Random(20261029);
        var connections = new List<Connection>();
        var traffic = new Dictionary<long, (long Sent, long Received)>();
        var localPort = 49200;
        for (var hour = 7 * 24; hour > 0; hour--)
        {
            var at = now.AddHours(-hour);
            var local = at.ToLocalTime().Hour;
            var awake = local is >= 8 and <= 23;
            foreach (var program in Programs)
            {
                var (perHour, size, length) = program == Firefox ? (awake ? 14 : 0, 2_500_000, 90.0)
                    : program == Thunderbird ? (awake ? 3 : 1, 60_000, 600.0)
                    : program == Syncthing ? (2, 4_000_000, 1800.0)
                    : program == Git ? (awake && random.Next(4) == 0 ? 2 : 0, 900_000, 6.0)
                    : program == Vlc ? (local is >= 19 and <= 22 ? 1 : 0, 900_000_000, 3000.0)
                    : program == KeePass ? (random.Next(6) == 0 ? 1 : 0, 20_000, 2.0)
                    : program == Updates ? (random.Next(8) == 0 ? 2 : 0, 45_000_000, 120.0)
                    : program == Time ? (random.Next(3) == 0 ? 1 : 0, 180, 0.2)
                    : (random.Next(2) == 0 ? 1 : 0, 350_000, 4.0);
                var sites = Sites[program];
                for (var i = 0; i < perHour; i++)
                {
                    var site = sites[random.Next(sites.Length)];
                    var started = at.AddSeconds(random.Next(3600));
                    var seconds = length * (0.2 + random.NextDouble() * 1.6);
                    var received = (long)(size * (0.3 + random.NextDouble() * 1.4));
                    var sent = program == Larkspur ? received / 2 : received / (12 + random.Next(30));
                    var ended = started.AddSeconds(seconds);
                    if (ended > now) ended = now;
                    var c = Connection(program, site, ++localPort, started, ended, sent, received, lists);
                    connections.Add(c);
                    var minute = Database.Ms(started) / 60_000;
                    var minutes = Math.Max(1, (long)(seconds / 60));
                    for (var m = 0L; m < minutes; m++)
                    {
                        var (s, r) = traffic.GetValueOrDefault(minute + m);
                        traffic[minute + m] = (s + sent / minutes, r + received / minutes);
                    }
                }
            }
        }
        database.Save(connections, traffic);
    }

    private static Connection Connection(Program program, Site site, int localPort, DateTime started, DateTime ended, long sent, long received, IReadOnlyList<ExpectedList> lists)
    {
        var key = KeyOf(site, localPort);
        var c = new Connection
        {
            Key = key,
            Pid = program.Pid,
            Process = program.Name,
            ProcessPath = program.Path,
            Service = program.Services,
            Host = site.Host,
            HostSource = site.Host.Length > 0 ? HostSource.Dns : HostSource.None,
            Place = Lookup.Lookup(key.Remote),
            Started = started,
            LastSeen = ended,
            Ended = ended,
            Sent = sent,
            Received = received,
            Measured = true,
            State = site.Protocol == Protocol.Tcp ? "Established" : "",
        };
        (c.Verdict, c.ExpectedBy) = ExpectedList.Judge(c, lists);
        return c;
    }

    /// <summary>Opens the sample's live connections in the watcher and sends their bytes, once a second, until it's
    /// disposed.</summary>
    public static IDisposable Run(Watcher watcher, DateTime now)
    {
        var open = new List<(FlowKey Key, int Pid, long Rate)>();
        void Open(Program program, Site site, int port, TimeSpan ago, long rate)
        {
            var key = KeyOf(site, port);
            if (site.Host.Length > 0) watcher.Resolved(site.Host, [key.Remote], now - ago);
            watcher.Opened(key, program.Pid, true, now - ago);
            watcher.Traffic(key, program.Pid, rate * 4 * (long)ago.TotalSeconds / 100, rate * (long)ago.TotalSeconds, now - ago / 2);
            open.Add((key, program.Pid, rate));
        }
        Open(Firefox, Sites[Firefox][0], 51001, TimeSpan.FromMinutes(4), 12_000);
        Open(Firefox, Sites[Firefox][1], 51002, TimeSpan.FromSeconds(48), 180_000);
        Open(Firefox, Sites[Firefox][3], 51003, TimeSpan.FromMinutes(11), 640_000);
        Open(Firefox, Sites[Firefox][4], 51004, TimeSpan.FromSeconds(12), 3_000);
        Open(Thunderbird, Sites[Thunderbird][0], 51010, TimeSpan.FromHours(3.2), 40);
        Open(Syncthing, Sites[Syncthing][0], 51020, TimeSpan.FromHours(26), 2_500);
        Open(Syncthing, Sites[Syncthing][1], 51021, TimeSpan.FromMinutes(37), 60);
        Open(Vlc, Sites[Vlc][0], 51030, TimeSpan.FromMinutes(52), 420_000);
        Open(Updates, Sites[Updates][0], 51040, TimeSpan.FromSeconds(95), 1_400_000);
        Open(Larkspur, Sites[Larkspur][0], 51050, TimeSpan.FromSeconds(20), 9_000);
        Open(Git, Sites[Git][0], 51060, TimeSpan.FromSeconds(3), 210_000);

        var random = new Random(7);
        return new Timer(_ =>
        {
            var at = DateTime.UtcNow;
            foreach (var (key, pid, rate) in open)
            {
                var received = (long)(rate * (0.5 + random.NextDouble()));
                watcher.Traffic(key, pid, received / 25 + 80, received, at);
            }
        }, null, 1000, 1000);
    }
}
