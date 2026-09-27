using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;
using Scootly.Infrastructure.Messaging.Outbox;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Infrastructure.Tests;

[Collection(InfrastructureCollection.Name)]
public sealed class MessagingTests
{
    private readonly InfrastructureFixture _fixture;

    public MessagingTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Her zaman verilen sonucu döndüren ve teslimat sayısını tutan test tüketicisi.</summary>
    private sealed class ScriptedConsumer : RabbitMqConsumerService
    {
        private readonly ConsumeResult _result;
        private int _deliveries;

        public ScriptedConsumer(IServiceProvider services, string queueName, string routingKey, ConsumeResult result)
            : base(
                services.GetRequiredService<RabbitMqConnectionProvider>(),
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IOptions<MessagingOptions>>(),
                services.GetRequiredService<ILoggerFactory>().CreateLogger<ScriptedConsumer>())
        {
            QueueName = queueName;
            RoutingKeys = [routingKey];
            _result = result;
        }

        protected override string QueueName { get; }

        protected override IReadOnlyCollection<string> RoutingKeys { get; }

        public int Deliveries => Volatile.Read(ref _deliveries);

        protected override Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _deliveries);
            return Task.FromResult(_result);
        }
    }

    private async Task<IChannel> OpenChannelAsync()
    {
        var connection = await _fixture.Services.GetRequiredService<RabbitMqConnectionProvider>().GetConnectionAsync();
        return await connection.CreateChannelAsync();
    }

    private async Task PublishAsync(string routingKey, string body)
    {
        await using var channel = await OpenChannelAsync();
        await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true);
        await channel.BasicPublishAsync(
            MessagingTopology.EventsExchange,
            routingKey,
            mandatory: false,
            new BasicProperties { MessageId = Guid.NewGuid().ToString(), Persistent = true },
            Encoding.UTF8.GetBytes(body));
    }

    private async Task<uint> MessageCountAsync(string queue)
    {
        await using var channel = await OpenChannelAsync();
        return await channel.MessageCountAsync(queue);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Beklenen durum zaman aşımı içinde oluşmadı.");

            await Task.Delay(100);
        }
    }

    private async Task<(ScriptedConsumer Consumer, string Queue, string RoutingKey)> StartConsumerAsync(ConsumeResult result)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var queue = $"test.consumer.{suffix}";
        var routingKey = $"Test.{suffix}";

        var consumer = new ScriptedConsumer(_fixture.Services, queue, routingKey, result);
        await consumer.StartAsync(CancellationToken.None);

        // Kuyruk ve bağlamalar oluşana kadar bekle.
        await WaitUntilAsync(async () =>
        {
            try
            {
                await MessageCountAsync(queue);
                return true;
            }
            catch
            {
                return false;
            }
        }, TimeSpan.FromSeconds(15));

        return (consumer, queue, routingKey);
    }

    [Fact]
    public async Task Surekli_Basarisiz_Mesaj_Gecikmeli_Yeniden_Denenip_DLQya_Tasinmali()
    {
        var (consumer, queue, routingKey) = await StartConsumerAsync(ConsumeResult.Retry);

        try
        {
            await PublishAsync(routingKey, """{"test":1}""");

            var deadLetterQueue = MessagingTopology.DeadLetterQueueFor(queue);
            await WaitUntilAsync(async () => await MessageCountAsync(deadLetterQueue) == 1, TimeSpan.FromSeconds(20));

            // İlk teslimat + MaxRetryAttempts yeniden deneme.
            Assert.Equal(1 + InfrastructureFixture.MaxRetryAttempts, consumer.Deliveries);
            Assert.Equal(0u, await MessageCountAsync(queue));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Kalici_Hata_Yeniden_Denenmeden_DLQya_Tasinmali()
    {
        var (consumer, queue, routingKey) = await StartConsumerAsync(ConsumeResult.DeadLetter);

        try
        {
            await PublishAsync(routingKey, "bozuk içerik");

            await WaitUntilAsync(async () => await MessageCountAsync(MessagingTopology.DeadLetterQueueFor(queue)) == 1, TimeSpan.FromSeconds(10));

            Assert.Equal(1, consumer.Deliveries);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Basarili_Mesaj_Onaylanmali_Ve_DLQya_Dusmemeli()
    {
        var (consumer, queue, routingKey) = await StartConsumerAsync(ConsumeResult.Ack);

        try
        {
            await PublishAsync(routingKey, """{"test":2}""");

            await WaitUntilAsync(() => Task.FromResult(consumer.Deliveries == 1), TimeSpan.FromSeconds(10));
            await Task.Delay(InfrastructureFixture.RetryDelayMilliseconds * 3);

            Assert.Equal(1, consumer.Deliveries);
            Assert.Equal(0u, await MessageCountAsync(queue));
            Assert.Equal(0u, await MessageCountAsync(MessagingTopology.DeadLetterQueueFor(queue)));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Outbox_Mesajlari_Kalici_Olarak_Yayinlanmali_Ve_Islendi_Isaretlenmeli()
    {
        var eventType = $"OutboxTest.{Guid.NewGuid():N}";
        var queue = $"test.outbox.{Guid.NewGuid():N}";

        await using (var channel = await OpenChannelAsync())
        {
            await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true);
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue, MessagingTopology.EventsExchange, eventType);
        }

        var messageId = Guid.NewGuid();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
            db.OutboxMessages.Add(new OutboxMessage(messageId, eventType, """{"hello":"world"}""", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        using (var scope = _fixture.Services.CreateScope())
        {
            var published = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().PublishPendingAsync(CancellationToken.None);
            Assert.True(published >= 1);
        }

        await using (var channel = await OpenChannelAsync())
        {
            var delivered = await channel.BasicGetAsync(queue, autoAck: true);

            Assert.NotNull(delivered);
            Assert.Equal(messageId.ToString(), delivered.BasicProperties.MessageId);
            Assert.True(delivered.BasicProperties.Persistent);
            Assert.Equal("""{"hello":"world"}""", Encoding.UTF8.GetString(delivered.Body.Span));
        }

        using (var scope = _fixture.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<ScootlyDbContext>()
                .OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId);

            Assert.NotNull(stored.ProcessedAt);
            Assert.Equal(1, stored.Attempts);
        }
    }

    [Fact]
    public async Task Ayni_Anda_Calisan_Iki_Yayinci_Ayni_Mesaji_Iki_Kez_Yayinlamamali()
    {
        var eventType = $"OutboxRace.{Guid.NewGuid():N}";
        var queue = $"test.outbox-race.{Guid.NewGuid():N}";

        await using (var channel = await OpenChannelAsync())
        {
            await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true);
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue, MessagingTopology.EventsExchange, eventType);
        }

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            for (var i = 0; i < 20; i++)
                db.OutboxMessages.Add(new OutboxMessage(Guid.NewGuid(), eventType, $$"""{"n":{{i}}}""", DateTime.UtcNow));

            await db.SaveChangesAsync();
        }

        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = _fixture.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

            while (await processor.PublishPendingAsync(CancellationToken.None) > 0)
            {
            }
        }));

        Assert.Equal(20u, await MessageCountAsync(queue));
    }
}
