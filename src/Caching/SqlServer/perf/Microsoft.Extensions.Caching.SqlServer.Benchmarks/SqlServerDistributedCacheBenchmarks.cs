// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Caching.SqlServer.Benchmarks;

[MemoryDiagnoser]
public class SqlServerDistributedCacheBenchmarks : IDisposable
{
    public void Dispose() => serviceProvider.Dispose();

    private readonly ServiceProvider serviceProvider;
    private readonly IDistributedCache cache;
    private readonly Random random = new Random();
    private readonly string[] keys;

    // create a local DB named CacheBench, then
    // dotnet sql-cache create "Data Source=.;Initial Catalog=CacheBench;Integrated Security=True;Trust Server Certificate=True" dbo BenchmarkCache

    public const string ConnectionString = "Data Source=.;Initial Catalog=CacheBench;Integrated Security=True;Trust Server Certificate=True";

    public SqlServerDistributedCacheBenchmarks()
    {
        var services = new ServiceCollection();
        services.AddDistributedSqlServerCache(options =>
        {
            options.TableName = "BenchmarkCache";
            options.SchemaName = "dbo";
            options.ConnectionString = ConnectionString;
        });

        serviceProvider = services.BuildServiceProvider();
        cache = serviceProvider.GetRequiredService<IDistributedCache>();

        keys = new string[10000];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = Guid.NewGuid().ToString();
        }
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        cache.Get(new Guid().ToString()); // touch the DB to ensure table exists
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "truncate table dbo.BenchmarkCache";
        conn.Open();
        cmd.ExecuteNonQuery();

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
        options.SlidingExpiration = Sliding ? TimeSpan.FromMinutes(5) : null;

        var value = new byte[PayloadSize];
        foreach (var key in keys)
        {
            random.NextBytes(value);
            cache.Set(key, value, options);
        }
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public int GetSingleRandom()
    {
        int total = 0;
        for (int i = 0; i < 128; i++)
        {
            total += cache.Get(RandomKey())?.Length ?? 0;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public void GetConcurrentRandom()
    {
        Parallel.For(0, 128, _ =>
        {
            cache.Get(RandomKey());
        });
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public async Task<int> GetSingleRandomAsync()
    {
        int total = 0;
        for (int i = 0; i < 128; i++)
        {
            total += (await cache.GetAsync(RandomKey()))?.Length ?? 0;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public Task GetConcurrentRandomAsync()
    {
        return Parallel.ForAsync(0, 128, (_, ct) => new(cache.GetAsync(RandomKey())));
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public int GetSingleFixed()
    {
        int total = 0;
        for (int i = 0; i < 128; i++)
        {
            total += cache.Get(FixedKey())?.Length ?? 0;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public void GetConcurrentFixed()
    {
        Parallel.For(0, 128, _ =>
        {
            cache.Get(FixedKey());
        });
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public async Task<int> GetSingleFixedAsync()
    {
        int total = 0;
        for (int i = 0; i < 128; i++)
        {
            total += (await cache.GetAsync(FixedKey()))?.Length ?? 0;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 128)]
    public Task GetConcurrentFixedAsync()
    {
        return Parallel.ForAsync(0, 128, (_, ct) => new(cache.GetAsync(FixedKey())));
    }

    private string FixedKey() => keys[42];

    private string RandomKey() => keys[random.Next(keys.Length)];

    [Params(1024)]
    public int PayloadSize { get; set; } =  1024;

    [Params(true, false)]
    public bool Sliding { get; set; } = true;
}
