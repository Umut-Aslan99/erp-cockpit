using Erp.Api.Kpis;

namespace Erp.Api.Workspaces;

public static class WorkspaceEndpoints
{
    public static void MapWorkspace(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => "ok").ExcludeFromDescription();

        app.MapGet("/api/workspace", async (WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
        {
            var w = await ws.FindAsync(me.Id, ct);
            return new { me.Created, w!.CreatedAt, ExpiresAfterIdleHours = (int)WorkspaceService.IdleLifetime.TotalHours, ws.Today };
        }).WithTags("Workspace");

        app.MapPost("/api/workspace/reset", async (WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
        {
            await ws.ResetAsync(me.Id, ct);
            return Results.NoContent();
        }).WithTags("Workspace").RequireRateLimiting("import");

        app.MapGet("/api/kpis", (KpiService kpis, WorkspaceService ws, CurrentWorkspace me, CancellationToken ct) =>
            kpis.OverviewAsync(me.Id, ws.Today, ct)).WithTags("Kennzahlen");
    }
}
