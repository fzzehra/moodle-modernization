using Moodle.Domain.Auth;
using Moodle.Infrastructure.Auth.Comparisons;

namespace Moodle.UnitTests;

/// <summary>
/// AUTH-FR-04, 05 karşılaştırma uygulamasının (<see cref="ManualAccountStatusCheck"/>, WBS-2 §4.4) saf
/// mantığını sınar. Bu dosya da YZ tarafından yazılmıştır; "elle" karşılaştırma çiftinin kod tarafını
/// doğrular, "elle yazıldı" iddiası taşımaz.
/// </summary>
public class ManualAccountStatusCheckTests
{
    [Fact]
    public void Unknown_username_is_rejected_generically() =>
        Assert.Equal(ManualAccountStatusCheck.Decision.RejectGeneric,
            ManualAccountStatusCheck.EvaluateAccountStatus(userExists: false, isDeleted: false, isSuspended: false));

    [Fact]
    public void Deleted_account_is_rejected_generically() =>
        Assert.Equal(ManualAccountStatusCheck.Decision.RejectGeneric,
            ManualAccountStatusCheck.EvaluateAccountStatus(userExists: true, isDeleted: true, isSuspended: false));

    [Fact]
    public void Suspended_account_is_rejected_generically() =>
        Assert.Equal(ManualAccountStatusCheck.Decision.RejectGeneric,
            ManualAccountStatusCheck.EvaluateAccountStatus(userExists: true, isDeleted: false, isSuspended: true));

    [Fact]
    public void Healthy_account_continues() =>
        Assert.Equal(ManualAccountStatusCheck.Decision.Continue,
            ManualAccountStatusCheck.EvaluateAccountStatus(userExists: true, isDeleted: false, isSuspended: false));

    [Theory]
    [InlineData(true, ManualAccountStatusCheck.Decision.RejectLockedOut)]
    [InlineData(false, ManualAccountStatusCheck.Decision.Continue)]
    public void Lockout_is_evaluated_independently(bool isLockedOut, ManualAccountStatusCheck.Decision expected) =>
        Assert.Equal(expected, ManualAccountStatusCheck.EvaluateLockout(isLockedOut));

    [Fact]
    public void Audit_entry_distinguishes_success_lockout_and_generic_failure()
    {
        var now = DateTimeOffset.UtcNow;

        var success = ManualAccountStatusCheck.BuildAuditEntry(1, "ogrenci1", ManualAccountStatusCheck.Decision.Continue, succeeded: true, now);
        Assert.Equal(SecurityEventType.LoginSucceeded, success.EventType);

        var lockedOut = ManualAccountStatusCheck.BuildAuditEntry(1, "ogrenci1", ManualAccountStatusCheck.Decision.RejectLockedOut, succeeded: false, now);
        Assert.Equal(SecurityEventType.LoginRejectedLockedOut, lockedOut.EventType);

        var failed = ManualAccountStatusCheck.BuildAuditEntry(null, "yok-boyle-biri", ManualAccountStatusCheck.Decision.RejectGeneric, succeeded: false, now);
        Assert.Equal(SecurityEventType.LoginFailed, failed.EventType);
        Assert.Null(failed.UserId);
    }
}
