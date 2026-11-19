namespace Moodle.Infrastructure.Auth;

/// <summary>AUTH bağlantı dizesi. Diğer modüllerin yapılandırması internal olduğundan AUTH kendi sınıfını kullanır.</summary>
internal static class AuthDatabaseConfiguration
{
    internal static string ConnectionString =>
        Environment.GetEnvironmentVariable("AUTH_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=moodle;Username=moodle;Password=moodle";
}
