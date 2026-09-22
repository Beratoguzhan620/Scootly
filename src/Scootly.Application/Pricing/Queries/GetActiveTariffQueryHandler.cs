using Scootly.Application.Abstractions;

namespace Scootly.Application.Pricing.Queries;

/// <summary>Aktif tarifeyi getirir. Önbellekli (46. gün).</summary>
/// <remarks>
/// <para>
/// Önbelleklemeye <b>bu</b> sorguyla başlamanın gerekçesi risk sıralaması:
/// tarife nadiren değişir ve herkes için aynıdır. Kullanıcıya özel veriyle
/// başlansaydı, ilk hata "bir kullanıcının verisini başka kullanıcıya
/// göstermek" olurdu — ve bu, ancak bir şikayetle fark edilen türden bir hata.
/// </para>
/// <para>
/// Yaşam süresi beş dakika. Tarife değişikliği nadir ve planlı bir olay
/// olduğundan, en kötü durumda beş dakika eski fiyat görünür. Bu kabul
/// edilebilir; fatura kesilirken tarife önbellekten DEĞİL, veritabanından
/// okunmalı — para hesabına giren tutar hiçbir zaman önbellekten gelmez.
/// </para>
/// </remarks>
public sealed class GetActiveTariffQueryHandler
{
    /// <summary>Önbellek anahtarı. Tek bir aktif tarife olduğu için sabit.</summary>
    public const string OnbellekAnahtari = "tarife:aktif";

    public static readonly TimeSpan YasamSuresi = TimeSpan.FromMinutes(5);

    private readonly IApplicationDbContext _dbContext;
    private readonly IQueryExecutor _queryExecutor;
    private readonly ICacheService _cache;

    public GetActiveTariffQueryHandler(
        IApplicationDbContext dbContext,
        IQueryExecutor queryExecutor,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _queryExecutor = queryExecutor;
        _cache = cache;
    }

    public async Task<TariffDto?> Handle(CancellationToken cancellationToken = default)
    {
        // 1. Önbelleğe bak (isabet).
        var onbellekten = await _cache.GetAsync<TariffDto>(OnbellekAnahtari, cancellationToken);

        if (onbellekten is not null)
        {
            return onbellekten;
        }

        // 2. Iska: veritabanından oku.
        var tarife = await VeritabanindanAsync(cancellationToken);

        // 3. Bulunduysa önbelleğe yaz.
        //
        // BULUNAMAYAN durum önbelleklenmiyor. Bilinçli: "aktif tarife yok"
        // cevabını önbelleğe yazsaydık, tarife tanımlandıktan sonra beş dakika
        // boyunca sistem hâlâ tarifesiz görünürdü. Bunun bedeli, tarife
        // tanımlanana kadar her isteğin veritabanına gitmesi — ki o durum
        // zaten bir yapılandırma hatası ve uzun sürmemeli.
        if (tarife is not null)
        {
            await _cache.SetAsync(OnbellekAnahtari, tarife, YasamSuresi, cancellationToken);
        }

        return tarife;
    }

    /// <summary>Önbelleği atlayarak doğrudan veritabanından okur.</summary>
    /// <remarks>
    /// Ücret hesaplayan kod bu metodu çağırmalı, <see cref="Handle"/>'ı değil.
    /// </remarks>
    public Task<TariffDto?> VeritabanindanAsync(CancellationToken cancellationToken = default)
    {
        var sorgu = _dbContext.Tariffs
            .Where(t => t.IsActive)
            .Select(t => new TariffDto(
                t.Id,
                t.Name,
                t.UnlockFee.Amount,
                t.PerMinuteFee.Amount,
                t.UnlockFee.Currency));

        return _queryExecutor.FirstOrDefaultAsync(sorgu, cancellationToken);
    }
}
