using System.Buffers.Binary;
using System.Net;

namespace Strongthium.OutboundWatch.Tests;

public sealed class AddressTests
{
    private static UInt128 A(string text) => Addresses.TryParse(text, out var a) ? a : throw new ArgumentException(text);

    [Fact]
    public void IPv4_is_mapped_into_IPv6_and_back()
    {
        var a = A("203.0.113.5");
        Assert.True(Addresses.IsV4(a));
        Assert.Equal(a, A("::ffff:203.0.113.5"));
        Assert.Equal("203.0.113.5", Addresses.Format(a));
        Assert.Equal("203.0.113.5:443", Addresses.Format(a, 443));
        Assert.Equal("[2001:db8::1]:443", Addresses.Format(A("2001:db8::1"), 443));
        // As Windows keeps it: the bytes in network order, read little-endian.
        Assert.Equal(a, Addresses.FromV4(BinaryPrimitives.ReadUInt32LittleEndian([203, 0, 113, 5])));
        Assert.Equal(IPAddress.Parse("2001:db8::1"), Addresses.ToIPAddress(A("2001:db8::1")));
    }

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.1", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.128.0.1", false)]
    [InlineData("169.254.10.1", true)]
    [InlineData("203.0.113.5", false)]
    [InlineData("fe80::1", true)]
    [InlineData("fd12::1", true)]
    [InlineData("2001:db8::1", false)]
    public void Local_network(string address, bool local) => Assert.Equal(local, Addresses.IsLocalNetwork(A(address)));

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("224.0.0.251")]
    [InlineData("255.255.255.255")]
    [InlineData("ff02::fb")]
    public void Nowhere(string address) => Assert.True(Addresses.IsNowhere(A(address)));

    [Fact]
    public void Networks()
    {
        Assert.True(Addresses.TryParseNetwork("203.0.113.0/24", out var first, out var last));
        Assert.Equal(A("203.0.113.0"), first);
        Assert.Equal(A("203.0.113.255"), last);
        Assert.True(Addresses.TryParseNetwork("2001:db8::/32", out first, out last));
        Assert.Equal(A("2001:db8::"), first);
        Assert.Equal(A("2001:db8:ffff:ffff:ffff:ffff:ffff:ffff"), last);
        Assert.True(Addresses.TryParseNetwork("0.0.0.0/0", out first, out last));
        Assert.True(Addresses.IsV4(first) && Addresses.IsV4(last));
        Assert.False(Addresses.TryParseNetwork("203.0.113.0/33", out _, out _));
        Assert.False(Addresses.TryParseNetwork("example.com", out _, out _));
    }

    [Fact]
    public void Address_database()
    {
        var country = new StringReader("""
            0.0.0.0,0.255.255.255,ZZ
            203.0.113.0,203.0.113.127,CA
            203.0.113.128,203.0.113.255,DE
            2001:db8::,2001:db8:ffff:ffff:ffff:ffff:ffff:ffff,NL
            """);
        var asn = new StringReader("""
            203.0.113.0,203.0.113.255,64500,"Example, Inc."
            2001:db8::,2001:db8:0:ffff:ffff:ffff:ffff:ffff,64501,"The ""Quoted"" Network"
            """);
        var database = AddressDatabase.Read(country, asn);
        Assert.Equal(new Place("CA", 64500, "Example, Inc."), database.Lookup(A("203.0.113.5")));
        Assert.Equal(new Place("DE", 64500, "Example, Inc."), database.Lookup(A("203.0.113.200")));
        Assert.Equal(new Place("NL", 64501, "The \"Quoted\" Network"), database.Lookup(A("2001:db8::1")));
        Assert.Equal(new Place("NL", 0, ""), database.Lookup(A("2001:db8:1::1")));
        Assert.Equal(Place.Unknown, database.Lookup(A("198.51.100.1")));
        Assert.Equal(Place.Unknown, database.Lookup(A("0.1.2.3")));
        Assert.Same(Place.LocalNetwork, database.Lookup(A("192.168.1.1")));
    }

    [Fact]
    public void Address_database_files_by_month()
    {
        var directory = Directory.CreateTempSubdirectory("outbound-watch-").FullName;
        try
        {
            Assert.Null(AddressDatabase.LatestMonth(directory));
            foreach (var month in (string[])["2026-09", "2026-10"])
            {
                Write(Path.Combine(directory, $"dbip-country-lite-{month}.csv.gz"), "203.0.113.0,203.0.113.255,CA\n");
                Write(Path.Combine(directory, $"dbip-asn-lite-{month}.csv.gz"), "203.0.113.0,203.0.113.255,64500,Example\n");
            }
            // A month with only one of the two isn't used.
            Write(Path.Combine(directory, "dbip-country-lite-2026-11.csv.gz"), "");
            var database = AddressDatabase.Load(directory)!;
            Assert.Equal("2026-10", database.Month);
            Assert.Equal(new Place("CA", 64500, "Example"), database.Lookup(A("203.0.113.9")));
        }
        finally { Directory.Delete(directory, recursive: true); }

        static void Write(string file, string text)
        {
            using var gzip = new System.IO.Compression.GZipStream(File.Create(file), System.IO.Compression.CompressionLevel.Fastest);
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(text));
        }
    }
}
