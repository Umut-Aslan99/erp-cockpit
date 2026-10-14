using System.Reflection;
using ClosedXML.Excel;
using Erp.Api.Data;
using Erp.Api.Kpis;
using Erp.Api.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Tables;

/// <summary>Query string of every table: search, view filter, sort column, paging.</summary>
public sealed record TableQuery(string? Q, string? View, string? Group, string? Supplier, string? Article, string? Sort, bool Desc = false, int Page = 1, int Size = 25);

public sealed record PageResult<T>(int Total, int Page, int Size, List<T> Rows);

/// <summary>A column for the Excel export: header text, value and number format.</summary>
public sealed record Column<T>(string Header, Func<T, object?> Value, string? Format = null);

public static class TableEndpoints
{
    private const string Money = "#,##0.00 €";
    private const string Qty = "#,##0.###";
    private const string Date = "dd.mm.yyyy";
    private const string Percent = "0.0%";

    private static readonly Column<StockRow>[] StockColumns =
    [
        new("Artikelnummer", r => r.Number), new("Bezeichnung", r => r.Name), new("Warengruppe", r => r.Group), new("Einheit", r => r.Unit),
        new("Einstandspreis", r => r.UnitCost, Money), new("Bestand", r => r.OnHand, Qty), new("Lagerwert", r => r.Value, Money),
        new("Abgang 12 Monate", r => r.Issued365, Qty), new("Umschlag", r => r.Turnover, "0.00"), new("Reichweite (Tage)", r => r.DaysOfSupply),
        new("Letzter Abgang", r => r.LastIssue, Date), new("Ladenhüter", r => r.SlowMover ? "ja" : ""),
    ];

    private static readonly Column<BacklogRow>[] BacklogColumns =
    [
        new("Auftrag", r => r.OrderNumber), new("Pos.", r => r.LineNo), new("Kunde", r => r.Customer), new("Artikelnummer", r => r.ArticleNumber),
        new("Bezeichnung", r => r.ArticleName), new("Menge", r => r.Quantity, Qty), new("Preis", r => r.UnitPrice, Money), new("Wert", r => r.Value, Money),
        new("Auftragsdatum", r => r.OrderDate, Date), new("Liefertermin", r => r.DueDate, Date), new("Tage überfällig", r => r.DaysOverdue),
    ];

    private static readonly Column<SupplierRow>[] SupplierColumns =
    [
        new("Lieferant", r => r.Number), new("Name", r => r.Name), new("Ort", r => r.City), new("Wareneingänge 12 Monate", r => r.Received),
        new("davon pünktlich", r => r.OnTime), new("Liefertreue", r => r.OnTimeRate, Percent), new("Ø Verzug (Tage, verspätete)", r => r.AvgDelayLate, "0.0"),
        new("Offene Positionen", r => r.Open), new("davon überfällig", r => r.OpenOverdue),
    ];

    private static readonly Column<PurchaseRow>[] PurchaseColumns =
    [
        new("Bestellung", r => r.OrderNumber), new("Pos.", r => r.LineNo), new("Lieferant", r => r.SupplierNumber), new("Lieferantenname", r => r.SupplierName),
        new("Artikelnummer", r => r.ArticleNumber), new("Bezeichnung", r => r.ArticleName), new("Menge", r => r.Quantity, Qty),
        new("Bestelldatum", r => r.OrderDate, Date), new("Bestätigter Termin", r => r.PromisedDate, Date), new("Wareneingang", r => r.ReceivedDate, Date),
        new("Verzug (Tage)", r => r.DelayDays), new("Status", r => r.State),
    ];

    private static readonly Column<StockMovement>[] MovementColumns =
    [
        new("Datum", r => r.Date, Date), new("Artikelnummer", r => r.ArticleNumber), new("Bewegungsart", r => r.Type),
        new("Menge", r => r.Quantity, Qty), new("Beleg", r => r.Reference),
    ];

    public static void MapTables(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tables").WithTags("Tables");

        g.MapGet("/stock", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Paged(FilterStock(await kpis.StockAsync(me.Id, ws.Today, ct), q), q, "Value"));
        g.MapGet("/stock/export", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Excel("Lagerbestand", StockColumns, Sorted(FilterStock(await kpis.StockAsync(me.Id, ws.Today, ct), q), q, "Value")));

        g.MapGet("/backlog", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Paged(FilterBacklog(await kpis.BacklogAsync(me.Id, ws.Today, ct), q), q, "DueDate"));
        g.MapGet("/backlog/export", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Excel("Auftragsrückstand", BacklogColumns, Sorted(FilterBacklog(await kpis.BacklogAsync(me.Id, ws.Today, ct), q), q, "DueDate")));

        g.MapGet("/suppliers", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Paged(Search(await kpis.SuppliersAsync(me.Id, ws.Today, ct), q.Q, r => [r.Number, r.Name, r.City]), q, "OnTimeRate"));
        g.MapGet("/suppliers/export", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Excel("Liefertreue", SupplierColumns, Sorted(Search(await kpis.SuppliersAsync(me.Id, ws.Today, ct), q.Q, r => [r.Number, r.Name, r.City]), q, "OnTimeRate")));

        g.MapGet("/purchases", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Paged(FilterPurchases(await kpis.PurchasesAsync(me.Id, ws.Today, ct), q), q, "PromisedDate"));
        g.MapGet("/purchases/export", async ([AsParameters] TableQuery q, KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            Excel("Bestellungen", PurchaseColumns, Sorted(FilterPurchases(await kpis.PurchasesAsync(me.Id, ws.Today, ct), q), q, "PromisedDate")));

        // Movements can be many: filtered, sorted and paged in the database.
        g.MapGet("/movements", async ([AsParameters] TableQuery q, ErpDb db, CurrentWorkspace me, CancellationToken ct) =>
        {
            var query = Movements(db, me.Id, q);
            var size = Math.Clamp(q.Size, 1, 200);
            var page = Math.Max(1, q.Page);
            var total = await query.CountAsync(ct);
            var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
            return new PageResult<StockMovement>(total, page, size, rows);
        });
        g.MapGet("/movements/export", async ([AsParameters] TableQuery q, ErpDb db, CurrentWorkspace me, CancellationToken ct) =>
            Excel("Lagerbewegungen", MovementColumns, await Movements(db, me.Id, q).Take(50_000).ToListAsync(ct)));
    }

    private static IQueryable<StockMovement> Movements(ErpDb db, Guid ws, TableQuery q)
    {
        var query = db.Movements.AsNoTracking().Where(m => m.WorkspaceId == ws);
        if (!string.IsNullOrWhiteSpace(q.Article)) query = query.Where(m => m.ArticleNumber == q.Article);
        if (q.View is "Zugang" or "Abgang" or "Korrektur") query = query.Where(m => m.Type == q.View);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(m => EF.Functions.ILike(m.ArticleNumber + " " + (m.Reference ?? ""), $"%{q.Q.Trim()}%"));
        return (q.Sort?.ToLowerInvariant(), q.Desc) switch
        {
            ("quantity", false) => query.OrderBy(m => m.Quantity).ThenBy(m => m.Id),
            ("quantity", true) => query.OrderByDescending(m => m.Quantity).ThenBy(m => m.Id),
            ("articlenumber", false) => query.OrderBy(m => m.ArticleNumber).ThenByDescending(m => m.Date),
            ("articlenumber", true) => query.OrderByDescending(m => m.ArticleNumber).ThenByDescending(m => m.Date),
            ("date", false) => query.OrderBy(m => m.Date).ThenBy(m => m.Id),
            _ => query.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id),
        };
    }

    private static IEnumerable<StockRow> FilterStock(IEnumerable<StockRow> rows, TableQuery q)
    {
        if (q.View == "slow") rows = rows.Where(r => r.SlowMover);
        if (q.View == "stock") rows = rows.Where(r => r.OnHand > 0);
        if (!string.IsNullOrWhiteSpace(q.Group)) rows = rows.Where(r => r.Group == q.Group);
        if (!string.IsNullOrWhiteSpace(q.Supplier)) rows = rows.Where(r => string.Equals(r.SupplierNumber, q.Supplier, StringComparison.OrdinalIgnoreCase));
        return Search(rows, q.Q, r => [r.Number, r.Name, r.Group]);
    }

    private static IEnumerable<BacklogRow> FilterBacklog(IEnumerable<BacklogRow> rows, TableQuery q)
    {
        if (q.View == "overdue") rows = rows.Where(r => r.DaysOverdue > 0);
        return Search(rows, q.Q, r => [r.OrderNumber, r.Customer, r.ArticleNumber, r.ArticleName]);
    }

    private static IEnumerable<PurchaseRow> FilterPurchases(IEnumerable<PurchaseRow> rows, TableQuery q)
    {
        rows = q.View switch
        {
            "open" => rows.Where(r => r.ReceivedDate is null),
            "overdue" => rows.Where(r => r.State == "überfällig"),
            "late" => rows.Where(r => r.State == "verspätet"),
            _ => rows,
        };
        if (!string.IsNullOrWhiteSpace(q.Supplier)) rows = rows.Where(r => r.SupplierNumber.Equals(q.Supplier, StringComparison.OrdinalIgnoreCase));
        return Search(rows, q.Q, r => [r.OrderNumber, r.SupplierName, r.ArticleNumber, r.ArticleName]);
    }

    private static IEnumerable<T> Search<T>(IEnumerable<T> rows, string? q, Func<T, string?[]> fields) =>
        string.IsNullOrWhiteSpace(q) ? rows : rows.Where(r => fields(r).Any(f => f?.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase) == true));

    /// <summary>Sorts by the record property the client names (camelCase), nulls last.</summary>
    private static List<T> Sorted<T>(IEnumerable<T> rows, TableQuery q, string defaultSort)
    {
        var prop = typeof(T).GetProperty(q.Sort ?? defaultSort, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? typeof(T).GetProperty(defaultSort)!;
        var desc = q.Sort is null ? defaultSort is "Value" : q.Desc;
        var withValue = rows.Where(r => prop.GetValue(r) is not null);
        var ordered = desc ? withValue.OrderByDescending(r => prop.GetValue(r)) : withValue.OrderBy(r => prop.GetValue(r));
        return [.. ordered, .. rows.Where(r => prop.GetValue(r) is null)];
    }

    private static PageResult<T> Paged<T>(IEnumerable<T> rows, TableQuery q, string defaultSort)
    {
        var all = Sorted(rows, q, defaultSort);
        var size = Math.Clamp(q.Size, 1, 200);
        var page = Math.Max(1, q.Page);
        return new PageResult<T>(all.Count, page, size, all.Skip((page - 1) * size).Take(size).ToList());
    }

    private static IResult Excel<T>(string title, Column<T>[] columns, IReadOnlyCollection<T> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(title.Length > 31 ? title[..31] : title);
        for (var c = 0; c < columns.Length; c++)
        {
            var header = sheet.Cell(1, c + 1);
            header.Value = columns[c].Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8834A");
            header.Style.Font.FontColor = XLColor.White;
        }
        var row = 2;
        foreach (var item in rows)
        {
            for (var c = 0; c < columns.Length; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                cell.Value = columns[c].Value(item) switch
                {
                    null => Blank.Value,
                    DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                    decimal m => m,
                    int i => i,
                    var other => other.ToString(),
                };
                if (columns[c].Format is { } f) cell.Style.NumberFormat.Format = f;
            }
            row++;
        }
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, row - 1), columns.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents(1, Math.Min(row, 200));
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        var fileName = $"{title}_{DateTime.Now:yyyy-MM-dd}.xlsx";
        return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
