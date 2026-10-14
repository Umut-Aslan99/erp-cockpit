using System.Text.RegularExpressions;
using Erp.Api.Data;
using Erp.Api.Samples;
using Erp.Api.Workspaces;
using Erp.Core;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Imports;

public sealed record FieldDto(string Key, string Label, string Type, bool Required);
public sealed record SchemaDto(Dataset Dataset, string Label, string Description, List<FieldDto> Fields);
public sealed record AnalyzeResponse(Guid UploadId, string FileName, string Format, int RowCount, string[] Headers, List<string[]> Sample, List<MappingSuggestion> Suggestions);
public sealed record MappingRequest(Guid UploadId, Dataset Dataset, Dictionary<string, int> Mapping);
public sealed record ErrorGroup(string Message, int Count);
public sealed record ValidateResponse(
    int TotalRows, int ValidRows, int InvalidRows, List<string> MissingFields, Dictionary<string, string> NumberStyles,
    List<RowError> Errors, List<ErrorGroup> ErrorSummary, List<Dictionary<string, string>> Preview);
public sealed record BatchDto(Guid Id, Dataset Dataset, string DatasetLabel, string FileName, DateTimeOffset CreatedAt,
    int RowsTotal, int Inserted, int Updated, int Rejected, string Status, bool CanRollback);

public static partial class ImportEndpoints
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    public static void MapImports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/imports").WithTags("Import");

        g.MapGet("/schemas", () => Schemas.All.Select(s => new SchemaDto(s.Dataset, s.Label, s.Description,
            s.Fields.Select(f => new FieldDto(f.Key, f.Label, f.Type.ToString(), f.Required)).ToList())));

        g.MapPost("/analyze", async (IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] Dataset dataset, UploadStore uploads, CurrentWorkspace me) =>
        {
            if (file.Length > MaxUploadBytes) return Results.Problem("Die Datei ist größer als 5 MB.", statusCode: 413);
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            TabularFile parsed;
            try { parsed = TabularReader.Read(file.FileName, ms.ToArray()); }
            catch (ImportFileException ex) { return Results.Problem(ex.Message, statusCode: 422); }
            if (parsed.Headers.All(string.IsNullOrWhiteSpace)) return Results.Problem("Die erste Zeile enthält keine Spaltenüberschriften.", statusCode: 422);

            var upload = uploads.Add(me.Id, Path.GetFileName(file.FileName), parsed);
            return Results.Ok(new AnalyzeResponse(upload.Id, upload.FileName, parsed.Format, parsed.Rows.Count, parsed.Headers,
                parsed.Rows.Take(8).ToList(), ColumnMatcher.Suggest(Schemas.For(dataset), parsed.Headers)));
        }).DisableAntiforgery().RequireRateLimiting("import");

        g.MapPost("/validate", async (MappingRequest req, UploadStore uploads, ImportService imports, CurrentWorkspace me, CancellationToken ct) =>
        {
            if (uploads.Get(me.Id, req.UploadId) is not { } upload) return Expired();
            try
            {
                var result = await imports.ValidateAsync(me.Id, upload, Schemas.For(req.Dataset), ToMapping(req), ct);
                return Results.Ok(ToResponse(Schemas.For(req.Dataset), result));
            }
            catch (ImportRejectedException ex) { return Results.Problem(ex.Message, statusCode: 422); }
        }).RequireRateLimiting("import");

        g.MapPost("/commit", async (MappingRequest req, UploadStore uploads, ImportService imports, CurrentWorkspace me, CancellationToken ct) =>
        {
            if (uploads.Get(me.Id, req.UploadId) is not { } upload) return Expired();
            try { return Results.Ok(await imports.CommitAsync(me.Id, upload, Schemas.For(req.Dataset), ToMapping(req), ct)); }
            catch (ImportRejectedException ex) { return Results.Problem(ex.Message, statusCode: 422); }
        }).RequireRateLimiting("import");

        g.MapGet("/", async (ErpDb db, CurrentWorkspace me, CancellationToken ct) =>
        {
            var batches = await db.ImportBatches.Where(b => b.WorkspaceId == me.Id).OrderByDescending(b => b.CreatedAt).AsNoTracking().ToListAsync(ct);
            var latestActive = batches.FirstOrDefault(b => b.Status == BatchStatus.Active)?.Id;
            return batches.Select(b =>
            {
                var ds = Enum.Parse<Dataset>(b.Dataset);
                return new BatchDto(b.Id, ds, Schemas.For(ds).Label, b.FileName, b.CreatedAt, b.RowsTotal, b.Inserted, b.Updated, b.Rejected,
                    b.Status == BatchStatus.Active ? "aktiv" : "zurückgenommen", b.Id == latestActive);
            });
        });

        g.MapPost("/{id:guid}/rollback", async (Guid id, ImportService imports, CurrentWorkspace me, CancellationToken ct) =>
        {
            try { await imports.RollbackAsync(me.Id, id, ct); return Results.NoContent(); }
            catch (ImportRejectedException ex) { return Results.Problem(ex.Message, statusCode: 409); }
        });

        app.MapGet("/api/samples", () => SampleFiles.All.Select(s => new { s.File, s.Dataset, DatasetLabel = Schemas.For(s.Dataset).Label, s.Title, s.Description }))
            .WithTags("Import");
        app.MapGet("/api/samples/{file}", async (string file, SampleFiles samples, WorkspaceService ws, CancellationToken ct) =>
            await samples.CreateAsync(file, ws.Today, ct) is { } f ? Results.File(f.Content, f.ContentType, file) : Results.NotFound())
            .WithTags("Import");
    }

    private static IResult Expired() => Results.Problem("Die hochgeladene Datei ist abgelaufen. Bitte erneut hochladen.", statusCode: 410);

    private static ColumnMapping ToMapping(MappingRequest req)
    {
        var m = new ColumnMapping();
        foreach (var (k, v) in req.Mapping) if (v >= 0) m[k] = v;
        return m;
    }

    private static ValidateResponse ToResponse(DatasetSchema schema, ValidationResult r)
    {
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        static string Show(object? v, System.Globalization.CultureInfo de) => v switch
        {
            null => "",
            DateOnly d => d.ToString("dd.MM.yyyy", de),
            decimal m => m.ToString("#,##0.###", de),
            _ => v.ToString() ?? "",
        };
        return new ValidateResponse(r.TotalRows, r.Valid.Count, r.InvalidRows, r.MissingFields,
            r.NumberStyles.ToDictionary(kv => schema[kv.Key].Label, kv => kv.Value == NumberStyle.German ? "1.234,56 (deutsch)" : "1,234.56 (englisch)"),
            r.Errors.Take(300).ToList(),
            r.Errors.GroupBy(e => $"{e.Field}: {RowRef().Replace(e.Message, "Zeile …")}").Select(g => new ErrorGroup(g.Key, g.Count())).OrderByDescending(g => g.Count).ToList(),
            r.Valid.Take(8).Select(v => schema.Fields.ToDictionary(f => f.Key, f => Show(v.Values.GetValueOrDefault(f.Key), de))).ToList());
    }

    [GeneratedRegex(@"Zeile \d+")]
    private static partial Regex RowRef();
}
