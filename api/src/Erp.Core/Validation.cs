namespace Erp.Core;

/// <summary>Field key → file column index. Unmapped fields are absent.</summary>
public sealed class ColumnMapping : Dictionary<string, int>;

/// <summary>Master data the file is checked against (from the database plus the file itself).</summary>
public sealed class ReferenceData
{
    public HashSet<string> Articles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Suppliers { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A row that passed validation, with typed values (string, decimal, int, DateOnly or null).</summary>
public sealed record ValidRecord(int Row, IReadOnlyDictionary<string, object?> Values)
{
    public T Get<T>(string key) => (T)Values[key]!;
    public T? Opt<T>(string key) where T : struct => Values.TryGetValue(key, out var v) && v is T t ? t : null;
    public string? Text(string key) => Values.TryGetValue(key, out var v) ? v as string : null;
}

/// <summary>Row number as the user sees it in Excel (header = row 1).</summary>
public sealed record RowError(int Row, string Field, string Value, string Message);

public sealed record ValidationResult(
    int TotalRows,
    List<ValidRecord> Valid,
    List<RowError> Errors,
    List<string> MissingFields,
    Dictionary<string, NumberStyle> NumberStyles)
{
    public int InvalidRows => Errors.Select(e => e.Row).Distinct().Count();
    public bool CanImport => MissingFields.Count == 0 && Valid.Count > 0;
}

public static class Validator
{
    private static readonly Dictionary<string, string[]> MovementTypeAliases = new()
    {
        ["Zugang"] = ["zugang", "we", "wareneingang", "eingang", "receipt", "in", "plus"],
        ["Abgang"] = ["abgang", "wa", "warenausgang", "entnahme", "verbrauch", "issue", "out", "minus"],
        ["Korrektur"] = ["korrektur", "inventur", "umbuchung", "inventurdifferenz", "adjustment"],
    };

    public static ValidationResult Validate(DatasetSchema schema, TabularFile file, ColumnMapping mapping, ReferenceData refs)
    {
        var missing = schema.Fields.Where(f => f.Required && !mapping.ContainsKey(f.Key)).Select(f => f.Label).ToList();
        var styles = schema.Fields
            .Where(f => f.Type is FieldType.Number or FieldType.Whole && mapping.ContainsKey(f.Key))
            .ToDictionary(f => f.Key, f => Values.DetectNumberStyle(file.Rows.Select(r => r[mapping[f.Key]])));
        if (missing.Count > 0) return new(file.Rows.Count, [], [], missing, styles);

        // Master data imported in the same file can be referenced by its own rows.
        var known = new ReferenceData();
        known.Articles.UnionWith(refs.Articles);
        known.Suppliers.UnionWith(refs.Suppliers);

        var valid = new List<ValidRecord>();
        var errors = new List<RowError>();
        var seenKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < file.Rows.Count; i++)
        {
            var rowNo = i + 2;
            var cells = file.Rows[i];
            var values = new Dictionary<string, object?>();
            var rowErrors = new List<RowError>();
            void Fail(Field f, string raw, string msg) => rowErrors.Add(new RowError(rowNo, f.Label, raw, msg));

            foreach (var field in schema.Fields)
            {
                if (!mapping.TryGetValue(field.Key, out var col)) { values[field.Key] = null; continue; }
                var raw = col < cells.Length ? cells[col].Trim() : "";
                if (raw.Length == 0)
                {
                    if (field.Required) Fail(field, raw, "Pflichtfeld ist leer.");
                    values[field.Key] = null;
                    continue;
                }
                switch (field.Type)
                {
                    case FieldType.Text:
                        if (raw.Length > 200) Fail(field, raw[..40] + "…", "Text ist länger als 200 Zeichen.");
                        else values[field.Key] = raw;
                        break;
                    case FieldType.Number:
                        if (!Values.TryDecimal(raw, styles[field.Key], out var d)) Fail(field, raw, "Keine gültige Zahl.");
                        else if (d < 0 && !field.AllowNegative) Fail(field, raw, "Darf nicht negativ sein.");
                        else values[field.Key] = d;
                        break;
                    case FieldType.Whole:
                        if (!Values.TryInteger(raw, out var n)) Fail(field, raw, "Keine ganze Zahl.");
                        else if (n <= 0) Fail(field, raw, "Muss größer als 0 sein.");
                        else values[field.Key] = n;
                        break;
                    case FieldType.Date:
                        if (!Values.TryDate(raw, out var date)) Fail(field, raw, "Kein gültiges Datum (z. B. 05.10.2026).");
                        else values[field.Key] = date;
                        break;
                    case FieldType.Choice:
                        var norm = Values.Normalize(raw);
                        var hit = MovementTypeAliases.FirstOrDefault(kv => kv.Value.Contains(norm)).Key;
                        if (hit is null) Fail(field, raw, $"Unbekannter Wert. Erlaubt: {string.Join(", ", field.Choices ?? [])}.");
                        else values[field.Key] = hit;
                        break;
                }
            }

            if (rowErrors.Count == 0) RowRules(schema, values, known, seenKeys, rowNo, rowErrors);
            if (rowErrors.Count == 0)
            {
                valid.Add(new ValidRecord(rowNo, values));
                if (schema.Dataset == Dataset.Articles) known.Articles.Add((string)values["number"]!);
                if (schema.Dataset == Dataset.Suppliers) known.Suppliers.Add((string)values["number"]!);
            }
            errors.AddRange(rowErrors);
        }
        return new(file.Rows.Count, valid, errors, missing, styles);
    }

    /// <summary>Rules spanning several fields, references and duplicates.</summary>
    private static void RowRules(DatasetSchema schema, Dictionary<string, object?> v, ReferenceData known,
        Dictionary<string, int> seenKeys, int rowNo, List<RowError> errors)
    {
        void Fail(string fieldKey, string msg) => errors.Add(new RowError(rowNo, schema[fieldKey].Label, Show(v[fieldKey]), msg));
        void Unique(string key, string fieldKey)
        {
            if (seenKeys.TryGetValue(key, out var first)) Fail(fieldKey, $"Doppelt in der Datei (bereits in Zeile {first}).");
            else seenKeys[key] = rowNo;
        }
        void Article(string fieldKey = "articleNumber")
        {
            if (!known.Articles.Contains((string)v[fieldKey]!)) Fail(fieldKey, "Artikel ist im Artikelstamm unbekannt.");
        }

        switch (schema.Dataset)
        {
            case Dataset.Articles:
                Unique((string)v["number"]!, "number");
                if (v["supplierNumber"] is string s && !known.Suppliers.Contains(s)) Fail("supplierNumber", "Lieferant ist unbekannt.");
                break;
            case Dataset.Suppliers:
                Unique((string)v["number"]!, "number");
                break;
            case Dataset.StockMovements:
                Article();
                var qty = (decimal)v["quantity"]!;
                if (qty == 0) { Fail("quantity", "Menge 0 ist keine Bewegung."); break; }
                // Sign follows the movement type, whatever the export used.
                v["quantity"] = (string)v["type"]! switch { "Zugang" => Math.Abs(qty), "Abgang" => -Math.Abs(qty), _ => qty };
                break;
            case Dataset.SalesOrders:
                Unique($"{v["orderNumber"]}/{v["lineNo"]}", "lineNo");
                Article();
                if ((decimal)v["quantity"]! == 0) Fail("quantity", "Menge muss größer als 0 sein.");
                if ((DateOnly)v["dueDate"]! < (DateOnly)v["orderDate"]!) Fail("dueDate", "Liefertermin liegt vor dem Auftragsdatum.");
                if (v["shippedDate"] is DateOnly sd && sd < (DateOnly)v["orderDate"]!) Fail("shippedDate", "Lieferdatum liegt vor dem Auftragsdatum.");
                break;
            case Dataset.PurchaseOrders:
                Unique($"{v["orderNumber"]}/{v["lineNo"]}", "lineNo");
                Article();
                if (!known.Suppliers.Contains((string)v["supplierNumber"]!)) Fail("supplierNumber", "Lieferant ist unbekannt.");
                if ((decimal)v["quantity"]! == 0) Fail("quantity", "Menge muss größer als 0 sein.");
                if ((DateOnly)v["promisedDate"]! < (DateOnly)v["orderDate"]!) Fail("promisedDate", "Termin liegt vor dem Bestelldatum.");
                if (v["receivedDate"] is DateOnly rd && rd < (DateOnly)v["orderDate"]!) Fail("receivedDate", "Wareneingang liegt vor dem Bestelldatum.");
                break;
        }
    }

    private static string Show(object? v) => v switch
    {
        null => "",
        DateOnly d => d.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture),
        decimal m => m.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
        _ => v.ToString() ?? "",
    };
}
