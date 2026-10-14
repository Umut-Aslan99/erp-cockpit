using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Erp.Core;

/// <summary>A file reduced to what an import needs: column headers and string cells.</summary>
public sealed record TabularFile(string[] Headers, List<string[]> Rows, string Format)
{
    public const int MaxRows = 20_000;
}

public sealed class ImportFileException(string message) : Exception(message);

public static class TabularReader
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    static TabularReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static TabularFile Read(string fileName, byte[] content)
    {
        if (content.Length == 0) throw new ImportFileException("Die Datei ist leer.");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xlsm" => ReadXlsx(content),
            ".csv" or ".txt" or ".tsv" => ReadCsv(content),
            ".xls" => throw new ImportFileException("Das alte Excel-Format .xls wird nicht unterstützt. Bitte als .xlsx oder .csv speichern."),
            _ => throw new ImportFileException($"Dateityp „{ext}“ wird nicht unterstützt. Erlaubt sind .csv und .xlsx."),
        };
    }

    // ── CSV ──────────────────────────────────────────────────────────────

    internal static TabularFile ReadCsv(byte[] content)
    {
        var (text, encodingName) = Decode(content);
        var firstLine = text.AsSpan()[..Math.Max(0, text.IndexOfAny(['\r', '\n']) is var i and >= 0 ? i : text.Length)].ToString();
        var delimiter = DetectDelimiter(firstLine);
        var records = ParseCsv(text, delimiter);
        records.RemoveAll(r => r.All(string.IsNullOrWhiteSpace));
        if (records.Count == 0) throw new ImportFileException("Die Datei enthält keine Zeilen.");

        var headers = records[0].Select(h => h.Trim()).ToArray();
        var rows = records.Skip(1).Select(r => Pad(r, headers.Length)).ToList();
        if (rows.Count > TabularFile.MaxRows) throw new ImportFileException($"Höchstens {TabularFile.MaxRows:N0} Zeilen pro Datei.");
        var name = delimiter switch { ';' => "Semikolon", '\t' => "Tabulator", _ => "Komma" };
        return new TabularFile(headers, rows, $"CSV · {name} · {encodingName}");
    }

    /// <summary>UTF-8 when the bytes are valid UTF-8, otherwise Windows-1252 (typical for older German ERP exports).</summary>
    internal static (string Text, string EncodingName) Decode(byte[] bytes)
    {
        if (bytes is [0xEF, 0xBB, 0xBF, ..]) return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "UTF-8");
        try
        {
            return (new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(1252).GetString(bytes), "Windows-1252");
        }
    }

    internal static char DetectDelimiter(string headerLine)
    {
        char[] candidates = [';', '\t', ','];
        return candidates.OrderByDescending(c => headerLine.Count(ch => ch == c)).First();
    }

    /// <summary>RFC 4180 parser: quoted fields, doubled quotes and line breaks inside quotes.</summary>
    internal static List<string[]> ParseCsv(string text, char delimiter)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0) quoted = true;
            else if (c == delimiter) { fields.Add(field.ToString()); field.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(field.ToString()); field.Clear();
                records.Add([.. fields]); fields.Clear();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); records.Add([.. fields]); }
        return records;
    }

    // ── Excel ────────────────────────────────────────────────────────────

    internal static TabularFile ReadXlsx(byte[] content)
    {
        XLWorkbook book;
        try { book = new XLWorkbook(new MemoryStream(content)); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { throw new ImportFileException("Die Excel-Datei konnte nicht gelesen werden."); }

        using (book)
        {
            var sheet = book.Worksheets.FirstOrDefault(s => s.LastRowUsed() is not null)
                ?? throw new ImportFileException("Die Excel-Datei enthält keine Daten.");
            var range = sheet.RangeUsed()!;
            var width = range.ColumnCount();
            if (range.RowCount() - 1 > TabularFile.MaxRows) throw new ImportFileException($"Höchstens {TabularFile.MaxRows:N0} Zeilen pro Datei.");

            var all = range.Rows().Select(r => Enumerable.Range(1, width).Select(c => CellText(r.Cell(c))).ToArray()).ToList();
            all.RemoveAll(r => r.All(string.IsNullOrWhiteSpace));
            if (all.Count == 0) throw new ImportFileException("Die Excel-Datei enthält keine Daten.");
            return new TabularFile(all[0].Select(h => h.Trim()).ToArray(), all.Skip(1).ToList(), $"Excel · Blatt „{sheet.Name}“");
        }
    }

    /// <summary>Typed Excel cells become canonical strings so the same value parsers handle CSV and Excel.</summary>
    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        // German notation without thousands separators, so Excel numbers are never mistaken for "1.234 = one thousand".
        XLDataType.Number => cell.GetDouble().ToString("0.##########", German),
        XLDataType.Boolean => cell.GetBoolean() ? "true" : "false",
        _ => cell.GetFormattedString(),
    };

    private static string[] Pad(string[] row, int width) =>
        row.Length >= width ? row[..width] : [.. row, .. Enumerable.Repeat("", width - row.Length)];
}
