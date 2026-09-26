using System.Diagnostics;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class ParallelProcessingTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Siralii_Vs_Paralel_Isleme_Karsilastirmasi(int chunkSize)
    {
        var data = Enumerable.Range(0, 10_000).ToList();

        var sequentialStopwatch = Stopwatch.StartNew();
        foreach (var chunk in data.Chunk(chunkSize))
        {
            foreach (var item in chunk)
            {
                SimulateWork(item);
            }
        }
        sequentialStopwatch.Stop();

        var parallelStopwatch = Stopwatch.StartNew();
        Parallel.ForEach(data.Chunk(chunkSize), chunk =>
        {
            foreach (var item in chunk)
            {
                SimulateWork(item);
            }
        });
        parallelStopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"Parça boyutu: {chunkSize} | Sıralı: {sequentialStopwatch.ElapsedMilliseconds} ms | " +
            $"Paralel: {parallelStopwatch.ElapsedMilliseconds} ms");
    }

    private static void SimulateWork(int item)
    {
        // 10.000 kayıtlık telemetri işleme benzeri, ölçülebilir ağırlıkta bir CPU işi simülasyonu
        var hash = item.GetHashCode();
        for (var i = 0; i < 50_000; i++)
        {
            hash = (hash * 31) + i;
            hash ^= hash >> 13;
        }
    }
}