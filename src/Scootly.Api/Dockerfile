# syntax=docker/dockerfile:1
# Derleme bağlamı depo köküdür: docker build -f src/Scootly.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kaynak kod değişse de restore katmanı önbellekten gelir.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Scootly.Domain/Scootly.Domain.csproj src/Scootly.Domain/
COPY src/Scootly.Application/Scootly.Application.csproj src/Scootly.Application/
COPY src/Scootly.Infrastructure/Scootly.Infrastructure.csproj src/Scootly.Infrastructure/
COPY src/Scootly.Api/Scootly.Api.csproj src/Scootly.Api/
RUN dotnet restore src/Scootly.Api/Scootly.Api.csproj

COPY src/ src/
RUN dotnet publish src/Scootly.Api/Scootly.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Veritabanı şemasını uygulayan, kendi başına çalışan EF Core migration paketi.
FROM build AS migrations-bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.12
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
        --project src/Scootly.Infrastructure \
        --startup-project src/Scootly.Api \
        --configuration Release \
        --self-contained -r linux-x64 \
        --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=migrations-bundle /app/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scootly.Api.dll"]
