using System.Text.RegularExpressions;

namespace Scootly.Infrastructure.Storage;

/// <summary>
/// Nesne depolama ayarları. Enabled=false iken (varsayılan) başka hiçbir alan zorunlu değildir.
/// Endpoint: uygulamanın depoya eriştiği adres. PublicEndpoint: tarayıcının eriştiği adres
/// (ön-imzalı URL bu adrese göre imzalanır; imza Host'u kapsar).
/// </summary>
public sealed partial class StorageOptions
{
    public const string SectionName = "Storage";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string PublicEndpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = "scootly-field-photos";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public static bool IsValid(StorageOptions options)
    {
        if (!options.Enabled)
            return true;

        return IsHttpUrl(options.Endpoint)
            && IsHttpUrl(options.PublicEndpoint)
            && BucketNamePattern().IsMatch(options.Bucket)
            && options.AccessKey.Length >= 3
            && options.SecretKey.Length >= 16
            && !string.IsNullOrWhiteSpace(options.Region);
    }

    private static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    private static partial Regex BucketNamePattern();
}
