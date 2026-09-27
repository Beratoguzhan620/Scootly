using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>.Result ile senkron bloklama ile doğru await kullanımının karşılaştırması (ölçüm).</summary>
[Trait(TestCategories.Key, TestCategories.Measurement)]
public sealed class AsyncMisuseTests : IClassFixture<ConcurrencyTestFactory>
{
    private const int ParallelCalls = 30;

    private readonly ConcurrencyTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public AsyncMisuseTests(ConcurrencyTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

#pragma warning disable xUnit1031 // Bu test, bloklayan çağrının kendisini göstermek için yazıldı.
    [Fact]
    public void Yanlis_Kullanim_Result_Ile_Senkron_Cagirma_Yavas_Olabilir()
    {
        _factory.SeedVehicleAsync().GetAwaiter().GetResult();

        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, ParallelCalls).Select(_ => Task.Run(() =>
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            // YANLIŞ KULLANIM: .Result ile async metodu senkron çağırmak iş parçacığını bloklar.
            return dbContext.Vehicles.CountAsync().Result;
        })).ToArray();

        Task.WaitAll(tasks);
        stopwatch.Stop();

        _output.WriteLine($"YANLIŞ (.Result ile bloklama): {ParallelCalls} paralel çağrı {stopwatch.ElapsedMilliseconds} ms");
        Assert.All(tasks, t => Assert.True(t.Result > 0));
    }
#pragma warning restore xUnit1031

    [Fact]
    public async Task Dogru_Kullanim_Await_Ile_Cagirma()
    {
        await _factory.SeedVehicleAsync();

        var stopwatch = Stopwatch.StartNew();

        var counts = await Task.WhenAll(Enumerable.Range(0, ParallelCalls).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            // DOĞRU KULLANIM: await ile iş parçacığı bloklanmaz, havuza geri döner.
            return await dbContext.Vehicles.CountAsync();
        }));

        stopwatch.Stop();

        _output.WriteLine($"DOĞRU (await): {ParallelCalls} paralel çağrı {stopwatch.ElapsedMilliseconds} ms");
        Assert.All(counts, count => Assert.True(count > 0));
    }
}
