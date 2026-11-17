using Moodle.Api.Errors;
using Moodle.Api.Courses;
using Moodle.Infrastructure;
using Moodle.Web;
using Moodle.Web.Crs;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddInfrastructure();
builder.Services.AddCrsApi();
builder.Services.AddOpenApi();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCrsWeb();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<ApiErrorMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapOpenApi("/api/v1/openapi.json");
app.MapHealthChecks("/api/v1/health");
app.MapCrsEndpoints();
app.MapStaticAssets($"{typeof(Program).Assembly.GetName().Name}.staticwebassets.endpoints.json");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

/// <summary>Entegrasyon testlerinin <c>WebApplicationFactory</c> ile erişmesi için.</summary>
public partial class Program;
