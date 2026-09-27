using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Payments;

/// <summary>
/// Geçici ödeme sağlayıcısı (69. gün). Gerçek simülatör 71. günde.
/// </summary>
/// <remarks>
/// <para>
/// Kararı rastgele değil <b>kuralla</b> veriyor: <see cref="PaymentOptions.FakeDeclineAbove"/>
/// üstündeki tutarlar reddediliyor. Rastgele bir sahte, telafi yolunu test
/// etmek istediğinde onu tutturmayı şansa bırakırdı.
/// </para>
/// <para>
/// Tekrar anahtarını gerçek bir sağlayıcı gibi ele alıyor: aynı anahtarla
/// gelen ikinci istek yeni bir karar değil, ilk kararı alıyor. Bellekte
/// tuttuğu için süreç yeniden başlayınca unutuyor — sahte olmasının bedeli.
/// </para>
/// </remarks>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly PaymentOptions _options;
    private readonly ILogger<FakePaymentGateway> _logger;
    private readonly ConcurrentDictionary<Guid, PaymentAuthorizationResult> _kararlar = new();

    public FakePaymentGateway(PaymentOptions options, ILogger<FakePaymentGateway> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var karar = _kararlar.GetOrAdd(request.IdempotencyKey, _ =>
            request.Amount > _options.FakeDeclineAbove
                ? new PaymentAuthorizationResult(false, $"Limit asildi (sahte saglayici, sinir {_options.FakeDeclineAbove}).")
                : new PaymentAuthorizationResult(true, null));

        _logger.LogInformation(
            "Sahte odeme: {Key} tutar={Tutar} -> {Sonuc}",
            request.IdempotencyKey, request.Amount, karar.Approved ? "ONAY" : "RET");

        return Task.FromResult(karar);
    }
}
