using Moodle.Api.Errors;
using Moodle.Infrastructure;
using Moodle.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
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

app.Run();

/// <summary>Entegrasyon testlerinin <c>WebApplicationFactory</c> ile erişmesi için.</summary>
public partial class Program;
