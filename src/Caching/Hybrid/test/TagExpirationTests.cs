// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Caching.Hybrid.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;

namespace Microsoft.Extensions.Caching.Hybrid.Tests;

public class TagExpirationTests
{
    private readonly TestTimeProvider _clock = new();
    private ServiceProvider GetCache(out DefaultHybridCache cache)
    {
        var services = new ServiceCollection();
        _clock.ResetAndAddTo(services);
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        cache = Assert.IsType<DefaultHybridCache>(provider.GetRequiredService<HybridCache>());
        return provider;
    }

    [Fact]
    public async Task BasicLocalTagExpiration()
    {
        using var provider = GetCache(out var cache);

        var creationDatePast = _clock.GetUtcNow();
        _clock.Add(TimeSpan.FromSeconds(5));
        var now = _clock.GetUtcNow();
        Assert.False(cache.IsLocalTagExpired("abc", creationDatePast));

        await cache.RemoveTagAsync("abc"); // which sets L1 tag expiry to "now", which is later
        Assert.True(cache.IsLocalTagExpired("abc", creationDatePast)); // expiry kills older content
        Assert.False(cache.IsLocalTagExpired("abc", now)); // expiry allows same-time content to stay

        var future = now + TimeSpan.FromSeconds(5);
        Assert.False(cache.IsLocalTagExpired("abc", future)); // expiry allows later content to stay
    }
}
