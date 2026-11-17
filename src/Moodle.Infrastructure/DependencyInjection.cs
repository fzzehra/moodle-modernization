using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure.Fakes;

namespace Moodle.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Altyapı kayıtları. Her modül gerçek gerçeklemesini bitirince ilgili sahte satırı buradan siler
    /// ve kendi gerçeklemesini kaydeder.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUser, FakeCurrentUser>();           // AUTH: İrem
        services.AddScoped<IAccessPolicy, FakeAccessPolicy>();         // AUTH: İrem
        services.AddScoped<ICourseDirectory, FakeCourseDirectory>();   // CRS: Sinem
        services.AddScoped<IEnrolmentQuery, FakeEnrolmentQuery>();     // CRS: Sinem
        services.AddScoped<IAssignmentQuery, FakeAssignmentQuery>();   // ASG: Aslı Ceren
        return services;
    }
}
