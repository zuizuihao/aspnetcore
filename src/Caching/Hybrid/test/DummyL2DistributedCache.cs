// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid.Tests;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides HybridCache configuration extension methods for Google.Protobuf types
    /// </summary>
    public static class DummyL2DistributedCacheExtensions
    {
        public static void AddDummyL2(this IServiceCollection services)
            => services.TryAddSingleton<IDistributedCache, DummyL2DistributedCache>();
    }
}

namespace Microsoft.Extensions.Caching.Hybrid.Tests
{
    // we can't use AddDistributedMemoryCache for this, because HybridCache recognizes it
    internal sealed class DummyL2DistributedCache : IDistributedCache, IBufferDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _storage = [];

        byte[]? IDistributedCache.Get(string key)
        {
            if (_storage.TryGetValue(key, out var value))
            {
                return value.ToArray(); // defensive copy
            }
            return null;
        }

        Task<byte[]?> IDistributedCache.GetAsync(string key, CancellationToken token)
        {
            if (_storage.TryGetValue(key, out var value))
            {
                return Task.FromResult<byte[]?>(value.ToArray()); // defensive copy
            }
            return SharedEmpty;
        }

        private static readonly Task<byte[]?> SharedEmpty = Task.FromResult<byte[]?>(null);

        void IDistributedCache.Refresh(string key) { }

        Task IDistributedCache.RefreshAsync(string key, CancellationToken token) => Task.CompletedTask;

        void IDistributedCache.Remove(string key) => _storage.TryRemove(key, out _);

        Task IDistributedCache.RemoveAsync(string key, CancellationToken token)
        {
            _storage.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        void IDistributedCache.Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            // note no expiration support
            _storage[key] = value.ToArray();
        }

        Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
        {
            // note no expiration support
            _storage[key] = value.ToArray();
            return Task.CompletedTask;
        }

        public bool TryGet(string key, IBufferWriter<byte> destination)
        {
            if (_storage.TryGetValue(key, out var value))
            {
                destination.Write(value);
                return true;
            }
            return false;
        }

        public ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
            => new(TryGet(key, destination));

        public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
        {
            // note no expiration support
            _storage[key] = value.ToArray();
        }

        public ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            // note no expiration support
            _storage[key] = value.ToArray();
            return default;
        }
    }

}
