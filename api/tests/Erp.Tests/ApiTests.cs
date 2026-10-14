using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace Erp.Tests;

/// <summary>Visitor journeys through the real API and database.</summary>
public class ApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonNode> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync(Ct);
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonNode.Parse(body)!;
    }

    private static async Task<JsonNode> Analyze(HttpClient c, string sample, string dataset)
    {
        var file = await c.GetByteArrayAsync($"/api/samples/{sample}", Ct);
        using var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", sample }, { new StringContent(dataset), "dataset" } };
        return await Json(await c.PostAsync("/api/imports/analyze", form, Ct));
    }

    private static object Request(JsonNode analyzed, string dataset) => new
    {
        uploadId = (Guid)analyzed["uploadId"]!,
        dataset,
        mapping = analyzed["suggestions"]!.AsArray().Where(s => s!["column"] is not null)
            .ToDictionary(s => (string)s!["fieldKey"]!, s => (int)s!["column"]!),
    };

    [Fact]
    public async Task A_new_visitor_gets_a_private_workspace_with_meaningful_figures()
    {
        var c = api.CreateClient();
        var res = await c.GetAsync("/api/kpis", Ct);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), v => v.StartsWith("erp_ws=", StringComparison.Ordinal) && v.Contains("httponly") && v.Contains("samesite=strict"));

        var k = await Json(res);
        Assert.True((decimal)k["stockValue"]! > 10_000);
        Assert.InRange((decimal)k["turnover"]!, 1m, 30m);
        Assert.True((int)k["slowMoverCount"]! > 3);
        Assert.True((int)k["overdueLines"]! > 0);
        Assert.InRange((decimal)k["onTimeRate"]!, 0.7m, 0.99m);
        Assert.Equal(12, k["trend"]!.AsArray().Count);
        Assert.Equal("L-170", (string)k["suppliers"]![0]!["number"]!);
    }

    [Fact]
    public async Task Movement_export_from_old_erp_imports_with_error_report_and_rolls_back()
    {
        var c = api.CreateClient();
        var before = await Json(await c.GetAsync("/api/tables/movements?size=1", Ct));

        var analyzed = await Analyze(c, "Lagerbewegungen_ERP-Export.csv", "StockMovements");
        Assert.Contains("Windows-1252", (string)analyzed["format"]!);
        Assert.Contains("Semikolon", (string)analyzed["format"]!);
        var map = analyzed["suggestions"]!.AsArray().ToDictionary(s => (string)s!["fieldKey"]!, s => (int?)s!["column"]);
        Assert.Equal((1, 0, 3, 5, 6), (map["articleNumber"], map["date"], map["quantity"], map["type"], map["reference"]));

        var check = await Json(await c.PostAsJsonAsync("/api/imports/validate", Request(analyzed, "StockMovements"), Ct));
        Assert.Equal(40, (int)check["totalRows"]!);
        Assert.Equal(4, (int)check["invalidRows"]!);
        var messages = check["errors"]!.AsArray().Select(e => (string)e!["message"]!).ToList();
        Assert.Contains(messages, m => m.Contains("unbekannt"));
        Assert.Contains(messages, m => m.Contains("Datum"));
        Assert.Contains(messages, m => m.Contains("Zahl"));
        Assert.Contains(messages, m => m.Contains("Unbekannter Wert"));

        var commit = await Json(await c.PostAsJsonAsync("/api/imports/commit", Request(analyzed, "StockMovements"), Ct));
        Assert.Equal(36, (int)commit["inserted"]!);
        var after = await Json(await c.GetAsync("/api/tables/movements?size=1", Ct));
        Assert.Equal((int)before["total"]! + 36, (int)after["total"]!);

        var history = await Json(await c.GetAsync("/api/imports", Ct));
        Assert.True((bool)history[0]!["canRollback"]!);
        var rollback = await c.PostAsync($"/api/imports/{history[0]!["id"]}/rollback", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, rollback.StatusCode);
        var restored = await Json(await c.GetAsync("/api/tables/movements?size=1", Ct));
        Assert.Equal((int)before["total"]!, (int)restored["total"]!);
    }

    [Fact]
    public async Task Article_excel_upserts_and_rollback_restores_previous_prices()
    {
        var c = api.CreateClient();
        var stockBefore = await Json(await c.GetAsync("/api/tables/stock?size=200", Ct));
        decimal Cost(JsonNode page, string number) => (decimal)page["rows"]!.AsArray().First(r => (string)r!["number"]! == number)!["unitCost"]!;

        var analyzed = await Analyze(c, "Artikelstamm_Neuanlage.xlsx", "Articles");
        Assert.Contains("Excel", (string)analyzed["format"]!);
        var check = await Json(await c.PostAsJsonAsync("/api/imports/validate", Request(analyzed, "Articles"), Ct));
        Assert.Equal(1, (int)check["invalidRows"]!); // "auf Anfrage" is no price
        var commit = await Json(await c.PostAsJsonAsync("/api/imports/commit", Request(analyzed, "Articles"), Ct));
        Assert.Equal((5, 2), ((int)commit["inserted"]!, (int)commit["updated"]!));

        var updated = analyzed["sample"]!.AsArray().Select(r => (string)r![0]!).First(n => !n.StartsWith("A-19", StringComparison.Ordinal));
        var stockAfter = await Json(await c.GetAsync("/api/tables/stock?size=200", Ct));
        Assert.True(Cost(stockAfter, updated) > Cost(stockBefore, updated));
        Assert.Contains(stockAfter["rows"]!.AsArray(), r => (string)r!["group"]! == "Werkzeuge");

        var history = await Json(await c.GetAsync("/api/imports", Ct));
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/imports/{history[0]!["id"]}/rollback", null, Ct)).StatusCode);
        var stockRestored = await Json(await c.GetAsync("/api/tables/stock?size=200", Ct));
        Assert.Equal(Cost(stockBefore, updated), Cost(stockRestored, updated));
        Assert.DoesNotContain(stockRestored["rows"]!.AsArray(), r => (string)r!["group"]! == "Werkzeuge");
    }

    [Fact]
    public async Task Only_the_latest_import_can_be_rolled_back()
    {
        var c = api.CreateClient();
        foreach (var (sample, ds) in new[] { ("Kundenauftraege_neu.xlsx", "SalesOrders"), ("Bestellungen_Lieferantenportal.csv", "PurchaseOrders") })
        {
            var a = await Analyze(c, sample, ds);
            await Json(await c.PostAsJsonAsync("/api/imports/commit", Request(a, ds), Ct));
        }
        var history = await Json(await c.GetAsync("/api/imports", Ct));
        var older = history[1]!;
        Assert.False((bool)older["canRollback"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/imports/{older["id"]}/rollback", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Supplier_portal_csv_is_read_with_english_numbers_and_rejects_unknown_suppliers()
    {
        var c = api.CreateClient();
        var analyzed = await Analyze(c, "Bestellungen_Lieferantenportal.csv", "PurchaseOrders");
        var check = await Json(await c.PostAsJsonAsync("/api/imports/validate", Request(analyzed, "PurchaseOrders"), Ct));

        Assert.Contains("englisch", (string)check["numberStyles"]!["Menge"]!);
        Assert.Equal(1, (int)check["invalidRows"]!);
        Assert.Contains("Lieferant", (string)check["errors"]![0]!["message"]!);
    }

    [Fact]
    public async Task Workspaces_are_isolated_and_reset_restores_the_demo()
    {
        var alice = api.CreateClient();
        var bob = api.CreateClient();
        var start = (int)(await Json(await bob.GetAsync("/api/tables/movements?size=1", Ct)))["total"]!;

        var a = await Analyze(alice, "Lagerbewegungen_ERP-Export.csv", "StockMovements");
        await Json(await alice.PostAsJsonAsync("/api/imports/commit", Request(a, "StockMovements"), Ct));

        Assert.Equal(start + 36, (int)(await Json(await alice.GetAsync("/api/tables/movements?size=1", Ct)))["total"]!);
        Assert.Equal(start, (int)(await Json(await bob.GetAsync("/api/tables/movements?size=1", Ct)))["total"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync("/api/workspace/reset", null, Ct)).StatusCode);
        Assert.Equal(start, (int)(await Json(await alice.GetAsync("/api/tables/movements?size=1", Ct)))["total"]!);
        Assert.Empty((await Json(await alice.GetAsync("/api/imports", Ct))).AsArray());
    }

    [Theory]
    [InlineData("stock?view=slow", "Ladenhüter")]
    [InlineData("backlog?view=overdue", "Tage überfällig")]
    [InlineData("suppliers", "Liefertreue")]
    [InlineData("purchases?view=overdue", "Status")]
    public async Task Tables_export_to_real_excel_files(string table, string expectedHeader)
    {
        var c = api.CreateClient();
        var path = table.Contains('?') ? table.Replace("?", "/export?") : $"{table}/export";
        var res = await c.GetAsync($"/api/tables/{path}", Ct);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", res.Content.Headers.ContentType?.MediaType);

        using var book = new XLWorkbook(await res.Content.ReadAsStreamAsync(Ct));
        var sheet = book.Worksheet(1);
        Assert.Contains(sheet.Row(1).CellsUsed(), cell => cell.GetString() == expectedHeader);
        Assert.True(sheet.LastRowUsed()!.RowNumber() > 1);
    }

    [Fact]
    public async Task Tables_filter_sort_and_page_on_the_server()
    {
        var c = api.CreateClient();
        var page = await Json(await c.GetAsync("/api/tables/stock?view=slow&sort=value&desc=true&size=5", Ct));
        var rows = page["rows"]!.AsArray();
        Assert.All(rows, r => Assert.True((bool)r!["slowMover"]!));
        var values = rows.Select(r => (decimal)r!["value"]!).ToList();
        Assert.Equal(values.OrderByDescending(v => v), values);
        Assert.True((int)page["total"]! >= rows.Count);
    }

    [Theory]
    [InlineData("alt.xls", "xls")]
    [InlineData("leer.csv", "leer")]
    public async Task Unsupported_or_empty_files_get_a_clear_message(string name, string expected)
    {
        var c = api.CreateClient();
        var content = name.EndsWith(".xls", StringComparison.Ordinal) ? new byte[] { 1, 2, 3 } : [];
        using var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", name }, { new StringContent("Articles"), "dataset" } };
        var res = await c.PostAsync("/api/imports/analyze", form, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        Assert.Contains(expected, await res.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var res = await api.CreateClient().GetAsync("/api/health", Ct);
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
