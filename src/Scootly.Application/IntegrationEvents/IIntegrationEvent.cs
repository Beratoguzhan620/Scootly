namespace Scootly.Application.IntegrationEvents;

/// <summary>
/// Sistem sınırının DIŞINA taşınan olay (63. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Domain olayı ile farkı.</b> <c>RideCompletedEvent</c> bir aggregate'in
/// kendi içinde "bir şey oldu" demesi; alan modeli değiştikçe o da değişebilir.
/// Entegrasyon olayı ise başka bir sürecin — bugün Worker, yarın başka bir
/// servis — okuyacağı bir <b>sözleşme</b>. Domain olayını doğrudan kuyruğa
/// koysaydık, <c>Ride</c>'a eklenen her iç alan dışarı sızar ve alan modelini
/// değiştirmek kuyruğun öbür ucundaki tüketiciyi kırardı.
/// </para>
/// <para>
/// <see cref="EventId"/> her olayda var, çünkü en az bir kez teslim edilen bir
/// sistemde aynı mesaj iki kez gelecek (68. gün). Onu tanımanın tek yolu
/// kimliği.
/// </para>
/// </remarks>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }

    /// <summary>
    /// JSON'dan okunan olayın anlamlı olup olmadığını denetler; değilse
    /// <see cref="InvalidIntegrationEventException"/> fırlatır.
    /// </summary>
    void Validate();
}

/// <summary>
/// Olayın yönlendirme anahtarı — örnek: <c>ride.completed</c>.
/// </summary>
/// <remarks>
/// <para>
/// Anahtar bir örneğin değil TİPİN özelliği, bu yüzden statik. Statik soyut
/// üye olması, yayınlayıcının ve tüketicinin anahtarı aynı yerden okumasını
/// derleyiciye garanti ettiriyor — iki yerde elle yazılmış iki metin er geç
/// ayrışır ve o gün mesajlar hiçbir kuyruğa düşmez, sessizce.
/// </para>
/// <para>
/// <see cref="IIntegrationEvent"/>'ten ayrı bir arayüz olmasının sebebi dilin
/// bir kuralı: statik soyut üyesi olan bir arayüz tip argümanı olarak
/// kullanılamıyor (<c>List&lt;IIntegrationEvent&gt;</c> derlenmezdi). Ayrı
/// olunca "herhangi bir entegrasyon olayı" listesi yine yazılabiliyor —
/// 66. günün outbox'ı tam olarak buna ihtiyaç duyacak.
/// </para>
/// </remarks>
public interface IHasEventName
{
    static abstract string EventName { get; }
}
