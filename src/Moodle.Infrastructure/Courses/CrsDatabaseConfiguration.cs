namespace Moodle.Infrastructure.Courses;

internal static class CrsDatabaseConfiguration
{
    internal static string ConnectionString =>
        Environment.GetEnvironmentVariable("CRS_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=moodle;Username=moodle;Password=moodle";
}
