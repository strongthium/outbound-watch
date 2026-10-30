using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Strongthium.OutboundWatch;

/// <summary>
/// Addresses as 128-bit numbers, IPv4 ones mapped into IPv6 (<c>::ffff:a.b.c.d</c>) as Windows' dual-stack sockets
/// show them. A number makes a connection's key without allocating, and ranges compare as numbers.
/// </summary>
public static class Addresses
{
    private static readonly UInt128 MappedPrefix = (UInt128)0xFFFF << 32;
    private static readonly UInt128 MappedMask = ~(UInt128)uint.MaxValue;

    public static bool IsV4(UInt128 address) => (address & MappedMask) == MappedPrefix;

    /// <summary>An IPv4 address as Windows keeps it: the four bytes in network order, read as a little-endian
    /// number (so 1.2.3.4 is 0x04030201).</summary>
    public static UInt128 FromV4(uint networkOrder) => MappedPrefix | BinaryPrimitives.ReverseEndianness(networkOrder);

    /// <summary>An IPv4 address with its first byte in the high bits: 1.2.3.4 is 0x01020304.</summary>
    public static UInt128 FromV4Number(uint number) => MappedPrefix | number;

    public static uint V4Number(UInt128 address) => (uint)(address & uint.MaxValue);

    public static UInt128 FromV6(ReadOnlySpan<byte> bytes)
    {
        var value = BinaryPrimitives.ReadUInt128BigEndian(bytes);
        return value;
    }

    public static UInt128 From(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> four = stackalloc byte[4];
            address.TryWriteBytes(four, out _);
            return FromV4Number(BinaryPrimitives.ReadUInt32BigEndian(four));
        }
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        return FromV6(bytes);
    }

    public static IPAddress ToIPAddress(UInt128 address)
    {
        if (IsV4(address))
        {
            Span<byte> four = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(four, V4Number(address));
            return new IPAddress(four);
        }
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128BigEndian(bytes, address);
        return new IPAddress(bytes);
    }

    public static bool TryParse(string text, out UInt128 address)
    {
        address = default;
        if (!IPAddress.TryParse(text.Trim(), out var ip)) return false;
        address = From(ip);
        return true;
    }

    public static string Format(UInt128 address) => ToIPAddress(address).ToString();

    /// <summary>The address and port as they're written: <c>1.2.3.4:443</c>, <c>[2001:db8::1]:443</c>.</summary>
    public static string Format(UInt128 address, int port) => IsV4(address) ? $"{Format(address)}:{port}" : $"[{Format(address)}]:{port}";

    public static bool IsUnspecified(UInt128 address) => address == 0 || address == MappedPrefix;

    public static bool IsLoopback(UInt128 address) => address == 1 || (IsV4(address) && V4Number(address) >> 24 == 127);

    /// <summary>Multicast and broadcast: sent to many, never a connection to one place.</summary>
    public static bool IsMulticast(UInt128 address)
    {
        if (IsV4(address))
        {
            var n = V4Number(address);
            return n >> 28 == 0xE || n == uint.MaxValue || (n & 0xFF) == 0xFF && IsPrivateV4(n);
        }
        return address >> 120 == 0xFF;
    }

    /// <summary>The local network rather than the internet: private ranges (10/8, 172.16/12, 192.168/16), shared
    /// address space (100.64/10), link-local (169.254/16, fe80::/10) and unique local IPv6 (fc00::/7).</summary>
    public static bool IsLocalNetwork(UInt128 address)
    {
        if (IsV4(address))
        {
            var n = V4Number(address);
            return IsPrivateV4(n) || n >> 22 == (100u << 2 | 1) || n >> 16 == (169u << 8 | 254);
        }
        var top = (ulong)(address >> 64);
        return top >> 54 == 0x3FA || top >> 57 == 0x7E;
    }

    private static bool IsPrivateV4(uint n) => n >> 24 == 10 || n >> 20 == (172u << 4 | 1) || n >> 16 == (192u << 8 | 168);

    /// <summary>Not somewhere a connection goes out to: unspecified, loopback or multicast.</summary>
    public static bool IsNowhere(UInt128 address) => IsUnspecified(address) || IsLoopback(address) || IsMulticast(address);

    /// <summary>A network written as <c>203.0.113.0/24</c> or <c>2001:db8::/32</c>, or one address.</summary>
    public static bool TryParseNetwork(string text, out UInt128 first, out UInt128 last)
    {
        first = last = default;
        var slash = text.IndexOf('/');
        if (slash < 0)
        {
            if (!TryParse(text, out first)) return false;
            last = first;
            return true;
        }
        if (!TryParse(text[..slash], out var address) || !int.TryParse(text[(slash + 1)..], out var bits)) return false;
        var v4 = IsV4(address);
        if (bits < 0 || bits > (v4 ? 32 : 128)) return false;
        var hostBits = (v4 ? 32 : 128) - bits;
        var hostMask = hostBits == 128 ? UInt128.MaxValue : ((UInt128)1 << hostBits) - 1;
        first = address & ~hostMask;
        last = address | hostMask;
        if (v4)
        {
            first |= MappedPrefix;
            last = (last & uint.MaxValue) | MappedPrefix;
        }
        return true;
    }
}
