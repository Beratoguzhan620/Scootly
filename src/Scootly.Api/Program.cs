using System.Text;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scootly.Api.Authorization;
using Scootly.Api.Identity;
using Scootly.Api.Logging;
using Scootly.Api.Middleware;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Infrastructure.Devices;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Time;
using Scootly.Infrastructure.Persistence.Repositories;

const string CorsPolitikasi = "ScootlyVarsayilan";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        // Gün 27: loga yazılan nesnelerde hassas alanları maskeler.
        .Destructure.With<SensitiveDataDestructuringPolicy>()
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ScootlyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ScootlyDbContext>());

builder.Services.AddScoped<IUnitOfWork>(provider =>
    provider.GetRequiredService<ScootlyDbContext>());

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IRideRepository, RideRepository>();

builder.Services.AddScoped<IClock, SystemClock>();

builder.Services.AddScoped<RegisterVehicleCommandHandler>();
builder.Services.AddScoped<ReserveVehicleCommandHandler>();
builder.Services.AddScoped<StartRideCommandHandler>();
builder.Services.AddScoped<CompleteRideCommandHandler>();
builder.Services.AddScoped<StartRideRequestValidator>();

// --- Gün 21: ASP.NET Core Identity -------------------------------------------
// AddIdentityCore, AddIdentity değil. AddIdentity çerez tabanlı oturum şemasını
// da kurar ve varsayılan kimlik doğrulama şemasını çereze bağlar; bu API token
// ile çalıştığı için o şema yalnızca çakışma üretirdi.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ScootlyDbContext>();

// --- Gün 22: JWT --------------------------------------------------------------
// Yapılandırma açılışta doğrulanır. Anahtar eksik veya kısaysa uygulama HİÇ
// BAŞLAMAZ; sessizce bir varsayılana düşmek, herkesin kendine yönetici token'ı
// üretebilmesi demek olurdu.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? new JwtOptions();
jwtOptions.Validate();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

// --- Gün 25: cihaz kimliği ----------------------------------------------------
builder.Services.AddScoped<IPasswordHasher<DeviceCredential>, PasswordHasher<DeviceCredential>>();
builder.Services.AddScoped<DeviceTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            // İki hedef kitle: kullanıcılar ve cihazlar. Ayrı olmaları,
            // bir cihaz token'ının kullanıcı uçlarında geçerli SAYILMAMASINI
            // sağlıyor — ayrımı asıl uygulayan yer DeviceTokenScopeMiddleware,
            // burası ise token'ın bizim ürettiğimizi doğruluyor.
            ValidateAudience = true,
            ValidAudiences = [jwtOptions.Audience, DeviceTokenService.DeviceAudience],

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            // Kabul edilen algoritma sabitlenir: aksi halde doğrulayıcı token'ın
            // kendi başlığında yazan algoritmaya bakar ("algoritma karışıklığı").
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // Varsayılan 5 dakika: süresi dolmuş token 5 dakika daha kabul edilir.
            ClockSkew = TimeSpan.Zero,

            NameClaimType = ScootlyClaimTypes.Subject,
            RoleClaimType = ScootlyClaimTypes.Role
        };
    });

builder.Services.AddScootlyAuthorization();

// --- Gün 30: CORS -------------------------------------------------------------
// İzinli kaynaklar yapılandırmadan geliyor; kodda sabit bir liste yok.
// AllowAnyOrigin KULLANILMIYOR: bu API'ye token ile erişiliyor ve ileride
// (18. haftada) bir tarayıcı arayüzü bağlanacak. Her kaynağa açık bir politika,
// o arayüzün domain'ini taklit eden bir sayfanın da aynı isteği yapabilmesi
// demektir. Liste boşsa hiçbir çapraz kaynak isteği geçmez — kapalı taraf.
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolitikasi, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .WithMethods("GET", "POST")
        .WithHeaders("Authorization", "Content-Type"));
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Gün 27: her isteğin yöntemi, yolu, durum kodu ve süresi tek satırda.
// Başlıklar loglanmıyor — Authorization başlığı tam da bu yüzden.
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await using var scope = app.Services.CreateAsyncScope();
    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>(),
        app.Configuration["Seed:TestUser:Email"],
        app.Configuration["Seed:TestUser:Password"]);
}

app.UseHttpsRedirection();

app.UseCors(CorsPolitikasi);

// SIRA ÖNEMLİ — üç satır, üç ayrı soru, bu sırayla:
//   1. UseAuthentication          : "sen kimsin"
//   2. DeviceTokenScopeMiddleware : "cihaz token'ı gitmemesi gereken yere mi gidiyor"
//   3. UseAuthorization           : "buna yetkin var mı"
//
// Ara katman 1'den önce olsaydı context.User henüz boş olurdu ve her isteği
// "cihaz değil" sayardı — hiçbir şeyi engellemez, ama bunu fark etmezdik çünkü
// başarısızlığı sessiz olurdu. 3'ten sonra olsaydı yetki kararı zaten verilmiş
// olurdu.
app.UseAuthentication();
app.UseMiddleware<DeviceTokenScopeMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
