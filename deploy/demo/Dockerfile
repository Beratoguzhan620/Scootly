# Herkese açık demonun Api tarafı tek kapsayıcıda: Api, Worker, ödeme simülatörü ve cihaz simülatörü yerel
# portlarda çalışır; veritabanı Neon'dadır. RabbitMQ ve Redis yoktur (Messaging:Enabled=false, süreç içi önbellek);
# tahsilatı Worker'ın bekleyen ödeme işi yapar. Panel (Mvc) ayrı bir servis olarak src/Scootly.Mvc/Dockerfile ile
# çalışır. Compose ise her parçayı ayrı kapsayıcıda, RabbitMQ ve Redis ile çalıştırır.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Scootly.Api/Scootly.Api.csproj -c Release -o /app/api /p:UseAppHost=false \
 && dotnet publish src/Scootly.Worker/Scootly.Worker.csproj -c Release -o /app/worker /p:UseAppHost=false \
 && dotnet publish src/Scootly.PaymentSimulator/Scootly.PaymentSimulator.csproj -c Release -o /app/payments /p:UseAppHost=false \
 && dotnet publish src/Scootly.DeviceSimulator/Scootly.DeviceSimulator.csproj -c Release -o /app/devices /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/api ./api
COPY --from=build /app/worker ./worker
COPY --from=build /app/payments ./payments
COPY --from=build /app/devices ./devices
COPY deploy/demo/start.sh ./start.sh
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "/app/start.sh"]
