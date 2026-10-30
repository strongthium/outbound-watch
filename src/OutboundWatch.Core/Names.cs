namespace Strongthium.OutboundWatch;

/// <summary>
/// The name of each address, from the best place it was heard: a name a program looked up as it happened (the DNS
/// client's events), then the DNS cache (a name asked through a CNAME before a content network's own name), then a
/// reverse lookup. A better name replaces a worse one; one as good replaces it only from the events, where the latest
/// lookup is the one a new connection follows.
/// </summary>
public sealed class Names
{
    private const int Event = 3, CacheAlias = 2, Cache = 1, Reverse = 0;
    private readonly Dictionary<UInt128, (string Name, int Rank)> names = [];
    private readonly Lock gate = new();

    public int Count
    {
        get { lock (gate) return names.Count; }
    }

    public bool TryGet(UInt128 address, out string name, out HostSource source)
    {
        lock (gate)
        {
            if (names.TryGetValue(address, out var entry))
            {
                name = entry.Name;
                source = entry.Rank == Reverse ? HostSource.Reverse : HostSource.Dns;
                return true;
            }
        }
        name = "";
        source = HostSource.None;
        return false;
    }

    /// <summary>Returns whether the address's name changed.</summary>
    public bool FromEvent(string name, UInt128 address) => Set(address, name, Event);

    public bool FromCache(string name, UInt128 address, bool alias) => Set(address, name, alias ? CacheAlias : Cache);

    public bool FromReverse(string name, UInt128 address) => Set(address, name, Reverse);

    private bool Set(UInt128 address, string name, int rank)
    {
        name = name.TrimEnd('.').ToLowerInvariant();
        if (name.Length == 0) return false;
        lock (gate)
        {
            if (names.TryGetValue(address, out var old))
            {
                if (old.Name == name && old.Rank >= rank) return false;
                if (old.Rank > rank || (old.Rank == rank && rank != Event)) return false;
            }
            names[address] = (name, rank);
            // Weeks of names: forget one rather than grow without end.
            if (names.Count > 200_000) names.Remove(names.Keys.First());
            return true;
        }
    }
}
