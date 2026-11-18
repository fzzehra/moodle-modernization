using Moodle.Api.Errors;
using Moodle.Application.Seeding;
using Moodle.Infrastructure;
using Moodle.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddRazorComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<ApiErrorMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapOpenApi("/api/v1/openapi.json");
app.MapHealthChecks("/api/v1/health");
app.MapRazorComponents<App>();

// Örnek veri: SEED_SAMPLE_DATA=true ile açılışta yüklenir (ortam/ornek-veri.feature ile aynı veri).
if (app.Configuration.GetValue<bool>("SEED_SAMPLE_DATA"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync(SampleDataset.Create());
}

app.Run();

/// <summary>Entegrasyon testlerinin <c>WebApplicationFactory</c> ile erişmesi için.</summary>
public partial class Program;
