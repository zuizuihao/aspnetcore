// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Caching.Hybrid.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Caching.Hybrid.Tests;
public class ValidationTests
{
    ServiceProvider GetDefaultCache(out DefaultHybridCache cache)
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        cache = Assert.IsType<DefaultHybridCache>(provider.GetRequiredService<HybridCache>());
        return provider;
    }

    [Fact]
    public async Task ValidKeyWorks()
    {
        using var provider = GetDefaultCache(out var cache);
        var id = await cache.GetOrCreateAsync<int>("some key", _ => new(42));
        Assert.Equal(42, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t\t")]
    public async Task EmptyKeyIsInvalid(string key)
    {
        using var provider = GetDefaultCache(out var cache);
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await cache.GetOrCreateAsync<int>("", _ => new(42)));
        Assert.Equal("key", ex.ParamName);
    }

    [Theory]
    [InlineData("abc", "ABC")] // case sensitivity
    [InlineData("abc", "a%2Db")] // %-encoding
    [InlineData("abc", "aḃc")] // accented b
    public async Task KeyIsNotAliased(string primary, string alt)
    {
        using var provider = GetDefaultCache(out var cache);
        Assert.Equal(42, await cache.GetOrCreateAsync<int>(primary, _ => new(42)));
        Assert.Equal(96, await cache.GetOrCreateAsync<int>(alt, _ => new(96)));
        Assert.Equal(42, await cache.GetOrCreateAsync<int>(primary, _ => new(42)));
        Assert.Equal(96, await cache.GetOrCreateAsync<int>(alt, _ => new(96)));
    }

    [Theory]
    [InlineData("a\0bc")]
    public async Task InvalidKeyDetected(string key)
    {
        using var provider = GetDefaultCache(out var cache);
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await cache.GetOrCreateAsync<int>(key, _ => new(42)));
        Assert.Equal("key", ex.ParamName);
    }
}
