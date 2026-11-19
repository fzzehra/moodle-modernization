using Moodle.Application.Files;
using Moodle.Infrastructure.Files;

namespace Moodle.UnitTests;

/// <summary>FILE-BR-11, 12: taslak ve özel dosyalar yalnızca sahibine açıktır. Saf mantık, veritabanı gerekmez.</summary>
public class UserFilePolicyTests
{
    private readonly UserFilePolicy _policy = new();

    [Theory]
    [InlineData("private")]
    [InlineData("draft")]
    public async Task Owner_can_read_and_write(string area)
    {
        var owned = new FileArea(42, "user", area, 1);
        Assert.True(await _policy.CanReadAsync(42, owned));
        Assert.True(await _policy.CanWriteAsync(42, owned));
    }

    [Theory]
    [InlineData("private")]
    [InlineData("draft")]
    public async Task Non_owner_is_denied(string area)
    {
        var owned = new FileArea(42, "user", area, 1);
        Assert.False(await _policy.CanReadAsync(43, owned));
        Assert.False(await _policy.CanWriteAsync(43, owned));
    }

    [Fact]
    public async Task Unknown_area_is_denied_even_for_the_context_owner()
    {
        var area = new FileArea(42, "user", "something-else", 1);
        Assert.False(await _policy.CanReadAsync(42, area));
    }
}
