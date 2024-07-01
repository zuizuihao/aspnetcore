// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using BranchMicrosoft.Extensions.Caching.Hybrid.Tests.Protos;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Caching.Hybrid.Tests;

/// <summary>
/// Validate usage of custom serializers and serializer factories (in this case: Google.Protobuf)
/// </summary>
public class SerializerTests(ITestOutputHelper Log)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomSerializer(bool useSerializerFactory)
    {
        // note this test uses a Google.Protobuf type *alongside*
        // an unrelated POCO type, to illustrate that the POCO
        // type is unaffected; multiple serialization setups
        // are possible in tandem, including multiple serializer factories;
        // side note: adding a factory is interpreted as "overrides", so
        // in the case of multiple factories claiming a type, the last-added
        // factory wins (or rather: the factories are processed in last-to-first
        // order and the "first" to claim a type in that reverse order: wins)

        var svc = new ServiceCollection();
        if (useSerializerFactory)
        {
            // serializer factory configuration (auto-detect *all*
            // Google.Protobuf types)
            svc.AddHybridCache().AddGoogleProtobuf();
        }
        else
        {
            // single serializer configuration (only applies to
            // this specific message type)
            svc.AddHybridCache().AddGoogleProtobuf<SomeMessage>();
        }
        svc.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = "127.0.0.1:6379";
        });
        //svc.AddDummyL2();
        using var provider = svc.BuildServiceProvider();

        // normally this would be injected simply by having a
        // HybridCache parameter on a service type
        var cache = provider.GetRequiredService<HybridCache>();

        int id = 42;

        // reset
        await cache.RemoveKeysAsync([$"Simple_{id}", $"Protobuf_{id}"]);

        // fetch via HybridCache
        var simpleMessage = await cache.GetOrCreateAsync(
           $"Simple_{id}", // key
           token => GetSomeSimpleMessageAsync(id, token) // factory
       );
        Log.WriteLine(simpleMessage.Id.ToString(CultureInfo.InvariantCulture));
        Log.WriteLine(simpleMessage.Name);

        var protobufMessage = await cache.GetOrCreateAsync(
            $"Protobuf_{id}", // key
            token => GetSomeProtobufMessageAsync(id, token) // factory
        );
        Log.WriteLine(protobufMessage.Id.ToString(CultureInfo.InvariantCulture));
        Log.WriteLine(protobufMessage.Name);

        // fetch via alternative HybridCache API (reduced alloc)
        simpleMessage = await cache.GetOrCreateAsync(
           $"Simple_{id}", // key
           id, // state
           static (id, token) => GetSomeSimpleMessageAsync(id, token) // factory
       );
        Log.WriteLine(simpleMessage.Id.ToString(CultureInfo.InvariantCulture));
        Log.WriteLine(simpleMessage.Name);

        protobufMessage = await cache.GetOrCreateAsync(
            $"Protobuf_{id}", // key
            id, // state
            static (id, token) => GetSomeProtobufMessageAsync(id, token) // factory
        );
        Log.WriteLine(protobufMessage.Id.ToString(CultureInfo.InvariantCulture));
        Log.WriteLine(protobufMessage.Name);

        // inspect what is in L2, to check protobuf happened
        var l2 = provider.GetRequiredService<IDistributedCache>();
        var bytes = await l2.GetAsync($"Simple_{id}") ?? [];
        Log.WriteLine("simple:");
        Log.WriteLine(Encoding.UTF8.GetString(bytes));
        var hex = BitConverter.ToString(bytes);
        Log.WriteLine(hex);
        Assert.Equal("7B-22-49-64-22-3A-34-32-2C-22-4E-61-6D-65-22-3A-22-61-62-63-22-7D", hex);
        Log.WriteLine("");

        bytes = await l2.GetAsync($"Protobuf_{id}") ?? [];
        Log.WriteLine("protobuf:");
        Log.WriteLine(Encoding.UTF8.GetString(bytes));
        hex = BitConverter.ToString(bytes);
        Log.WriteLine(hex);
        Assert.Equal("08-2A-12-03-61-62-63", hex);
        Log.WriteLine("");

        /* sample output:

            42
            abc
            42
            abc
            42
            abc
            42
            abc
            simple:
            {"Id":42,"Name":"abc"}    <=== i.e. "JSON"
            7B-22-49-64-22-3A-34-32-2C-22-4E-61-6D-65-22-3A-22-61-62-63-22-7D

            protobuf:
            \x08*\x12\x03abc   <=== i.e. "not JSON"
            08-2A-12-03-61-62-63

         */
    }

    // this is a simple POCO to contrast with SomeMessage which is generated from sample.proto
    // - we expect that SimpleMessage will continue using the inbuilt JSON serialization
    public class SimpleMessage
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // fake underlying data methods
    private static ValueTask<SomeMessage> GetSomeProtobufMessageAsync(int id, CancellationToken token)
        => new(new SomeMessage { Id = id, Name = "abc" });

    private static ValueTask<SimpleMessage> GetSomeSimpleMessageAsync(int id, CancellationToken token)
        => new(new SimpleMessage { Id = id, Name = "abc" });
}
