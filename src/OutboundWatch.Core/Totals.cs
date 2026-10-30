namespace Strongthium.OutboundWatch;

/// <summary>What the totals are added up by.</summary>
public enum GroupBy { Program, Organization, Country, Domain, Address, Port }

/// <summary>A line of the totals: everything to one organization, say, over a time.</summary>
public sealed class GroupTotal
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    /// <summary>A second line under the name: the program's folder, the AS number, the names in a domain...</summary>
    public string Detail { get; set; } = "";
    public int Connections { get; set; }
    public long Sent { get; set; }
    public long Received { get; set; }
    public TimeSpan Open { get; set; }
    public DateTime First { get; set; } = DateTime.MaxValue;
    public DateTime Last { get; set; } = DateTime.MinValue;
    /// <summary>How many of the connections aren't on an expected list (when any list is on).</summary>
    public int Unexpected { get; set; }
    public bool Measured { get; set; }
    public List<DestinationTotal> Parts { get; } = [];
    public long Bytes => Sent + Received;
}

public static class Totals
{
    /// <summary>Adds up the destinations by <paramref name="by"/>, most bytes first (or most time open, when no bytes
    /// were counted), each judged against the lists as they are now.</summary>
    public static List<GroupTotal> Group(IEnumerable<DestinationTotal> destinations, GroupBy by, IReadOnlyList<ExpectedList> lists)
    {
        var groups = new Dictionary<string, GroupTotal>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in destinations)
        {
            var c = d.Latest;
            var (key, name, detail) = by switch
            {
                GroupBy.Program => (c.ProcessPath.Length > 0 ? c.ProcessPath : c.Process, c.Service.Length > 0 ? $"{c.Process} ({c.Service})" : c.Process,
                    c.ProcessPath.Length > 0 ? Path.GetDirectoryName(c.ProcessPath) ?? "" : ""),
                GroupBy.Organization => c.Place.Asn > 0 ? ($"AS{c.Place.Asn}", c.Place.Org.Length > 0 ? c.Place.Org : $"AS{c.Place.Asn}", $"AS{c.Place.Asn}")
                    : c.Place == Place.LocalNetwork ? ("local", Place.LocalNetwork.Org, "") : ("", "Unknown network", ""),
                GroupBy.Country => (c.Place.Country, c.Place.Country, ""),
                GroupBy.Domain => c.Host.Length > 0 ? (Domain(c.Host), Domain(c.Host), "") : (c.Address, c.Address, "no name"),
                GroupBy.Address => (c.Address, c.Address, c.Host),
                _ => ($"{c.Protocol}/{c.RemotePort}", PortName(c.Protocol, c.RemotePort), ""),
            };
            if (!groups.TryGetValue(key, out var group)) groups[key] = group = new GroupTotal { Key = key, Name = name, Detail = detail };
            group.Connections += d.Connections;
            group.Sent += d.Sent;
            group.Received += d.Received;
            group.Open += d.Open;
            if (d.First < group.First) group.First = d.First;
            if (d.Last > group.Last) group.Last = d.Last;
            group.Measured |= d.Measured;
            if (ExpectedList.Judge(c, lists).Verdict == Verdict.Unexpected) group.Unexpected += d.Connections;
            group.Parts.Add(d);
        }
        if (by == GroupBy.Domain)
        {
            // A domain's detail: how many names in it.
            foreach (var group in groups.Values.Where(g => g.Detail.Length == 0))
            {
                var hosts = group.Parts.Select(p => p.Latest.Host).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                group.Detail = hosts == 1 ? group.Parts[0].Latest.Host : $"{hosts} names";
            }
        }
        return [.. groups.Values.OrderByDescending(g => g.Bytes).ThenByDescending(g => g.Open).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The domain a name is in, as a person would say it: <c>example.com</c> for <c>cdn.eu.example.com</c>,
    /// <c>example.co.uk</c> for <c>www.example.co.uk</c>. Without the public suffix list, a two-letter country ending
    /// after a short generic label (co, com, net, org, gov, ac, edu...) is taken as one ending.</summary>
    public static string Domain(string host)
    {
        var labels = host.TrimEnd('.').Split('.');
        if (labels.Length <= 2) return host.TrimEnd('.');
        var take = 2;
        if (labels[^1].Length == 2 && labels[^2] is "co" or "com" or "net" or "org" or "gov" or "ac" or "edu" or "or" or "ne" or "go") take = 3;
        return string.Join('.', labels[^take..]);
    }

    /// <summary>A port with what it's usually for: "TCP 443 (HTTPS)".</summary>
    public static string PortName(Protocol protocol, int port)
    {
        var what = (protocol, port) switch
        {
            (Protocol.Tcp, 443) => "HTTPS",
            (Protocol.Udp, 443) => "QUIC",
            (_, 80) => "HTTP",
            (_, 53) => "DNS",
            (Protocol.Tcp, 853) => "DNS over TLS",
            (Protocol.Udp, 123) => "NTP",
            (Protocol.Tcp, 22) => "SSH",
            (Protocol.Tcp, 25) or (Protocol.Tcp, 465) or (Protocol.Tcp, 587) => "SMTP",
            (Protocol.Tcp, 993) => "IMAPS",
            (Protocol.Tcp, 995) => "POP3S",
            (Protocol.Tcp, 143) => "IMAP",
            (Protocol.Tcp, 445) => "SMB",
            (Protocol.Tcp, 3389) => "Remote Desktop",
            (_, 3478) or (_, 19302) => "STUN",
            (Protocol.Tcp, 5222) or (Protocol.Tcp, 5223) => "XMPP / push",
            (Protocol.Tcp, 5228) => "push",
            (Protocol.Tcp, 1194) or (Protocol.Udp, 1194) => "OpenVPN",
            (Protocol.Udp, 51820) => "WireGuard",
            (Protocol.Tcp, 9418) => "git",
            _ => "",
        };
        var name = $"{protocol.ToString().ToUpperInvariant()} {port}";
        return what.Length > 0 ? $"{name} ({what})" : name;
    }
}
