namespace Strongthium.OutboundWatch;

public enum Protocol { Tcp, Udp }

/// <summary>Where a name for an address came from. Only a name the PC looked up itself (<see cref="Dns"/>) is
/// trusted by the expected lists: a reverse lookup's name is whatever the address's owner chose to call it.</summary>
public enum HostSource { None, Dns, Reverse }

/// <summary>One end of a connection seen from this PC: the protocol, the local address and port, and where it goes.
/// Addresses are <see cref="Addresses"/>' numbers.</summary>
public readonly record struct FlowKey(Protocol Protocol, UInt128 Local, int LocalPort, UInt128 Remote, int RemotePort)
{
    public override string ToString() => $"{Protocol} {Addresses.Format(Local, LocalPort)} -> {Addresses.Format(Remote, RemotePort)}";
}

/// <summary>Where an address is, from the address databases: the country the address is used in, and the network
/// (autonomous system) that announces it.</summary>
public sealed record Place(string Country, int Asn, string Org)
{
    public static readonly Place Unknown = new("", 0, "");
    public static readonly Place LocalNetwork = new("", 0, "Local network");
}

/// <summary>Whether a connection is on an expected list.</summary>
public enum Verdict
{
    /// <summary>No list is turned on, so nothing is judged.</summary>
    NotChecked,
    Expected,
    Unexpected,
}

/// <summary>
/// One outgoing connection: a TCP connection from its start to its end, or the UDP packets between one local port and
/// one remote address and port until they stop for two minutes. Live ones are changed by the watcher as packets come;
/// the history keeps each one as it last was.
/// </summary>
public sealed class Connection
{
    /// <summary>Its line in the history; 0 until it's first saved.</summary>
    public long Id { get; set; }
    public FlowKey Key { get; init; }
    public Protocol Protocol => Key.Protocol;
    public UInt128 Remote => Key.Remote;
    public int RemotePort => Key.RemotePort;

    public int Pid { get; set; }
    /// <summary>The program's file name, <c>firefox.exe</c>; "System" for the kernel, or "PID 1234" if it had gone
    /// before it could be named.</summary>
    public string Process { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    /// <summary>The services in the process, for a <c>svchost.exe</c>: "Dnscache, NlaSvc".</summary>
    public string Service { get; set; } = "";

    public string Host { get; set; } = "";
    public HostSource HostSource { get; set; }
    public Place Place { get; set; } = Place.Unknown;

    /// <summary>When it was first seen; UTC.</summary>
    public DateTime Started { get; set; }
    /// <summary>When it was last seen open, or last sent or received.</summary>
    public DateTime LastSeen { get; set; }
    /// <summary>When it ended; null while it's open.</summary>
    public DateTime? Ended { get; set; }
    /// <summary>It was already open when watching began, so it started before <see cref="Started"/>.</summary>
    public bool OpenAtStart { get; set; }

    public long Sent { get; set; }
    public long Received { get; set; }
    /// <summary>Its bytes were counted: the kernel's network events were being read (as administrator).</summary>
    public bool Measured { get; set; }

    /// <summary>The TCP state, as Windows' table last showed it: "Established", "SynSent"...</summary>
    public string State { get; set; } = "";

    public Verdict Verdict { get; set; }
    /// <summary>The list it's on, when it's <see cref="Verdict.Expected"/>.</summary>
    public string ExpectedBy { get; set; } = "";

    public bool IsOpen => Ended is null;
    public TimeSpan Duration => (Ended ?? LastSeen) - Started;
    public string Address => Addresses.Format(Remote);

    /// <summary>The name when there is one, or the address.</summary>
    public string Destination => Host.Length > 0 ? Host : Address;

    // The watcher's own: polls it has been missing from the table, whether it changed since it was saved, and whether
    // it was told about as unexpected.
    internal int Missed;
    internal bool Dirty;
    internal bool Notified;

    public Connection Copy() => (Connection)MemberwiseClone();
}
