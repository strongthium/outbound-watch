using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Strongthium.OutboundWatch;

/// <summary>The connections to one destination over a time: one line of the totals, before it's grouped further.</summary>
public sealed class DestinationTotal
{
    /// <summary>The destination and the program, as the latest of the connections had them.</summary>
    public required Connection Latest { get; init; }
    public int Connections { get; init; }
    public long Sent { get; init; }
    public long Received { get; init; }
    /// <summary>The time the connections were open, added up, within the time asked for.</summary>
    public TimeSpan Open { get; init; }
    public DateTime First { get; init; }
    public DateTime Last { get; init; }
    public bool Measured { get; init; }
}

/// <summary>
/// The history: every connection, the bytes a minute, the expected lists and the settings, in one SQLite file. One
/// connection, used from one thread at a time: every call takes the lock. Times are kept as UTC milliseconds.
/// </summary>
public sealed class Database : IDisposable
{
    private const int SchemaVersion = 1;
    private readonly SqliteConnection connection;
    private readonly Lock gate = new();

    /// <summary>Where the history and the address databases are kept: %LocalAppData%\Strongthium\outbound-watch.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "Strongthium", "outbound-watch");

    public string Directory { get; }
    public string File { get; }

    public Database(string directory) : this(directory, Path.Combine(directory, "outbound-watch.db")) { }

    /// <summary>A database in memory, for tests and the sample data.</summary>
    public static Database InMemory() => new(Path.GetTempPath(), ":memory:");

    private Database(string directory, string file)
    {
        Directory = directory;
        File = file;
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file }.ToString());
        connection.Open();
        Migrate();
    }

    private void Migrate()
    {
        Execute("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");
        var version = Convert.ToInt32(Scalar("PRAGMA user_version"), CultureInfo.InvariantCulture);
        if (version >= SchemaVersion) return;
        Execute("""
            CREATE TABLE IF NOT EXISTS connection (
                id            INTEGER PRIMARY KEY,
                protocol      TEXT NOT NULL,             -- Tcp, Udp
                pid           INTEGER NOT NULL,
                process       TEXT NOT NULL,             -- "firefox.exe"
                path          TEXT NOT NULL DEFAULT '',
                service       TEXT NOT NULL DEFAULT '',  -- "Dnscache, NlaSvc"
                local         TEXT NOT NULL,
                local_port    INTEGER NOT NULL,
                remote        TEXT NOT NULL,
                remote_port   INTEGER NOT NULL,
                host          TEXT NOT NULL DEFAULT '',
                host_source   INTEGER NOT NULL DEFAULT 0, -- HostSource
                country       TEXT NOT NULL DEFAULT '',  -- "CA"
                asn           INTEGER NOT NULL DEFAULT 0,
                org           TEXT NOT NULL DEFAULT '',
                started       INTEGER NOT NULL,          -- UTC milliseconds
                last_seen     INTEGER NOT NULL,
                ended         INTEGER,                   -- null while it's open
                open_at_start INTEGER NOT NULL DEFAULT 0,
                sent          INTEGER NOT NULL DEFAULT 0,
                received      INTEGER NOT NULL DEFAULT 0,
                measured      INTEGER NOT NULL DEFAULT 0,
                state         TEXT NOT NULL DEFAULT '',
                verdict       INTEGER NOT NULL DEFAULT 0, -- Verdict, when it was saved
                expected_by   TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS connection_started ON connection (started);
            CREATE INDEX IF NOT EXISTS connection_last_seen ON connection (last_seen);
            CREATE TABLE IF NOT EXISTS traffic (
                minute   INTEGER PRIMARY KEY,           -- UTC minutes since 1970
                sent     INTEGER NOT NULL,
                received INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS expected_list (
                id       INTEGER PRIMARY KEY,
                name     TEXT NOT NULL,
                enabled  INTEGER NOT NULL DEFAULT 1,
                rules    TEXT NOT NULL DEFAULT '',
                position INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS setting (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);
        Execute($"PRAGMA user_version = {SchemaVersion}");
    }

    // ---- Connections -------------------------------------------------------------------------------------------

    /// <summary>Saves connections and the bytes a minute: a new connection gets its <see cref="Connection.Id"/>, one
    /// saved before is changed.</summary>
    public void Save(IReadOnlyList<Connection> connections, IReadOnlyDictionary<long, (long Sent, long Received)> traffic)
    {
        if (connections.Count == 0 && traffic.Count == 0) return;
        lock (gate)
        {
            using var transaction = connection.BeginTransaction();
            using var insert = Command("""
                INSERT INTO connection (protocol, pid, process, path, service, local, local_port, remote, remote_port, host, host_source,
                    country, asn, org, started, last_seen, ended, open_at_start, sent, received, measured, state, verdict, expected_by)
                VALUES ($protocol, $pid, $process, $path, $service, $local, $local_port, $remote, $remote_port, $host, $host_source,
                    $country, $asn, $org, $started, $last_seen, $ended, $open_at_start, $sent, $received, $measured, $state, $verdict, $expected_by)
                RETURNING id
                """);
            using var update = Command("""
                UPDATE connection SET pid = $pid, process = $process, path = $path, service = $service, host = $host, host_source = $host_source,
                    country = $country, asn = $asn, org = $org, started = $started, last_seen = $last_seen, ended = $ended, sent = $sent,
                    received = $received, measured = $measured, state = $state, verdict = $verdict, expected_by = $expected_by
                WHERE id = $id
                """);
            foreach (var c in connections)
            {
                var command = c.Id == 0 ? insert : update;
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", c.Id);
                command.Parameters.AddWithValue("$protocol", c.Protocol.ToString());
                command.Parameters.AddWithValue("$pid", c.Pid);
                command.Parameters.AddWithValue("$process", c.Process);
                command.Parameters.AddWithValue("$path", c.ProcessPath);
                command.Parameters.AddWithValue("$service", c.Service);
                command.Parameters.AddWithValue("$local", Addresses.Format(c.Key.Local));
                command.Parameters.AddWithValue("$local_port", c.Key.LocalPort);
                command.Parameters.AddWithValue("$remote", c.Address);
                command.Parameters.AddWithValue("$remote_port", c.RemotePort);
                command.Parameters.AddWithValue("$host", c.Host);
                command.Parameters.AddWithValue("$host_source", (int)c.HostSource);
                command.Parameters.AddWithValue("$country", c.Place.Country);
                command.Parameters.AddWithValue("$asn", c.Place.Asn);
                command.Parameters.AddWithValue("$org", c.Place.Org);
                command.Parameters.AddWithValue("$started", Ms(c.Started));
                command.Parameters.AddWithValue("$last_seen", Ms(c.LastSeen));
                command.Parameters.AddWithValue("$ended", c.Ended is { } ended ? Ms(ended) : DBNull.Value);
                command.Parameters.AddWithValue("$open_at_start", c.OpenAtStart ? 1 : 0);
                command.Parameters.AddWithValue("$sent", c.Sent);
                command.Parameters.AddWithValue("$received", c.Received);
                command.Parameters.AddWithValue("$measured", c.Measured ? 1 : 0);
                command.Parameters.AddWithValue("$state", c.State);
                command.Parameters.AddWithValue("$verdict", (int)c.Verdict);
                command.Parameters.AddWithValue("$expected_by", c.ExpectedBy);
                if (c.Id == 0) c.Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                else command.ExecuteNonQuery();
            }

            using var add = Command("""
                INSERT INTO traffic (minute, sent, received) VALUES ($minute, $sent, $received)
                ON CONFLICT (minute) DO UPDATE SET sent = sent + excluded.sent, received = received + excluded.received
                """);
            foreach (var (minute, (sent, received)) in traffic)
            {
                add.Parameters.Clear();
                add.Parameters.AddWithValue("$minute", minute);
                add.Parameters.AddWithValue("$sent", sent);
                add.Parameters.AddWithValue("$received", received);
                add.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    /// <summary>Ends the connections a run that didn't close properly left open, when they were last seen.</summary>
    public void CloseLeftOpen()
    {
        lock (gate) Execute("UPDATE connection SET ended = last_seen WHERE ended IS NULL");
    }

    /// <summary>The connections open at any time from <paramref name="from"/> to <paramref name="to"/>, latest first:
    /// at most <paramref name="limit"/>. <paramref name="search"/> narrows them to those with it in their program,
    /// service, name, address, port, organization, AS number or country.</summary>
    public List<Connection> Connections(DateTime from, DateTime to, string search = "", int limit = 20_000)
    {
        lock (gate)
        {
            using var command = Command($"""
                SELECT {Columns} FROM connection
                WHERE started < $to AND coalesce(ended, last_seen) >= $from {SearchClause(search)}
                ORDER BY started DESC LIMIT $limit
                """);
            command.Parameters.AddWithValue("$from", Ms(from));
            command.Parameters.AddWithValue("$to", Ms(to));
            command.Parameters.AddWithValue("$limit", limit);
            if (search.Length > 0) command.Parameters.AddWithValue("$search", "%" + search.Replace("%", "").Replace("_", "") + "%");
            using var reader = command.ExecuteReader();
            var list = new List<Connection>();
            while (reader.Read()) list.Add(ReadConnection(reader));
            return list;
        }
    }

    private static string SearchClause(string search) => search.Length == 0 ? "" : """
        AND (process LIKE $search OR service LIKE $search OR host LIKE $search OR remote LIKE $search OR org LIKE $search
             OR country LIKE $search OR CAST(remote_port AS TEXT) LIKE $search OR ('AS' || asn) LIKE $search)
        """;

    private const string Columns = """
        id, protocol, pid, process, path, service, local, local_port, remote, remote_port, host, host_source, country, asn, org,
        started, last_seen, ended, open_at_start, sent, received, measured, state, verdict, expected_by
        """;

    private static Connection ReadConnection(SqliteDataReader r)
    {
        Addresses.TryParse(r.GetString(6), out var local);
        Addresses.TryParse(r.GetString(8), out var remote);
        return new Connection
        {
            Id = r.GetInt64(0),
            Key = new FlowKey(Enum.Parse<Protocol>(r.GetString(1)), local, r.GetInt32(7), remote, r.GetInt32(9)),
            Pid = r.GetInt32(2),
            Process = r.GetString(3),
            ProcessPath = r.GetString(4),
            Service = r.GetString(5),
            Host = r.GetString(10),
            HostSource = (HostSource)r.GetInt32(11),
            Place = PlaceOf(r.GetString(12), r.GetInt32(13), r.GetString(14)),
            Started = FromMs(r.GetInt64(15)),
            LastSeen = FromMs(r.GetInt64(16)),
            Ended = r.IsDBNull(17) ? null : FromMs(r.GetInt64(17)),
            OpenAtStart = r.GetInt32(18) != 0,
            Sent = r.GetInt64(19),
            Received = r.GetInt64(20),
            Measured = r.GetInt32(21) != 0,
            State = r.GetString(22),
            Verdict = (Verdict)r.GetInt32(23),
            ExpectedBy = r.GetString(24),
        };
    }

    private static Place PlaceOf(string country, int asn, string org) =>
        country.Length == 0 && asn == 0 && org.Length == 0 ? Place.Unknown
        : org == Place.LocalNetwork.Org && asn == 0 ? Place.LocalNetwork
        : new Place(country, asn, org);

    /// <summary>The connections from <paramref name="from"/> to <paramref name="to"/>, added up by destination and
    /// program: what the totals group further, by program, organization, country, name or address. The time open is
    /// counted within the range; bytes are a connection's whole.</summary>
    public List<DestinationTotal> Destinations(DateTime from, DateTime to)
    {
        lock (gate)
        {
            using var command = Command("""
                SELECT protocol, process, path, service, remote, remote_port, host, host_source, country, asn, org,
                       count(*), sum(sent), sum(received),
                       sum(max(0, min(coalesce(ended, last_seen), $to) - max(started, $from))),
                       min(started), max(coalesce(ended, last_seen)), max(measured), max(id)
                FROM connection
                WHERE started < $to AND coalesce(ended, last_seen) >= $from
                GROUP BY protocol, process, path, service, remote, remote_port, host, host_source, country, asn, org
                """);
            command.Parameters.AddWithValue("$from", Ms(from));
            command.Parameters.AddWithValue("$to", Ms(to));
            using var r = command.ExecuteReader();
            var list = new List<DestinationTotal>();
            while (r.Read())
            {
                Addresses.TryParse(r.GetString(4), out var remote);
                var latest = new Connection
                {
                    Id = r.GetInt64(18),
                    Key = new FlowKey(Enum.Parse<Protocol>(r.GetString(0)), 0, 0, remote, r.GetInt32(5)),
                    Process = r.GetString(1),
                    ProcessPath = r.GetString(2),
                    Service = r.GetString(3),
                    Host = r.GetString(6),
                    HostSource = (HostSource)r.GetInt32(7),
                    Place = PlaceOf(r.GetString(8), r.GetInt32(9), r.GetString(10)),
                };
                list.Add(new DestinationTotal
                {
                    Latest = latest,
                    Connections = r.GetInt32(11),
                    Sent = r.GetInt64(12),
                    Received = r.GetInt64(13),
                    Open = TimeSpan.FromMilliseconds(r.GetInt64(14)),
                    First = FromMs(r.GetInt64(15)),
                    Last = FromMs(r.GetInt64(16)),
                    Measured = r.GetInt32(17) != 0,
                });
            }
            return list;
        }
    }

    /// <summary>The bytes sent and received from <paramref name="from"/> to <paramref name="to"/>, in steps of
    /// <paramref name="step"/> (a whole number of minutes), each with the UTC time it starts at.</summary>
    public List<(DateTime At, long Sent, long Received)> Traffic(DateTime from, DateTime to, TimeSpan step)
    {
        var minutes = Math.Max(1, (long)step.TotalMinutes);
        lock (gate)
        {
            using var command = Command("""
                SELECT (minute / $step) * $step, sum(sent), sum(received) FROM traffic
                WHERE minute >= $from AND minute < $to GROUP BY minute / $step ORDER BY 1
                """);
            command.Parameters.AddWithValue("$step", minutes);
            command.Parameters.AddWithValue("$from", Ms(from) / 60_000);
            command.Parameters.AddWithValue("$to", Ms(to) / 60_000);
            using var r = command.ExecuteReader();
            var list = new List<(DateTime, long, long)>();
            while (r.Read()) list.Add((FromMs(r.GetInt64(0) * 60_000), r.GetInt64(1), r.GetInt64(2)));
            return list;
        }
    }

    /// <summary>Deletes the connections that ended, and the traffic, before <paramref name="before"/>.</summary>
    public void Prune(DateTime before)
    {
        lock (gate)
        {
            using var command = Command("DELETE FROM connection WHERE ended IS NOT NULL AND ended < $before; DELETE FROM traffic WHERE minute < $minute");
            command.Parameters.AddWithValue("$before", Ms(before));
            command.Parameters.AddWithValue("$minute", Ms(before) / 60_000);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Deletes all the history (the lists and settings stay).</summary>
    public void ClearHistory()
    {
        lock (gate)
        {
            Execute("DELETE FROM connection WHERE ended IS NOT NULL; DELETE FROM traffic;");
            Execute("VACUUM");
        }
    }

    public long HistorySize
    {
        get
        {
            lock (gate) return Convert.ToInt64(Scalar("SELECT count(*) FROM connection"), CultureInfo.InvariantCulture);
        }
    }

    // ---- Expected lists ----------------------------------------------------------------------------------------

    public List<ExpectedList> Lists()
    {
        lock (gate)
        {
            using var command = Command("SELECT id, name, enabled, rules FROM expected_list ORDER BY position, id");
            using var r = command.ExecuteReader();
            var lists = new List<ExpectedList>();
            while (r.Read()) lists.Add(new ExpectedList(r.GetString(1), r.GetString(3), r.GetInt32(2) != 0, r.GetInt64(0)));
            return lists;
        }
    }

    public void SaveList(ExpectedList list)
    {
        lock (gate)
        {
            using var command = list.Id == 0
                ? Command("INSERT INTO expected_list (name, enabled, rules, position) VALUES ($name, $enabled, $rules, (SELECT coalesce(max(position), 0) + 1 FROM expected_list)) RETURNING id")
                : Command("UPDATE expected_list SET name = $name, enabled = $enabled, rules = $rules WHERE id = $id RETURNING id");
            command.Parameters.AddWithValue("$id", list.Id);
            command.Parameters.AddWithValue("$name", list.Name);
            command.Parameters.AddWithValue("$enabled", list.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$rules", list.Text);
            list.Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    public void DeleteList(long id)
    {
        lock (gate)
        {
            using var command = Command("DELETE FROM expected_list WHERE id = $id");
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    // ---- Settings ----------------------------------------------------------------------------------------------

    public string? Setting(string key)
    {
        lock (gate)
        {
            using var command = Command("SELECT value FROM setting WHERE key = $key");
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (gate)
        {
            using var command = Command("INSERT INTO setting (key, value) VALUES ($key, $value) ON CONFLICT (key) DO UPDATE SET value = excluded.value");
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
    }

    // ---- Plumbing ----------------------------------------------------------------------------------------------

    public static long Ms(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    public static DateTime FromMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    private SqliteCommand Command(string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private void Execute(string sql)
    {
        using var command = Command(sql);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var command = Command(sql);
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        lock (gate) connection.Dispose();
    }
}
