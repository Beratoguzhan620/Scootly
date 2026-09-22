using System.Threading.Channels;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Telemetry;

/// <summary>
/// API isteği ile veritabanı yazımı arasındaki sınırlı kuyruk (52. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Çözdüğü problem.</b> Telemetri isteğini veritabanı yazımını bekleterek
/// yanıtlamak, API'nin yanıt süresini doğrudan yazma hızına bağlar. 200 sanal
/// araç beş saniyede bir gönderdiğinde bu, saniyede 40 isteğin her birinin bir
/// veritabanı gidiş dönüşü beklemesi demek. Kanal ikisini ayırıyor: istek
/// kuyruğa yazıp hemen dönüyor, yazma kendi hızında arka planda ilerliyor.
/// </para>
/// <para>
/// <b>Kuyruk SINIRLI, ve dolduğunda kayıt ATILIYOR.</b> Üç seçenek vardı:
/// </para>
/// <list type="number">
///   <item><b>Beklemek</b> (<c>Wait</c>): kuyruk dolduğunda istek bekler.
///   Reddedildi — bu, kanalın çözdüğü problemi geri getirir: yanıt süresi
///   yine yazma hızına bağlanır, üstelik bu sefer kuyruk dolu olduğu için en
///   kötü anda.</item>
///   <item><b>Reddetmek</b> (503): dürüst ama cihaz için işe yaramaz. Cihaz
///   yeniden dener, yük daha da artar.</item>
///   <item><b>Atmak</b> (<c>DropWrite</c>): seçilen. Telemetri kayıp
///   toleranslı bir veri türü — bir ölçüm kaybolursa beş saniye sonra
///   yenisi gelir. Araç konumunun beş saniye eski olması, sistemin yük
///   altında durmasından iyidir.</item>
/// </list>
/// <para>
/// <b>Atılan kayıt sayılıyor.</b> Sessizce atmak kabul edilemez: hiç kimsenin
/// fark etmediği bir veri kaybı, veri kaybı olduğunu ancak bir rapor yanlış
/// çıktığında belli eder.
/// </para>
/// <para>
/// <b>Sayacın ilk hali YANLIŞTI ve bunu bir test yakaladı.</b> Atılanlar
/// <c>TryWrite</c>'ın dönüş değerinden sayılıyordu; oysa <c>DropWrite</c>
/// modunda <c>TryWrite</c> <b>hiçbir zaman <c>false</c> dönmez</b> — kanal
/// dolu olsa bile yazılmak istenen kaydı atıp <c>true</c> döner. Yani
/// "kayıp sessiz kalmasın" diye konulan sayacın kendisi sessizce
/// çalışmıyordu. Doğrusu, kanalın <c>itemDropped</c> geri çağrısı: atılan her
/// kayıt için kanal bizi haberdar ediyor.
/// </para>
/// <para>
/// Ders bu projede birkaç kez tekrarlananın aynısı: bir API'nin adına bakarak
/// davranışını varsaymak, sessiz hatanın en yaygın kaynağı. <c>TryWrite</c>'ın
/// adı "deneyip başarısız olabilir" diyor, ama seçilen moda göre davranışı
/// değişiyor.
/// </para>
/// </remarks>
public sealed class TelemetryChannel
{
    /// <summary>
    /// Kuyruk kapasitesi. 10.000 kayıt, 200 cihazın beş saniyelik gönderiminin
    /// yaklaşık elli katı — yani yazma tarafı birkaç dakika duraksasa bile
    /// kuyruk taşmaz.
    /// </summary>
    public const int Kapasite = 10_000;

    private readonly Channel<TelemetryReading> _kanal;
    private long _atilanKayitSayisi;

    public TelemetryChannel()
    {
        var secenekler = new BoundedChannelOptions(Kapasite)
        {
            FullMode = BoundedChannelFullMode.DropWrite,

            // Tek tüketici var (TelemetryDrainService); bunu söylemek kanalın
            // daha ucuz bir uygulama seçmesini sağlıyor.
            SingleReader = true,

            // Birden fazla istek iş parçacığı aynı anda yazıyor.
            SingleWriter = false
        };

        // itemDropped: kanal bir kaydı attığında BU çağrılıyor. Sayacı burada
        // artırmak, atılanları TryWrite'ın dönüş değerinden çıkarmaya
        // çalışmaktan farklı olarak gerçekten doğru.
        //
        // Geri çağrı yazan iş parçacığı üzerinde ve aynı anda birden fazla
        // iş parçacığından çağrılabiliyor — bu yüzden Interlocked (57. gün).
        _kanal = Channel.CreateBounded<TelemetryReading>(
            secenekler,
            itemDropped: _ => Interlocked.Increment(ref _atilanKayitSayisi));
    }

    /// <summary>Kanala yazılamadığı için atılan toplam kayıt sayısı.</summary>
    public long AtilanKayitSayisi => Interlocked.Read(ref _atilanKayitSayisi);

    /// <summary>Tüketici tarafı — arka plan servisi bunu okur.</summary>
    public ChannelReader<TelemetryReading> Okuyucu => _kanal.Reader;

    /// <summary>
    /// Kayıtları kuyruğa koyar. Bu çağrı sırasında atılan kayıt sayısını
    /// döndürür.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TryWrite</c> kullanılıyor, <c>WriteAsync</c> değil: bu metodun hiç
    /// beklememesi gerekiyor.
    /// </para>
    /// <para>
    /// Dönen sayı, eşzamanlı yazan başka istekler varken <b>yaklaşıktır</b>:
    /// sayaç küresel, dolayısıyla bu pencerede başkasının attığı kayıtlar da
    /// sayıma girebilir. Kesin olması gereken sayı
    /// <see cref="AtilanKayitSayisi"/> — alarmın dayandığı yer orası. HTTP
    /// yanıtındaki sayı cihaza "bir şeyler atıldı" demek için yeterli.
    /// </para>
    /// </remarks>
    public int Yaz(IReadOnlyCollection<TelemetryReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var oncesi = Interlocked.Read(ref _atilanKayitSayisi);

        foreach (var okuma in readings)
        {
            // DropWrite modunda bu her zaman true döner; dönüş değerine
            // bakmıyoruz, atılanları itemDropped sayıyor.
            _kanal.Writer.TryWrite(okuma);
        }

        return (int)(Interlocked.Read(ref _atilanKayitSayisi) - oncesi);
    }

    /// <summary>Kapanışta çağrılır: yeni yazım kabul edilmez, kuyruktakiler okunur.</summary>
    public void Kapat() => _kanal.Writer.TryComplete();
}
