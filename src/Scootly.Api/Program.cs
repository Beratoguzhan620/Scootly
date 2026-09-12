using Serilog;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
// bu API token ile çalışacağı için (22. gün) o şema yalnızca çakışma üretirdi.
// Core sürümü UserManager, PasswordHasher ve doğrulayıcıları kurar, oturum kurmaz.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Aynı e-posta ile ikinci hesap açılmasın: giriş e-posta üzerinden
        // yapılacağı için tekillik bir işlevsellik şartı, tercih değil.
        options.User.RequireUniqueEmail = true;

        // Parola politikası. Uzunluk, karakter çeşitliliğinden daha belirleyicidir;
        // bu yüzden varsayılan 6 yerine 12 karakter.
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Kaba kuvvet (brute force) denemesini yavaşlatır. AllowedForNewUsers
        // varsayılanda true'dur; açıkça yazıldı ki ileride biri kapatırsa
        // bunun bir karar olduğu görülsün.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ScootlyDbContext>();
// -----------------------------------------------------------------------------

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Rolleri ve (parola yapılandırılmışsa) tek bir test kullanıcısını oluşturur.
    // Yalnızca geliştirme ortamında çalışır.
    await using var scope = app.Services.CreateAsyncScope();
    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>(),
        app.Configuration["Seed:TestUser:Email"],
        app.Configuration["Seed:TestUser:Password"]);
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }
