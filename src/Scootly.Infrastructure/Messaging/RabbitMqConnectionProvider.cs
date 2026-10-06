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

            // Baglanti kapali ama kutuphane onu kurtarmaya calisiyorsa (ag/broker kesintisi), burada dispose
            // etmek kurtarmayi oldurur ve baglantiyi tutan tuketicileri olu kanalda birakir (106. gun bulgusu).
            // Yalnizca uygulamanin kendi kapattigi baglanti yeniden kurulur.
            if (_connection is not null && IsRecovering(_connection))
                throw new InvalidOperationException("RabbitMQ baglantisi kesildi; otomatik kurtarma suruyor.");

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
