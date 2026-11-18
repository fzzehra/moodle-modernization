using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Moodle.Infrastructure.Grading;

public sealed class GrdDbContextFactory : IDesignTimeDbContextFactory<GrdDbContext>
{
    public GrdDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GrdDbContext>()
            .UseNpgsql(GrdDatabaseConfiguration.ConnectionString)
            .Options;

        return new GrdDbContext(options);
    }
}
