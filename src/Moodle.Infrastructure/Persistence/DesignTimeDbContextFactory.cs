using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Moodle.Infrastructure.Persistence;
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MoodleDbContext>
{
    public MoodleDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<MoodleDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                ?? "Host=localhost;Database=moodle_modern").Options);
}
