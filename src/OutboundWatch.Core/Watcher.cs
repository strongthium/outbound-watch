using System.Net;
using System.Net.NetworkInformation;

namespace Strongthium.OutboundWatch;

/// <summary>
/// Follows every outgoing connection from when it opens to when it ends. Once a second it reads Windows' TCP table
/// (which any user can), and as administrator it also reads the kernel's network events, which add the bytes each
/// connection sends and receives, UDP, and connections too short for the table. Names come from the DNS client's events
/// and cache, places from the address databases, and each connection is judged against the expected lists. Connections
/// are saved to the history as they change, every few seconds.
/// <para>Incoming connections (to a port that's listening here), loopback, multicast and, unless asked, the local
/// network are left out.</para>
/// </summary>
public sealed class Watcher : INetworkEvents, IDisposable
{
    /// <summary>UDP has no end: its packets stopping for this long is one.</summary>
    public static readonly TimeSpan UdpIdle = TimeSpan.FromMinutes(2);
    /// <summary>How long a new connection waits for its name before it's told about as unexpected.</summary>
    private static readonly TimeSpan NotifyAfter = TimeSpan.FromSeconds(3);

    private readonly Database? database;
    private readonly Func<int, ProcessInfo> processNames;
    private readonly Lock gate = new();
    private readonly Lock saving = new();
    private readonly Dictionary<FlowKey, Connection> open = [];
    private readonly List<Connection> finished = [];
    /// <summary>Incoming connections and UDP from outside, kept so that their later packets aren't taken as outgoing.</summary>
    private readonly Dictionary<FlowKey, DateTime> ignored = [];
    private readonly Dictionary<long, (long Sent, long Received)> minutes = [];
    private readonly HashSet<string> reversePending = [];
    private HashSet<UInt128> localAddresses = [];
    private HashSet<int> listeningPorts = [];
    private IReadOnlyList<ExpectedList> lists = [];
    private IPlaces? places;
    private EtwCapture? capture;
    private Timer? timer;
    private bool polledOnce;
    private int ticking;
    private DateTime lastCache, lastAddresses, lastSave, lastPrune;
    private bool disposed;

    public Names Names { get; } = new();

    /// <summary>The kernel's network events are being read: bytes are counted, and UDP is seen.</summary>
    public bool Measuring { get; private set; }

    /// <summary>Why the network events aren't being read, when they aren't.</summary>
    public string CaptureError { get; private set; } = "";

    public bool WatchLocalNetwork { get; set; }

    /// <summary>Looks up the name of an address no lookup was seen for (a reverse lookup, through the PC's own DNS).</summary>
    public bool ReverseLookups { get; set; } = true;

    /// <summary>Days of history kept; 0 keeps everything.</summary>
    public int KeepDays { get; set; } = 90;

    /// <summary>New connections to unexpected places, a few seconds after they open (once their names are known).
    /// Raised on the watcher's thread.</summary>
    public event Action<IReadOnlyList<Connection>>? Unexpected;

    /// <summary>A watcher that saves to <paramref name="database"/>; without one (for tests), nothing is saved.</summary>
    public Watcher(Database? database, Func<int, ProcessInfo>? processNames = null)
    {
        this.database = database;
        this.processNames = processNames ?? new Processes().Get;
    }

    /// <summary>Starts watching: the TCP table every second and, with <paramref name="events"/> (which needs
    /// administrator rights), the kernel's network events. Sample data starts it with neither.</summary>
    public void Start(bool table, bool events)
    {
        database?.CloseLeftOpen();
        if (events)
        {
            capture = new EtwCapture(this);
            Measuring = capture.Start();
            if (!Measuring)
            {
                CaptureError = capture.Error;
                capture.Dispose();
                capture = null;
            }
        }
        else CaptureError = "Not running as administrator.";
        timer = new Timer(_ => Tick(table), null, 0, 1000);
    }

    /// <summary>Starts with nothing read from Windows, for the sample data, which brings its own connections and bytes.</summary>
    public void StartSample()
    {
        Measuring = true;
        timer = new Timer(_ => Tick(false), null, 0, 1000);
    }

    private void Tick(bool table)
    {
        // A slow tick (a big table, a slow disk) is let finish rather than run twice at once.
        if (Interlocked.Exchange(ref ticking, 1) == 1) return;
        try
        {
            var now = DateTime.UtcNow;
            if (now - lastAddresses > TimeSpan.FromSeconds(30))
            {
                lastAddresses = now;
                ReadLocalAddresses();
            }
            if (table) Apply(ConnectionTable.Read(), now);
            if (table && now - lastCache > TimeSpan.FromSeconds(5))
            {
                lastCache = now;
                ReadDnsCache();
            }
            Housekeep(now);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine("outbound-watch: tick: " + exception);
        }
        finally { ticking = 0; }
    }

    // ---- What's watched ----------------------------------------------------------------------------------------

    public void SetLists(IReadOnlyList<ExpectedList> value)
    {
        lock (gate)
        {
            lists = value;
            foreach (var c in open.Values) Judge(c);
        }
    }

    public void SetPlaces(IPlaces? value)
    {
        lock (gate)
        {
            places = value;
            foreach (var c in open.Values)
            {
                c.Place = Place(c.Remote);
                Judge(c);
                c.Dirty = true;
            }
        }
    }

    /// <summary>The open connections, as they are now: copies, which the watcher won't change.</summary>
    public List<Connection> Snapshot()
    {
        lock (gate) return [.. open.Values.Select(c => c.Copy())];
    }

    // ---- The TCP table -----------------------------------------------------------------------------------------

    /// <summary>Takes in a reading of the TCP table: new connections start, and ones gone from it twice end when they
    /// were last seen. The first reading's connections were already open when watching began.</summary>
    internal void Apply(IReadOnlyList<TcpRow> rows, DateTime now)
    {
        lock (gate)
        {
            var listening = new HashSet<int>();
            foreach (var row in rows)
            {
                if (row.State == TcpState.Listen) listening.Add(row.Key.LocalPort);
            }
            listeningPorts = listening;

            var seen = new HashSet<FlowKey>();
            foreach (var row in rows)
            {
                if (!row.IsOpen || !Wanted(row.Key.Remote)) continue;
                var key = row.Key;
                if (open.TryGetValue(key, out var c))
                {
                    seen.Add(key);
                    c.LastSeen = Later(c.LastSeen, now);
                    c.Missed = 0;
                    var state = row.State.ToString();
                    if (c.State != state)
                    {
                        c.State = state;
                        c.Dirty = true;
                    }
                    continue;
                }
                if (ignored.ContainsKey(key) || listening.Contains(key.LocalPort))
                {
                    ignored[key] = now;
                    continue;
                }
                c = Create(key, row.Pid, now, openAtStart: !polledOnce);
                c.State = row.State.ToString();
                seen.Add(key);
            }

            foreach (var c in open.Values.Where(c => c.Protocol == Protocol.Tcp && !seen.Contains(c.Key)).ToList())
            {
                // Missing once can be the table read as it changed; twice, it's gone.
                if (++c.Missed >= 2) End(c, c.LastSeen);
            }
            polledOnce = true;
        }
    }

    // ---- The network events ------------------------------------------------------------------------------------

    public void Traffic(FlowKey key, int pid, long sent, long received, DateTime at)
    {
        lock (gate)
        {
            var c = Find(ref key);
            if (c is null)
            {
                if (!Wanted(key.Remote) || ignored.ContainsKey(key)) return;
                // A connection that's new to the watcher: outgoing if this end sent first (UDP), or for TCP, if its
                // local port isn't one that's listening.
                var outbound = key.Protocol == Protocol.Udp ? sent > 0 : !listeningPorts.Contains(key.LocalPort);
                if (!outbound)
                {
                    ignored[key] = at;
                    return;
                }
                c = Create(key, pid, at, openAtStart: false);
            }
            c.Sent += sent;
            c.Received += received;
            c.Measured = true;
            c.LastSeen = Later(c.LastSeen, at);
            c.Dirty = true;
            var minute = Database.Ms(at) / 60_000;
            var (s, r) = minutes.GetValueOrDefault(minute);
            minutes[minute] = (s + sent, r + received);
        }
    }

    public void Opened(FlowKey key, int pid, bool outbound, DateTime at)
    {
        lock (gate)
        {
            if (Find(ref key) is not null) return;
            if (!Wanted(key.Remote)) return;
            if (!outbound)
            {
                ignored[key] = at;
                return;
            }
            Create(key, pid, at, openAtStart: false);
        }
    }

    public void Closed(FlowKey key, int pid, DateTime at)
    {
        lock (gate)
        {
            if (Find(ref key) is { } c) End(c, at);
            ignored.Remove(key);
        }
    }

    public void Resolved(string name, IReadOnlyList<UInt128> addresses, DateTime at)
    {
        foreach (var address in addresses)
        {
            if (Names.FromEvent(name, address)) Renamed(address);
        }
    }

    /// <summary>The open connection an event is about. An event names its far end as the destination even for a
    /// receive, but in case it's the other way round, the key is tried both ways, and turned round if its
    /// "remote" is one of this PC's addresses. Whether it was sent or received stays as the event said: that's always
    /// from this PC.</summary>
    private Connection? Find(ref FlowKey key)
    {
        if (open.TryGetValue(key, out var c)) return c;
        var turned = key with { Local = key.Remote, LocalPort = key.RemotePort, Remote = key.Local, RemotePort = key.LocalPort };
        if (open.TryGetValue(turned, out c) || (localAddresses.Contains(key.Remote) && !localAddresses.Contains(key.Local)))
        {
            key = turned;
        }
        return c;
    }

    // ---- Connections -------------------------------------------------------------------------------------------

    private bool Wanted(UInt128 remote) =>
        !Addresses.IsNowhere(remote) && !localAddresses.Contains(remote) && (WatchLocalNetwork || !Addresses.IsLocalNetwork(remote));

    private Connection Create(FlowKey key, int pid, DateTime at, bool openAtStart)
    {
        var info = processNames(pid);
        var c = new Connection
        {
            Key = key,
            Pid = pid,
            Process = info.Name,
            ProcessPath = info.Path,
            Service = info.Services,
            Started = at,
            LastSeen = at,
            OpenAtStart = openAtStart,
            Measured = Measuring,
            Place = Place(key.Remote),
            Dirty = true,
        };
        if (Names.TryGet(key.Remote, out var name, out var source))
        {
            c.Host = name;
            c.HostSource = source;
        }
        else if (ReverseLookups) LookUpReverse(key.Remote);
        Judge(c);
        open[key] = c;
        return c;
    }

    private void End(Connection c, DateTime at)
    {
        c.LastSeen = Later(c.LastSeen, at);
        c.Ended = c.LastSeen;
        c.Dirty = true;
        open.Remove(c.Key);
        finished.Add(c);
    }

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

    private Place Place(UInt128 address) =>
        Addresses.IsLocalNetwork(address) ? OutboundWatch.Place.LocalNetwork : places?.Lookup(address) ?? OutboundWatch.Place.Unknown;

    private void Judge(Connection c)
    {
        var (verdict, by) = ExpectedList.Judge(c, lists);
        if (verdict == c.Verdict && by == c.ExpectedBy) return;
        c.Verdict = verdict;
        c.ExpectedBy = by;
        c.Dirty = true;
    }

    /// <summary>The address has a new name: the open connections to it take it, and are judged again.</summary>
    private void Renamed(UInt128 address)
    {
        if (!Names.TryGet(address, out var name, out var source)) return;
        lock (gate)
        {
            foreach (var c in open.Values)
            {
                if (c.Remote != address || (c.Host == name && c.HostSource == source)) continue;
                c.Host = name;
                c.HostSource = source;
                c.Dirty = true;
                Judge(c);
            }
        }
    }

    // ---- Names -------------------------------------------------------------------------------------------------

    private void ReadDnsCache()
    {
        foreach (var (name, address, alias) in DnsCache.Read())
        {
            if (Names.FromCache(name, address, alias)) Renamed(address);
        }
    }

    private void LookUpReverse(UInt128 address)
    {
        var text = Addresses.Format(address);
        if (reversePending.Count > 16 || !reversePending.Add(text)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                // Give the DNS events and cache a moment to name it first.
                await Task.Delay(4000).ConfigureAwait(false);
                if (Names.TryGet(address, out _, out _)) return;
                var entry = await Dns.GetHostEntryAsync(Addresses.ToIPAddress(address)).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (entry.HostName.Length > 0 && entry.HostName != text && Names.FromReverse(entry.HostName, address)) Renamed(address);
            }
            catch (Exception exception) when (exception is System.Net.Sockets.SocketException or TimeoutException or ArgumentException)
            {
                // No name.
            }
            finally
            {
                lock (gate) reversePending.Remove(text);
            }
        });
    }

    private void ReadLocalAddresses()
    {
        var found = new HashSet<UInt128>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in nic.GetIPProperties().UnicastAddresses) found.Add(Addresses.From(unicast.Address));
            }
        }
        catch (NetworkInformationException)
        {
            return;
        }
        lock (gate) localAddresses = found;
    }

    /// <summary>For tests: this PC's addresses, which a connection never goes to.</summary>
    internal void SetLocalAddresses(IEnumerable<UInt128> addresses)
    {
        lock (gate) localAddresses = [.. addresses];
    }

    // ---- Housekeeping ------------------------------------------------------------------------------------------

    /// <summary>Ends UDP that's gone quiet, tells of new unexpected connections, and saves every few seconds.</summary>
    internal void Housekeep(DateTime now)
    {
        List<Connection> unexpected = [];
        lock (gate)
        {
            foreach (var c in open.Values.Where(c => c.Protocol == Protocol.Udp && now - c.LastSeen > UdpIdle).ToList()) End(c, c.LastSeen);
            foreach (var key in ignored.Where(pair => now - pair.Value > UdpIdle).Select(pair => pair.Key).ToList()) ignored.Remove(key);
            foreach (var c in open.Values.Concat(finished))
            {
                if (c.Verdict != Verdict.Unexpected || c.Notified || now - c.Started < NotifyAfter && c.IsOpen) continue;
                c.Notified = true;
                unexpected.Add(c.Copy());
            }
        }
        if (unexpected.Count > 0) Unexpected?.Invoke(unexpected);
        if (now - lastSave >= TimeSpan.FromSeconds(5))
        {
            lastSave = now;
            Save();
        }
        if (database is not null && KeepDays > 0 && now - lastPrune > TimeSpan.FromHours(6))
        {
            lastPrune = now;
            database.Prune(now.AddDays(-KeepDays));
        }
    }

    /// <summary>Writes what changed since the last save to the history.</summary>
    public void Save()
    {
        if (database is null) return;
        lock (saving)
        {
            List<(Connection Original, Connection Copy)> changed;
            Dictionary<long, (long, long)> traffic;
            lock (gate)
            {
                changed = [.. open.Values.Concat(finished).Where(c => c.Dirty).Select(c => (c, c.Copy()))];
                foreach (var (c, _) in changed) c.Dirty = false;
                finished.Clear();
                traffic = new(minutes);
                minutes.Clear();
            }
            database.Save(changed.Select(pair => pair.Copy).ToList(), traffic);
            lock (gate)
            {
                foreach (var (original, copy) in changed) original.Id = copy.Id;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer?.Dispose();
        capture?.Dispose();
        lock (gate)
        {
            // What's open now is open until now: it's saved as ended, and the next start begins it again.
            var now = DateTime.UtcNow;
            foreach (var c in open.Values.ToList()) End(c, now);
        }
        Save();
    }
}
