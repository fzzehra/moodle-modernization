using Moodle.Application.Errors;

namespace Moodle.UnitTests;

public class AppExceptionTests
{
    [Fact]
    public void NotFound_404_ve_not_found_kodu_tasir()
    {
        var ex = AppException.NotFound();
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("not_found", ex.Code);
    }

    [Fact]
    public void Validation_422_tasir()
        => Assert.Equal(422, AppException.Validation("invalid_name", "Ad geçersiz.").StatusCode);

    [Fact]
    public void Conflict_409_tasir()
        => Assert.Equal(409, AppException.Conflict("version_conflict", "Sürüm çakıştı.").StatusCode);
}
