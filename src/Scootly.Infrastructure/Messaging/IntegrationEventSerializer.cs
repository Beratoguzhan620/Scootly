using System.Text.Json;

namespace Scootly.Infrastructure.Messaging;

/// <summary>Mesaj gövdesinin JSON biçimi — yayınlayıcı ve tüketici aynısını kullanıyor.</summary>
/// <remarks>
/// <c>RespectRequiredConstructorParameters</c> açık: varsayılan davranışta
/// eksik bir alan hata vermez, sessizce <c>Guid.Empty</c> ya da <c>0</c> olur.
/// "RideId alanı yok" diye gelen bozuk bir mesaj böylece "Guid.Empty numaralı
/// sürüş" diye işlenmeye çalışılırdı. Açıkken eksik alan <c>JsonException</c>
/// fırlatıyor ve mesaj zehirli olarak ayrılıyor (65. gün).
/// </remarks>
public static class IntegrationEventSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public static byte[] Serialize<T>(T integrationEvent) =>
        JsonSerializer.SerializeToUtf8Bytes(integrationEvent, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<T>(body, Options)
        ?? throw new JsonException("Mesaj govdesi bos (null).");
}
