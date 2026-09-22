using Scootly.Domain.Common;

namespace Scootly.Domain.Pricing;

/// <summary>
/// Bir para tutarı ve para birimi, ayrılmaz biçimde birlikte.
/// </summary>
/// <remarks>
/// <para>
/// Tutarı çıplak bir <c>decimal</c> olarak taşımak, iki farklı para biriminin
/// toplanmasını derleme zamanında mümkün kılar: <c>tryTutar + euroTutar</c>
/// derlenir, çalışır ve yanlış bir sayı üretir. Bu tip o toplamayı çalışma
/// zamanında reddediyor.
/// </para>
/// <para>
/// <c>decimal</c> seçimi — <c>double</c> değil — 32. günde <c>Ride.Fare</c>
/// için verilen kararla aynı: ikili kayan nokta ondalık kesirleri tam
/// gösteremez ve hata fatura toplamlarında birikir.
/// </para>
/// </remarks>
public sealed class Money : ValueObject
{
    public const string VarsayilanParaBirimi = "TRY";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = VarsayilanParaBirimi)
    {
        if (amount < 0)
        {
            throw new DomainException("Tutar negatif olamaz.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new DomainException("Para birimi üç harfli ISO 4217 kodu olmalı.");
        }

        Amount = amount;
        // Ordinal büyütme: kültüre duyarlı ToUpper, Türkçe yerelinde "i"yi
        // noktalı "İ"ye çevirir ve "TRY" dışındaki kodlarda sessizce farklı
        // bir metin üretirdi.
        Currency = currency.ToUpperInvariant();
    }

    public static Money Sifir(string currency = VarsayilanParaBirimi) => new(0m, currency);

    public Money Plus(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                $"Farklı para birimleri toplanamaz: {Currency} ve {other.Currency}.");
        }

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Times(decimal multiplier)
    {
        if (multiplier < 0)
        {
            throw new DomainException("Çarpan negatif olamaz.");
        }

        return new Money(Amount * multiplier, Currency);
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
