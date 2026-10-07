using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;
using Scootly.Testing;

namespace Scootly.E2E.Tests;

/// <summary>
/// Geçici Postgres (ScootlyApiFactory) + gerçek bir Scootly.Mvc süreci + Chromium.
/// Fabrikanın Api sunucusu yalnızca veritabanı hazırlama ve veri tohumlama aracı olarak kullanılır.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private readonly StringBuilder _mvcOutput = new();
    private readonly object _outputLock = new();
    private Process? _mvc;

    public ScootlyApiFactory Factory { get; } = new();

    public string BaseUrl { get; private set; } = string.Empty;

    public IPlaywright PlaywrightHost { get; private set; } = null!;

    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Factory.InitializeAsync();

        StartMvc();
        await WaitUntilMvcIsLiveAsync();

        PlaywrightHost = await Playwright.CreateAsync();
        Browser = await PlaywrightHost.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Yeni bir sayfa açar ve gövdeyi çalıştırır. Gövde hata fırlatırsa ekran görüntüsü, sayfa HTML'i ve
    /// Mvc süreç çıktısını kaydeder, sonra hatayı aynen tekrar fırlatır.
    /// </summary>
    public async Task RunAsync(string testName, Func<IPage, Task> body)
    {
        var page = await NewPageAsync();

        try
        {
            await body(page);
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, testName);
            throw;
        }
    }

    private async Task SaveFailureArtifactsAsync(IPage page, string testName)
    {
        try
        {
            var directory = Environment.GetEnvironmentVariable("E2E_ARTIFACTS_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(AppContext.BaseDirectory, "e2e-artifacts");

            Directory.CreateDirectory(directory);

            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, $"{testName}.png"), FullPage = true });
            await File.WriteAllTextAsync(Path.Combine(directory, $"{testName}.html"), await page.ContentAsync());
            await File.WriteAllTextAsync(Path.Combine(directory, $"{testName}.mvc.log"), ReadOutput());
        }
        catch (Exception)
        {
            // Kanıt toplama başarısız olursa asıl test hatası gizlenmemeli.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();

        PlaywrightHost?.Dispose();

        if (_mvc is { HasExited: false })
        {
            _mvc.Kill(entireProcessTree: true);
            await _mvc.WaitForExitAsync();
        }

        _mvc?.Dispose();

        await Factory.DisposeAsync();
    }

    private void StartMvc()
    {
        var repoRoot = FindRepoRoot();
        var mvcDirectory = Path.Combine(repoRoot, "src", "Scootly.Mvc");
        var mvcDll = Path.Combine(mvcDirectory, "bin", Configuration, "net10.0", "Scootly.Mvc.dll");

        if (!File.Exists(mvcDll))
            throw new FileNotFoundException($"Mvc derlemesi bulunamadı (önce derleyin): {mvcDll}");

        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = mvcDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(mvcDll);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = Factory.ConnectionString;
        startInfo.Environment["Jwt__HubKey"] = TestSecrets.HubKey;
        startInfo.Environment["Messaging__Enabled"] = "false";
        startInfo.Environment["Redis__ConnectionString"] = string.Empty;

        // Canlı S3 uyumlu depo tanımlıysa fotoğraf testleri için depolama açık başlatılır; yoksa varsayılan (kapalı) kalır.
        var liveEndpoint = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ENDPOINT");
        var liveAccessKey = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ACCESS_KEY");
        var liveSecretKey = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_SECRET_KEY");

        if (!string.IsNullOrWhiteSpace(liveEndpoint) && !string.IsNullOrWhiteSpace(liveAccessKey) && !string.IsNullOrWhiteSpace(liveSecretKey))
        {
            startInfo.Environment["Storage__Enabled"] = "true";
            startInfo.Environment["Storage__Endpoint"] = liveEndpoint;
            startInfo.Environment["Storage__PublicEndpoint"] = liveEndpoint;
            startInfo.Environment["Storage__Bucket"] = "scootly-e2e-photos";
            startInfo.Environment["Storage__AccessKey"] = liveAccessKey;
            startInfo.Environment["Storage__SecretKey"] = liveSecretKey;
        }

        _mvc = new Process { StartInfo = startInfo };
        _mvc.OutputDataReceived += (_, e) => AppendOutput(e.Data);
        _mvc.ErrorDataReceived += (_, e) => AppendOutput(e.Data);
        _mvc.Start();
        _mvc.BeginOutputReadLine();
        _mvc.BeginErrorReadLine();
    }

    private async Task WaitUntilMvcIsLiveAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            if (_mvc!.HasExited)
                throw new InvalidOperationException($"Mvc süreci kapandı (çıkış kodu {_mvc.ExitCode}). Çıktı:{Environment.NewLine}{ReadOutput()}");

            try
            {
                var response = await http.GetAsync($"{BaseUrl}/health/live");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Mvc 60 sn içinde hazır olmadı. Çıktı:{Environment.NewLine}{ReadOutput()}");
    }

    private void AppendOutput(string? line)
    {
        if (line is null)
            return;

        lock (_outputLock)
            _mvcOutput.AppendLine(line);
    }

    private string ReadOutput()
    {
        lock (_outputLock)
            return _mvcOutput.ToString();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repo kökü bulunamadı.");
    }
}

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Name = "E2E";
}
