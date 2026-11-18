using Npgsql;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

/// <summary>
/// Testcontainers hattının çalıştığını gösterir: gerçek PostgreSQL 17 açılır.
/// Modüller veri katmanını eklediğinde aynı kalıpla kendi testlerini yazar. Docker gerektirir.
/// </summary>
public class PostgresContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _db.StartAsync();
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task PostgreSQL_17_konteyneri_sorgu_calistirir()
    {
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SHOW server_version", conn);
        var version = (string?)await cmd.ExecuteScalarAsync();
        Assert.StartsWith("17", version);
    }
}
