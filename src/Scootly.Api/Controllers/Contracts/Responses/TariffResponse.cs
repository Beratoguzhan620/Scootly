namespace Scootly.Api.Contracts.Responses;

/// <summary>Aktif tarifenin HTTP yanıtı.</summary>
/// <remarks>
/// Para birimi tek alanda, iki kez değil: alan modelinde her iki ücret de
/// kendi para birimini taşıyor ama <c>Tariff</c> yapıcısı ikisinin eşit
/// olmasını zorunlu kılıyor. Sözleşmede tekrarlamak, istemciye aslında
/// olmayan bir esneklik vaat etmek olurdu.
/// </remarks>
public sealed record TariffResponse(
    Guid Id,
    string Name,
    decimal UnlockAmount,
    decimal PerMinuteAmount,
    string Currency);
