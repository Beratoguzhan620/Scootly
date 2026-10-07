using Scootly.Domain.Common;

namespace Scootly.Application.FieldOps;

public sealed record FieldTaskPhotoInfo(string ContentType, string Extension);

/// <summary>
/// Saha görevi fotoğrafı kuralları. Tür, istemcinin bildirdiği Content-Type'a ve dosya adına değil,
/// içeriğin ilk baytlarına (imza) göre belirlenir. Görüntü çözümlenmez ve zararlı yazılım taraması yapılmaz.
/// </summary>
public static class FieldTaskPhotoRules
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Result<FieldTaskPhotoInfo> Inspect(byte[] content)
    {
        if (content.Length == 0)
            return Result<FieldTaskPhotoInfo>.Validation("Fotoğraf boş.");

        if (content.Length > MaxBytes)
            return Result<FieldTaskPhotoInfo>.Validation("Fotoğraf 5 MB'dan büyük olamaz.");

        if (content.AsSpan().StartsWith(JpegSignature))
            return Result<FieldTaskPhotoInfo>.Success(new FieldTaskPhotoInfo("image/jpeg", ".jpg"));

        if (content.AsSpan().StartsWith(PngSignature))
            return Result<FieldTaskPhotoInfo>.Success(new FieldTaskPhotoInfo("image/png", ".png"));

        return Result<FieldTaskPhotoInfo>.Validation("Yalnızca JPEG veya PNG fotoğraf yüklenebilir.");
    }
}
