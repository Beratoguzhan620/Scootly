using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Storage;

/// <summary>Nesne depolama kapaliyken kullanilir; hicbir islem yapmaz, cagrilirsa hata verir.</summary>
public sealed class DisabledFileStorage : IFileStorage
{
    public bool IsEnabled => false;

    public Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Nesne depolama kapali (Storage:Enabled=false).");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Nesne depolama kapali (Storage:Enabled=false).");

    public string CreateDownloadUrl(string objectKey, TimeSpan lifetime)
        => throw new InvalidOperationException("Nesne depolama kapali (Storage:Enabled=false).");
}
