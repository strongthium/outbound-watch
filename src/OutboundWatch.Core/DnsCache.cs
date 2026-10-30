namespace Strongthium.OutboundWatch;

/// <summary>
/// The names in Windows' DNS cache and the addresses they're cached with: what <c>ipconfig /displaydns</c> shows. Any
/// user can read it, and nothing is sent: each name is asked of the cache only. Read every few seconds, it names most
/// connections without administrator rights, since a program looks a name up just before it connects.
/// </summary>
public static unsafe class DnsCache
{
    /// <summary>Each name and address. <c>Alias</c>: the name was a CNAME for another, so it is the one the program
    /// asked for, rather than a content network's own name for the address.</summary>
    public static List<(string Name, UInt128 Address, bool Alias)> Read()
    {
        var found = new List<(string, UInt128, bool)>();
        var names = new List<string>();
        if (!Native.DnsGetCacheDataTable(out var entry)) return found;
        while (entry is not null)
        {
            var next = entry->Next;
            if (entry->Name is not null)
            {
                if (entry->Type is Native.DnsTypeA or Native.DnsTypeAaaa or Native.DnsTypeCname) names.Add(new string(entry->Name));
                Native.DnsFree(entry->Name, 0);
            }
            Native.DnsFree(entry, 0);
            entry = next;
        }

        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var type in (ReadOnlySpan<ushort>)[Native.DnsTypeA, Native.DnsTypeAaaa])
            {
                Native.DnsRecord* records;
                int status;
                fixed (char* n = name) status = Native.DnsQuery(n, type, Native.DnsQueryNoWireQuery, 0, out records, 0);
                if (status != 0 || records is null) continue;
                // An answer through a CNAME lists the CNAME first, then the addresses under the name it points at: they
                // all belong to the name that was asked.
                var alias = false;
                for (var r = records; r is not null; r = r->Next) alias |= r->Type == Native.DnsTypeCname;
                for (var r = records; r is not null; r = r->Next)
                {
                    if (r->Type == Native.DnsTypeA)
                        found.Add((name, Addresses.FromV4(*(uint*)r->Data), alias));
                    else if (r->Type == Native.DnsTypeAaaa)
                        found.Add((name, Addresses.FromV6(new ReadOnlySpan<byte>(r->Data, 16)), alias));
                }
                Native.DnsRecordListFree(records, 1);
            }
        }
        return found;
    }
}
