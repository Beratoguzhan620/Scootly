using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.UnitTests;

/// <summary>
/// Testlerde zamanı sabitlemek için. Gerçek saat kullanılsaydı, süre dolmasına
/// bağlı davranışı sınamanın tek yolu testte beklemek olurdu.
/// </summary>
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
