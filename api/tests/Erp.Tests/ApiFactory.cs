using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Erp.Tests;

/// <summary>The real API against a throw-away PostgreSQL database (server from ERP_TEST_DB).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _database = $"erp_test_{Guid.NewGuid():N}";

    public static string DbServer => Environment.GetEnvironmentVariable("ERP_TEST_DB") ?? "Host=localhost;Username=erp;Password=erp";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Db", $"{DbServer};Database={_database}");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await using var conn = new Npgsql.NpgsqlConnection($"{DbServer};Database=postgres");
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
