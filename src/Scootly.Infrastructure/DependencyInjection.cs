using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Caching;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Persistence.Repositories;
using Scootly.Infrastructure.Time;
using StackExchange.Redis;

namespace Scootly.Infrastructure;

/// <summary>
/// Altyapı bileşenlerinin tek kayıt noktası (60. gün — tekrarların temizliği).
/// </summary>
/// <remarks>
/// <para>
/// 54. günde <c>Scootly.Worker</c> eklendiğinde aynı kayıtların ikinci bir
/// kopyası gerekti. İki <c>Program.cs</c>'de yan yana duran kayıt listeleri
/// kaçınılmaz olarak ayrışır: biri güncellenir, diğeri unutulur ve arka plan
/// servisi API'den farklı davranmaya başlar — üstelik bu fark ancak üretimde
/// görülür.
/// </para>
/// <para>
/// Önbellek seçimi yapılandırmadan: <c>Redis:ConnectionString</c> doluysa
/// dağıtık önbellek, boşsa bellek içi. Varsayılanın bellek içi olması
/// bilinçli — Redis'siz bir geliştirme makinesinde uygulama çalışmaya devam
/// ediyor, yalnızca 47. günün paylaşımlı önbellek garantisi olmadan.
/// </para>
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddScootlyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baglantiDizesi = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(baglantiDizesi))
        {
            // Sessizce varsayılana düşmek yerine açılışta durmak: eksik
            // bağlantı dizesiyle başlayan bir uygulama, ilk isteğe kadar
            // sağlıklı görünür ve hatayı kullanıcıya gösterir.
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection tanimli degil. " +
                "Gelistirmede 'dotnet user-secrets set' ile tanimlayin.");
        }

        services.AddDbContext<ScootlyDbContext>(options => options.UseNpgsql(baglantiDizesi));

        services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ScootlyDbContext>());
        services.AddScoped<IUnitOfWork>(p => p.GetRequiredService<ScootlyDbContext>());
        services.AddScoped<ITransactionManager>(p => p.GetRequiredService<ScootlyDbContext>());

        services.AddScoped<IQueryExecutor, EfQueryExecutor>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IRideRepository, RideRepository>();
        services.AddScoped<IClock, SystemClock>();

        // Toplu yazıcı kendi bağlantısını açıyor (bkz. TelemetryBulkWriter):
        // arka plan servisinden çağrıldığı için paylaşılan bir DbContext'e
        // bağlanamaz. Tekil olması da bu yüzden güvenli.
        services.AddSingleton(new TelemetryWriteOptions(baglantiDizesi));
        services.AddSingleton<ITelemetryWriter, TelemetryBulkWriter>();

        services.AddScootlyCaching(configuration);

        return services;
    }

    private static IServiceCollection AddScootlyCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Dizin erişimi (GetValue<T> değil): GetValue ayrı bir pakette
        // (Configuration.Binder) yaşıyor ve bu proje için tek bir metot uğruna
        // fazladan bir bağımlılık demek olurdu.
        var redisBaglantisi = configuration["Redis:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(redisBaglantisi))
        {
            // 47. gün: dağıtık önbellek.
            //
            // ConnectionMultiplexer TEKİL olmalı. StackExchange.Redis'in
            // belgeleri bunu açıkça söylüyor: nesne iş parçacığı güvenli ve
            // kendi bağlantı havuzunu yönetiyor. Her istekte yenisini açmak,
            // Redis'te bağlantı tükenmesine yol açan klasik hatadır.
            services.AddSingleton<IConnectionMultiplexer>(saglayici =>
            {
                var secenekler = ConfigurationOptions.Parse(redisBaglantisi);

                // Redis kapalıyken uygulamanın AÇILIŞTA ÇÖKMEMESİ için.
                // Önbellek bir iyileştirme; onsuz uygulama çalışmalı.
                secenekler.AbortOnConnectFail = false;

                var gunlukcu = saglayici.GetRequiredService<ILogger<ConnectionMultiplexer>>();
                gunlukcu.LogInformation("Redis onbellegi etkin.");

                return ConnectionMultiplexer.Connect(secenekler);
            });

            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<RedisDistributedLock>();
        }
        else
        {
            // 46. gün: bellek içi önbellek.
            services.AddMemoryCache(options =>
            {
                // Sınırsız bir bellek içi önbellek, anahtar çeşidi arttıkça
                // (harita sorgusunda her koordinat bir anahtar) uygulamayı
                // bellek yetersizliğine kadar götürür.
                options.SizeLimit = 10_000;
            });

            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        // Fabrika ile kayıt: NearbyVehicleCache'in Redis bağlantısı İSTEĞE
        // BAĞLI (bellek içi modda yok). Bunu yapıcının varsayılan
        // parametresine bırakmak yerine burada açıkça GetService ile
        // çözüyoruz — kapsayıcının isteğe bağlı parametreleri nasıl ele
        // aldığına güvenmek, çalışma zamanında "bu servis çözülemedi"
        // hatasıyla biten türden bir varsayım.
        services.AddSingleton<INearbyVehicleCache>(saglayici => new NearbyVehicleCache(
            saglayici.GetRequiredService<ICacheService>(),
            saglayici.GetRequiredService<ILogger<NearbyVehicleCache>>(),
            saglayici.GetService<IConnectionMultiplexer>()));

        return services;
    }
}
