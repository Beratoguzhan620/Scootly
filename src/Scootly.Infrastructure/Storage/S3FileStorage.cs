using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Storage;

/// <summary>
/// S3 uyumlu depo (yerelde SeaweedFS). Yol tarzı adresleme kullanılır. Sağlama toplamı yalnızca gerekli
/// olduğunda hesaplanır: uyumlu depoların bir kısmı SDK'nın varsayılan akışlı sağlama toplamını desteklemez.
/// Ön-imzalı URL, tarayıcının eriştiği PublicEndpoint'e göre yerel olarak imzalanır (ağ çağrısı yapmaz).
/// </summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly StorageOptions _options;
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signingClient;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private volatile bool _bucketReady;

    public S3FileStorage(StorageOptions options)
    {
        _options = options;

        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        _client = new AmazonS3Client(credentials, CreateConfig(options.Endpoint, options.Region));
        _signingClient = new AmazonS3Client(credentials, CreateConfig(options.PublicEndpoint, options.Region));
    }

    public bool IsEnabled => true;

    public async Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        using var stream = new MemoryStream(content, writable: false);

        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType
            },
            cancellationToken);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);
    }

    public string CreateDownloadUrl(string objectKey, TimeSpan lifetime)
    {
        var isHttps = new Uri(_options.PublicEndpoint).Scheme == Uri.UriSchemeHttps;

        return _signingClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime),
            Protocol = isHttps ? Protocol.HTTPS : Protocol.HTTP
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _signingClient.Dispose();
        _bucketLock.Dispose();
    }

    private static AmazonS3Config CreateConfig(string endpoint, string region) => new()
    {
        ServiceURL = endpoint,
        ForcePathStyle = true,
        AuthenticationRegion = region,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
    };

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
            return;

        await _bucketLock.WaitAsync(cancellationToken);

        try
        {
            if (_bucketReady)
                return;

            try
            {
                await _client.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Kova zaten var; beklenen durum.
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}
