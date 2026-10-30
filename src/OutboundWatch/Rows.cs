using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace Strongthium.OutboundWatch.App;

internal static class Rows
{
    private static readonly Dictionary<string, string> countries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A country's name from its code, as Windows names it: "Germany" for DE.</summary>
    public static string CountryName(string code)
    {
        if (code.Length != 2) return "";
        if (countries.TryGetValue(code, out var name)) return name;
        try { name = new RegionInfo(code).EnglishName; }
        catch (ArgumentException) { name = code.ToUpperInvariant(); }
        return countries[code] = name;
    }

    /// <summary>"Example Cloud GmbH, Germany".</summary>
    public static string PlaceText(Place place) =>
        string.Join(", ", new[] { place.Org, CountryName(place.Country) }.Where(part => part.Length > 0));

    /// <summary>A time in the PC's own time: the time alone today, the day and time before.</summary>
    public static string When(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return local.Date == DateTime.Today ? local.ToString("T", CultureInfo.CurrentCulture) : local.ToString("MMM d, HH:mm", CultureInfo.CurrentCulture);
    }

    public static string Bytes(long bytes, bool measured) => measured ? Sizes.Format(bytes) : "—";

    /// <summary>Sorts a list that's on screen into the order by moving its items, so that what's selected stays
    /// selected and the list doesn't jump.</summary>
    public static void SortInPlace<T>(ObservableCollection<T> items, Comparison<T> comparison) where T : class
    {
        var sorted = items.ToList();
        sorted.Sort(comparison);
        for (var i = 0; i < sorted.Count; i++)
        {
            if (!ReferenceEquals(items[i], sorted[i])) items.Move(items.IndexOf(sorted[i]), i);
        }
    }

    public static void Show(this UIElement element, bool visible) => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>A connection as a line of a list, live or from the history.</summary>
public sealed class ConnectionRow : INotifyPropertyChanged
{
    private long lastBytes = -1;
    private DateTime lastAt;

    public Connection C { get; private set; }
    private DateTime now;
    public double Rate { get; private set; }

    public ConnectionRow(Connection c, DateTime now)
    {
        C = c;
        this.now = now;
        lastBytes = c.Sent + c.Received;
        lastAt = now;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The connection as it is now: every column is read again.</summary>
    public void Update(Connection c, DateTime at)
    {
        var bytes = c.Sent + c.Received;
        var seconds = (at - lastAt).TotalSeconds;
        if (seconds > 0.2)
        {
            Rate = Math.Max(0, (bytes - lastBytes) / seconds);
            lastBytes = bytes;
            lastAt = at;
        }
        C = c;
        now = at;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public string Program => C.Process;
    public string ProgramDetail => C.Service;
    public string ProgramTip => C.ProcessPath.Length > 0 ? C.ProcessPath : C.Process;

    public string Destination => C.Destination;
    /// <summary>Under the name: its address and the port (with what it's usually for); a reverse lookup's name is
    /// marked as one.</summary>
    public string DestinationDetail => C.Host.Length == 0 ? Port + " · no name found"
        : Addresses.Format(C.Remote) + " · " + Port + (C.HostSource == HostSource.Reverse ? " · reverse lookup" : "");

    public string Org => C.Place == Place.Unknown ? "—" : C.Place.Org.Length > 0 ? C.Place.Org : $"AS{C.Place.Asn}";
    public string OrgDetail => C.Place.Asn > 0 ? $"AS{C.Place.Asn}" : "";
    public string Country => Rows.CountryName(C.Place.Country);
    public string CountryCode => C.Place.Country;
    public string Port => Totals.PortName(C.Protocol, C.RemotePort);

    public string StartedText => Rows.When(C.Started);
    public string EndedText => C.Ended is { } ended ? "to " + Rows.When(ended) : "still open";

    public TimeSpan Duration => C.IsOpen ? now - C.Started : C.Duration;
    /// <summary>How long it was open; "at least", for one open before watching began.</summary>
    public string DurationText => (C.OpenAtStart ? "≥ " : "") + Sizes.Duration(Duration);

    public string SentText => Rows.Bytes(C.Sent, C.Measured);
    public string ReceivedText => Rows.Bytes(C.Received, C.Measured);
    public string RateText => C.Measured && Rate >= 1 ? Sizes.Rate(Rate) : "";

    public bool IsUnexpected => C.Verdict == Verdict.Unexpected;
    public bool IsExpected => C.Verdict == Verdict.Expected;
    public string VerdictText => C.Verdict switch
    {
        Verdict.Expected => C.ExpectedBy,
        Verdict.Unexpected => "Unexpected",
        _ => "",
    };

    /// <summary>The order of a column, by its heading's Tag.</summary>
    public static Comparison<ConnectionRow> Order(string column) => column switch
    {
        "Program" => (a, b) => string.Compare(a.Program, b.Program, StringComparison.OrdinalIgnoreCase),
        "Destination" => (a, b) => string.Compare(a.Destination, b.Destination, StringComparison.OrdinalIgnoreCase),
        "Org" => (a, b) => string.Compare(a.Org, b.Org, StringComparison.OrdinalIgnoreCase),
        "Country" => (a, b) => string.Compare(a.Country, b.Country, StringComparison.OrdinalIgnoreCase),
        "Port" => (a, b) => a.C.RemotePort.CompareTo(b.C.RemotePort),
        "Started" => (a, b) => a.C.Started.CompareTo(b.C.Started),
        "Duration" => (a, b) => a.Duration.CompareTo(b.Duration),
        "Sent" => (a, b) => a.C.Sent.CompareTo(b.C.Sent),
        "Received" => (a, b) => a.C.Received.CompareTo(b.C.Received),
        "Rate" => (a, b) => a.Rate.CompareTo(b.Rate),
        "Verdict" => (a, b) => b.C.Verdict.CompareTo(a.C.Verdict) is var v and not 0 ? v : string.Compare(a.VerdictText, b.VerdictText, StringComparison.OrdinalIgnoreCase),
        _ => (a, b) => a.C.Started.CompareTo(b.C.Started),
    };

    /// <summary>The columns that read best largest first.</summary>
    public static bool LargestFirst(string column) => column is "Started" or "Duration" or "Sent" or "Received" or "Rate" or "Verdict";

    /// <summary>Whether it has <paramref name="text"/> in its program, service, name, address, network or country.</summary>
    public bool Contains(string text) =>
        text.Length == 0
        || C.Process.Contains(text, StringComparison.OrdinalIgnoreCase) || C.Service.Contains(text, StringComparison.OrdinalIgnoreCase)
        || C.Host.Contains(text, StringComparison.OrdinalIgnoreCase) || C.Address.Contains(text, StringComparison.OrdinalIgnoreCase)
        || C.Place.Org.Contains(text, StringComparison.OrdinalIgnoreCase) || $"AS{C.Place.Asn}".Equals(text, StringComparison.OrdinalIgnoreCase)
        || Country.Contains(text, StringComparison.OrdinalIgnoreCase) || C.Place.Country.Equals(text, StringComparison.OrdinalIgnoreCase)
        || C.RemotePort.ToString(CultureInfo.InvariantCulture) == text;
}

/// <summary>A line of the totals.</summary>
public sealed class GroupRow(GroupTotal group, GroupBy by, long largest, TimeSpan longest)
{
    public GroupTotal Group { get; } = group;
    public GroupBy By { get; } = by;

    public string Name => By == GroupBy.Country ? (Group.Key.Length == 0 ? "Unknown country" : Rows.CountryName(Group.Key)) : Group.Name;
    public string Detail => By == GroupBy.Country ? Group.Key : Group.Detail;
    public string Connections => Sizes.Count(Group.Connections);
    public string OpenText => Sizes.Duration(Group.Open);
    public string SentText => Rows.Bytes(Group.Sent, Group.Measured);
    public string ReceivedText => Rows.Bytes(Group.Received, Group.Measured);
    public string LastText => Rows.When(Group.Last);
    public string UnexpectedText => Group.Unexpected > 0 ? Sizes.Count(Group.Unexpected) : "";

    /// <summary>The share bar: of the most bytes, or when none were counted, of the longest time open.</summary>
    private double Share => largest > 0 ? (double)Group.Bytes / largest : longest > TimeSpan.Zero ? Group.Open / longest : 0;
    public GridLength ShareFill => new(Math.Max(Share, 0.001), GridUnitType.Star);
    public GridLength ShareRest => new(Math.Max(1 - Share, 0.001), GridUnitType.Star);

    /// <summary>Whether a connection is one of this line's.</summary>
    public bool Includes(Connection c)
    {
        var key = Group.Key;
        return By switch
        {
            GroupBy.Program => (c.ProcessPath.Length > 0 ? c.ProcessPath : c.Process).Equals(key, StringComparison.OrdinalIgnoreCase),
            GroupBy.Organization => c.Place.Asn > 0 ? $"AS{c.Place.Asn}" == key : key == "local" ? c.Place == Place.LocalNetwork : key.Length == 0 && c.Place.Asn == 0 && c.Place != Place.LocalNetwork,
            GroupBy.Country => c.Place.Country.Equals(key, StringComparison.OrdinalIgnoreCase),
            GroupBy.Domain => c.Host.Length > 0 ? Totals.Domain(c.Host).Equals(key, StringComparison.OrdinalIgnoreCase) : c.Address == key,
            GroupBy.Address => c.Address == key,
            _ => $"{c.Protocol}/{c.RemotePort}" == key,
        };
    }

    public static Comparison<GroupRow> Order(string column) => column switch
    {
        "Name" => (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
        "Connections" => (a, b) => a.Group.Connections.CompareTo(b.Group.Connections),
        "Open" => (a, b) => a.Group.Open.CompareTo(b.Group.Open),
        "Sent" => (a, b) => a.Group.Sent.CompareTo(b.Group.Sent),
        "Received" => (a, b) => a.Group.Received.CompareTo(b.Group.Received),
        "Unexpected" => (a, b) => a.Group.Unexpected.CompareTo(b.Group.Unexpected),
        "Last" => (a, b) => a.Group.Last.CompareTo(b.Group.Last),
        _ => (a, b) => a.Group.Bytes.CompareTo(b.Group.Bytes) is var v and not 0 ? v : a.Group.Open.CompareTo(b.Group.Open),
    };
}
