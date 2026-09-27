namespace Scootly.Application.IntegrationEvents;

/// <summary>
/// Okunabilen ama anlamsız bir mesaj — örneğin boş bir sürüş kimliği.
/// </summary>
/// <remarks>
/// Zehirli mesaj (65. gün) sayılıyor: kaç kez denenirse denensin aynı sonuç
/// çıkar, bu yüzden yeniden denenmeden ölü mektup kuyruğuna gider.
/// </remarks>
public sealed class InvalidIntegrationEventException : Exception
{
    public InvalidIntegrationEventException(string message) : base(message) { }
}
