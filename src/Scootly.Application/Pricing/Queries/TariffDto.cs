namespace Scootly.Application.Pricing.Queries;

/// <summary>Aktif tarifenin okunabilir hali.</summary>
/// <remarks>
/// Bu tip <b>serileştirilebilir olmak zorunda</b>: 47. günden itibaren Redis'e
/// JSON olarak yazılıyor. Alan modelindeki <c>Tariff</c> serileştirilseydi,
/// ona eklenen her iç alan sessizce önbelleğe ve oradan da istemciye sızardı —
/// ve önbellekteki eski biçimli JSON, tip değiştiği gün okunamaz hale gelirdi.
/// </remarks>
public sealed record TariffDto(
    Guid Id,
    string Name,
    decimal UnlockAmount,
    decimal PerMinuteAmount,
    string Currency);
