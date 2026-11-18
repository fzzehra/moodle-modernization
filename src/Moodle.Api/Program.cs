using Microsoft.EntityFrameworkCore;
using Moodle.Infrastructure.Persistence;
using Moodle.Api;
using Moodle.Api.Errors;
using Moodle.Infrastructure;
using Moodle.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(sp => DevelopmentConfiguration.Validate(sp.GetRequiredService<IHostEnvironment>(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddDbContext<MoodleDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=moodle_modern"));
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<Moodle.Web.Assignments.AsgClient>(client => client.BaseAddress = new Uri(builder.Configuration["AsgApi:BaseUrl"] ?? "http://localhost:5202")).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddRazorComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();
var useDevelopmentFakes = DevelopmentConfiguration.Validate(app.Environment, app.Configuration);

app.UseMiddleware<ApiErrorMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapAssignmentEndpoints();
app.MapOpenApi("/api/v1/openapi.json");
app.MapHealthChecks("/api/v1/health");
app.MapRazorComponents<App>();

if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
    await db.Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("Database:SeedAsg"))
        await AsgSeed.ApplyAsync(db,
            scope.ServiceProvider.GetRequiredService<Moodle.Application.Abstractions.ICourseDirectory>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Application.Abstractions.ICourseSectionQuery>(),
            scope.ServiceProvider.GetRequiredService<Moodle.Application.Abstractions.IEnrolmentQuery>(), allowSyntheticFixture: useDevelopmentFakes);
}
app.Run();

/// <summary>Entegrasyon testlerinin <c>WebApplicationFactory</c> ile erişmesi için.</summary>
public partial class Program;
