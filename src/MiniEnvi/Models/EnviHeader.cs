using System.Globalization;

namespace MiniEnvi.Models;

public sealed class EnviHeader
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    public IEnumerable<KeyValuePair<string, string>> Entries =>
        _order.Select(key => new KeyValuePair<string, string>(key, _values[key]));

    public int Samples => GetInt("samples");
    public int Lines => GetInt("lines");
    public int Bands => GetInt("bands");
    public int HeaderOffset => GetInt("header offset", 0);
    public int DataType => GetInt("data type");
    public string Interleave => Get("interleave", "bsq").Trim().ToLowerInvariant();
    public int ByteOrder => GetInt("byte order", 0);
    public string? DataFile => TryGet("data file", out var value) ? TrimBraces(value) : null;

    public void Set(string key, string value)
    {
        key = key.Trim();
        if (!_values.ContainsKey(key)) _order.Add(key);
        _values[key] = value.Trim();
    }

    public bool TryGet(string key, out string value) => _values.TryGetValue(key, out value!);

    public string Get(string key, string defaultValue = "") =>
        TryGet(key, out var value) ? value : defaultValue;

    public int GetInt(string key, int defaultValue = 0) =>
        TryGet(key, out var value) && int.TryParse(TrimBraces(value), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    public double? GetDouble(string key)
    {
        if (!TryGet(key, out var value)) return null;
        return double.TryParse(TrimBraces(value), NumberStyles.Float, CultureInfo.InvariantCulture,
            out var result) ? result : null;
    }

    public IReadOnlyList<string> GetList(string key)
    {
        if (!TryGet(key, out var raw)) return [];
        raw = TrimBraces(raw);
        return raw.Split(',', StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();
    }

    public EnviHeader Clone()
    {
        var copy = new EnviHeader();
        foreach (var item in Entries) copy.Set(item.Key, item.Value);
        return copy;
    }

    public static string TrimBraces(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '{' && value[^1] == '}'
            ? value[1..^1].Trim()
            : value;
    }
}
