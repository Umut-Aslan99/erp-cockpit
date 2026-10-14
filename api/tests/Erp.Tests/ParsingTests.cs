using System.Text;
using Erp.Core;

namespace Erp.Tests;

public class ValueParsingTests
{
    [Theory]
    [InlineData("1.234,56", NumberStyle.German, 1234.56)]
    [InlineData("12,5", NumberStyle.German, 12.5)]
    [InlineData("-3", NumberStyle.German, -3)]
    [InlineData("1.234", NumberStyle.German, 1234)]
    [InlineData("1,234.56", NumberStyle.English, 1234.56)]
    [InlineData("1.234", NumberStyle.English, 1.234)]
    [InlineData("48,50 €", NumberStyle.German, 48.5)]
    [InlineData("12 Stk", NumberStyle.German, 12)]
    public void Numbers_follow_the_column_style(string raw, NumberStyle style, double expected)
    {
        Assert.True(Values.TryDecimal(raw, style, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void Column_style_is_decided_by_unambiguous_values()
    {
        Assert.Equal(NumberStyle.German, Values.DetectNumberStyle(["1.234", "12,5", "3"]));
        Assert.Equal(NumberStyle.English, Values.DetectNumberStyle(["1.234", "3.75", "8"]));
        Assert.Equal(NumberStyle.German, Values.DetectNumberStyle(["1.234", "100"])); // no evidence → German
    }

    [Theory]
    [InlineData("05.10.2026")]
    [InlineData("5.10.26")]
    [InlineData("2026-10-05")]
    [InlineData("2026-10-05 00:00:00")]
    [InlineData("05/10/2026")]
    [InlineData("46300")]
    public void Common_date_formats_are_understood(string raw)
    {
        Assert.True(Values.TryDate(raw, out var d));
        Assert.True(d.Year is 2026, $"{raw} → {d}");
    }

    [Theory]
    [InlineData("31.02.2026")]
    [InlineData("morgen")]
    [InlineData("05.10.1850")]
    public void Invalid_dates_are_rejected(string raw) => Assert.False(Values.TryDate(raw, out _));

    [Fact]
    public void Headers_are_normalised_for_matching() =>
        Assert.Equal("artikelnr intern", Values.Normalize("  Artikelnr. (intern) "));
}

public class FileReadingTests
{
    [Fact]
    public void Windows_1252_csv_with_semicolons_and_quotes_is_read()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "Artikelnr.;Bezeichnung;Menge\r\nA-1;\"Dichtung; Ø50 \"\"neu\"\"\";12,5\r\nA-2;Schütz\n24 V;3\r\n";
        var bytes = Encoding.GetEncoding(1252).GetBytes(text.Replace("Schütz\n24 V", "\"Schütz\n24 V\""));

        var file = TabularReader.Read("export.csv", bytes);

        Assert.Equal(["Artikelnr.", "Bezeichnung", "Menge"], file.Headers);
        Assert.Equal(2, file.Rows.Count);
        Assert.Equal("Dichtung; Ø50 \"neu\"", file.Rows[0][1]);
        Assert.Equal("Schütz\n24 V", file.Rows[1][1]);
        Assert.Contains("Windows-1252", file.Format);
        Assert.Contains("Semikolon", file.Format);
    }

    [Fact]
    public void Utf8_comma_csv_with_bom_and_short_rows()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Item,Qty,Note\nA-1,5\n\nA-2,7,ok\n")).ToArray();
        var file = TabularReader.Read("x.CSV", bytes);
        Assert.Equal("Item", file.Headers[0]);
        Assert.Equal(2, file.Rows.Count);
        Assert.Equal("", file.Rows[0][2]);
        Assert.Contains("UTF-8", file.Format);
    }

    [Fact]
    public void Excel_files_are_read_with_typed_cells()
    {
        using var book = new ClosedXML.Excel.XLWorkbook();
        var ws = book.AddWorksheet("Bewegungen");
        ws.Cell(1, 1).Value = "Artikel"; ws.Cell(1, 2).Value = "Datum"; ws.Cell(1, 3).Value = "Menge";
        ws.Cell(2, 1).Value = "A-1"; ws.Cell(2, 2).Value = new DateTime(2026, 10, 5); ws.Cell(2, 3).Value = 12.5;
        using var ms = new MemoryStream();
        book.SaveAs(ms);

        var file = TabularReader.Read("b.xlsx", ms.ToArray());

        Assert.Equal(["A-1", "2026-10-05", "12,5"], file.Rows[0]);
        Assert.Contains("Bewegungen", file.Format);
    }

    [Theory]
    [InlineData("alt.xls", ".xls")]
    [InlineData("bild.png", "png")]
    public void Unsupported_files_get_a_clear_message(string name, string expected)
    {
        var ex = Assert.Throws<ImportFileException>(() => TabularReader.Read(name, [1, 2, 3]));
        Assert.Contains(expected, ex.Message);
    }
}

public class ColumnMatcherTests
{
    [Fact]
    public void Typical_german_export_headers_are_mapped()
    {
        string[] headers = ["Buchungsdatum", "ArtNr", "Menge (Stk)", "Bewegungsart", "Belegnr.", "Lagerort"];
        var map = ColumnMatcher.Suggest(Schemas.StockMovements, headers).ToDictionary(m => m.FieldKey, m => m.Column);

        Assert.Equal(1, map["articleNumber"]);
        Assert.Equal(0, map["date"]);
        Assert.Equal(2, map["quantity"]);
        Assert.Equal(3, map["type"]);
        Assert.Equal(4, map["reference"]);
    }

    [Fact]
    public void Typos_are_tolerated_and_each_column_is_used_once()
    {
        string[] headers = ["Artikelnumer", "Bezeichnug", "Gruppe", "ME", "EK-Preis"];
        var map = ColumnMatcher.Suggest(Schemas.Articles, headers).ToDictionary(m => m.FieldKey, m => m.Column);

        Assert.Equal([0, 1, 2, 3, 4], new[] { map["number"], map["name"], map["group"], map["unit"], map["unitCost"] });
        Assert.Null(map["supplierNumber"]);
    }

    [Fact]
    public void Unrelated_columns_stay_unmapped()
    {
        var map = ColumnMatcher.Suggest(Schemas.Suppliers, ["Telefon", "IBAN"]);
        Assert.All(map, m => Assert.Null(m.Column));
    }
}
