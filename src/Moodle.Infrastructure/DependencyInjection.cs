using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Courses;
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
    /// <summary>AUTH-NFR-03 (aday): güvenlik damgasının yeniden doğrulanma aralığı (Identity varsayılanı, açıkça sabitlendi).</summary>
    public static readonly TimeSpan SecurityStampValidationInterval = TimeSpan.FromMinutes(30);

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

        // AUTH-NFR-03 (aday): oturum çerezini şifreleyen Data Protection anahtarları DataProtection:KeysPath
        // (ör. Docker'da DataProtection__KeysPath=/var/moodle-keys, kalıcı volume) altında saklanır; böylece konteyner
        // yeniden oluşturulunca oturumlar düşmez. Ayar yoksa çerçevenin varsayılan anahtar deposu kullanılır.
        // Linux'ta anahtarlar dosyada şifresiz durur; dizin yalnızca uygulama kullanıcısına açık olmalıdır (Dockerfile: 700).
        services.AddDataProtection().SetApplicationName("Moodle.Modernization");
        services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
        {
            var path = sp.GetService<IConfiguration>()?["DataProtection:KeysPath"];
            if (!string.IsNullOrWhiteSpace(path))
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path),
                    sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance);
        });
        // AUTH-NFR-03 (aday): parola değişince güvenlik damgası değişir; bu kullanıcının diğer çerezleri en geç
        // ValidationInterval sonra yapılan ilk istekte reddedilir (anında değil). Parolayı değiştiren oturum
        // AuthService.ChangePasswordAsync içinde yenilenir.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = SecurityStampValidationInterval);

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true; // Çerçeve varsayılanı; davranış değişmedi, açıkça yazıldı.
                options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
                // Uçlar kendi 401/403'ünü ApiErrorMiddleware ile döner; çerez tabanlı 302 yönlendirmesi istenmez.
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            })
            // SecurityStampValidator bir çerezi reddederken bu şemadan da çıkış yapar; AddIdentityCookies kullanılmadığı
            // için şema kayıtlı değildi ve ret yanıtı 500 oluyordu. İki adımlı doğrulama yoktur; şema yalnızca bu çağrı için kayıtlıdır.
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
        services.AddAuthorization();

        services.AddScoped<AuthService>();
        services.AddScoped<ICurrentUser>(sp => useDevelopmentFakes(sp) ? new FakeCurrentUser() : sp.GetRequiredService<AuthCurrentUser>());
        services.AddScoped<AuthCurrentUser>();
        services.AddScoped<IAccessPolicy>(sp => useDevelopmentFakes(sp) ? new FakeAccessPolicy() : sp.GetRequiredService<AuthAccessPolicy>());
        services.AddScoped<AuthAccessPolicy>();
        services.AddScoped<ISeedContributor, AuthSeedContributor>();
        services.AddScoped<ISeedContributor, ConfiguredSiteAdminSeedContributor>();   // AUTH örnek verisi: kullanıcılar ve sistem rolleri

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
        // CRS-FR-05 / NFR-02 / FR-10: diğer modüller ICrsEventExtension kaydederek course.deleted vb. olayları alır.
        services.AddScoped<CrsEventExtensionPipeline>();
        services.AddScoped<CrsEventDispatcher>();
        services.AddHostedService<CrsEventDispatchWorker>();
        // FILE: course.deleted olayında ders dosyalarını çöpe taşır (Seray). ASG ve GRD kendi uzantılarını aynı şekilde kaydeder.
        services.AddScoped<ICrsEventExtension, FileCourseDeletedExtension>();

        // Real CRS data must never inherit the synthetic evaluator-2/learner-3..5 grants.
        services.AddScoped<ISubmissionAccessPolicy, Moodle.Infrastructure.Assignments.AssignmentSubmissionAccessPolicy>();
        // CRS has no section model yet; do not infer ownership from synthetic IDs.
        services.AddScoped<CourseSectionService>();
        services.AddScoped<ICourseSectionQuery>(sp => sp.GetRequiredService<CourseSectionService>());
        services.AddScoped<IAssignmentQuery, AssignmentQuery>();
        services.AddScoped<IAssignmentStore, AssignmentStore>();
        services.AddScoped<Moodle.Infrastructure.Assignments.AssignmentFileWriteScope>();
        services.AddScoped<IFilePolicy, Moodle.Infrastructure.Assignments.AssignmentFilePolicy>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<ICrsEventExtension, Moodle.Infrastructure.Assignments.AsgCourseDeletedExtension>();
        services.AddScoped<ICrsEventExtension, Moodle.Infrastructure.Grading.GrdCourseDeletedExtension>();
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
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            .GetSection("FileUpload").Get<UploadLimits>() ?? new UploadLimits());
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            .GetSection("ClamAv").Get<ClamAvOptions>() ?? new ClamAvOptions());
        services.AddSingleton<IVirusScanner, ClamAvVirusScanner>();
        services.AddScoped<DraftUploadService>();
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
        services.AddScoped<ISeedContributor, Moodle.Infrastructure.Assignments.AsgSampleSeedContributor>();
        services.AddScoped<ISeedContributor, CrsSeedContributor>();   // CRS örnek verisi
        services.AddScoped<ISeedContributor, FilesSeedContributor>(); // FILE örnek verisi (İrem)
        return services;
    }
}
