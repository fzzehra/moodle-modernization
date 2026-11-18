using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Moodle.Infrastructure.Courses;

public sealed class CrsDbContextFactory : IDesignTimeDbContextFactory<CrsDbContext>
{
    public CrsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql(CrsDatabaseConfiguration.ConnectionString)
            .Options;

        return new CrsDbContext(options);
    }
}
