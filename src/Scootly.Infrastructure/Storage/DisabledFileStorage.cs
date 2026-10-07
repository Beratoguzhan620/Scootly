using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Storage;

/// <summary>Nesne depolama kapalıyken kullanılır; hiçbir işlem yapmaz, çağrılırsa hata verir.</summary>
public sealed class DisabledFileStorage : IFileStorage
{
    public bool IsEnabled => false;

    public Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Nesne depolama kapalı (Storage:Enabled=false).");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Nesne depolama kapalı (Storage:Enabled=false).");

    public string CreateDownloadUrl(string objectKey, TimeSpan lifetime)
        => throw new InvalidOperationException("Nesne depolama kapalı (Storage:Enabled=false).");
}
