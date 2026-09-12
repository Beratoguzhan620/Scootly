namespace Scootly.Application.Common;

/// <summary>
/// Bir kayıt, okunduğu andan beri başka biri tarafından değiştirilmiş.
/// </summary>
/// <remarks>
/// <para>
/// Bu tip Application katmanında duruyor ve EF Core'un
/// <c>DbUpdateConcurrencyException</c>'ının yerini alıyor. Infrastructure
/// katmanı EF'in istisnasını yakalayıp bunu fırlatıyor.
/// </para>
/// <para>
/// Neden: Karar 1 (Application somut teknolojiden bağımsız olmalı). Handler'lar
/// doğrudan <c>DbUpdateConcurrencyException</c> yakalasaydı, Application
/// projesinin EF Core paketine bağımlı olması gerekirdi — ve o bağımlılık bir
/// kez girdiğinde, "madem EF zaten var" diyerek başka EF tiplerinin de
/// sızmasının önü açılırdı.
/// </para>
/// <para>
/// Çakışma bir <b>hata</b> değil, beklenen bir sonuçtur: iki kişi aynı anda aynı
/// aracı istedi, biri kazandı. Bu yüzden handler'lar bunu yakalayıp
/// <c>Result.Failure</c> döndürüyor; istisna yalnızca katmanlar arası taşıma
/// aracı.
/// </para>
/// </remarks>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Kayıt, okunduğundan beri başka bir işlem tarafından değiştirildi.")
    {
    }

    public ConcurrencyConflictException(string message) : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
