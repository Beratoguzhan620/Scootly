# syntax=docker/dockerfile:1
# Derleme bağlamı depo köküdür: docker build -f src/Scootly.Worker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Scootly.Domain/Scootly.Domain.csproj src/Scootly.Domain/
COPY src/Scootly.Application/Scootly.Application.csproj src/Scootly.Application/
COPY src/Scootly.Infrastructure/Scootly.Infrastructure.csproj src/Scootly.Infrastructure/
COPY src/Scootly.Worker/Scootly.Worker.csproj src/Scootly.Worker/
RUN dotnet restore src/Scootly.Worker/Scootly.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Scootly.Worker/Scootly.Worker.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Infrastructure, ASP.NET Core paylaşımlı çerçevesine (Identity, HealthChecks) başvurduğu için aspnet çalışma zamanı gerekir.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Scootly.Worker.dll"]
