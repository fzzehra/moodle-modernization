using Microsoft.AspNetCore.Http.HttpResults;
using Moodle.Application.Abstractions;
using Moodle.Application.Courses;
using Moodle.Application.Errors;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.Api.Courses;

public sealed record SectionRequest(string? Name);

public static class CrsEndpoints
{
    public static IServiceCollection AddCrsApi(this IServiceCollection services)
    {
        services.AddScoped<CrsAuthorization>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").WithTags("CRS")
            .AddEndpointFilter<CrsEndpointFilter>();

        api.MapGet("/courses/{courseId:int}/sections", async (int courseId, CourseSectionService sections, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("course.view", ContextRef.ForCourse(courseId), ct);
            var course = await sections.RequireCourseAsync(courseId, ct);
            if (!course.Visible) await auth.RequireAsync("course.viewhidden", ContextRef.ForCourse(courseId), ct);
            return Results.Ok(await sections.ListAsync(courseId, ct));
        });
        api.MapPost("/courses/{courseId:int}/sections", async (int courseId, SectionRequest body, CourseSectionService sections, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("course.update", ContextRef.ForCourse(courseId), ct);
            var section = await sections.CreateAsync(courseId, body.Name, ct);
            return Results.Created($"/api/v1/courses/{courseId}/sections", section);
        }).AddEndpointFilter<CookieWriteProtection>();

        api.MapGet("/categories", async (CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            var result = new List<CourseCategory>();
            foreach (var category in await service.ListCategoriesAsync(ct))
                if (await auth.CanAsync("category.view", CategoryContext(category.Id), ct))
                    result.Add(category);
            return TypedResults.Ok(result);
        });

        api.MapGet("/categories/{categoryId:int}", async (int categoryId, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("category.view", CategoryContext(categoryId), ct);
            return TypedResults.Ok(await service.FindCategoryAsync(categoryId, ct) ?? throw AppException.NotFound());
        });

        api.MapPost("/categories", async (CategoryRequest request, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("category.manage", ParentContext(request.ParentId), ct);
            var category = await service.SaveCategoryAsync(new() { Name = request.Name, ParentId = request.ParentId }, ct);
            return TypedResults.Created($"/api/v1/categories/{category.Id}", category);
        });

        api.MapPatch("/categories/{categoryId:int}", async (int categoryId, CategoryPatchRequest request, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("category.manage", CategoryContext(categoryId), ct);
            var category = await service.FindCategoryAsync(categoryId, ct) ?? throw AppException.NotFound();
            if (request.Name is null && !request.ParentIdSpecified)
                throw AppException.Validation("validation_failed", "En az bir kategori alanı gönderilmelidir.");
            if (request.ParentIdSpecified && request.ParentId != category.ParentId)
                await auth.RequireAsync("category.manage", ParentContext(request.ParentId), ct);
            category.Name = request.Name ?? category.Name;
            if (request.ParentIdSpecified) category.ParentId = request.ParentId;
            return TypedResults.Ok(await service.SaveCategoryAsync(category, ct));
        });

        api.MapDelete("/categories/{categoryId:int}", async (int categoryId, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("category.manage", CategoryContext(categoryId), ct);
            await service.DeleteCategoryAsync(categoryId, ct);
            return TypedResults.NoContent();
        });

        api.MapGet("/courses", async (CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            var result = new List<Course>();
            foreach (var course in await service.ListCoursesAsync(ct))
                if (await auth.CanAsync("course.view", ContextRef.ForCourse(course.Id), ct)
                    && (course.Visible || await auth.CanAsync("course.viewhidden", ContextRef.ForCourse(course.Id), ct)))
                    result.Add(course);
            return TypedResults.Ok(result);
        });

        api.MapGet("/courses/{courseId:int}", async (int courseId, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("course.view", ContextRef.ForCourse(courseId), ct);
            var course = await RequireCourseAsync(service, courseId, ct);
            if (!course.Visible)
                await auth.RequireAsync("course.viewhidden", ContextRef.ForCourse(courseId), ct);
            return TypedResults.Ok(course);
        });

        api.MapPost("/courses", async (CourseRequest request, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("course.create", CategoryContext(request.CategoryId), ct);
            var course = await service.SaveCourseAsync(new()
            {
                CategoryId = request.CategoryId, FullName = request.FullName, ShortName = request.ShortName,
                IdNumber = request.IdNumber, Visible = request.Visible, StartDate = request.StartDate, EndDate = request.EndDate
            }, ct);
            return TypedResults.Created($"/api/v1/courses/{course.Id}", course);
        });

        api.MapPatch("/courses/{courseId:int}", async (int courseId, CoursePatchRequest request, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("course.update", ContextRef.ForCourse(courseId), ct);
            var course = await RequireCourseAsync(service, courseId, ct);
            if (request.CategoryId is null && request.FullName is null && request.ShortName is null
                && request.Visible is null && !request.IdNumberSpecified && !request.StartDateSpecified && !request.EndDateSpecified)
                throw AppException.Validation("validation_failed", "En az bir ders alanı gönderilmelidir.");
            if (request.CategoryId.HasValue && request.CategoryId != course.CategoryId)
                await auth.RequireAsync("course.create", CategoryContext(request.CategoryId.Value), ct);
            course.CategoryId = request.CategoryId ?? course.CategoryId;
            course.FullName = request.FullName ?? course.FullName;
            course.ShortName = request.ShortName ?? course.ShortName;
            course.Visible = request.Visible ?? course.Visible;
            if (request.IdNumberSpecified) course.IdNumber = request.IdNumber;
            if (request.StartDateSpecified) course.StartDate = request.StartDate;
            if (request.EndDateSpecified) course.EndDate = request.EndDate;
            return TypedResults.Ok(await service.SaveCourseAsync(course, ct));
        });

        api.MapDelete("/courses/{courseId:int}", async Task<Results<NoContent, Accepted<CrsCourseDeletionJob>>> (
            int courseId, CrsService service, CrsCourseDeletionJobs jobs, CrsBackgroundDeletionOptions options,
            ICurrentUser user, CrsAuthorization auth, CancellationToken ct) =>
        {
            // Yetki, silme veya kuyruğa alma kabul edilmeden önce denetlenir.
            await auth.RequireAsync("course.delete", ContextRef.ForCourse(courseId), ct);
            if (!options.Enabled)
            {
                await service.DeleteCourseAsync(courseId, ct);
                return TypedResults.NoContent();
            }
            // CRS-FR-11 / CRS-NFR-06: istek token'ı yalnızca kuyruğa almayı kapsar; silmeyi worker yapar.
            var job = await jobs.EnqueueAsync(courseId, user.UserId, ct);
            return TypedResults.Accepted($"/api/v1/courses/deletion-jobs/{job.Id}", job);
        });

        api.MapGet("/courses/deletion-jobs/{jobId:guid}", async (Guid jobId, CrsService service, CrsCourseDeletionJobs jobs,
            ICurrentUser user, CrsAuthorization auth, CancellationToken ct) =>
        {
            var job = await jobs.FindAsync(jobId, ct) ?? throw AppException.NotFound();
            if (await service.FindCourseAsync(job.CourseId, ct) is not null)
                await auth.RequireAsync("course.delete", ContextRef.ForCourse(job.CourseId), ct);
            else if (!(job.RequestedByUserId is int owner && owner == user.UserId
                || await auth.CanAsync("course.delete", ContextRef.Site, ct)
                || job.CategoryId is int categoryId && await auth.CanAsync("course.delete", CategoryContext(categoryId), ct)))
                // Ders silindiyse ders bağlamı yoktur: yalnızca talep sahibi veya sistem/kategori yetkilisi görebilir.
                throw new AppException("forbidden", "Bu işlem için yetkiniz yok.", 403);
            return TypedResults.Ok(job);
        });

        api.MapGet("/courses/{courseId:int}/enrolments", async (int courseId, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("enrolment.view", ContextRef.ForCourse(courseId), ct);
            await RequireCourseAsync(service, courseId, ct);
            return TypedResults.Ok(await service.ListEnrolmentsAsync(courseId, ct));
        });

        api.MapGet("/courses/{courseId:int}/enrolments/{enrolmentId:int}", async (int courseId, int enrolmentId, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("enrolment.view", ContextRef.ForCourse(courseId), ct);
            return TypedResults.Ok(await RequireEnrolmentAsync(service, courseId, enrolmentId, ct));
        });

        api.MapPost("/courses/{courseId:int}/enrolments", async (int courseId, EnrolmentRequest request, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("enrolment.create", ContextRef.ForCourse(courseId), ct);
            await RequireCourseAsync(service, courseId, ct);
            CourseValidation.ValidateRole(request.Role);
            await auth.RequireRoleAssignmentAsync(request.Role, courseId, ct);
            var enrolment = await service.EnrolAsync(new()
            {
                CourseId = courseId, UserId = request.UserId, Role = request.Role, Suspended = request.Suspended,
                StartsAt = request.StartsAt, EndsAt = request.EndsAt
            }, ct);
            return TypedResults.Created($"/api/v1/courses/{courseId}/enrolments/{enrolment.Id}", enrolment);
        });

        api.MapPatch("/courses/{courseId:int}/enrolments/{enrolmentId:int}", async (int courseId, int enrolmentId, EnrolmentPatchRequest request, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("enrolment.update", ContextRef.ForCourse(courseId), ct);
            var enrolment = await RequireEnrolmentAsync(service, courseId, enrolmentId, ct);
            if (request.Suspended is null && request.Role is null && !request.StartsAtSpecified && !request.EndsAtSpecified)
                throw AppException.Validation("validation_failed", "Kayıt durumu, rol veya kayıt tarihleri gönderilmelidir.");
            if (request.Role is not null)
            {
                CourseValidation.ValidateRole(request.Role);
                await auth.RequireRoleAssignmentAsync(request.Role, courseId, ct);
            }
            if (request.StartsAtSpecified || request.EndsAtSpecified)
                // CRS-FR-07: gönderilmeyen alanlar mevcut değeriyle korunur; aralık serviste doğrulanır (422).
                await service.UpdateEnrolmentAsync(enrolmentId, request.Suspended ?? enrolment.Suspended, request.Role ?? enrolment.Role,
                    request.StartsAtSpecified ? request.StartsAt : enrolment.StartsAt,
                    request.EndsAtSpecified ? request.EndsAt : enrolment.EndsAt, ct);
            else
                await service.UpdateEnrolmentAsync(enrolmentId, request.Suspended ?? enrolment.Suspended, request.Role ?? enrolment.Role, ct);
            return TypedResults.Ok(await RequireEnrolmentAsync(service, courseId, enrolmentId, ct));
        });

        api.MapDelete("/courses/{courseId:int}/enrolments/{enrolmentId:int}", async (int courseId, int enrolmentId, CrsService service, CrsAuthorization auth, CancellationToken ct) =>
        {
            await auth.RequireAsync("enrolment.delete", ContextRef.ForCourse(courseId), ct);
            await RequireEnrolmentAsync(service, courseId, enrolmentId, ct);
            await service.UnenrolAsync(enrolmentId, ct);
            return TypedResults.NoContent();
        });

        return endpoints;
    }

    private static ContextRef CategoryContext(int id) => new(ContextLevel.CourseCategory, id);
    private static ContextRef ParentContext(int? id) => id.HasValue ? CategoryContext(id.Value) : ContextRef.Site;
    private static async Task<Course> RequireCourseAsync(CrsService service, int courseId, CancellationToken ct) =>
        await service.FindCourseAsync(courseId, ct) ?? throw AppException.NotFound();
    private static async Task<CourseEnrolment> RequireEnrolmentAsync(CrsService service, int courseId, int enrolmentId, CancellationToken ct) =>
        await service.FindEnrolmentAsync(courseId, enrolmentId, ct) ?? throw AppException.NotFound();
}
