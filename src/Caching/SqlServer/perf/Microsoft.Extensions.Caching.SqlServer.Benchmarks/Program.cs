using BenchmarkDotNet.Running;
using Microsoft.Extensions.Caching.SqlServer.Benchmarks;

#if DEBUG
using var obj = new SqlServerDistributedCacheBenchmarks();
obj.GlobalSetup();
Console.WriteLine(obj.GetSingleRandom());
Console.WriteLine(obj.GetSingleFixed());
Console.WriteLine(await obj.GetSingleRandomAsync());
Console.WriteLine(await obj.GetSingleFixedAsync());
#else
BenchmarkRunner.Run(typeof(SqlServerDistributedCacheBenchmarks).Assembly, args: args);
#endif
