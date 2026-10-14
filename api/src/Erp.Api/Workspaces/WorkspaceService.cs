using Erp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Workspaces;

/// <summary>
/// Every visitor gets a private copy of the demo data, so imports and rollbacks never affect
/// anyone else. Copies are made inside PostgreSQL (INSERT … SELECT) from a template workspace,
/// which is regenerated each day so the data always ends "today".
/// </summary>
public sealed class WorkspaceService(IDbContextFactory<ErpDb> dbs, TimeProvider clock, ILogger<WorkspaceService> log)
{
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(24);
    public const int MaxWorkspaces = 100;
    private static readonly SemaphoreSlim TemplateLock = new(1, 1);

    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().Date);

    public async Task EnsureTemplateAsync(CancellationToken ct = default)
    {
        await TemplateLock.WaitAsync(ct);
        try
        {
            await using var db = await dbs.CreateDbContextAsync(ct);
            var template = await db.Workspaces.FindAsync([Workspace.Template], ct);
            if (template?.SeedDate == Today) return;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (template is not null) await db.Workspaces.Where(w => w.Id == Workspace.Template).ExecuteDeleteAsync(ct);
            db.ChangeTracker.Clear();
            db.Workspaces.Add(new Workspace { Id = Workspace.Template, CreatedAt = clock.GetUtcNow(), LastSeenAt = clock.GetUtcNow(), SeedDate = Today });
            await db.SaveChangesAsync(ct);

            var set = DemoData.Generate(Today);
            db.Articles.AddRange(set.Articles);
            db.Suppliers.AddRange(set.Suppliers);
            db.Movements.AddRange(set.Movements);
            db.SalesOrders.AddRange(set.SalesOrders);
            db.PurchaseOrders.AddRange(set.PurchaseOrders);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            log.LogInformation("Template seeded for {Day}: {Articles} articles, {Moves} movements, {SO} sales lines, {PO} purchase lines",
                Today, set.Articles.Count, set.Movements.Count, set.SalesOrders.Count, set.PurchaseOrders.Count);
        }
        finally { TemplateLock.Release(); }
    }

    /// <summary>Returns the workspace if it exists (and marks it as used), otherwise null.</summary>
    public async Task<Workspace?> FindAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Workspace.Template) return null;
        await using var db = await dbs.CreateDbContextAsync(ct);
        var ws = await db.Workspaces.FindAsync([id], ct);
        if (ws is not null && clock.GetUtcNow() - ws.LastSeenAt > TimeSpan.FromMinutes(5))
        {
            ws.LastSeenAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        return ws;
    }

    public async Task<Workspace> CreateAsync(CancellationToken ct = default)
    {
        await EnsureTemplateAsync(ct);
        await using var db = await dbs.CreateDbContextAsync(ct);
        var ws = new Workspace { Id = Guid.NewGuid(), CreatedAt = clock.GetUtcNow(), LastSeenAt = clock.GetUtcNow(), SeedDate = Today };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Workspaces.Add(ws);
        await db.SaveChangesAsync(ct);
        await CopyTemplateAsync(db, ws.Id, ct);
        await tx.CommitAsync(ct);
        return ws;
    }

    /// <summary>Back to the original demo data (same workspace id, so the cookie stays valid).</summary>
    public async Task ResetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureTemplateAsync(ct);
        await using var db = await dbs.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.ImportBatches.Where(x => x.WorkspaceId == id).ExecuteDeleteAsync(ct);
        await db.Movements.Where(x => x.WorkspaceId == id).ExecuteDeleteAsync(ct);
        await db.SalesOrders.Where(x => x.WorkspaceId == id).ExecuteDeleteAsync(ct);
        await db.PurchaseOrders.Where(x => x.WorkspaceId == id).ExecuteDeleteAsync(ct);
        await db.Articles.Where(x => x.WorkspaceId == id).ExecuteDeleteAsync(ct);
        await db.Suppliers.Where(x => x.WorkspaceId == id).ExecuteDeleteAsync(ct);
        await CopyTemplateAsync(db, id, ct);
        await db.Workspaces.Where(w => w.Id == id).ExecuteUpdateAsync(s => s.SetProperty(w => w.SeedDate, Today), ct);
        await tx.CommitAsync(ct);
    }

    private static async Task CopyTemplateAsync(ErpDb db, Guid target, CancellationToken ct)
    {
        string[] statements =
        [
            """INSERT INTO "Suppliers" ("WorkspaceId","Number","Name","City","Country") SELECT {0},"Number","Name","City","Country" FROM "Suppliers" WHERE "WorkspaceId" = {1}""",
            """INSERT INTO "Articles" ("WorkspaceId","Number","Name","Group","Unit","UnitCost","SupplierNumber") SELECT {0},"Number","Name","Group","Unit","UnitCost","SupplierNumber" FROM "Articles" WHERE "WorkspaceId" = {1}""",
            """INSERT INTO "Movements" ("WorkspaceId","ArticleNumber","Date","Quantity","Type","Reference") SELECT {0},"ArticleNumber","Date","Quantity","Type","Reference" FROM "Movements" WHERE "WorkspaceId" = {1}""",
            """INSERT INTO "SalesOrders" ("WorkspaceId","OrderNumber","LineNo","Customer","ArticleNumber","Quantity","UnitPrice","OrderDate","DueDate","ShippedDate") SELECT {0},"OrderNumber","LineNo","Customer","ArticleNumber","Quantity","UnitPrice","OrderDate","DueDate","ShippedDate" FROM "SalesOrders" WHERE "WorkspaceId" = {1}""",
            """INSERT INTO "PurchaseOrders" ("WorkspaceId","OrderNumber","LineNo","SupplierNumber","ArticleNumber","Quantity","OrderDate","PromisedDate","ReceivedDate") SELECT {0},"OrderNumber","LineNo","SupplierNumber","ArticleNumber","Quantity","OrderDate","PromisedDate","ReceivedDate" FROM "PurchaseOrders" WHERE "WorkspaceId" = {1}""",
        ];
        foreach (var sql in statements)
#pragma warning disable EF1002 // table/column names are constants; values are passed as parameters
            await db.Database.ExecuteSqlRawAsync(sql, [target, Workspace.Template], ct);
#pragma warning restore EF1002
    }

    /// <summary>Removes idle workspaces and keeps the total bounded.</summary>
    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var cutoff = clock.GetUtcNow() - IdleLifetime;
        var removed = await db.Workspaces.Where(w => w.Id != Workspace.Template && w.LastSeenAt < cutoff).ExecuteDeleteAsync(ct);
        var surplus = await db.Workspaces.CountAsync(w => w.Id != Workspace.Template, ct) - MaxWorkspaces;
        if (surplus > 0)
        {
            var oldest = await db.Workspaces.Where(w => w.Id != Workspace.Template).OrderBy(w => w.LastSeenAt).Take(surplus).Select(w => w.Id).ToListAsync(ct);
            removed += await db.Workspaces.Where(w => oldest.Contains(w.Id)).ExecuteDeleteAsync(ct);
        }
        if (removed > 0) log.LogInformation("Removed {Count} idle workspaces", removed);
        return removed;
    }
}

/// <summary>The workspace of the current request, resolved by <see cref="WorkspaceMiddleware"/>.</summary>
public sealed class CurrentWorkspace
{
    public Guid Id { get; set; }
    public bool Created { get; set; }
}

/// <summary>Finds (or creates) the visitor's workspace from an HttpOnly cookie for every API call.</summary>
public sealed class WorkspaceMiddleware(RequestDelegate next)
{
    public const string Cookie = "erp_ws";

    public async Task InvokeAsync(HttpContext http, WorkspaceService workspaces, CurrentWorkspace current)
    {
        if (!http.Request.Path.StartsWithSegments("/api") || http.Request.Path.StartsWithSegments("/api/health"))
        {
            await next(http);
            return;
        }

        Workspace? ws = null;
        if (Guid.TryParse(http.Request.Cookies[Cookie], out var id)) ws = await workspaces.FindAsync(id, http.RequestAborted);
        if (ws is null)
        {
            ws = await workspaces.CreateAsync(http.RequestAborted);
            current.Created = true;
            http.Response.Cookies.Append(Cookie, ws.Id.ToString(), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = http.Request.IsHttps, MaxAge = TimeSpan.FromDays(7), Path = "/",
            });
        }
        current.Id = ws.Id;
        await next(http);
    }
}

public sealed class WorkspaceHousekeeping(IServiceScopeFactory scopes, ILogger<WorkspaceHousekeeping> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ws = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
                await ws.EnsureTemplateAsync(stoppingToken);
                await ws.CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Workspace housekeeping failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
