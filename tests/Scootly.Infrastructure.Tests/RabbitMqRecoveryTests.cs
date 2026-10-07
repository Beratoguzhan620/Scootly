using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;
using Testcontainers.RabbitMq;
using Xunit;

namespace Scootly.Infrastructure.Tests;

/// <summary>
/// 106. gun bulgusu: RabbitMQ kesintisinde baglanti saglayiciya dokunuldugunda tuketiciler geri gelmiyordu.
/// Bu test kendi RabbitMQ container'ini (sabit port) durdurup baslatir ve kesinti sirasinda
/// GetConnectionAsync cagrilarini (outbox / saglik kontrolu gibi) taklit eder.
/// </summary>
[Trait("Category", "Resilience")]
public sealed class RabbitMqRecoveryTests : IAsyncLifetime
{
    private readonly int _port = GetFreePort();
    private readonly RabbitMqContainer _rabbitMq;
    private ServiceProvider _services = null!;

    public RabbitMqRecoveryTests()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management")
            .WithUsername("scootly")
            .WithPassword("test_sifre")
            .WithPortBinding(_port, 5672)
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        _services = new ServiceCollection().BuildServiceProvider();
        await _rabbitMq.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }

    private sealed class CountingConsumer : RabbitMqConsumerService
    {
        private int _deliveries;

        public CountingConsumer(RabbitMqConnectionProvider provider, IServiceScopeFactory scopes, string queue, string routingKey)
            : base(provider, scopes, Options.Create(new MessagingOptions()), NullLogger.Instance)
        {
            QueueName = queue;
            RoutingKeys = [routingKey];
        }

        protected override string QueueName { get; }

        protected override IReadOnlyCollection<string> RoutingKeys { get; }

        public int Deliveries => Volatile.Read(ref _deliveries);

        protected override Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _deliveries);
            return Task.FromResult(ConsumeResult.Ack);
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<bool> PublishUntilDeliveredAsync(
        ConnectionFactory factory, CountingConsumer consumer, string routingKey, TimeSpan timeout)
    {
        var before = consumer.Deliveries;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var connection = await factory.CreateConnectionAsync(cts.Token);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);
                await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true, cancellationToken: cts.Token);
                await channel.BasicPublishAsync(
                    MessagingTopology.EventsExchange,
                    routingKey,
                    mandatory: false,
                    new BasicProperties { MessageId = Guid.NewGuid().ToString(), Persistent = true },
                    Encoding.UTF8.GetBytes("{}"),
                    cts.Token);
            }
            catch
            {
                // Kesinti sonrasi ilk denemeler basarisiz olabilir; zaman asimina kadar tekrar denenir.
            }

            await Task.Delay(1000);

            if (consumer.Deliveries > before)
                return true;
        }

        return false;
    }

    private async Task RunOutageScenarioAsync(bool kesintideSaglayiciyaDokun)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var queue = $"test.recovery.{suffix}";
        var routingKey = $"Recovery.{suffix}";

        await using var provider = new RabbitMqConnectionProvider(Options.Create(new RabbitMqOptions
        {
            HostName = _rabbitMq.Hostname,
            Port = _port,
            UserName = "scootly",
            Password = "test_sifre"
        }));

        var monitorFactory = new ConnectionFactory
        {
            HostName = _rabbitMq.Hostname,
            Port = _port,
            UserName = "scootly",
            Password = "test_sifre"
        };

        var consumer = new CountingConsumer(provider, _services.GetRequiredService<IServiceScopeFactory>(), queue, routingKey);
        await consumer.StartAsync(CancellationToken.None);

        try
        {
            // 1) Kesintiden önce akış çalışıyor mu? (başarısızlık kesintiye yorulabilsin diye ölçülür)
            Assert.True(
                await PublishUntilDeliveredAsync(monitorFactory, consumer, routingKey, TimeSpan.FromSeconds(30)),
                "Kesintiden ÖNCE mesaj teslim edilemedi; test düzeneği hatalı.");

            // 2) Kesinti + baglanti saglayiciya dokunan cagrilar (outbox/saglik kontrolu taklidi)
            await _rabbitMq.StopAsync();

            var until = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < until)
            {
                if (!kesintideSaglayiciyaDokun)
                {
                    // Kontrol senaryosu: kesintide baglanti saglayiciya hic dokunulmaz.
                    await Task.Delay(500);
                    continue;
                }

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await provider.GetConnectionAsync(cts.Token);
                }
                catch
                {
                    // Beklenen: kesintide baglanti kurulamaz.
                }

                await Task.Delay(500);
            }

            // 3) Broker geri geldi: tüketici kendiliğinden toparlanıp yeni mesaj almalı.
            await _rabbitMq.StartAsync();

            Assert.True(
                await PublishUntilDeliveredAsync(monitorFactory, consumer, routingKey, TimeSpan.FromSeconds(60)),
                "Kesinti sonrası 60 sn içinde tüketici mesaj almadı (tüketici kurtarılamadı).");
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public Task Kesintide_Saglayiciya_Dokunulmazsa_Tuketici_Kurtarilmali()
        => RunOutageScenarioAsync(kesintideSaglayiciyaDokun: false);

    [Fact]
    public Task Kesintide_Saglayiciya_Dokunulsa_Bile_Tuketici_Kurtarilmali()
        => RunOutageScenarioAsync(kesintideSaglayiciyaDokun: true);
}
