namespace Scootly.Application.Abstractions;

/// <summary>Nesne depolama (S3 uyumlu). Kapaliyken IsEnabled false doner ve yukleme yapilmaz.</summary>
public interface IFileStorage
{
    bool IsEnabled { get; }

    Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Kisa omurlu on-imzali indirme adresi uretir (ag cagrisi yapmaz).</summary>
    string CreateDownloadUrl(string objectKey, TimeSpan lifetime);
}
