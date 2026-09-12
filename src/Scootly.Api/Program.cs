using System.Text;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scootly.Api.Identity;
using Scootly.Api.Middleware;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Time;
using Scootly.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
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

builder.Services.AddScoped<ReserveVehicleCommandHandler>();
builder.Services.AddScoped<StartRideCommandHandler>();
builder.Services.AddScoped<CompleteRideCommandHandler>();
builder.Services.AddScoped<StartRideRequestValidator>();

// --- Gün 21: ASP.NET Core Identity -------------------------------------------
// AddIdentityCore, AddIdentity değil. AddIdentity çerez (cookie) tabanlı oturum
// şemasını da kurar ve varsayılan kimlik doğrulama şemasını çereze bağlar;
// bu API token ile çalıştığı için o şema yalnızca çakışma üretirdi.
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
// BAŞLAMAZ. Alternatif — sessizce varsayılan bir anahtara düşmek — herkesin
// kendine yönetici token'ı üretebilmesi demek olurdu.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? new JwtOptions();
jwtOptions.Validate();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Kısa iddia adları (sub, role) uzun URI'lere çevrilmesin.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            // Kabul edilen algoritma tek başına sabitlenir. Bu liste verilmezse
            // doğrulayıcı, token'ın kendi başlığında yazan algoritmaya bakar;
            // saldırganın algoritmayı değiştirerek imza kontrolünü atlatmaya
            // çalıştığı sınıfa "algoritma karışıklığı" saldırısı denir.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // Varsayılan 5 dakikadır: süresi dolmuş bir token 5 dakika daha
            // kabul edilir. Tek makinede çalışan bir sistemde buna gerek yok.
            ClockSkew = TimeSpan.Zero,

            // Kısa adlar kullandığımız için bunlar şart. Ayarlanmazsa
            // [Authorize(Roles = ...)] hiçbir rolü bulamaz ve her istek
            // sessizce 403 döner — teşhisi zor bir hata.
            NameClaimType = ScootlyClaimTypes.Subject,
            RoleClaimType = ScootlyClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();
// -----------------------------------------------------------------------------

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

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

// Sıra önemli: önce "sen kimsin" (authentication), sonra "buna yetkin var mı"
// (authorization). Ters çevrilirse yetki kontrolü henüz doldurulmamış bir
// kimliğe bakar ve herkesi anonim sanar.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
