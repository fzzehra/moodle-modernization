using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Moodle.Infrastructure.Persistence;
using Moodle.Api;
using Moodle.Api.Auth;
using Moodle.Api.Errors;
using Moodle.Application.Seeding;
using Moodle.Api.Courses;
using Moodle.Api.Files;
using Moodle.Api.Grading;
using Moodle.Infrastructure;
using Moodle.Infrastructure.Auth;
using Moodle.Web;
using Moodle.Web.Auth;
using Moodle.Web.Crs;
using Moodle.Web.Files;
using Moodle.Web.Grd;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddInfrastructure(sp => DevelopmentConfiguration.Validate(sp.GetRequiredService<IHostEnvironment>(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddDbContext<MoodleDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=moodle_modern"));
builder.Services.AddGrdApi();
builder.Services.AddAuthApi();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<Moodle.Web.Assignments.AsgClient>(client => client.BaseAddress = new Uri(builder.Configuration["AsgApi:BaseUrl"] ?? "http://localhost:5202")).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddCrsApi();
builder.Services.AddFileAdminApi();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddCrsWeb();
builder.Services.AddGrdWeb();
builder.Services.AddAuthWeb();
builder.Services.AddFileWeb();
builder.Services.AddHealthChecks();

var app = builder.Build();
var useDevelopmentFakes = DevelopmentConfiguration.Validate(app.Environment, app.Configuration);

app.UseMiddleware<ApiErrorMiddleware>();
app.UseStaticFiles();
// AUTH: HttpContext.User, çerezden burada doldurulur; uçlar kendi 401/403'ünü yukarıdaki ApiErrorMiddleware
// ile döndüğünden çerez tabanlı 302 yönlendirmesi AddCookie olay işleyicilerinde devre dışı bırakılmıştır.
app.UseAuthentication();
app.UseAuthorization();
// AUTH-FR-09: zorunlu parola değişikliği tamamlanmadan AUTH dışındaki API işlemleri reddedilir.
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseAntiforgery();

app.MapAssignmentEndpoints();
app.MapOpenApi("/api/v1/openapi.json");
app.MapHealthChecks("/api/v1/health");
app.MapCrsEndpoints();
app.MapGrdEndpoints();
app.MapAuthEndpoints();
app.MapFileEndpoints();
app.MapFileAdminEndpoints();
app.MapStaticAssets($"{typeof(Program).Assembly.GetName().Name}.staticwebassets.endpoints.json");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
    await scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Auth.AuthDbContext>().Database.MigrateAsync();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Courses.CrsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Grading.GrdDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<Moodle.Infrastructure.Files.FilesDbContext>().Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("Database:SeedAsg"))
        await AsgSeed.ApplyAsync(db,
            scope.ServiceProvider.GetRequiredService<Moodle.Application.Abstractions.ICourseDirectory>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Application.Abstractions.ICourseSectionQuery>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Application.Abstractions.IEnrolmentQuery>(), allowSyntheticFixture: useDevelopmentFakes);
}
// Örnek veri: SEED_SAMPLE_DATA=true ile yüklenir (ortam/ornek-veri.feature ile aynı veri). Modüller ISeedContributor kaydeder.
if (builder.Configuration.GetValue<bool>("SEED_SAMPLE_DATA"))
{
    using var seedScope = app.Services.CreateScope();
    await seedScope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync(SampleDataset.Create());
}

app.Run();

/// <summary>Entegrasyon testlerinin <c>WebApplicationFactory</c> ile erişmesi için.</summary>
public partial class Program;
