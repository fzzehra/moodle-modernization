using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Files;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Auth;
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
        // AUTH (İrem): gerçek kimlik ve yetki. Geliştirmede sahteler açıkça istenirse (Development:UseFakeIdentity)
        // diğer modüller AUTH'tan bağımsız denenebilir; aksi hâlde — Production dâhil — her zaman gerçek AUTH kullanılır.
        services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(AuthDatabaseConfiguration.ConnectionString));
        services.AddHttpContextAccessor();
        services.AddIdentityCore<AppUser>(options =>
        {
            // Örnek veri kuralı (ortam/ornek-veri.feature): şifre kullanıcı adıyla aynıdır (ör. "ogrenci1").
            // Bu yalnızca geliştirme/test fikstürü için bir kolaylıktır, üretim şifre politikası değildir.
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 4;
            options.User.RequireUniqueEmail = true;
            // AUTH-BR-03: etkin hesap kilitleme kuralları.
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.AllowedForNewUsers = true;
        })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                // Uçlar kendi 401/403'ünü ApiErrorMiddleware ile döner; çerez tabanlı 302 yönlendirmesi istenmez.
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            });
        services.AddAuthorization();

        services.AddScoped<AuthService>();
        services.AddScoped<ICurrentUser>(sp => useDevelopmentFakes(sp) ? new FakeCurrentUser() : sp.GetRequiredService<AuthCurrentUser>());
        services.AddScoped<AuthCurrentUser>();
        services.AddScoped<IAccessPolicy>(sp => useDevelopmentFakes(sp) ? new FakeAccessPolicy() : sp.GetRequiredService<AuthAccessPolicy>());
        services.AddScoped<AuthAccessPolicy>();
        services.AddScoped<ISeedContributor, AuthSeedContributor>();   // AUTH örnek verisi: kullanıcılar ve sistem rolleri

        services.AddDbContext<CrsDbContext>(options => options.UseNpgsql(CrsDatabaseConfiguration.ConnectionString));
        services.AddScoped<CrsService>();
        services.AddScoped<ICourseDirectory>(sp => sp.GetRequiredService<CrsService>());
        services.AddScoped<IEnrolmentQuery>(sp => sp.GetRequiredService<CrsService>());
        services.AddScoped<IAssignmentLearnerQuery>(sp => sp.GetRequiredService<CrsService>());
        // CRS-FR-11 / CRS-NFR-06: Crs:BackgroundDeletion:Enabled (varsayılan false) / PollIntervalMilliseconds / MaxAttempts.
        services.AddSingleton(sp => CrsBackgroundDeletionOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
        services.AddScoped<CrsCourseDeletionJobs>();
        services.AddScoped<CrsCourseDeletionProcessor>();
        services.AddHostedService<CrsCourseDeletionWorker>();

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
        services.AddScoped<IFilePolicy, UserFilePolicy>();                  // İrem: taslak ve özel dosyalar yalnızca sahibine (component="user")
        // İçerik deposu: gerçek yerel disk gerçeklemesi (Seray, FILES_ROOT). Dosya servisi: geliştirmede açık istekle sahte.
        services.AddSingleton(FileStoreOptions.FromEnvironment());
        services.AddSingleton<IFileStore, LocalDiskFileStore>();
        services.AddScoped<FileConsistencyChecker>();
        services.AddScoped<FileService>();                                  // İrem: gerçek dosya servisi (FR-02, 03, 04, 05, 09)
        services.AddScoped<IFileService>(sp => useDevelopmentFakes(sp)
            ? new FakeFileService(sp.GetRequiredService<IFileStore>()) : sp.GetRequiredService<FileService>());
        // Fatma Zehra: temizlik görevi (FILE-FR-12). Files:CleanupEnabled / Files:CleanupIntervalMinutes.
        services.AddScoped<IFileMaintenance, FileMaintenanceService>();
        services.AddSingleton(sp => FileCleanupOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
        services.AddHostedService<FileCleanupWorker>();
        // Fatma Zehra: yönetici dosya ayarları ve ders dosyaları (FILE-FR-13, component="course").
        services.AddScoped<IFileSettingsService, FileSettingsService>();
        services.AddScoped<ISiteAdminQuery, IdentitySiteAdminQuery>();
        services.AddScoped<IFilePolicy, CourseFilePolicy>();
        services.AddScoped<ISeedContributor, CourseFilesSeedContributor>(); // SENG101 ders dosyası, CRS'den sonra (Order 31)

        // Örnek veri yükleyici (Seray). Modüller ISeedContributor kaydederek kendi tablolarına veri yükler.
        services.AddScoped<SeedRunner>();
        services.AddScoped<ISeedContributor, CrsSeedContributor>();   // CRS örnek verisi
        services.AddScoped<ISeedContributor, FilesSeedContributor>(); // FILE örnek verisi (İrem)
        return services;
    }
}
