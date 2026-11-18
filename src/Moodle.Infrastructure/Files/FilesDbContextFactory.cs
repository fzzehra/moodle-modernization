using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Moodle.Infrastructure.Files;

/// <summary>Yalnızca <c>dotnet ef</c> araçları içindir.</summary>
public sealed class FilesDbContextFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    public FilesDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(FilesDatabaseConfiguration.ConnectionString).Options);
}
