using System.Globalization;
using System.IO.Compression;

namespace Strongthium.OutboundWatch;

/// <summary>Where an address is: the watcher asks this of every new connection.</summary>
public interface IPlaces
{
    Place Lookup(UInt128 address);
}

/// <summary>
/// DB-IP's free databases, read into memory: <c>dbip-country-lite</c> (the country each address is used in) and
/// <c>dbip-asn-lite</c> (the autonomous system that announces it, and its organization). Both are published monthly
/// under CC BY 4.0, as gzipped CSV of ranges: <c>first,last,value</c>. Nothing is looked up online: once the files are
/// downloaded, every lookup is a binary search in memory.
/// </summary>
public sealed class AddressDatabase : IPlaces
{
    public const string Attribution = "IP geolocation and AS data by DB-IP (https://db-ip.com), CC BY 4.0";
    private const string BaseUrl = "https://download.db-ip.com/free/";

    private readonly RangeTable countries = new();
    private readonly RangeTable networks = new();
    private readonly List<string> countryCodes = [];
    private readonly List<(int Asn, string Org)> orgs = [];

    /// <summary>The month the files are of, "2026-10"; "" when they were read from elsewhere.</summary>
    public string Month { get; private init; } = "";
    public int Ranges => countries.Count + networks.Count;

    public Place Lookup(UInt128 address)
    {
        if (Addresses.IsLocalNetwork(address)) return Place.LocalNetwork;
        var country = countries.Find(address) is var c and >= 0 ? countryCodes[c] : "";
        if (country == "ZZ") country = "";
        var (asn, org) = networks.Find(address) is var n and >= 0 ? orgs[n] : (0, "");
        return country.Length == 0 && asn == 0 ? Place.Unknown : new Place(country, asn, org);
    }

    // ---- Reading -----------------------------------------------------------------------------------------------

    public static AddressDatabase Read(TextReader country, TextReader asn, string month = "")
    {
        var database = new AddressDatabase { Month = month };
        var codeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        string? line;
        while ((line = country.ReadLine()) is not null)
        {
            if (!TryRange(line, out var first, out var last, out var rest)) continue;
            var code = rest.Trim().ToString().ToUpperInvariant();
            if (!codeIndex.TryGetValue(code, out var index))
            {
                codeIndex[code] = index = database.countryCodes.Count;
                database.countryCodes.Add(code);
            }
            database.countries.Add(first, last, index);
        }

        var orgIndex = new Dictionary<(int, string), int>();
        while ((line = asn.ReadLine()) is not null)
        {
            if (!TryRange(line, out var first, out var last, out var rest)) continue;
            var comma = rest.IndexOf(',');
            if (comma < 0 || !int.TryParse(rest[..comma], NumberStyles.None, CultureInfo.InvariantCulture, out var number)) continue;
            var key = (number, Unquote(rest[(comma + 1)..]));
            if (!orgIndex.TryGetValue(key, out var index))
            {
                orgIndex[key] = index = database.orgs.Count;
                database.orgs.Add(key);
            }
            database.networks.Add(first, last, index);
        }
        database.countries.Seal();
        database.networks.Seal();
        return database;
    }

    /// <summary>The databases in <paramref name="directory"/>, the latest month there; null when there are none.</summary>
    public static AddressDatabase? Load(string directory)
    {
        var month = LatestMonth(directory);
        if (month is null) return null;
        using var country = OpenGzip(Path.Combine(directory, CountryFile(month)));
        using var asn = OpenGzip(Path.Combine(directory, AsnFile(month)));
        return Read(country, asn, month);
    }

    /// <summary>The latest month that both files are in <paramref name="directory"/> for.</summary>
    public static string? LatestMonth(string directory)
    {
        if (!Directory.Exists(directory)) return null;
        return Directory.EnumerateFiles(directory, "dbip-country-lite-*.csv.gz")
            .Select(file => Path.GetFileName(file)["dbip-country-lite-".Length..^".csv.gz".Length])
            .Where(month => File.Exists(Path.Combine(directory, AsnFile(month))))
            .OrderDescending(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string CountryFile(string month) => $"dbip-country-lite-{month}.csv.gz";
    private static string AsnFile(string month) => $"dbip-asn-lite-{month}.csv.gz";

    private static StreamReader OpenGzip(string file) =>
        new(new GZipStream(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16), CompressionMode.Decompress));

    /// <summary>A line's first two fields, the range's first and last address, and what follows them.</summary>
    private static bool TryRange(string line, out UInt128 first, out UInt128 last, out ReadOnlySpan<char> rest)
    {
        first = last = default;
        rest = default;
        var span = line.AsSpan();
        var a = span.IndexOf(',');
        if (a < 0) return false;
        var b = span[(a + 1)..].IndexOf(',');
        if (b < 0) return false;
        b += a + 1;
        if (!System.Net.IPAddress.TryParse(span[..a], out var from) || !System.Net.IPAddress.TryParse(span[(a + 1)..b], out var to)) return false;
        first = Addresses.From(from);
        last = Addresses.From(to);
        rest = span[(b + 1)..];
        return first <= last;
    }

    /// <summary>A CSV field: <c>"Example, Inc."</c> without its quotes, a doubled quote in it made one.</summary>
    internal static string Unquote(ReadOnlySpan<char> field)
    {
        field = field.Trim();
        if (field.Length >= 2 && field[0] == '"' && field[^1] == '"') return field[1..^1].ToString().Replace("\"\"", "\"");
        return field.ToString();
    }

    // ---- Downloading -------------------------------------------------------------------------------------------

    /// <summary>
    /// Downloads this month's two files into <paramref name="directory"/> (last month's, early in a month before the
    /// new ones are out), checks that they read, and deletes older months. About 12 MB. Returns the month.
    /// </summary>
    public static async Task<string> DownloadAsync(HttpClient http, string directory, IProgress<string>? progress, CancellationToken cancel)
    {
        Directory.CreateDirectory(directory);
        var now = DateTime.UtcNow;
        Exception? last = null;
        foreach (var date in (DateTime[])[now, now.AddMonths(-1)])
        {
            var month = date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (LatestMonth(directory) == month) return month;
            try
            {
                var country = Path.Combine(directory, CountryFile(month));
                var asn = Path.Combine(directory, AsnFile(month));
                progress?.Report("Downloading the countries…");
                await DownloadFileAsync(http, BaseUrl + CountryFile(month), country, cancel).ConfigureAwait(false);
                progress?.Report("Downloading the networks…");
                await DownloadFileAsync(http, BaseUrl + AsnFile(month), asn, cancel).ConfigureAwait(false);
                progress?.Report("Reading them…");
                if (await Task.Run(() => Load(directory), cancel).ConfigureAwait(false) is not { Ranges: > 1000 })
                    throw new InvalidDataException("The downloaded files hold no address ranges.");
                foreach (var old in Directory.EnumerateFiles(directory, "dbip-*-lite-*.csv.gz"))
                {
                    if (!Path.GetFileName(old).Contains(month, StringComparison.Ordinal)) File.Delete(old);
                }
                return month;
            }
            catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                last = exception;
            }
        }
        throw new InvalidOperationException("DB-IP has no files for this month or the last one.", last);
    }

    private static async Task DownloadFileAsync(HttpClient http, string url, string file, CancellationToken cancel)
    {
        var part = file + ".part";
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var output = File.Create(part);
            await response.Content.CopyToAsync(output, cancel).ConfigureAwait(false);
        }
        File.Move(part, file, overwrite: true);
    }
}

/// <summary>Address ranges, each with a value, kept IPv4 and IPv6 apart and sorted for binary search.</summary>
internal sealed class RangeTable
{
    private readonly List<(uint First, uint Last, int Value)> v4Building = [];
    private readonly List<(UInt128 First, UInt128 Last, int Value)> v6Building = [];
    private uint[] v4First = [], v4Last = [];
    private int[] v4Value = [];
    private UInt128[] v6First = [], v6Last = [];
    private int[] v6Value = [];

    public int Count => v4First.Length + v6First.Length;

    public void Add(UInt128 first, UInt128 last, int value)
    {
        if (Addresses.IsV4(first) && Addresses.IsV4(last)) v4Building.Add((Addresses.V4Number(first), Addresses.V4Number(last), value));
        else if (!Addresses.IsV4(first) && !Addresses.IsV4(last)) v6Building.Add((first, last, value));
    }

    public void Seal()
    {
        v4Building.Sort((a, b) => a.First.CompareTo(b.First));
        v6Building.Sort((a, b) => a.First.CompareTo(b.First));
        v4First = [.. v4Building.Select(r => r.First)];
        v4Last = [.. v4Building.Select(r => r.Last)];
        v4Value = [.. v4Building.Select(r => r.Value)];
        v6First = [.. v6Building.Select(r => r.First)];
        v6Last = [.. v6Building.Select(r => r.Last)];
        v6Value = [.. v6Building.Select(r => r.Value)];
        v4Building.Clear();
        v4Building.TrimExcess();
        v6Building.Clear();
        v6Building.TrimExcess();
    }

    /// <summary>The value of the range the address is in; -1 when it's in none.</summary>
    public int Find(UInt128 address)
    {
        if (Addresses.IsV4(address))
        {
            var n = Addresses.V4Number(address);
            var i = Array.BinarySearch(v4First, n);
            if (i < 0) i = ~i - 1;
            return i >= 0 && n <= v4Last[i] ? v4Value[i] : -1;
        }
        var j = Array.BinarySearch(v6First, address);
        if (j < 0) j = ~j - 1;
        return j >= 0 && address <= v6Last[j] ? v6Value[j] : -1;
    }
}
