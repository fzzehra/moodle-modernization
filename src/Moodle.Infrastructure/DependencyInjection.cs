
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Grading;

namespace Moodle.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Altyapı kayıtları. Her modül kendi gerçek servisini
    /// tamamladığında ilgili sahte servis kaydını değiştirir.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        // AUTH: İrem
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddScoped<IAccessPolicy, FakeAccessPolicy>();

        // CRS: Sinem
        services.AddDbContext<CrsDbContext>(options =>
            options.UseNpgsql(CrsDatabaseConfiguration.ConnectionString));

        services.AddScoped<CrsService>();

        services.AddScoped<ICourseDirectory>(
            sp => sp.GetRequiredService<CrsService>());

        services.AddScoped<IEnrolmentQuery>(
            sp => sp.GetRequiredService<CrsService>());

        // ASG: Aslı Ceren
        services.AddScoped<IAssignmentQuery, FakeAssignmentQuery>();

        // GRD
        services.AddDbContext<GrdDbContext>(options =>
            options.UseNpgsql(GrdDatabaseConfiguration.ConnectionString));

        // Gizleme ve kilit hesapları sunucu saatini buradan alır; testler sabit saat verebilir.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<GrdService>();

        return services;
    }
}
