namespace Scootly.Application.Abstractions;

/// <summary>Nesne depolama (S3 uyumlu). Kapalıyken IsEnabled false döner ve yükleme yapılmaz.</summary>
public interface IFileStorage
{
    bool IsEnabled { get; }

    Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Kısa ömürlü ön-imzalı indirme adresi üretir (ağ çağrısı yapmaz).</summary>
    string CreateDownloadUrl(string objectKey, TimeSpan lifetime);
}
