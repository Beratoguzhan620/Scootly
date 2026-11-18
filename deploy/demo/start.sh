#!/bin/sh
# Demonun Api kapsayıcısı: ödeme simülatörü (8082), Api (8080, Render'ın yönlendirdiği port), Worker ve araçlar
# kaydedilince cihaz simülatörü. Gizli değerler Render'ın ürettiği ortam değişkenlerinden gelir.
set -eu

# Dört .NET süreci ücretsiz planın belleğini paylaşır: iş istasyonu çöp toplayıcısı ve daha küçük bir yığın.
export DOTNET_gcServer=0 DOTNET_GCConserveMemory=7

cd /app/payments
ASPNETCORE_HTTP_PORTS=8082 \
Webhook__CallbackUrl=http://127.0.0.1:8080/api/webhooks/payment-callback \
Webhook__Secret="$PaymentWebhook__Secret" \
  dotnet Scootly.PaymentSimulator.dll &

cd /app/api
ASPNETCORE_HTTP_PORTS=8080 PaymentGateway__BaseUrl=http://127.0.0.1:8082 dotnet Scootly.Api.dll &
api=$!

# Worker, şemayı Api'nin migration'ından sonra bulsun diye Api hazır olunca başlar.
until bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080' 2>/dev/null; do sleep 2; done
sleep 10

cd /app/worker
PaymentGateway__BaseUrl=http://127.0.0.1:8082 dotnet Scootly.Worker.dll &
worker=$!

# Cihaz simülatörü kayıtlı araç yoksa çıkar; demo araçları kaydedene kadar yeniden dener.
(
  cd /app/devices
  while true; do
    SCOOTLY_ApiBaseUrl=http://127.0.0.1:8080 SCOOTLY_ClientSecret="$DeviceAuth__ClientSecret" SCOOTLY_IntervalSeconds=15 SCOOTLY_MaxVehicles=40 \
      dotnet Scootly.DeviceSimulator.dll || true
    sleep 30
  done
) &

trap 'kill 0' TERM INT
# Kapsayıcı, Api ve Worker ayakta kaldıkça yaşar.
wait "$api" "$worker"
