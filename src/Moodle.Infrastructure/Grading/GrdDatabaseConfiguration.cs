namespace Moodle.Infrastructure.Grading;

/// <summary>GRD bağlantı dizesi. CRS'nin yapılandırması internal olduğundan GRD kendi sınıfını kullanır.</summary>
internal static class GrdDatabaseConfiguration
{
    internal static string ConnectionString =>
        Environment.GetEnvironmentVariable("GRD_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=moodle;Username=moodle;Password=moodle";
}
