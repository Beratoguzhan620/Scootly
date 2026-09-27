using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Scootly.Api.Extensions;

public static class SwaggerExtensions
{
    private const string BearerSchemeName = "Bearer";

    public static IServiceCollection AddScootlySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerDocuments>();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Giriş (/api/auth/login) veya cihaz token ucundan alınan JWT."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseScootlySwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in app.DescribeApiVersions())
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        });

        return app;
    }

    /// <summary>Her API sürümü için ayrı bir Swagger dokümanı üretir.</summary>
    private sealed class ConfigureSwaggerDocuments : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider;

        public ConfigureSwaggerDocuments(IApiVersionDescriptionProvider provider)
        {
            _provider = provider;
        }

        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in _provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, new OpenApiInfo
                {
                    Title = "Scootly API",
                    Version = description.ApiVersion.ToString(),
                    Description = description.IsDeprecated ? "Bu sürüm kullanımdan kaldırıldı." : null
                });
            }
        }
    }
}
