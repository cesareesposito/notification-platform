using System.Reflection;
using Npgsql;

namespace Notification.Api.Scheduling;

/// <summary>
/// Ensures the Quartz.NET PostgreSQL tables exist before the scheduler starts.
/// Reads the embedded quartz_postgres.sql and executes it (idempotent — all CREATE TABLE IF NOT EXISTS).
/// </summary>
public static class QuartzSchemaInitializer
{
    private const string ResourceName = "Notification.Api.Scheduling.quartz_postgres.sql";

    public static async Task InitializeAsync(string connectionString, ILogger logger, CancellationToken ct = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.LogInformation("Quartz schema initialized (tables created if not existing).");
    }
}
