using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.UnitTests;

public sealed class CrsEventExtensionTests
{
    [Fact]
    public void CRS_NFR_08_Extensions_have_deterministic_order_and_stable_ids()
    {
        var pipeline = new CrsEventExtensionPipeline([
            new Extension("z", 1), new Extension("b", 0), new Extension("a", 0)]);
        Assert.Equal(["a", "b", "z"], pipeline.Extensions.Select(x => x.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CRS_NFR_08_Blank_extension_id_is_rejected(string? id) =>
        Assert.Throws<ArgumentException>(() => new CrsEventExtensionPipeline([new Extension(id!, 0)]));

    [Fact]
    public void CRS_NFR_08_Duplicate_or_oversized_extension_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new CrsEventExtensionPipeline([
            new Extension("same", 0), new Extension("same", 1)]));
        Assert.Throws<ArgumentException>(() => new CrsEventExtensionPipeline([new Extension(new string('x', 101), 0)]));
    }

    [Fact]
    public void CRS_NFR_08_Registration_input_cannot_change_pipeline_after_validation()
    {
        var extensions = new List<ICrsEventExtension> { new Extension("first", 0) };
        var pipeline = new CrsEventExtensionPipeline(extensions);
        extensions.Clear();
        Assert.Single(pipeline.Extensions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task CRS_NFR_08_Invalid_dispatch_limit_is_rejected_without_database(int limit)
    {
        await using var db = ContextWithoutDatabase();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new CrsEventDispatcher(db, new([])).DispatchAsync(limit));
    }

    [Fact]
    public async Task CRS_NFR_08_No_consumers_requires_no_database_connection()
    {
        await using var db = ContextWithoutDatabase();
        Assert.Equal(0, await new CrsEventDispatcher(db, new([])).DispatchAsync());
    }

    private static CrsDbContext ContextWithoutDatabase() => new(
        new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=not_used;Username=not_used;Timeout=1").Options);

    private sealed class Extension(string id, int order) : ICrsEventExtension
    {
        public string Id => id;
        public int Order => order;
        public Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default) => Task.CompletedTask;
    }
}
