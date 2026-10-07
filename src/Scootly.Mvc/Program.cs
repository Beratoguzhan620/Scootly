using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Scootly.Application;
using Scootly.Infrastructure;
using Scootly.Infrastructure.Logging;
using Scootly.Infrastructure.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Scootly.Mvc")
        .Destructure.With<SensitiveDataDestructuringPolicy>()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

    var seqUrl = context.Configuration["Seq:ServerUrl"];

    if (!string.IsNullOrWhiteSpace(seqUrl))
        configuration.WriteTo.Seq(seqUrl);
});

builder.Services.AddControllersWithViews();

builder.Services.AddScootlyApplication();
builder.Services.AddScootlyInfrastructure(builder.Configuration);
builder.Services.AddScootlyTelemetry(builder.Configuration, "Scootly.Mvc");
// Mvc ödeme akışını kullanmaz (ödeme handler'ları ve sağlayıcı istemcisi yalnızca Api ve Worker'da kayıtlı).
// Harita sayfası yalnızca hub token'ı üretir; ana API imza anahtarını bilmez.
builder.Services.AddScootlyHubTokens();

// Ters vekil (Nginx) arkasında gerçek şema/istemci IP'si yalnızca tanımlı vekillerden kabul edilir.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    // Birden fazla Mvc kopyası aynı anahtar halkasını kullanmalı; aksi halde bir kopyanın ürettiği cookie ve
    // antiforgery token'ı diğerinde çözülemez (105. gün ölçümü: 10 denemede 10 kez 400).
    var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
    redisOptions.AbortOnConnectFail = false;
    var redisMultiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions);

    builder.Services.AddDataProtection()
        .SetApplicationName("Scootly.Mvc")
        .PersistKeysToStackExchangeRedis(redisMultiplexer, "Scootly:Mvc:DataProtection-Keys");
}

builder.Services.AddScootlyIdentityCore();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Rol değişikliği veya hesap silme (güvenlik damgası) açık oturumlara en geç 1 dakikada yansır (varsayılan 30 dk).
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddScootlyAuthorization();

// Mvc'de Messaging:Enabled=false olduğundan yalnızca Postgres ve Redis kontrolleri kurulur.
builder.Services.AddHealthChecks().AddScootlyHealthChecks(builder.Configuration);

var connectSrc = BuildConnectSrc(builder.Configuration["ApiBaseUrl"]);

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Gövdesiz 4xx/5xx yanıtları (bilinmeyen adres, yetkisiz istek) kullanıcıya anlamlı bir hata sayfasıyla gösterilir.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https://*.tile.openstreetmap.org; " +
        $"connect-src {connectSrc};");

    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseScootlyCorrelationId();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Canlılık: süreç ayakta mı? Hazırlık: bağımlılıklar (veritabanı, önbellek) erişilebilir mi?
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Tarayıcıdaki JS'in Api'ye (fetch ve SignalR/WebSocket) bağlanabilmesi için ApiBaseUrl'in kökenini izin listesine ekler.
// Geliştirmede http://localhost:5016 verilirse çıktı eski sabit değerle aynıdır.
static string BuildConnectSrc(string? apiBaseUrl)
{
    var sources = new List<string> { "'self'" };

    if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri))
    {
        sources.Add(uri.GetLeftPart(UriPartial.Authority));
        sources.Add((uri.Scheme == Uri.UriSchemeHttps ? "wss://" : "ws://") + uri.Authority);
    }

    return string.Join(' ', sources);
}
public partial class Program { }
