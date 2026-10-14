using Erp.Api.Data;
using Erp.Api.Kpis;

namespace Erp.Tests;

public class DemoDataTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly DemoData.DemoSet Data = DemoData.Generate(Today);

    [Fact]
    public void Generation_is_deterministic()
    {
        var again = DemoData.Generate(Today);
        Assert.Equal(Data.Movements.Count, again.Movements.Count);
        Assert.Equal(Data.Movements.Sum(m => m.Quantity), again.Movements.Sum(m => m.Quantity));
    }

    [Fact]
    public void Stock_never_goes_negative_and_nothing_lies_in_the_future()
    {
        foreach (var article in Data.Movements.GroupBy(m => m.ArticleNumber))
        {
            decimal stock = 0;
            foreach (var m in article.OrderBy(m => m.Date).ThenByDescending(m => m.Quantity))
            {
                stock += m.Quantity;
                Assert.True(stock >= 0, $"{article.Key} negative on {m.Date}");
            }
        }
        Assert.All(Data.Movements, m => Assert.True(m.Date <= Today));
        Assert.All(Data.PurchaseOrders, p => Assert.True(p.ReceivedDate is null || p.ReceivedDate <= Today));
    }

    [Fact]
    public void Data_contains_the_stories_the_dashboard_tells()
    {
        Assert.Equal(120, Data.Articles.Count);
        // An open, partly overdue backlog.
        Assert.Contains(Data.SalesOrders, s => s.ShippedDate is null && s.DueDate < Today);
        Assert.Contains(Data.SalesOrders, s => s.ShippedDate is null && s.DueDate >= Today);
        // The sensor supplier is the least punctual one.
        double Rate(string s)
        {
            var received = Data.PurchaseOrders.Where(p => p.SupplierNumber == s && p.ReceivedDate is not null).ToList();
            return received.Count(p => p.ReceivedDate <= p.PromisedDate) / (double)received.Count;
        }
        var worst = Data.Suppliers.Select(s => s.Number).Where(s => Data.PurchaseOrders.Any(p => p.SupplierNumber == s && p.ReceivedDate is not null)).MinBy(Rate);
        Assert.Equal("L-170", worst);
    }

    [Fact]
    public void Backlog_buckets_split_overdue_weeks_and_later()
    {
        var today = new DateOnly(2026, 10, 14); // Wednesday, KW 42
        BacklogRow Row(int dueInDays, decimal value) => new("A", 1, "K", "A-1", "x", 1, value, value, today.AddDays(-30), today.AddDays(dueInDays), Math.Max(0, -dueInDays));
        var buckets = KpiService.BacklogByWeek([Row(-3, 10), Row(0, 20), Row(6, 30), Row(70, 40)], today);

        Assert.Equal(10, buckets[0].Value);
        Assert.True(buckets[0].Overdue);
        Assert.Equal(("KW 42", 20m), (buckets[1].Bucket, buckets[1].Value));
        Assert.Equal(("KW 43", 30m), (buckets[2].Bucket, buckets[2].Value));
        Assert.Equal(("Später", 40m), (buckets[^1].Bucket, buckets[^1].Value));
    }
}
