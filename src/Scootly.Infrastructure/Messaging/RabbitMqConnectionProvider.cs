using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Scootly.Infrastructure.Messaging;

/// <summary>
/// Süreç başına tek, otomatik kurtarmalı (automatic recovery) RabbitMQ bağlantısı.
/// Bağlantı ağ kesintisinden sonra kendiliğinden yeniden kurulur; ilk bağlantı hatası çağırana iletilir.
/// </summary>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options)
    {
        var settings = options.Value;

        _factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            ClientProvidedName = AppDomain.CurrentDomain.FriendlyName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
    }

    public bool IsConnected => _connection is { IsOpen: true };

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            // Bağlantı kapalı ama kütüphane onu kurtarmaya çalışıyorsa (ağ/broker kesintisi), burada dispose
            // etmek kurtarmayı öldürür ve bağlantıyı tutan tüketicileri ölü kanalda bırakır (106. gün bulgusu).
            // Yalnızca uygulamanın kendi kapattığı bağlantı yeniden kurulur.
            if (_connection is not null && IsRecovering(_connection))
                throw new InvalidOperationException("RabbitMQ bağlantısı kesildi; otomatik kurtarma sürüyor.");

            if (_connection is not null)
                await _connection.DisposeAsync();

            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool IsRecovering(IConnection connection)
        => connection.CloseReason is null || connection.CloseReason.Initiator != ShutdownInitiator.Application;

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        _lock.Dispose();
    }
}
