# syntax=docker/dockerfile:1
# Derleme bağlamı depo kökündür: docker build -f src/Scootly.Mvc/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kaynak kod değişse de restore katmanı önbellekten gelir.
# Mvc, Api'ye bağlı değildir; Api projesi bu imaja alınmaz.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Scootly.Domain/Scootly.Domain.csproj src/Scootly.Domain/
COPY src/Scootly.Application/Scootly.Application.csproj src/Scootly.Application/
COPY src/Scootly.Infrastructure/Scootly.Infrastructure.csproj src/Scootly.Infrastructure/
COPY src/Scootly.Mvc/Scootly.Mvc.csproj src/Scootly.Mvc/
RUN dotnet restore src/Scootly.Mvc/Scootly.Mvc.csproj

# wwwroot (Leaflet, SignalR istemcisi, map.js) ve Views, publish çıktısına dahil edilir.
COPY src/ src/
RUN dotnet publish src/Scootly.Mvc/Scootly.Mvc.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scootly.Mvc.dll"]