using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Application.Files;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

/// <summary>
/// FILE'ın kendi Postgres + HTTP fixture'ı (CRS/GRD'deki desenle aynı). Seray'ın gerçek (yerel disk)
/// <see cref="IFileStore"/>'u henüz bağlanmadığından, CRS/GRD'nin AUTH'u bekleme döneminde yaptığı gibi
/// burada açıkça <see cref="FakeFileStore"/> kullanılır — İrem'in gerçek <see cref="IFileService"/>'i
/// yalnızca arayüze bağlıdır, Seray'ın gerçek deposu bağlanınca üretimde otomatik devreye girer.
/// </summary>
public sealed class FilesApiFixture : IAsyncLifetime
{
    public FilesPostgresFixture Database { get; } = new();
    private readonly WebApplicationFactory<Program> _root = new();

    public Task InitializeAsync() => Database.InitializeAsync();

    public WebApplicationFactory<Program> CreateFactory(int userId = 42, string environment = "Production")
    {
        using var db = Database.CreateContext();
        var connectionString = db.Database.GetConnectionString();
        return _root.WithWebHostBuilder(builder => builder.UseEnvironment(environment).ConfigureTestServices(services =>
        {
            services.RemoveAll<FilesDbContext>();
            services.RemoveAll<DbContextOptions<FilesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FilesDbContext>>();
            services.AddDbContext<FilesDbContext>(options => options.UseNpgsql(connectionString));

            services.RemoveAll<IFileStore>();
            services.AddSingleton<IFileStore>(new FakeFileStore());

            services.RemoveAll<ICurrentUser>();
            services.AddSingleton<ICurrentUser>(new TestUser(userId));
        }));
    }

    public async Task DisposeAsync()
    {
        await _root.DisposeAsync();
        await Database.DisposeAsync();
    }

    private sealed class TestUser(int userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public int? UserId => userId;
        public string? Username => $"user-{userId}";
    }
}
