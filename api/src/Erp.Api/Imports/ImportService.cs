using System.Text.Json;
using Erp.Api.Data;
using Erp.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Erp.Api.Imports;

public sealed record Upload(Guid Id, Guid WorkspaceId, string FileName, TabularFile File);

/// <summary>Parsed uploads wait here between "analyse", "check" and "import" (20 minutes, bounded memory).</summary>
public sealed class UploadStore(IMemoryCache cache)
{
    public Upload Add(Guid workspace, string fileName, TabularFile file)
    {
        var upload = new Upload(Guid.NewGuid(), workspace, fileName, file);
        var size = Math.Max(1, file.Rows.Count * Math.Max(1, file.Headers.Length) / 50);
        cache.Set(Key(workspace, upload.Id), upload, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(20), Size = size });
        return upload;
    }

    public Upload? Get(Guid workspace, Guid id) => cache.Get<Upload>(Key(workspace, id));

    private static string Key(Guid ws, Guid id) => $"upload:{ws}:{id}";
}

public sealed class ImportRejectedException(string message) : Exception(message);

public sealed record CommitResult(Guid BatchId, int Inserted, int Updated, int Rejected);

/// <summary>
/// Applies validated rows. Master data and order lines are upserted by their business key,
/// movements are appended. Each import records an undo log, so the most recent one can be
/// taken back exactly – including restoring the previous values of updated rows.
/// </summary>
public sealed class ImportService(IDbContextFactory<ErpDb> dbs, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Undo(List<string[]> Inserted, List<JsonElement> Previous);

    public async Task<ReferenceData> ReferencesAsync(Guid ws, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var refs = new ReferenceData();
        refs.Articles.UnionWith(await db.Articles.Where(a => a.WorkspaceId == ws).Select(a => a.Number).ToListAsync(ct));
        refs.Suppliers.UnionWith(await db.Suppliers.Where(s => s.WorkspaceId == ws).Select(s => s.Number).ToListAsync(ct));
        return refs;
    }

    public async Task<ValidationResult> ValidateAsync(Guid ws, Upload upload, DatasetSchema schema, ColumnMapping mapping, CancellationToken ct)
    {
        foreach (var (key, col) in mapping)
        {
            if (schema.Fields.All(f => f.Key != key)) throw new ImportRejectedException($"Unbekanntes Feld „{key}“.");
            if (col < 0 || col >= upload.File.Headers.Length) throw new ImportRejectedException($"Spalte {col} existiert nicht.");
        }
        if (mapping.Values.Distinct().Count() != mapping.Count) throw new ImportRejectedException("Eine Spalte ist mehreren Feldern zugeordnet.");
        return Validator.Validate(schema, upload.File, mapping, await ReferencesAsync(ws, ct));
    }

    public async Task<CommitResult> CommitAsync(Guid ws, Upload upload, DatasetSchema schema, ColumnMapping mapping, CancellationToken ct)
    {
        var result = await ValidateAsync(ws, upload, schema, mapping, ct);
        if (!result.CanImport)
            throw new ImportRejectedException(result.MissingFields.Count > 0
                ? $"Pflichtfelder ohne Spalte: {string.Join(", ", result.MissingFields)}."
                : "Keine gültigen Zeilen zum Übernehmen.");

        await using var db = await dbs.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var batchId = Guid.NewGuid();
        var undo = new Undo([], []);
        int inserted = 0, updated = 0;
        var rows = result.Valid;

        switch (schema.Dataset)
        {
            case Dataset.Articles:
            {
                var keys = rows.Select(r => r.Text("number")!).ToList();
                var existing = await db.Articles.Where(a => a.WorkspaceId == ws && keys.Contains(a.Number)).ToDictionaryAsync(a => a.Number, StringComparer.OrdinalIgnoreCase, ct);
                foreach (var r in rows)
                {
                    if (existing.TryGetValue(r.Text("number")!, out var a))
                    {
                        undo.Previous.Add(JsonSerializer.SerializeToElement(a, Json));
                        a.Name = r.Text("name")!; a.Group = r.Text("group")!; a.Unit = r.Text("unit")!; a.UnitCost = r.Get<decimal>("unitCost");
                        if (r.Text("supplierNumber") is { } s) a.SupplierNumber = s;
                        updated++;
                    }
                    else
                    {
                        db.Articles.Add(new Article { WorkspaceId = ws, Number = r.Text("number")!, Name = r.Text("name")!, Group = r.Text("group")!, Unit = r.Text("unit")!, UnitCost = r.Get<decimal>("unitCost"), SupplierNumber = r.Text("supplierNumber") });
                        undo.Inserted.Add([r.Text("number")!]);
                        inserted++;
                    }
                }
                break;
            }
            case Dataset.Suppliers:
            {
                var keys = rows.Select(r => r.Text("number")!).ToList();
                var existing = await db.Suppliers.Where(s => s.WorkspaceId == ws && keys.Contains(s.Number)).ToDictionaryAsync(s => s.Number, StringComparer.OrdinalIgnoreCase, ct);
                foreach (var r in rows)
                {
                    if (existing.TryGetValue(r.Text("number")!, out var s))
                    {
                        undo.Previous.Add(JsonSerializer.SerializeToElement(s, Json));
                        s.Name = r.Text("name")!; s.City = r.Text("city") ?? s.City; s.Country = r.Text("country") ?? s.Country;
                        updated++;
                    }
                    else
                    {
                        db.Suppliers.Add(new Supplier { WorkspaceId = ws, Number = r.Text("number")!, Name = r.Text("name")!, City = r.Text("city"), Country = r.Text("country") });
                        undo.Inserted.Add([r.Text("number")!]);
                        inserted++;
                    }
                }
                break;
            }
            case Dataset.StockMovements:
                foreach (var r in rows)
                {
                    db.Movements.Add(new StockMovement
                    {
                        WorkspaceId = ws, ArticleNumber = r.Text("articleNumber")!, Date = r.Get<DateOnly>("date"), Quantity = r.Get<decimal>("quantity"),
                        Type = r.Text("type")!, Reference = r.Text("reference"), ImportBatchId = batchId,
                    });
                    inserted++;
                }
                break;
            case Dataset.SalesOrders:
            {
                var existing = await db.SalesOrders.Where(l => l.WorkspaceId == ws).ToDictionaryAsync(l => (l.OrderNumber.ToUpperInvariant(), l.LineNo), ct);
                foreach (var r in rows)
                {
                    var key = (r.Text("orderNumber")!.ToUpperInvariant(), r.Get<int>("lineNo"));
                    if (!existing.TryGetValue(key, out var l))
                    {
                        l = new SalesOrderLine { WorkspaceId = ws, OrderNumber = r.Text("orderNumber")!, LineNo = r.Get<int>("lineNo"), Customer = "", ArticleNumber = "" };
                        db.SalesOrders.Add(l);
                        undo.Inserted.Add([l.OrderNumber, l.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                        inserted++;
                    }
                    else { undo.Previous.Add(JsonSerializer.SerializeToElement(l, Json)); updated++; }
                    l.Customer = r.Text("customer")!; l.ArticleNumber = r.Text("articleNumber")!; l.Quantity = r.Get<decimal>("quantity");
                    l.UnitPrice = r.Get<decimal>("unitPrice"); l.OrderDate = r.Get<DateOnly>("orderDate"); l.DueDate = r.Get<DateOnly>("dueDate");
                    l.ShippedDate = r.Opt<DateOnly>("shippedDate");
                }
                break;
            }
            case Dataset.PurchaseOrders:
            {
                var existing = await db.PurchaseOrders.Where(l => l.WorkspaceId == ws).ToDictionaryAsync(l => (l.OrderNumber.ToUpperInvariant(), l.LineNo), ct);
                foreach (var r in rows)
                {
                    var key = (r.Text("orderNumber")!.ToUpperInvariant(), r.Get<int>("lineNo"));
                    if (!existing.TryGetValue(key, out var l))
                    {
                        l = new PurchaseOrderLine { WorkspaceId = ws, OrderNumber = r.Text("orderNumber")!, LineNo = r.Get<int>("lineNo"), SupplierNumber = "", ArticleNumber = "" };
                        db.PurchaseOrders.Add(l);
                        undo.Inserted.Add([l.OrderNumber, l.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                        inserted++;
                    }
                    else { undo.Previous.Add(JsonSerializer.SerializeToElement(l, Json)); updated++; }
                    l.SupplierNumber = r.Text("supplierNumber")!; l.ArticleNumber = r.Text("articleNumber")!; l.Quantity = r.Get<decimal>("quantity");
                    l.OrderDate = r.Get<DateOnly>("orderDate"); l.PromisedDate = r.Get<DateOnly>("promisedDate"); l.ReceivedDate = r.Opt<DateOnly>("receivedDate");
                }
                break;
            }
        }

        db.ImportBatches.Add(new ImportBatch
        {
            Id = batchId, WorkspaceId = ws, Dataset = schema.Dataset.ToString(), FileName = upload.FileName, CreatedAt = clock.GetUtcNow(),
            RowsTotal = result.TotalRows, Inserted = inserted, Updated = updated, Rejected = result.InvalidRows,
            Status = BatchStatus.Active, UndoJson = JsonSerializer.Serialize(undo, Json),
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CommitResult(batchId, inserted, updated, result.InvalidRows);
    }

    /// <summary>Takes back the most recent active import of the workspace (LIFO keeps references consistent).</summary>
    public async Task RollbackAsync(Guid ws, Guid batchId, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var batch = await db.ImportBatches.SingleOrDefaultAsync(b => b.Id == batchId && b.WorkspaceId == ws, ct)
            ?? throw new ImportRejectedException("Import nicht gefunden.");
        if (batch.Status != BatchStatus.Active) throw new ImportRejectedException("Dieser Import wurde bereits zurückgenommen.");
        var latest = await db.ImportBatches.Where(b => b.WorkspaceId == ws && b.Status == BatchStatus.Active).OrderByDescending(b => b.CreatedAt).Select(b => b.Id).FirstAsync(ct);
        if (latest != batchId) throw new ImportRejectedException("Nur der jeweils letzte Import kann zurückgenommen werden.");

        var undo = JsonSerializer.Deserialize<Undo>(batch.UndoJson, Json)!;
        switch (Enum.Parse<Dataset>(batch.Dataset))
        {
            case Dataset.StockMovements:
                await db.Movements.Where(m => m.WorkspaceId == ws && m.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
                break;
            case Dataset.Articles:
                foreach (var key in undo.Inserted) await db.Articles.Where(a => a.WorkspaceId == ws && a.Number == key[0]).ExecuteDeleteAsync(ct);
                foreach (var prev in undo.Previous.Select(p => p.Deserialize<Article>(Json)!)) db.Articles.Update(prev);
                break;
            case Dataset.Suppliers:
                foreach (var key in undo.Inserted) await db.Suppliers.Where(s => s.WorkspaceId == ws && s.Number == key[0]).ExecuteDeleteAsync(ct);
                foreach (var prev in undo.Previous.Select(p => p.Deserialize<Supplier>(Json)!)) db.Suppliers.Update(prev);
                break;
            case Dataset.SalesOrders:
                foreach (var key in undo.Inserted)
                {
                    var line = int.Parse(key[1], System.Globalization.CultureInfo.InvariantCulture);
                    await db.SalesOrders.Where(l => l.WorkspaceId == ws && l.OrderNumber == key[0] && l.LineNo == line).ExecuteDeleteAsync(ct);
                }
                foreach (var prev in undo.Previous.Select(p => p.Deserialize<SalesOrderLine>(Json)!)) db.SalesOrders.Update(prev);
                break;
            case Dataset.PurchaseOrders:
                foreach (var key in undo.Inserted)
                {
                    var line = int.Parse(key[1], System.Globalization.CultureInfo.InvariantCulture);
                    await db.PurchaseOrders.Where(l => l.WorkspaceId == ws && l.OrderNumber == key[0] && l.LineNo == line).ExecuteDeleteAsync(ct);
                }
                foreach (var prev in undo.Previous.Select(p => p.Deserialize<PurchaseOrderLine>(Json)!)) db.PurchaseOrders.Update(prev);
                break;
        }
        batch.Status = BatchStatus.RolledBack;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
