using Scootly.Domain.Common;

namespace Scootly.Domain.Telemetry;

/// <summary>
/// Telemetri gönderen cihazın kimliği.
/// </summary>
/// <remarks>
/// <para>
/// <b>Plandan sapma ve gerekçesi.</b> 51. günün tarifi bu tipin tek bir
/// <c>Guid</c> taşımasını söylüyor. Metin (string) seçildi, çünkü bu sistemde
/// cihaz kimliği zaten var: 25. günde yazılan <c>device_credentials</c>
/// tablosunun anahtarı 64 karakterlik bir metin ve cihazlar token'larını o
/// kimlikle alıyor.
/// </para>
/// <para>
/// İkinci bir Guid kimlik tanımlamak, aynı fiziksel scooter için birbirine
/// çevrilemeyen iki kimlik üretirdi: token'daki kimlik ile telemetrideki kimlik
/// eşleşmez, "bu telemetriyi gerçekten bu cihaz mı gönderdi" sorusu
/// cevaplanamaz hale gelirdi. Kimliği ikiye bölmek, birleştirmekten her zaman
/// daha pahalıdır.
/// </para>
/// <para>
/// Uzunluk sınırı burada, alan modelinde. Veritabanı kısıtı (64 karakter)
/// ikinci savunma; ilki bu, çünkü geçersiz bir kimliğin veritabanına kadar
/// gitmemesi gerekiyor.
/// </para>
/// </remarks>
public sealed class DeviceId : ValueObject
{
    public const int MaxLength = 64;

    public string Value { get; }

    public DeviceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Cihaz kimliği boş olamaz.");
        }

        if (value.Length > MaxLength)
        {
            throw new DomainException($"Cihaz kimliği en fazla {MaxLength} karakter olabilir.");
        }

        Value = value;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordinal karşılaştırma bilinçli: kültüre duyarlı karşılaştırma
        // Türkçe yerelinde "I" harfini "ı"ya çevirir ve iki farklı cihaz
        // kimliği aynı sayılabilir. 23. günde yetkilendirmede öğrenilen ders
        // burada da geçerli — kimlik karşılaştırması asla kültüre duyarlı
        // olmamalı.
        yield return Value;
    }
}
