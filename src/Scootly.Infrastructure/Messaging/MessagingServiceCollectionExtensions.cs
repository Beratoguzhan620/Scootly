using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// RabbitMQ bağlantısını ve yayınlayıcıyı kaydeder. API de Worker da bunu
    /// çağırıyor; iki <c>Program.cs</c>'de iki ayrı liste ayrışırdı.
    /// </summary>
    /// <remarks>
    /// Ayarlar eksikse (parola yok) burada, uygulama AÇILIRKEN hata veriyor.
    /// Sessizce açılıp ilk sürüş bitiminde "yayınlanamadı" logu düşmek, sorunu
    /// saatler sonra ve yanlış yerde gösterirdi.
    /// </remarks>
    public static IServiceCollection AddScootlyMessaging(this IServiceCollection services, RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<RabbitMqConnectionProvider>();
        // Ayni ornek iki adla: outbox gondericisi somut tipi (PublishRawAsync),
        // digerleri arayuzu kullaniyor. Iki ayri kayit iki ayri kanal acardi.
        services.AddSingleton<RabbitMqEventPublisher>();
        services.AddSingleton<IEventPublisher>(p => p.GetRequiredService<RabbitMqEventPublisher>());

        return services;
    }
}
