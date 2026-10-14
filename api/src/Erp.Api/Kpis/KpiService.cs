using System.Globalization;
using Erp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Kpis;

public sealed record StockRow(
    string Number, string Name, string Group, string Unit, decimal UnitCost, string? SupplierNumber,
    decimal OnHand, decimal Value, decimal Issued365, decimal? Turnover, int? DaysOfSupply,
    DateOnly? LastIssue, DateOnly? LastMovement, bool SlowMover);

public sealed record BacklogRow(
    string OrderNumber, int LineNo, string Customer, string ArticleNumber, string ArticleName,
    decimal Quantity, decimal UnitPrice, decimal Value, DateOnly OrderDate, DateOnly DueDate, int DaysOverdue);

public sealed record SupplierRow(
    string Number, string Name, string? City, int Received, int OnTime, decimal? OnTimeRate,
    decimal? AvgDelayLate, int Open, int OpenOverdue);

public sealed record PurchaseRow(
    string OrderNumber, int LineNo, string SupplierNumber, string SupplierName, string ArticleNumber, string ArticleName,
    decimal Quantity, DateOnly OrderDate, DateOnly PromisedDate, DateOnly? ReceivedDate, int? DelayDays, string State);

public sealed record MonthPoint(string Month, decimal Issues, decimal Receipts);
public sealed record GroupPoint(string Group, decimal StockValue, decimal Cogs365, decimal? Turnover, int SlowMovers);
public sealed record WeekPoint(string Bucket, decimal Value, int Lines, bool Overdue);

public sealed record Overview(
    DateOnly Today,
    decimal StockValue, decimal Cogs365, decimal? Turnover,
    int SlowMoverCount, decimal SlowMoverValue,
    decimal BacklogValue, int BacklogLines, decimal OverdueValue, int OverdueLines,
    decimal? OnTimeRate, int OpenPurchaseLines, int OverduePurchaseLines,
    List<MonthPoint> Trend, List<GroupPoint> Groups, List<WeekPoint> BacklogByWeek, List<SupplierRow> Suppliers);

/// <summary>
/// Key figures as a controlling department would define them:
/// stock turnover = cost of goods issued (12 months) ÷ current stock value;
/// slow mover = stock on hand but no issue for 180 days;
/// on-time rate = received order lines delivered on or before the confirmed date (12 months).
/// </summary>
public sealed class KpiService(IDbContextFactory<ErpDb> dbs)
{
    public const int SlowMoverDays = 180;
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public async Task<List<StockRow>> StockAsync(Guid ws, DateOnly today, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var from365 = today.AddDays(-365);
        var from90 = today.AddDays(-90);
        var stats = await db.Movements.Where(m => m.WorkspaceId == ws)
            .GroupBy(m => m.ArticleNumber)
            .Select(g => new
            {
                Article = g.Key,
                OnHand = g.Sum(m => m.Quantity),
                Issued365 = g.Where(m => m.Type == "Abgang" && m.Date >= from365).Sum(m => -m.Quantity),
                Issued90 = g.Where(m => m.Type == "Abgang" && m.Date >= from90).Sum(m => -m.Quantity),
                LastIssue = g.Where(m => m.Type == "Abgang").Max(m => (DateOnly?)m.Date),
                LastMovement = g.Max(m => (DateOnly?)m.Date),
            })
            .ToDictionaryAsync(x => x.Article, ct);
        var articles = await db.Articles.Where(a => a.WorkspaceId == ws).AsNoTracking().ToListAsync(ct);

        return articles.Select(a =>
        {
            stats.TryGetValue(a.Number, out var s);
            var onHand = s?.OnHand ?? 0;
            var value = Math.Round(Math.Max(onHand, 0) * a.UnitCost, 2);
            var issued365 = s?.Issued365 ?? 0;
            var daily = (s?.Issued90 ?? 0) / 90m;
            var slow = onHand > 0 && (s?.LastIssue is null || s.LastIssue < today.AddDays(-SlowMoverDays));
            return new StockRow(a.Number, a.Name, a.Group, a.Unit, a.UnitCost, a.SupplierNumber, onHand, value, issued365,
                value > 0 ? Math.Round(issued365 * a.UnitCost / value, 2) : null,
                daily > 0 ? (int)Math.Round(Math.Max(onHand, 0) / daily) : null,
                s?.LastIssue, s?.LastMovement, slow);
        }).ToList();
    }

    public async Task<List<BacklogRow>> BacklogAsync(Guid ws, DateOnly today, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var names = await db.Articles.Where(a => a.WorkspaceId == ws).ToDictionaryAsync(a => a.Number, a => a.Name, ct);
        var open = await db.SalesOrders.Where(l => l.WorkspaceId == ws && l.ShippedDate == null).AsNoTracking().ToListAsync(ct);
        return open.Select(l => new BacklogRow(l.OrderNumber, l.LineNo, l.Customer, l.ArticleNumber, names.GetValueOrDefault(l.ArticleNumber, "–"),
            l.Quantity, l.UnitPrice, Math.Round(l.Quantity * l.UnitPrice, 2), l.OrderDate, l.DueDate,
            Math.Max(0, today.DayNumber - l.DueDate.DayNumber))).ToList();
    }

    public async Task<List<PurchaseRow>> PurchasesAsync(Guid ws, DateOnly today, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var names = await db.Articles.Where(a => a.WorkspaceId == ws).ToDictionaryAsync(a => a.Number, a => a.Name, ct);
        var suppliers = await db.Suppliers.Where(s => s.WorkspaceId == ws).ToDictionaryAsync(s => s.Number, s => s.Name, ct);
        var lines = await db.PurchaseOrders.Where(l => l.WorkspaceId == ws).AsNoTracking().ToListAsync(ct);
        return lines.Select(l =>
        {
            int? delay = l.ReceivedDate is { } r ? r.DayNumber - l.PromisedDate.DayNumber : l.PromisedDate < today ? today.DayNumber - l.PromisedDate.DayNumber : null;
            var state = l.ReceivedDate is null ? (l.PromisedDate < today ? "überfällig" : "offen") : delay > 0 ? "verspätet" : "pünktlich";
            return new PurchaseRow(l.OrderNumber, l.LineNo, l.SupplierNumber, suppliers.GetValueOrDefault(l.SupplierNumber, "–"),
                l.ArticleNumber, names.GetValueOrDefault(l.ArticleNumber, "–"), l.Quantity, l.OrderDate, l.PromisedDate, l.ReceivedDate, delay, state);
        }).ToList();
    }

    public async Task<List<SupplierRow>> SuppliersAsync(Guid ws, DateOnly today, CancellationToken ct)
    {
        var purchases = await PurchasesAsync(ws, today, ct);
        await using var db = await dbs.CreateDbContextAsync(ct);
        var suppliers = await db.Suppliers.Where(s => s.WorkspaceId == ws).AsNoTracking().ToListAsync(ct);
        var from365 = today.AddDays(-365);
        return suppliers.Select(s =>
        {
            var mine = purchases.Where(p => p.SupplierNumber.Equals(s.Number, StringComparison.OrdinalIgnoreCase)).ToList();
            var received = mine.Where(p => p.ReceivedDate >= from365).ToList();
            var onTime = received.Count(p => p.DelayDays <= 0);
            var late = received.Where(p => p.DelayDays > 0).ToList();
            return new SupplierRow(s.Number, s.Name, s.City, received.Count, onTime,
                received.Count > 0 ? Math.Round((decimal)onTime / received.Count, 3) : null,
                late.Count > 0 ? Math.Round((decimal)late.Average(p => p.DelayDays!.Value), 1) : null,
                mine.Count(p => p.ReceivedDate is null), mine.Count(p => p.State == "überfällig"));
        }).OrderBy(s => s.OnTimeRate ?? 2).ToList();
    }

    public async Task<Overview> OverviewAsync(Guid ws, DateOnly today, CancellationToken ct)
    {
        var stock = await StockAsync(ws, today, ct);
        var backlog = await BacklogAsync(ws, today, ct);
        var suppliers = await SuppliersAsync(ws, today, ct);

        var stockValue = stock.Sum(s => s.Value);
        var cogs = stock.Sum(s => s.Issued365 * s.UnitCost);
        var slow = stock.Where(s => s.SlowMover).ToList();
        var overdue = backlog.Where(b => b.DaysOverdue > 0).ToList();
        var received = suppliers.Sum(s => s.Received);

        var groups = stock.GroupBy(s => s.Group).Select(g =>
        {
            var value = g.Sum(s => s.Value);
            var c = g.Sum(s => s.Issued365 * s.UnitCost);
            return new GroupPoint(g.Key, value, Math.Round(c, 2), value > 0 ? Math.Round(c / value, 2) : null, g.Count(s => s.SlowMover));
        }).OrderByDescending(g => g.Turnover ?? 0).ToList();

        return new Overview(today,
            stockValue, Math.Round(cogs, 2), stockValue > 0 ? Math.Round(cogs / stockValue, 2) : null,
            slow.Count, slow.Sum(s => s.Value),
            backlog.Sum(b => b.Value), backlog.Count, overdue.Sum(b => b.Value), overdue.Count,
            received > 0 ? Math.Round((decimal)suppliers.Sum(s => s.OnTime) / received, 3) : null,
            suppliers.Sum(s => s.Open), suppliers.Sum(s => s.OpenOverdue),
            await TrendAsync(ws, today, ct), groups, BacklogByWeek(backlog, today), suppliers);
    }

    /// <summary>Monthly value of issues and receipts at cost, last 12 months (current month included).</summary>
    private async Task<List<MonthPoint>> TrendAsync(Guid ws, DateOnly today, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var rows = await (
            from m in db.Movements
            join a in db.Articles on new { m.WorkspaceId, Number = m.ArticleNumber } equals new { a.WorkspaceId, a.Number }
            where m.WorkspaceId == ws && m.Date >= first && m.Type != "Korrektur"
            group new { m.Quantity, m.Type, a.UnitCost } by new { m.Date.Year, m.Date.Month } into g
            select new
            {
                g.Key.Year, g.Key.Month,
                Issues = g.Where(x => x.Type == "Abgang").Sum(x => -x.Quantity * x.UnitCost),
                Receipts = g.Where(x => x.Type == "Zugang").Sum(x => x.Quantity * x.UnitCost),
            }).ToListAsync(ct);

        return Enumerable.Range(0, 12).Select(i =>
        {
            var month = first.AddMonths(i);
            var r = rows.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
            return new MonthPoint(month.ToString("MMM yy", De), Math.Round(r?.Issues ?? 0, 2), Math.Round(r?.Receipts ?? 0, 2));
        }).ToList();
    }

    /// <summary>Open order value by due week: one "overdue" bucket, the next eight calendar weeks, then "later".</summary>
    internal static List<WeekPoint> BacklogByWeek(List<BacklogRow> backlog, DateOnly today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var buckets = new List<WeekPoint>
        {
            new("Überfällig", backlog.Where(b => b.DueDate < today).Sum(b => b.Value), backlog.Count(b => b.DueDate < today), true),
        };
        for (var w = 0; w < 8; w++)
        {
            var from = monday.AddDays(7 * w);
            var to = from.AddDays(7);
            var lines = backlog.Where(b => b.DueDate >= today && b.DueDate >= from && b.DueDate < to).ToList();
            buckets.Add(new($"KW {ISOWeek.GetWeekOfYear(from.ToDateTime(TimeOnly.MinValue))}", lines.Sum(b => b.Value), lines.Count, false));
        }
        var later = backlog.Where(b => b.DueDate >= monday.AddDays(56)).ToList();
        buckets.Add(new("Später", later.Sum(b => b.Value), later.Count, false));
        return buckets;
    }
}
