using System.Diagnostics;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>ADR 0014: sıralı ve paralel CPU işi karşılaştırması (ölçüm).</summary>
[Trait(TestCategories.Key, TestCategories.Measurement)]
public sealed class ParallelProcessingTests
{
    private readonly ITestOutputHelper _output;

    public ParallelProcessingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Sirali_Vs_Paralel_Isleme_Karsilastirmasi(int chunkSize)
    {
        var data = Enumerable.Range(0, 10_000).ToList();

        var sequentialStopwatch = Stopwatch.StartNew();
        var sequentialChecksum = data.Chunk(chunkSize).SelectMany(chunk => chunk).Sum(item => (long)SimulateWork(item));
        sequentialStopwatch.Stop();

        long parallelChecksum = 0;
        var parallelStopwatch = Stopwatch.StartNew();
        Parallel.ForEach(data.Chunk(chunkSize), chunk =>
        {
            var local = chunk.Sum(item => (long)SimulateWork(item));
            Interlocked.Add(ref parallelChecksum, local);
        });
        parallelStopwatch.Stop();

        _output.WriteLine(
            $"Parça boyutu {chunkSize}: sıralı {sequentialStopwatch.ElapsedMilliseconds} ms, " +
            $"paralel {parallelStopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(sequentialChecksum, parallelChecksum);
    }

    private static int SimulateWork(int item)
    {
        // Telemetri işleme benzeri, ölçülebilir ağırlıkta bir CPU işi simülasyonu.
        var hash = item.GetHashCode();
        for (var i = 0; i < 50_000; i++)
        {
            hash = (hash * 31) + i;
            hash ^= hash >> 13;
        }

        return hash;
    }
}
