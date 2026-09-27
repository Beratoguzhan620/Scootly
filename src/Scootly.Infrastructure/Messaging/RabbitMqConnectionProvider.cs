using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Scootly.Infrastructure.Messaging;

/// <summary>
/// Uygulama boyunca TEK bir RabbitMQ bağlantısı (61. gün).
/// </summary>
/// <remarks>
/// <para>
/// Bağlantı pahalı (TCP + kimlik doğrulama + AMQP el sıkışması), kanal ucuz.
/// Doğru kullanım: süreç başına bir bağlantı, iş başına bir kanal. Her
/// yayınlamada yeni bağlantı açmak, 50. günde Redis için gördüğümüz bağlantı
/// tükenmesinin aynısı olurdu.
/// </para>
/// <para>
/// <b>Kütüphanenin otomatik yeniden bağlanması KAPALI.</b> Açık olsaydı iki
/// mekanizma aynı işi yapardı: kütüphane kanalı ve tüketiciyi arka planda
/// geri getirir, bizim tüketici servisimiz de "kanal kapandı" görüp yenisini
/// açardı — sonuç aynı kuyrukta iki tüketici. Yeniden bağlanma tek yerde,
/// tüketici servisinin döngüsünde.
/// </para>
/// </remarks>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private IConnection? _baglanti;

    public RabbitMqConnectionProvider(RabbitMqOptions options, ILogger<RabbitMqConnectionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            ClientProvidedName = options.ClientName,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds),
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false
        };
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var mevcut = _baglanti;
        if (mevcut is { IsOpen: true })
            return mevcut;

        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (_baglanti is { IsOpen: true })
                return _baglanti;

            if (_baglanti is not null)
            {
                _logger.LogWarning("RabbitMQ baglantisi kapanmis, yeniden aciliyor.");
                await GuvenliKapatAsync(_baglanti);
            }

            _baglanti = await _factory.CreateConnectionAsync(cancellationToken);

            _logger.LogInformation(
                "RabbitMQ'ya baglanildi: {Host}:{Port} ({Istemci})",
                _factory.HostName, _factory.Port, _factory.ClientProvidedName);

            return _baglanti;
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_baglanti is not null)
            await GuvenliKapatAsync(_baglanti);

        _kilit.Dispose();
    }

    private static async Task GuvenliKapatAsync(IConnection baglanti)
    {
        try
        {
            await baglanti.DisposeAsync();
        }
        catch (Exception)
        {
            // Zaten kopmus bir baglantiyi kapatmak hata verebilir; burada
            // yapilacak bir sey yok, yenisi aciliyor.
        }
    }
}
