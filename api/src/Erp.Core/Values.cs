using System.Globalization;
using System.Text.RegularExpressions;

namespace Erp.Core;

/// <summary>
/// Lenient parsing of the number and date formats found in real exports:
/// "1.234,56" (German), "1,234.56" (English), "12,5", "-3", "12 Stk";
/// "05.10.2026", "5.10.26", "2026-10-05", "05/10/2026", Excel serials like "46300".
/// </summary>
public enum NumberStyle { German, English }

public static partial class Values
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyyMMdd"];

    /// <summary>
    /// Decides per column whether "," or "." is the decimal separator. A single "1.234" is ambiguous
    /// (one thousand in German, 1.234 in English); values like "12,5" or "3.75" settle it.
    /// Without evidence, German wins – these are German ERP exports.
    /// </summary>
    public static NumberStyle DetectNumberStyle(IEnumerable<string> values)
    {
        int german = 0, english = 0;
        foreach (var raw in values.Take(2000))
        {
            var s = Clean(raw);
            if (GermanDecimal().IsMatch(s)) german++;
            else if (EnglishDecimal().IsMatch(s)) english++;
        }
        return english > german ? NumberStyle.English : NumberStyle.German;
    }

    public static bool TryDecimal(string raw, out decimal value) => TryDecimal(raw, NumberStyle.German, out value);

    public static bool TryDecimal(string raw, NumberStyle style, out decimal value)
    {
        value = 0;
        var s = Clean(raw);
        if (s.Length == 0) return false;
        s = style == NumberStyle.German ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Inv, out value);
    }

    private static string Clean(string raw) =>
        UnitSuffix().Replace(raw.Trim(), "").Replace("€", "").Replace(" ", "").Replace("\u00a0", "");

    public static bool TryInteger(string raw, out int value)
    {
        value = 0;
        if (!TryDecimal(raw, out var d) || d != decimal.Truncate(d) || d is > int.MaxValue or < int.MinValue) return false;
        value = (int)d;
        return true;
    }

    public static bool TryDate(string raw, out DateOnly value)
    {
        var s = raw.Trim();
        if (s.Length > 10 && s[10] is ' ' or 'T') s = s[..10]; // "2026-10-05 00:00:00"
        if (DateOnly.TryParseExact(s, DateFormats, Inv, DateTimeStyles.None, out value)) return Plausible(value);
        // Excel serial date (days since 1899-12-30), as produced by some CSV exports.
        if (int.TryParse(s, NumberStyles.None, Inv, out var serial) && serial is > 20000 and < 80000)
        {
            value = DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays(serial));
            return true;
        }
        if (double.TryParse(s, NumberStyles.AllowDecimalPoint, Inv, out var d) && d is > 20000 and < 80000)
        {
            value = DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays(Math.Floor(d)));
            return true;
        }
        return false;
    }

    private static bool Plausible(DateOnly d) => d.Year is >= 1990 and <= 2100;

    /// <summary>Normalises a header or value for comparison: lower case, umlauts spelled out, only letters/digits/spaces.</summary>
    public static string Normalize(string s)
    {
        var lower = s.Trim().ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        return Spaces().Replace(NonWord().Replace(lower, " "), " ").Trim();
    }

    [GeneratedRegex(@"\s*(stk|stck|st|pcs|pc|l|kg|m)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex UnitSuffix();

    // "12,5" · "1.234,56" · "-0,75": comma followed by 1–2 or 4+ digits at the end, or dot-thousands before a comma.
    [GeneratedRegex(@"^-?(\d{1,3}(\.\d{3})+,\d+|\d+,(\d{1,2}|\d{4,}))$")]
    private static partial Regex GermanDecimal();

    // "3.75" · "1,234.5": dot followed by 1–2 or 4+ digits, or comma-thousands before a dot.
    [GeneratedRegex(@"^-?(\d{1,3}(,\d{3})+\.\d+|\d+\.(\d{1,2}|\d{4,}))$")]
    private static partial Regex EnglishDecimal();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonWord();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
