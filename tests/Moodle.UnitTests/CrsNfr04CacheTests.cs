using Moodle.Infrastructure.Courses;

namespace Moodle.UnitTests;

public sealed class CrsNfr04CacheTests
{
    [Fact]
    public async Task CRS_NFR_04_Unchanged_stamp_reuses_snapshot_without_loading()
    {
        var cache = new CrsQueryCache();
        var loads = 0;
        Task<string> Load() => Task.FromResult($"snapshot-{++loads}");
        Assert.Equal("snapshot-1", await cache.ReadAsync("a", false, () => Task.FromResult(new CrsQueryCache.Stamp(1, 2)), Load, x => x));
        Assert.Equal("snapshot-1", await cache.ReadAsync("a", false, () => Task.FromResult(new CrsQueryCache.Stamp(1, 2)), Load, x => x));
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task CRS_NFR_04_Count_change_detects_late_commit_even_if_max_id_is_unchanged()
    {
        var cache = new CrsQueryCache();
        var count = 1L;
        var loads = 0;
        Task<CrsQueryCache.Stamp> Revision() => Task.FromResult(new CrsQueryCache.Stamp(count, 99));
        Task<int> Load() => Task.FromResult(++loads);
        Assert.Equal(1, await cache.ReadAsync("a", false, Revision, Load, x => x));
        count++;
        Assert.Equal(2, await cache.ReadAsync("a", false, Revision, Load, x => x));
    }

    [Fact]
    public async Task CRS_NFR_04_Bypass_does_not_read_revision_or_replace_existing_snapshot()
    {
        var cache = new CrsQueryCache();
        Task<CrsQueryCache.Stamp> Revision() => Task.FromResult(new CrsQueryCache.Stamp(1, 1));
        await cache.ReadAsync("a", false, Revision, () => Task.FromResult("committed"), x => x);
        Assert.Equal("uncommitted", await cache.ReadAsync("a", true,
            () => throw new InvalidOperationException("must not read revision"), () => Task.FromResult("uncommitted"), x => x));
        Assert.Equal("committed", await cache.ReadAsync<string>("a", false, Revision, () => throw new Exception("must hit"), x => x));
    }

    [Fact]
    public async Task CRS_NFR_04_Copies_protect_cached_value_from_caller_mutation()
    {
        var cache = new CrsQueryCache();
        Task<CrsQueryCache.Stamp> Revision() => Task.FromResult(new CrsQueryCache.Stamp(0, 0));
        var first = await cache.ReadAsync("a", false, Revision, () => Task.FromResult(new List<int> { 1 }), x => x.ToList());
        first.Clear();
        var second = await cache.ReadAsync("a", false, Revision, () => throw new Exception("must hit"), (List<int> x) => x.ToList());
        Assert.Equal([1], second);
        second.Add(2);
        var third = await cache.ReadAsync("a", false, Revision, () => throw new Exception("must hit"), (List<int> x) => x.ToList());
        Assert.Equal([1], third);
    }

    [Fact]
    public async Task CRS_NFR_04_Failed_revision_or_reload_propagates_instead_of_serving_stale_data()
    {
        var cache = new CrsQueryCache();
        await cache.ReadAsync("a", false, () => Task.FromResult(new CrsQueryCache.Stamp(0, 0)), () => Task.FromResult(1), x => x);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ReadAsync<int>("a", false,
            () => throw new InvalidOperationException("database unavailable"), () => Task.FromResult(2), x => x));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ReadAsync<int>("a", false,
            () => Task.FromResult(new CrsQueryCache.Stamp(1, 1)), () => throw new InvalidOperationException("reload failed"), x => x));
    }

    [Fact]
    public async Task CRS_NFR_04_Separate_instances_do_not_share_snapshots()
    {
        Task<CrsQueryCache.Stamp> Revision() => Task.FromResult(new CrsQueryCache.Stamp(0, 0));
        Assert.Equal("first scope", await new CrsQueryCache().ReadAsync("a", false, Revision, () => Task.FromResult("first scope"), x => x));
        Assert.Equal("second scope", await new CrsQueryCache().ReadAsync("a", false, Revision, () => Task.FromResult("second scope"), x => x));
    }

    [Fact]
    public async Task CRS_NFR_04_Cache_is_bounded_and_eviction_does_not_remove_other_recent_keys()
    {
        var cache = new CrsQueryCache();
        Task<CrsQueryCache.Stamp> Revision() => Task.FromResult(new CrsQueryCache.Stamp(0, 0));
        for (var i = 0; i < 65; i++)
            await cache.ReadAsync(i.ToString(), false, Revision, () => Task.FromResult(i), x => x);
        Assert.Equal(64, await cache.ReadAsync<int>("64", false, Revision, () => throw new Exception("must hit"), x => x));
        Assert.Equal(99, await cache.ReadAsync("0", false, Revision, () => Task.FromResult(99), x => x));
    }

    [Fact]
    public async Task CRS_NFR_04_Missing_result_can_be_cached_and_is_invalidated_when_revision_changes()
    {
        var cache = new CrsQueryCache();
        await cache.ReadAsync<string?>("a", false, () => Task.FromResult(new CrsQueryCache.Stamp(0, 0)), () => Task.FromResult<string?>(null), x => x);
        Assert.Null(await cache.ReadAsync<string?>("a", false, () => Task.FromResult(new CrsQueryCache.Stamp(0, 0)), () => throw new Exception("must hit"), x => x));
        Assert.Equal("created", await cache.ReadAsync<string?>("a", false, () => Task.FromResult(new CrsQueryCache.Stamp(1, 1)), () => Task.FromResult<string?>("created"), x => x));
    }
}
