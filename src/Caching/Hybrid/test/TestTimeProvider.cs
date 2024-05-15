// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;

namespace Microsoft.Extensions.Caching.Hybrid.Tests;

public sealed class TestTimeProvider : TimeProvider, ISystemClock
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public void Reset() => _now = DateTimeOffset.UtcNow;

    DateTimeOffset ISystemClock.UtcNow => _now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Add(TimeSpan delta) => _now += delta;

    public void ResetAndAddTo(ServiceCollection services)
    {
        Reset();
        services.AddSingleton<TimeProvider>(this);
        services.AddSingleton<ISystemClock>(this);
    }
}
