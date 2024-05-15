// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Caching.Hybrid.Internal;

// logic related to tag expiration
partial class DefaultHybridCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tagExpirations = new(StringComparer.Ordinal);
    internal bool IsLocalTagExpired(string tag, DateTimeOffset testDate)
    {
        return !string.IsNullOrWhiteSpace(tag)
            && _tagExpirations.TryGetValue(tag, out var expiry)
            && testDate < expiry; // if tag was invalidated *after* the test date (item creation), then: count as expired
    }

    public override ValueTask RemoveTagAsync(string tag, CancellationToken token = default)
    {
        SetLocalTagExpiration(tag, Now());
        return default;
    }

    public override ValueTask RemoveTagsAsync(IEnumerable<string> tags, CancellationToken token = default)
    {
        if (tags is not null)
        {
            var now = Now(); // use the same time for all
            foreach (var tag in tags)
            {
                SetLocalTagExpiration(tag, now);
            }
        }
        return default;
    }

    private void SetLocalTagExpiration(string tag, DateTimeOffset effective)
    {
        if (!_tagExpirations.TryGetValue(tag, out var existing))
        {
            // try to add; if we're in a thread-race, don't fight
            _tagExpirations.TryAdd(tag, effective);
        }
        else if (existing < effective)
        {
            // bring the expiration forwards (only); again, if
            // we're in a thread race, don't fight
            _tagExpirations.TryUpdate(tag, newValue: effective, comparisonValue: existing);
        }
    }
}
