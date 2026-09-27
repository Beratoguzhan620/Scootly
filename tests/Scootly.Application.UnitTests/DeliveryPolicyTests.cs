using Scootly.Application.IntegrationEvents;
using Xunit;

namespace Scootly.Application.UnitTests;

/// <summary>65. gun — "bir mesaj kac kez denenmeli ve sonra ne olmali".</summary>
public sealed class DeliveryPolicyTests
{
    [Fact]
    public void Gecici_Hata_Uc_Denemede_Olu_Kuyruga_Duser()
    {
        Assert.Equal(DeliveryOutcome.Retry, DeliveryPolicy.OnFailure(FailureKind.Transient, 1));
        Assert.Equal(DeliveryOutcome.Retry, DeliveryPolicy.OnFailure(FailureKind.Transient, 2));
        Assert.Equal(DeliveryOutcome.DeadLetter, DeliveryPolicy.OnFailure(FailureKind.Transient, 3));
        Assert.Equal(3, DeliveryPolicy.MaxAttempts);
    }

    [Theory]
    [InlineData(FailureKind.Poison)]
    [InlineData(FailureKind.Rejected)]
    public void Kalici_Hata_Hic_Yeniden_Denenmez(FailureKind tur)
    {
        // Bozuk bir mesaji uc kez denemek ayni sonucu uc kez almak ve
        // arkadaki saglikli mesajlari uc kez bekletmek demek.
        Assert.Equal(DeliveryOutcome.DeadLetter, DeliveryPolicy.OnFailure(tur, 1));
    }

    [Fact]
    public void Deneme_Numarasi_Birden_Baslar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryPolicy.OnFailure(FailureKind.Transient, 0));
    }

    [Fact]
    public void Yeniden_Deneme_Hemen_Degil_Beklemeyle_Yapilir()
    {
        Assert.True(DeliveryPolicy.RetryDelay >= TimeSpan.FromSeconds(1));
    }
}
