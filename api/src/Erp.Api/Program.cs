using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Erp.Api.Data;
using Erp.Api.Imports;
using Erp.Api.Kpis;
using Erp.Api.Samples;
using Erp.Api.Tables;
using Erp.Api.Workspaces;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddDbContextFactory<ErpDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Db")));
services.AddSingleton(TimeProvider.System);
services.AddMemoryCache(o => o.SizeLimit = 20_000);
services.AddSingleton<UploadStore>();
services.AddSingleton<WorkspaceService>();
services.AddScoped<CurrentWorkspace>();
services.AddSingleton<ImportService>();
services.AddSingleton<KpiService>();
services.AddSingleton<SampleFiles>();
services.AddHostedService<WorkspaceHousekeeping>();
services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
services.AddProblemDetails();
services.AddOpenApi();
services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Imports and resets are the expensive calls: 30 per minute per visitor IP.
    o.AddPolicy("import", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});
services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = ImportEndpoints.MaxUploadBytes + 64 * 1024);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (http, next) =>
{
    var h = http.Response.Headers;
    h.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    h.XContentTypeOptions = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next(http);
});
app.UseRateLimiter();
app.UseMiddleware<WorkspaceMiddleware>();

app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapWorkspace();
app.MapImports();
app.MapTables();

// The React app is served by the same process: one origin, no CORS, one container.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        ctx.Context.Response.Headers.CacheControl = path.StartsWith("/assets/", StringComparison.Ordinal)
            ? "public, max-age=31536000, immutable" : "no-cache";
    },
});
app.MapFallbackToFile("index.html", new StaticFileOptions { OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache" });

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ErpDb>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
