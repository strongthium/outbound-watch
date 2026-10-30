using System.Globalization;

namespace Strongthium.OutboundWatch;

/// <summary>
/// A list of where connections are expected to go, written a rule a line:
/// <code>
/// # Updates
/// host *.example.com                    # the name looked up: example.com and every name under it
/// asn 64500,64501 process firefox.exe   # both: that network, from that program
/// ip 203.0.113.0/24 port 443            # a network or an address, on that port
/// country CA,US proto udp
/// org "Example Cloud"                   # the network's organization: a part of its name, or a pattern with *
/// process "C:\Program Files\Example\*"  # a program by name, or by path when there's a \ in it
/// service Dnscache                      # a service inside svchost.exe
/// </code>
/// Every word on a line must match; a word with commas matches any of its values. <c>*</c> stands for any characters
/// and <c>?</c> for one. Everything after a <c>#</c> is a note. A host only matches a name the PC looked up itself, not
/// one from a reverse lookup, which the address's owner chooses.
/// </summary>
public sealed class ExpectedList
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Text { get; private set; } = "";
    public IReadOnlyList<Rule> Rules { get; private set; } = [];
    /// <summary>The lines that couldn't be read, as "Line 3: …".</summary>
    public IReadOnlyList<string> Errors { get; private set; } = [];

    public ExpectedList(string name, string text, bool enabled = true, long id = 0)
    {
        Id = id;
        Name = name;
        Enabled = enabled;
        SetText(text);
    }

    public void SetText(string text)
    {
        Text = text;
        var rules = new List<Rule>();
        var errors = new List<string>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var result = Rule.Parse(lines[i], out var rule);
            if (result.Length > 0) errors.Add($"Line {i + 1}: {result}");
            else if (rule is not null) rules.Add(rule);
        }
        Rules = rules;
        Errors = errors;
    }

    public bool Matches(Connection connection) => Rules.Any(rule => rule.Matches(connection));

    /// <summary>Whether the connection is on one of the lists that are on, and which.</summary>
    public static (Verdict Verdict, string By) Judge(Connection connection, IReadOnlyList<ExpectedList> lists)
    {
        var any = false;
        foreach (var list in lists)
        {
            if (!list.Enabled) continue;
            any = true;
            if (list.Matches(connection)) return (Verdict.Expected, list.Name);
        }
        return (any ? Verdict.Unexpected : Verdict.NotChecked, "");
    }
}

/// <summary>A line of an expected list: the conditions that must all match.</summary>
public sealed class Rule
{
    private readonly List<Func<Connection, bool>> conditions = [];

    public string Line { get; }

    private Rule(string line) => Line = line;

    public bool Matches(Connection c) => conditions.Count > 0 && conditions.All(condition => condition(c));

    public static readonly string[] Words = ["host", "ip", "asn", "org", "country", "process", "service", "port", "proto"];

    /// <summary>Reads a line: "" and the rule, or null for a line with nothing but a note; otherwise what's wrong.</summary>
    public static string Parse(string line, out Rule? rule)
    {
        rule = null;
        var tokens = Tokenize(line, out var error);
        if (error.Length > 0) return error;
        if (tokens.Count == 0) return "";
        var parsed = new Rule(line.Trim());
        for (var i = 0; i < tokens.Count; i += 2)
        {
            var word = tokens[i].ToLowerInvariant();
            if (!Words.Contains(word)) return $"\"{tokens[i]}\" isn't one of {string.Join(", ", Words)}.";
            if (i + 1 >= tokens.Count) return $"\"{word}\" needs a value after it.";
            var values = tokens[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length == 0) return $"\"{word}\" needs a value after it.";
            var condition = Condition(word, values, out error);
            if (condition is null) return error;
            parsed.conditions.Add(condition);
        }
        rule = parsed;
        return "";
    }

    private static Func<Connection, bool>? Condition(string word, string[] values, out string error)
    {
        error = "";
        switch (word)
        {
            case "host":
                return c => c.HostSource == HostSource.Dns && values.Any(v => HostMatches(v, c.Host));
            case "ip":
            {
                var networks = new List<(UInt128 First, UInt128 Last)>();
                foreach (var v in values)
                {
                    if (!Addresses.TryParseNetwork(v, out var first, out var last))
                    {
                        error = $"\"{v}\" isn't an address or a network like 203.0.113.0/24.";
                        return null;
                    }
                    networks.Add((first, last));
                }
                return c => networks.Any(n => c.Remote >= n.First && c.Remote <= n.Last);
            }
            case "asn":
            {
                var numbers = new HashSet<int>();
                foreach (var v in values)
                {
                    var digits = v.StartsWith("AS", StringComparison.OrdinalIgnoreCase) ? v[2..] : v;
                    if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                    {
                        error = $"\"{v}\" isn't an AS number like 64500 or AS64500.";
                        return null;
                    }
                    numbers.Add(n);
                }
                return c => numbers.Contains(c.Place.Asn);
            }
            case "org":
                return c => c.Place.Org.Length > 0 && values.Any(v => v.Contains('*') || v.Contains('?')
                    ? Wildcard.Matches(v, c.Place.Org)
                    : c.Place.Org.Contains(v, StringComparison.OrdinalIgnoreCase));
            case "country":
            {
                foreach (var v in values)
                {
                    if (v.Length != 2 || !char.IsAsciiLetter(v[0]) || !char.IsAsciiLetter(v[1]))
                    {
                        error = $"\"{v}\" isn't a two-letter country code like CA.";
                        return null;
                    }
                }
                return c => values.Any(v => v.Equals(c.Place.Country, StringComparison.OrdinalIgnoreCase));
            }
            case "process":
                return c => values.Any(v => v.Contains('\\') ? Wildcard.Matches(v, c.ProcessPath) : Wildcard.Matches(v, c.Process));
            case "service":
                return c => c.Service.Length > 0 && values.Any(v => c.Service.Split(", ").Any(s => Wildcard.Matches(v, s)));
            case "port":
            {
                var ranges = new List<(int From, int To)>();
                foreach (var v in values)
                {
                    var dash = v.IndexOf('-');
                    var (a, b) = dash < 0 ? (v, v) : (v[..dash], v[(dash + 1)..]);
                    if (!int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var from) || !int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var to)
                        || from > to || to > 65535)
                    {
                        error = $"\"{v}\" isn't a port like 443 or a range like 8000-8100.";
                        return null;
                    }
                    ranges.Add((from, to));
                }
                return c => ranges.Any(r => c.RemotePort >= r.From && c.RemotePort <= r.To);
            }
            case "proto":
            {
                var protocols = new List<Protocol>();
                foreach (var v in values)
                {
                    if (!Enum.TryParse<Protocol>(v, ignoreCase: true, out var p))
                    {
                        error = $"\"{v}\" isn't tcp or udp.";
                        return null;
                    }
                    protocols.Add(p);
                }
                return c => protocols.Contains(c.Protocol);
            }
        }
        error = $"\"{word}\" isn't a word a rule has.";
        return null;
    }

    /// <summary><c>*.example.com</c> matches example.com and every name under it; anything else is a pattern of the
    /// whole name.</summary>
    internal static bool HostMatches(string pattern, string host)
    {
        if (host.Length == 0) return false;
        host = host.TrimEnd('.');
        if (pattern.StartsWith("*.", StringComparison.Ordinal) && host.Equals(pattern[2..], StringComparison.OrdinalIgnoreCase)) return true;
        return Wildcard.Matches(pattern, host);
    }

    /// <summary>The words of a line, a quoted value one word with its spaces; a # outside quotes ends the line.</summary>
    private static List<string> Tokenize(string line, out string error)
    {
        error = "";
        var tokens = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            var ch = line[i];
            if (char.IsWhiteSpace(ch)) { i++; continue; }
            if (ch == '#') break;
            if (ch == '"')
            {
                var end = line.IndexOf('"', i + 1);
                if (end < 0)
                {
                    error = "a quote isn't closed.";
                    return tokens;
                }
                tokens.Add(line[(i + 1)..end]);
                i = end + 1;
                continue;
            }
            var start = i;
            while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '#') i++;
            tokens.Add(line[start..i]);
        }
        return tokens;
    }
}

/// <summary>Patterns with <c>*</c> (any characters) and <c>?</c> (one), ignoring case.</summary>
public static class Wildcard
{
    public static bool Matches(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else return false;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }
}
