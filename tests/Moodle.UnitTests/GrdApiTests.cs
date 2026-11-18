using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moodle.Api.Grading;
using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Application.Grading;
using Moodle.Infrastructure.Fakes;
using Npgsql;

namespace Moodle.UnitTests;

/// <summary>GRD API yetki girişi, hata eşlemesi ve istek gövdesi.</summary>
public sealed class GrdApiTests
{
    private sealed class User(bool authenticated = true, int? id = 42) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public int? UserId => id;
        public string? Username => "grd-test";
    }

    private sealed class Policy(bool allowed = true) : IAccessPolicy
    {
        public List<(int UserId, string Capability, ContextRef Context)> Calls { get; } = [];
        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
        {
            Calls.Add((userId, capability, context));
            return Task.FromResult(allowed);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GRD_FR_01_AUTH_placeholders_are_always_rejected(bool fakeUser, bool fakePolicy)
    {
        var auth = new GrdAuthorization(fakeUser ? new FakeCurrentUser() : new User(),
            fakePolicy ? new FakeAccessPolicy() : new Policy());
        var error = Assert.Throws<AppException>(auth.RequireIdentity);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal("auth_not_ready", error.Code);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GRD_FR_01_unavailable_AUTH_defaults_are_reported_as_not_ready(bool unavailableUser, bool unavailablePolicy)
    {
        var auth = new GrdAuthorization(unavailableUser ? new UnavailableIdentity() : new User(),
            unavailablePolicy ? new UnavailableIdentity() : new Policy());
        var error = Assert.Throws<AppException>(auth.RequireIdentity);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal("auth_not_ready", error.Code);
    }

    [Theory]
    [InlineData(false, 42)]
    [InlineData(true, null)]
    [InlineData(true, 0)]
    public async Task GRD_FR_01_missing_session_is_rejected_before_policy_lookup(bool authenticated, int? id)
    {
        var policy = new Policy();
        var auth = new GrdAuthorization(new User(authenticated, id), policy);
        var error = await Assert.ThrowsAsync<AppException>(() =>
            auth.RequireAsync(GrdAuthorization.GradeEdit, ContextRef.ForCourse(7), default));
        Assert.Equal(401, error.StatusCode);
        Assert.Empty(policy.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GRD_FR_01_grade_edit_is_checked_for_session_user_in_course_context(bool allowed)
    {
        var policy = new Policy(allowed);
        var auth = new GrdAuthorization(new User(), policy);
        if (allowed)
            await auth.RequireAsync(GrdAuthorization.GradeEdit, ContextRef.ForCourse(7), default);
        else
            Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() =>
                auth.RequireAsync(GrdAuthorization.GradeEdit, ContextRef.ForCourse(7), default))).StatusCode);
        Assert.Equal((42, "grade.edit", ContextRef.ForCourse(7)), Assert.Single(policy.Calls));
    }

    public static IEnumerable<object[]> ExpectedErrors()
    {
        // Doğrulama mesajı kullanıcıya gösterilmek üzere yazılır ve yanıta geçer (CRS ile aynı).
        yield return [new ArgumentException("Puan 0 ile 100 arasında olmalıdır."), 422, "validation_failed"];
        yield return [new KeyNotFoundException("private detail"), 404, "not_found"];
        yield return [GrdConflictException.Locked(), 409, "grade_locked"];
        yield return [GrdConflictException.Overridden(), 409, "grade_overridden"];
        yield return [GrdConflictException.Version(), 409, "version_conflict"];
        yield return [new DbUpdateConcurrencyException("private SQL"), 409, "version_conflict"];
        foreach (var state in new[] { PostgresErrorCodes.UniqueViolation, PostgresErrorCodes.ForeignKeyViolation })
            yield return [new DbUpdateException("private SQL", new PostgresException("private constraint", "ERROR", "ERROR", state)), 409, "conflict"];
    }

    [Theory]
    [MemberData(nameof(ExpectedErrors))]
    public async Task GRD_FR_01_endpoint_filter_maps_errors_without_database_details(Exception original, int status, string code)
    {
        var filter = new GrdEndpointFilter(new GrdAuthorization(new User(), new Policy()));
        var error = await Assert.ThrowsAsync<AppException>(async () => await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()),
            _ => ValueTask.FromException<object?>(original)));
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task GRD_FR_01_unexpected_errors_are_not_misreported_as_client_errors()
    {
        var filter = new GrdEndpointFilter(new GrdAuthorization(new User(), new Policy()));
        var original = new InvalidOperationException("Unexpected implementation error");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()),
            _ => ValueTask.FromException<object?>(original)));
        Assert.Same(original, error);
    }

    [Fact]
    public void GRD_FR_01_grade_request_rejects_unknown_fields_such_as_grader()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var request = JsonSerializer.Deserialize<GradeRequest>("""{"score":85.5,"feedback":"İyi","version":2}""", options)!;
        Assert.Equal(85.5m, request.Score);
        Assert.Equal("İyi", request.Feedback);
        Assert.Equal(2, request.Version);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<GradeRequest>("""{"score":85,"version":0,"graderId":99}""", options));
    }
}
