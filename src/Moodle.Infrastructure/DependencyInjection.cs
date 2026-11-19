using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Files;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;
using Moodle.Infrastructure.Grading;
using Moodle.Infrastructure.Persistence;

namespace Moodle.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, bool useDevelopmentFakes = false)
        => services.AddInfrastructure(_ => useDevelopmentFakes);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, Func<IServiceProvider, bool> useDevelopmentFakes)
    {
        services.AddScoped<ICurrentUser>(sp => useDevelopmentFakes(sp) ? new FakeCurrentUser() : new UnavailableIdentity());
        services.AddScoped<IAccessPolicy>(sp => useDevelopmentFakes(sp) ? new FakeAccessPolicy() : new UnavailableIdentity());

        services.AddDbContext<CrsDbContext>(options => options.UseNpgsql(CrsDatabaseConfiguration.ConnectionString));
        services.AddScoped<CrsService>();
        services.AddScoped<ICourseDirectory>(sp => sp.GetRequiredService<CrsService>());
        services.AddScoped<IEnrolmentQuery>(sp => sp.GetRequiredService<CrsService>());
        services.AddScoped<IAssignmentLearnerQuery>(sp => sp.GetRequiredService<CrsService>());

        // Real CRS data must never inherit the synthetic evaluator-2/learner-3..5 grants.
        services.AddScoped<ISubmissionAccessPolicy, UnavailableSubmissionScope>();
        // CRS has no section model yet; do not infer ownership from synthetic IDs.
        services.AddScoped<ICourseSectionQuery, UnavailableCourseSectionQuery>();
        services.AddScoped<IAssignmentQuery, AssignmentQuery>();
        services.AddScoped<IAssignmentStore, AssignmentStore>();
        services.AddScoped<AssignmentService>();
        services.AddSingleton(TimeProvider.System);

        // GRD: Fatma Zehra. TimeProvider yukarıda (ASG) kaydedilir; GRD gizleme ve kilit hesaplarında onu kullanır.
        services.AddDbContext<GrdDbContext>(options =>
            options.UseNpgsql(GrdDatabaseConfiguration.ConnectionString));
        services.AddScoped<GrdService>();

        // FILE: Seray (altyapı), İrem, Aslı Ceren, Fatma Zehra. Gerçek gerçeklemeler hazır olunca sahteler silinir.
        services.AddDbContext<FilesDbContext>(options => options.UseNpgsql(FilesDatabaseConfiguration.ConnectionString));
        services.AddScoped<IFilePolicyRegistry, FilePolicyRegistry>();      // modüller IFilePolicy kaydeder; kayıtsız bileşende erişim reddedilir
        // Dosya servisi: geliştirmede sahte, aksi hâlde gerçeği bağlanana kadar "kapalı" (UnavailableFiles). Depo gerçektir.
        services.AddSingleton(FileStoreOptions.FromEnvironment());
        services.AddSingleton<IFileStore, LocalDiskFileStore>();           // Seray: yerel disk (FILES_ROOT)
        services.AddScoped<FileConsistencyChecker>();
        services.AddScoped<IFileService>(sp => useDevelopmentFakes(sp)
            ? new FakeFileService(sp.GetRequiredService<IFileStore>()) : new UnavailableFiles());                     // İrem, Aslı Ceren, Fatma Zehra

        // Örnek veri yükleyici (Seray). Modüller ISeedContributor kaydederek kendi tablolarına veri yükler.
        services.AddScoped<SeedRunner>();
        services.AddScoped<ISeedContributor, CrsSeedContributor>();   // CRS örnek verisi
        return services;
    }
}
