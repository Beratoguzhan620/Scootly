using Scootly.Domain.Common;

namespace Scootly.Application.FieldOps;

public sealed record FieldTaskPhotoInfo(string ContentType, string Extension);

/// <summary>
/// Saha gorevi fotografi kurallari. Tur, istemcinin bildirdigi Content-Type'a ve dosya adina degil,
/// icerigin ilk baytlarina (imza) gore belirlenir. Goruntu cozumlenmez ve zararli yazilim taramasi yapilmaz.
/// </summary>
public static class FieldTaskPhotoRules
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Result<FieldTaskPhotoInfo> Inspect(byte[] content)
    {
        if (content.Length == 0)
            return Result<FieldTaskPhotoInfo>.Validation("Foto\u011fraf bo\u015f.");

        if (content.Length > MaxBytes)
            return Result<FieldTaskPhotoInfo>.Validation("Foto\u011fraf 5 MB'dan b\u00fcy\u00fck olamaz.");

        if (content.AsSpan().StartsWith(JpegSignature))
            return Result<FieldTaskPhotoInfo>.Success(new FieldTaskPhotoInfo("image/jpeg", ".jpg"));

        if (content.AsSpan().StartsWith(PngSignature))
            return Result<FieldTaskPhotoInfo>.Success(new FieldTaskPhotoInfo("image/png", ".png"));

        return Result<FieldTaskPhotoInfo>.Validation("Yaln\u0131zca JPEG veya PNG foto\u011fraf y\u00fcklenebilir.");
    }
}
