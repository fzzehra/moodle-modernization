using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Persistence;

namespace Moodle.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Altyapı kayıtları. Her modül gerçek gerçeklemesini bitirince ilgili sahte satırı buradan siler
    /// ve kendi gerçeklemesini kaydeder.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Bağlantı dizesi: ConnectionStrings:Default (docker-compose ortam değişkeniyle verilir).
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("Default")
                        ?? "Host=localhost;Port=5432;Database=moodle_modern;Username=moodle;Password=local_dev_only"));

        services.AddScoped<ICurrentUser, FakeCurrentUser>();           // AUTH: İrem
        services.AddScoped<IAccessPolicy, FakeAccessPolicy>();         // AUTH: İrem
        services.AddScoped<ICourseDirectory, FakeCourseDirectory>();   // CRS: Sinem
        services.AddScoped<IEnrolmentQuery, FakeEnrolmentQuery>();     // CRS: Sinem
        services.AddScoped<IAssignmentQuery, FakeAssignmentQuery>();   // ASG: Aslı Ceren
        services.AddScoped<SeedRunner>();   // örnek veri yükleyici; modüller ISeedContributor kaydeder
        return services;
    }
}
