using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

/// <summary>Tüm API testleri tek bir PostgreSQL container'ını ve test sunucusunu paylaşır (her test kendi verisini üretir).</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ScootlyApiFactory>
{
    public const string Name = "api";
}

public static class Eventually
{
    /// <summary>Asenkron (arka plan servisi) bir sonucun gerçekleşmesini zaman aşımıyla bekler.</summary>
    public static async Task<T> WaitAsync<T>(Func<Task<T>> probe, Func<T, bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (true)
        {
            var value = await probe();

            if (condition(value) || DateTime.UtcNow > deadline)
                return value;

            await Task.Delay(100);
        }
    }
}
