namespace Erp.Core;

public sealed record MappingSuggestion(string FieldKey, int? Column, double Confidence);

/// <summary>
/// Suggests which file column feeds which target field: exact synonym hits first, then
/// containment ("Artikelnr. (intern)") and finally edit distance for typos ("Artikelnumer").
/// Each column is used at most once; the user can correct every suggestion.
/// </summary>
public static class ColumnMatcher
{
    public const double Threshold = 0.72;

    public static List<MappingSuggestion> Suggest(DatasetSchema schema, IReadOnlyList<string> headers)
    {
        var normalized = headers.Select(Values.Normalize).ToArray();
        var candidates =
            from f in schema.Fields
            from c in Enumerable.Range(0, headers.Count)
            let score = Score(normalized[c], f)
            where score >= Threshold
            orderby score descending, f.Required descending
            select (Field: f, Column: c, Score: score);

        var usedFields = new HashSet<string>();
        var usedColumns = new HashSet<int>();
        var picks = new Dictionary<string, (int Column, double Score)>();
        foreach (var (field, column, score) in candidates)
        {
            if (usedFields.Contains(field.Key) || usedColumns.Contains(column)) continue;
            picks[field.Key] = (column, score);
            usedFields.Add(field.Key);
            usedColumns.Add(column);
        }

        return schema.Fields.Select(f => picks.TryGetValue(f.Key, out var p)
            ? new MappingSuggestion(f.Key, p.Column, Math.Round(p.Score, 2))
            : new MappingSuggestion(f.Key, null, 0)).ToList();
    }

    internal static double Score(string header, Field field)
    {
        if (header.Length == 0) return 0;
        var names = field.Synonyms.Append(Values.Normalize(field.Label)).Append(Values.Normalize(field.Key)).Select(Values.Normalize);
        double best = 0;
        foreach (var name in names)
        {
            if (header == name) return 1;
            if (header.Replace(" ", "") == name.Replace(" ", "")) best = Math.Max(best, 0.97);
            else if (name.Length >= 4 && (header.Contains(name, StringComparison.Ordinal) || name.Contains(header, StringComparison.Ordinal) && header.Length >= 4))
                best = Math.Max(best, 0.85);
            else best = Math.Max(best, Similarity(header, name));
        }
        return best;
    }

    internal static double Similarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return 1.0 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
    }
}
