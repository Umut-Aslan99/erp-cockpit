using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Erp.Api.Data;
using Erp.Core;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Samples;

public sealed record SampleInfo(string File, Dataset Dataset, string Title, string Description);

/// <summary>
/// Example exports to try the import with. They mimic real sources on purpose: an old ERP
/// (Windows-1252, semicolons, abbreviated headers), an SAP-style Excel list, a supplier portal
/// (English headers and numbers) – including a few broken rows to show the error report.
/// </summary>
public sealed class SampleFiles(IDbContextFactory<ErpDb> dbs)
{
    public static readonly SampleInfo[] All =
    [
        new("Lagerbewegungen_ERP-Export.csv", Dataset.StockMovements, "Lagerbewegungen aus dem Alt-ERP",
            "CSV mit Semikolon, Windows-1252, Kurzbezeichnungen wie „ArtNr“ – und vier fehlerhaften Zeilen."),
        new("Artikelstamm_Neuanlage.xlsx", Dataset.Articles, "Neue Artikel (Excel)",
            "Spalten wie in SAP („Material“, „Kurztext“, „BME“). Neue Werkzeug-Artikel und zwei Preisänderungen."),
        new("Bestellungen_Lieferantenportal.csv", Dataset.PurchaseOrders, "Bestellungen aus dem Lieferantenportal",
            "Englische Spaltennamen und Zahlenformate, ISO-Datum. Ein unbekannter Lieferant."),
        new("Kundenauftraege_neu.xlsx", Dataset.SalesOrders, "Neue Kundenaufträge (Excel)",
            "Offene Aufträge mit Wunschterminen – erhöhen den Auftragsrückstand."),
    ];

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<(byte[] Content, string ContentType)?> CreateAsync(string file, DateOnly today, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var articles = await db.Articles.Where(a => a.WorkspaceId == Workspace.Template).OrderBy(a => a.Number).AsNoTracking().ToListAsync(ct);
        if (articles.Count == 0) return null;
        var rnd = new Random(today.DayNumber);
        Article Pick() => articles[rnd.Next(articles.Count)];

        switch (file)
        {
            case "Lagerbewegungen_ERP-Export.csv":
            {
                var sb = new StringBuilder("Buchungsdatum;ArtNr;Bezeichnung;Menge;ME;Bewegungsart;Belegnr.\r\n");
                for (var i = 0; i < 36; i++)
                {
                    var a = Pick();
                    var issue = rnd.NextDouble() < 0.7;
                    var qty = a.Unit == "l" ? (rnd.Next(5, 60) / 2m).ToString(De) : rnd.Next(1, a.UnitCost < 5 ? 120 : 9).ToString(Inv);
                    var date = today.AddDays(-rnd.Next(0, 6));
                    sb.Append(CultureInfo.InvariantCulture, $"{date.ToString("dd.MM.yyyy", Inv)};{a.Number};{a.Name.Replace(';', ',')};{qty};{a.Unit};{(issue ? "WA" : "WE")};{(issue ? "LS" : "WE")}-{rnd.Next(10000, 99999)}\r\n");
                }
                sb.Append(CultureInfo.InvariantCulture, $"{today.ToString("dd.MM.yyyy", Inv)};A-99999;Unbekannter Artikel;4;Stk;WA;LS-77001\r\n");
                sb.Append(CultureInfo.InvariantCulture, $"31.09.{today.Year};{articles[3].Number};{articles[3].Name};2;Stk;WA;LS-77002\r\n");
                sb.Append(CultureInfo.InvariantCulture, $"{today.ToString("dd.MM.yyyy", Inv)};{articles[5].Number};{articles[5].Name};zwölf;Stk;WA;LS-77003\r\n");
                sb.Append(CultureInfo.InvariantCulture, $"{today.ToString("dd.MM.yyyy", Inv)};{articles[7].Number};{articles[7].Name};3;Stk;Umlagerung;UL-0042\r\n");
                return (Encoding.GetEncoding(1252).GetBytes(sb.ToString()), "text/csv; charset=windows-1252");
            }
            case "Artikelstamm_Neuanlage.xlsx":
            {
                string[] tools = ["Bohrer HSS Ø8", "Gewindebohrer M10", "Schaftfräser Ø12", "Wendeschneidplatte CNMG", "Drehmomentschlüssel 20–100 Nm", "Messschieber 150 mm"];
                var rows = new List<object?[]>();
                for (var i = 0; i < tools.Length; i++)
                    rows.Add([$"A-19{i + 1:000}", tools[i], "Werkzeuge", "ST", i == 4 ? "auf Anfrage" : Math.Round(4.9m + i * 17.35m, 2), "L-200"]);
                foreach (var a in articles.Where((_, i) => i % 40 == 1).Take(2))
                    rows.Add([a.Number, a.Name, a.Group, a.Unit, Math.Round(a.UnitCost * 1.08m, 2), a.SupplierNumber]);
                return (Xlsx("Artikel", ["Material", "Kurztext", "Artikelgruppe", "BME", "Standardpreis", "Kreditor"], rows), XlsxType);
            }
            case "Bestellungen_Lieferantenportal.csv":
            {
                var sb = new StringBuilder("PO Number,Line,Vendor,Item,Qty,Order Date,Confirmed Date,Received Date\n");
                var sensors = articles.Where(a => a.SupplierNumber == "L-170").ToList();
                for (var i = 0; i < 10; i++)
                {
                    var a = sensors[i % sensors.Count];
                    var ordered = today.AddDays(-rnd.Next(10, 40));
                    var promised = ordered.AddDays(14);
                    var received = i < 6 ? promised.AddDays(i % 3 == 0 ? rnd.Next(3, 12) : -1).ToString("yyyy-MM-dd", Inv) : "";
                    var qty = rnd.Next(2, 12).ToString("0.00", Inv); // portals export "10.00"
                    sb.Append(Inv, $"PO-{today:yy}{900 + i},1,L-170,{a.Number},{qty},{ordered.ToString("yyyy-MM-dd", Inv)},{promised.ToString("yyyy-MM-dd", Inv)},{received}\n");
                }
                sb.Append(Inv, $"PO-{today:yy}999,1,L-999,{articles[0].Number},5,{today.AddDays(-5).ToString("yyyy-MM-dd", Inv)},{today.AddDays(9).ToString("yyyy-MM-dd", Inv)},\n");
                return (Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8");
            }
            case "Kundenauftraege_neu.xlsx":
            {
                string[] customers = ["Aawerk Kunststofftechnik GmbH", "Dinkel Maschinenbau GmbH", "Vredener Textil AG"];
                var rows = new List<object?[]>();
                for (var o = 0; o < 4; o++)
                {
                    var ordered = today.AddDays(-o);
                    for (var l = 1; l <= 2; l++)
                    {
                        var a = Pick();
                        rows.Add([$"AU-{today:yy}{8100 + o}", l, customers[o % customers.Length], a.Number, rnd.Next(1, 10),
                            Math.Round(a.UnitCost * 1.5m, 2), ordered.ToDateTime(TimeOnly.MinValue), ordered.AddDays(o == 0 ? -1 : 10 + 7 * o).ToDateTime(TimeOnly.MinValue)]);
                    }
                }
                // One order with a due date before the order date: caught by the validation.
                rows[^1][7] = today.AddDays(-30).ToDateTime(TimeOnly.MinValue);
                return (Xlsx("Aufträge", ["Auftrag", "Pos", "Kunde", "Artikel", "Menge", "VK-Preis", "Auftragsdatum", "Wunschtermin"], rows), XlsxType);
            }
            default:
                return null;
        }
    }

    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static byte[] Xlsx(string sheetName, string[] headers, List<object?[]> rows)
    {
        using var book = new XLWorkbook();
        var ws = book.AddWorksheet(sheetName);
        for (var c = 0; c < headers.Length; c++) { ws.Cell(1, c + 1).Value = headers[c]; ws.Cell(1, c + 1).Style.Font.Bold = true; }
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                cell.Value = rows[r][c] switch
                {
                    null => Blank.Value,
                    DateTime d => d,
                    decimal m => m,
                    int i => i,
                    var o => o.ToString(),
                };
                if (rows[r][c] is DateTime) cell.Style.NumberFormat.Format = "dd.mm.yyyy";
            }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return ms.ToArray();
    }
}
