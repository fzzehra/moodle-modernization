using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moodle.Api.Courses;
using Moodle.Application.Abstractions;
using Moodle.Application.Courses;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Fakes;
using Npgsql;

namespace Moodle.UnitTests;

public sealed class CrsApiTests
{
    private sealed class User(bool authenticated = true, int? id = 42) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public int? UserId => id;
        public string? Username => "test-user";
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
    public void AUTH_placeholders_are_always_rejected(bool fakeUser, bool fakePolicy)
    {
        var auth = new CrsAuthorization(fakeUser ? new FakeCurrentUser() : new User(),
            fakePolicy ? new FakeAccessPolicy() : new Policy());
        var error = Assert.Throws<AppException>(auth.RequireIdentity);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal("auth_not_ready", error.Code);
    }

    [Theory]
    [InlineData(false, 42)]
    [InlineData(true, null)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public async Task Invalid_identity_is_rejected_before_policy_lookup(bool authenticated, int? id)
    {
        var policy = new Policy();
        var auth = new CrsAuthorization(new User(authenticated, id), policy);
        var error = await Assert.ThrowsAsync<AppException>(() => auth.RequireAsync("course.update", ContextRef.ForCourse(7), default));
        Assert.Equal(401, error.StatusCode);
        Assert.Empty(policy.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Authorization_uses_session_identity_and_exact_resource_scope(bool allowed)
    {
        var policy = new Policy(allowed);
        var auth = new CrsAuthorization(new User(), policy);
        if (allowed)
            await auth.RequireAsync("course.update", ContextRef.ForCourse(7), default);
        else
            Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() =>
                auth.RequireAsync("course.update", ContextRef.ForCourse(7), default))).StatusCode);
        Assert.Equal((42, "course.update", ContextRef.ForCourse(7)), Assert.Single(policy.Calls));
    }

    [Fact]
    public async Task Assigning_a_role_requires_general_and_target_role_permissions()
    {
        var policy = new Policy();
        await new CrsAuthorization(new User(), policy).RequireRoleAssignmentAsync("Teacher", 7, default);
        Assert.Equal(new[] { "course.role.assign", "course.role.assign:Teacher" }, policy.Calls.Select(x => x.Capability));
        Assert.All(policy.Calls, call => Assert.Equal(ContextRef.ForCourse(7), call.Context));
    }

    public static IEnumerable<object[]> ExpectedErrors()
    {
        yield return [new ArgumentException("Invalid input"), 422];
        yield return [new KeyNotFoundException("private detail"), 404];
        yield return [new CrsConflictException("duplicate", "Duplicate"), 409];
        foreach (var state in new[] { PostgresErrorCodes.UniqueViolation, PostgresErrorCodes.ForeignKeyViolation })
            yield return [new DbUpdateException("private SQL", new PostgresException("private constraint", "ERROR", "ERROR", state)), 409];
    }

    [Theory]
    [MemberData(nameof(ExpectedErrors))]
    public async Task Endpoint_filter_maps_expected_errors_without_database_details(Exception original, int status)
    {
        var filter = new CrsEndpointFilter(new CrsAuthorization(new User(), new Policy()));
        var error = await Assert.ThrowsAsync<AppException>(async () => await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()),
            _ => ValueTask.FromException<object?>(original)));
        Assert.Equal(status, error.StatusCode);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task Unexpected_errors_are_not_misreported_as_client_conflicts()
    {
        var filter = new CrsEndpointFilter(new CrsAuthorization(new User(), new Policy()));
        var original = new InvalidOperationException("Unexpected implementation error");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()),
            _ => ValueTask.FromException<object?>(original)));
        Assert.Same(original, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\tTeacher")]
    [InlineData(null)]
    public void Invalid_roles_are_rejected(string? role) =>
        Assert.Throws<ArgumentException>(() => CourseValidation.ValidateRole(role!));

    [Fact]
    public void Database_field_lengths_are_validated_before_persistence()
    {
        Assert.Throws<ArgumentException>(() => CourseValidation.Validate(new string('x', 256), "OK", null, null, null));
        Assert.Throws<ArgumentException>(() => CourseValidation.Validate("OK", new string('x', 101), null, null, null));
        Assert.Throws<ArgumentException>(() => CourseValidation.Validate("OK", "OK", new string('x', 101), null, null));
        Assert.Throws<ArgumentException>(() => CourseValidation.ValidateRole(new string('x', 51)));
    }
}
