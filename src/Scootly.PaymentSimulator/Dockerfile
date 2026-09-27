# syntax=docker/dockerfile:1
# Derleme bağlamı depo köküdür: docker build -f src/Scootly.PaymentSimulator/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Scootly.PaymentSimulator/Scootly.PaymentSimulator.csproj src/Scootly.PaymentSimulator/
RUN dotnet restore src/Scootly.PaymentSimulator/Scootly.PaymentSimulator.csproj

COPY src/Scootly.PaymentSimulator/ src/Scootly.PaymentSimulator/
RUN dotnet publish src/Scootly.PaymentSimulator/Scootly.PaymentSimulator.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scootly.PaymentSimulator.dll"]
