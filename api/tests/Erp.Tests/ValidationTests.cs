using Erp.Core;

namespace Erp.Tests;

public class ValidationTests
{
    private static TabularFile File(string[] headers, params string[][] rows) => new(headers, rows.ToList(), "test");

    private static ColumnMapping Map(DatasetSchema s, string[] headers)
    {
        var m = new ColumnMapping();
        foreach (var x in ColumnMatcher.Suggest(s, headers)) if (x.Column is int c) m[x.FieldKey] = c;
        return m;
    }

    private static ReferenceData Refs()
    {
        var r = new ReferenceData();
        r.Articles.UnionWith(["A-10010", "A-10020"]);
        r.Suppliers.Add("L-100");
        return r;
    }

    [Fact]
    public void Movements_get_the_sign_of_their_type_and_unknown_articles_are_reported()
    {
        string[] h = ["Artikelnr.", "Datum", "Menge", "Bewegungsart"];
        var file = File(h,
            ["A-10010", "05.10.2026", "5", "Abgang"],
            ["A-10010", "05.10.2026", "-2", "WE"],
            ["X-999", "05.10.2026", "1", "Zugang"],
            ["A-10020", "kein Datum", "1", "Zugang"],
            ["A-10020", "06.10.2026", "0", "Korrektur"]);

        var result = Validator.Validate(Schemas.StockMovements, file, Map(Schemas.StockMovements, h), Refs());

        Assert.Equal(2, result.Valid.Count);
        Assert.Equal(-5m, result.Valid[0].Get<decimal>("quantity"));
        Assert.Equal(2m, result.Valid[1].Get<decimal>("quantity"));
        Assert.Equal([4, 5, 6], result.Errors.Select(e => e.Row));
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("unbekannt"));
        Assert.Contains(result.Errors, e => e.Row == 5 && e.Field == "Buchungsdatum");
    }

    [Fact]
    public void Missing_required_columns_block_the_import()
    {
        string[] h = ["Artikelnr.", "Menge"];
        var result = Validator.Validate(Schemas.StockMovements, File(h, ["A-10010", "1"]), Map(Schemas.StockMovements, h), Refs());

        Assert.False(result.CanImport);
        Assert.Contains("Buchungsdatum", result.MissingFields);
        Assert.Contains("Bewegungsart", result.MissingFields);
    }

    [Fact]
    public void Articles_in_the_same_file_can_be_referenced_and_duplicates_are_caught()
    {
        string[] h = ["Artikelnummer", "Bezeichnung", "Warengruppe", "Einheit", "Einstandspreis", "Lieferant"];
        var result = Validator.Validate(Schemas.Articles, File(h,
            ["N-1", "Neu", "Hydraulik", "Stk", "12,90", "L-100"],
            ["N-1", "Neu doppelt", "Hydraulik", "Stk", "1", ""],
            ["N-2", "Unbekannter Lieferant", "Hydraulik", "Stk", "1", "L-999"]),
            Map(Schemas.Articles, h), Refs());

        Assert.Single(result.Valid);
        Assert.Equal(12.90m, result.Valid[0].Get<decimal>("unitCost"));
        Assert.Contains(result.Errors, e => e.Row == 3 && e.Message.Contains("Zeile 2"));
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("Lieferant"));
    }

    [Fact]
    public void Purchase_orders_check_dates_and_references()
    {
        string[] h = ["Bestellnr", "Pos", "Lieferant", "Artikelnr", "Menge", "Bestelldatum", "Bestätigter Termin", "Wareneingang"];
        var result = Validator.Validate(Schemas.PurchaseOrders, File(h,
            ["B-1", "1", "L-100", "A-10010", "10", "01.09.2026", "15.09.2026", "17.09.2026"],
            ["B-1", "2", "L-100", "A-10010", "10", "01.09.2026", "20.08.2026", ""],
            ["B-1", "1", "L-100", "A-10020", "5", "01.09.2026", "15.09.2026", ""]),
            Map(Schemas.PurchaseOrders, h), Refs());

        Assert.Single(result.Valid);
        Assert.Equal(new DateOnly(2026, 9, 17), result.Valid[0].Opt<DateOnly>("receivedDate"));
        Assert.Contains(result.Errors, e => e.Row == 3 && e.Message.Contains("vor dem Bestelldatum"));
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("Doppelt"));
    }
}
