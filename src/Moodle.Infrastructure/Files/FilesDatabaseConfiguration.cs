namespace Moodle.Infrastructure.Files;

/// <summary>FILE bağlantı dizesi. Diğer modüller gibi kendi sınıfını kullanır.</summary>
internal static class FilesDatabaseConfiguration
{
    internal static string ConnectionString =>
        Environment.GetEnvironmentVariable("FILES_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=moodle;Username=moodle;Password=moodle";
}
