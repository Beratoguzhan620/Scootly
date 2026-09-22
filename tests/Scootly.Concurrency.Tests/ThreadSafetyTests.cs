using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class ThreadSafetyTests
{
    private int _unsafeCounter;
    private int _safeCounter;

    [Fact]
    public async Task Guvensiz_Artirma_Paralel_Yukte_Yanlis_Sonuc_Verebilir()
    {
        _unsafeCounter = 0;

        var tasks = Enumerable.Range(0, 1000).Select(_ => Task.Run(() =>
        {
            var temp = _unsafeCounter;
            Thread.Sleep(0);
            _unsafeCounter = temp + 1;
        }));

        await Task.WhenAll(tasks);

        throw new Xunit.Sdk.XunitException(
            $"1000 paralel artırma sonrası GÜVENSİZ sayaç: {_unsafeCounter} (beklenen: 1000)");
    }

    [Fact]
    public async Task Interlocked_Ile_Artirma_Her_Zaman_Dogru_Sonuc_Vermeli()
    {
        _safeCounter = 0;

        var tasks = Enumerable.Range(0, 1000).Select(_ => Task.Run(() =>
        {
            Interlocked.Increment(ref _safeCounter);
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(1000, _safeCounter);
    }
}