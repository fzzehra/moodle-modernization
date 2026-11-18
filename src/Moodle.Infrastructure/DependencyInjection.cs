using Microsoft.EntityFrameworkCore;
using Moodle.Infrastructure.Persistence;
using Moodle.Application.Assignments;
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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, bool useDevelopmentFakes = false)
        => services.AddInfrastructure(_ => useDevelopmentFakes);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, Func<IServiceProvider, bool> useDevelopmentFakes)
    {
        services.AddScoped<ICurrentUser>(sp => useDevelopmentFakes(sp) ? new FakeCurrentUser() : new UnavailableIdentity());
        services.AddScoped<IAccessPolicy>(sp => useDevelopmentFakes(sp) ? new FakeAccessPolicy() : new UnavailableIdentity());
        services.AddScoped<IAssignmentLearnerQuery>(sp => useDevelopmentFakes(sp)
            ? new FakeAssignmentLearnerQuery() : new UnavailableSubmissionScope());
        services.AddScoped<ISubmissionAccessPolicy>(sp => useDevelopmentFakes(sp)
            ? new FakeSubmissionAccessPolicy(sp.GetRequiredService<IAssignmentStore>()) : new UnavailableSubmissionScope());
        services.AddScoped<ICourseDirectory, FakeCourseDirectory>();   // CRS: Sinem
        services.AddScoped<IEnrolmentQuery, FakeEnrolmentQuery>();     // CRS: Sinem
        services.AddScoped<IAssignmentQuery, AssignmentQuery>();   // ASG: Aslı Ceren
        services.AddScoped<ICourseSectionQuery, FakeCourseSectionQuery>();
        services.AddScoped<IAssignmentStore, AssignmentStore>();
        services.AddScoped<AssignmentService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
