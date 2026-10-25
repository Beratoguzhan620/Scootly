#!/usr/bin/env bash
# Scootly backend'ini (https://github.com/Beratoguzhan620/Scootly) bu Mac'te Docker ile çalıştırır ve API'yi
# telefondaki uygulamanın ulaşabileceği şekilde yerel ağa açar.
#
#   bash scripts/mac/backend.sh          # başlat (ilk sefer: backend'i indirir, imajları derler, ~20 dk)
#   bash scripts/mac/backend.sh status   # servislerin durumu ve adresler
#   bash scripts/mac/backend.sh tunnel   # internet tünelini yeniden aç (yeni adres)
#   bash scripts/mac/backend.sh stop     # durdur (veriler Docker volume'unda kalır)
#   bash scripts/mac/backend.sh logs     # API'nin son logları
#
# Backend klasörü: SCOOTLY_BACKEND_DIR (varsayılan ~/scootly-backend). Gizli değerler backend'in kendi
# deploy/.env dosyasında durur (git'e girmez). Dosya yoksa önce .local-build/backend.env aranır ve oraya taşınır;
# o da yoksa değerler rastgele üretilir, yalnızca panel yöneticisinin e-postası ve parolası sorulur.
#
# Veritabanında hiç araç yoksa (ilk kurulum) bir hizmet bölgesi ve örnek scooter'lar eklenir: bir küme panelin
# varsayılan harita merkezine (İstanbul), bir küme de verdiğin konuma; böylece telefonda haritayı açınca görünürler.
#
# API iki yoldan telefona açılır: aynı Wi-Fi'dan http://<Mac IP'si>:5017 ve her yerden (mobil veri dahil)
# Cloudflare hızlı tüneliyle https://<rastgele>.trycloudflare.com (hesap gerekmez; SCOOTLY_TUNNEL=0 kapatır).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
LOG_DIR="$ROOT/.local-build"
STATUS="$LOG_DIR/backend-status"
STAGED_ENV="$LOG_DIR/backend.env"
mkdir -p "$LOG_DIR"

BACKEND="${SCOOTLY_BACKEND_DIR:-$HOME/scootly-backend}"
DEPLOY="$BACKEND/deploy"
ENV_FILE="$DEPLOY/.env"
REPO_URL="https://github.com/Beratoguzhan620/Scootly.git"
API_LOCAL="http://127.0.0.1:5016"
LAN_PORT=5017
PANEL_URL="http://127.0.0.1:5096"
VOLUME="deploy_scootly-postgres-data"
CLOUDFLARED_VERSION="2026.9.1"
# Adana merkez; konum sorusunda boş bırakılırsa kullanılır.
DEFAULT_CENTER="37.0000,35.3213"
REQUIRED_SECRETS="POSTGRES_PASSWORD APP_DB_PASSWORD RABBITMQ_PASSWORD REDIS_PASSWORD JWT_KEY JWT_HUB_KEY DEVICE_CLIENT_SECRET PAYMENT_WEBHOOK_SECRET GRAFANA_ADMIN_PASSWORD"

COMMAND="${1:-up}"

step() { printf '\n==> %s\n' "$*"; }
fail() {
  printf '\nHATA: %s\n' "$*"
  [ "$COMMAND" = "up" ] && echo "DURUM: BASARISIZ" > "$STATUS"
  exit 1
}

# --- Docker --------------------------------------------------------------------------------------
# Docker Desktop CLI'yi her zaman /usr/local/bin'e koymaz; VS Code görevinin PATH'inde olmayabilir.
export PATH="$PATH:/usr/local/bin:/opt/homebrew/bin:$HOME/.docker/bin:/Applications/Docker.app/Contents/Resources/bin"
COMPOSE_PLUGIN="/Applications/Docker.app/Contents/Resources/cli-plugins/docker-compose"

docker_ready() { command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; }

ensure_docker() {
  docker_ready && return 0
  [ -d /Applications/Docker.app ] ||
    fail "Docker Desktop kurulu değil: https://www.docker.com/products/docker-desktop/ (Apple Silicon sürümü)"
  echo "Docker Desktop açılıyor. İlk açılışta çıkan sözleşmeyi ve izin pencerelerini onaylaman gerekebilir."
  open -a Docker
  local i
  for i in $(seq 1 90); do
    docker_ready && { echo "  Docker hazır."; return 0; }
    [ $((i % 15)) -eq 0 ] && echo "  hâlâ bekleniyor ($((i * 2)) sn)…"
    sleep 2
  done
  fail "Docker Desktop 3 dakikada hazır olmadı. Menü çubuğundaki balina simgesi 'Docker Desktop is running' diyene kadar bekleyip tekrar çalıştır."
}

compose() {
  if docker compose version >/dev/null 2>&1; then
    (cd "$DEPLOY" && docker compose --profile app "$@")
  elif [ -x "$COMPOSE_PLUGIN" ]; then
    (cd "$DEPLOY" && "$COMPOSE_PLUGIN" --profile app "$@")
  else
    fail "docker compose bulunamadı (Docker Desktop güncel mi?)."
  fi
}

# --- .env yardımcıları ---------------------------------------------------------------------------
env_value() { sed -n "s/^$1=//p" "$ENV_FILE" | tail -1; }

set_env() { # $1 = anahtar, $2 = değer. awk -v kaçış dizilerini işlediği için değer ortam değişkeniyle geçer.
  if grep -q "^$1=" "$ENV_FILE"; then
    K="$1" V="$2" awk -F= '$1 == ENVIRON["K"] { print ENVIRON["K"] "=" ENVIRON["V"]; next } { print }' \
      "$ENV_FILE" > "$ENV_FILE.tmp"
    mv "$ENV_FILE.tmp" "$ENV_FILE"
  else
    printf '%s=%s\n' "$1" "$2" >> "$ENV_FILE"
  fi
}

valid_password() { # Identity kuralı: en az 8 karakter, büyük harf, küçük harf, rakam
  local p="$1"
  [ "${#p}" -ge 8 ] && [[ $p =~ [A-Z] ]] && [[ $p =~ [a-z] ]] && [[ $p =~ [0-9] ]]
}

json_escape() { printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }

yes_answer() { case "$1" in e|E|evet|Evet|EVET|y|Y|yes|Yes) return 0 ;; *) return 1 ;; esac; }

# --- Adresler ------------------------------------------------------------------------------------
lan_ip() { # Varsayılan ağ arayüzünün IP'si; VPN tüneli (utun/ipsec) telefondan erişilemeyeceği için atlanır.
  local iface ip candidate
  iface=$(route -n get default 2>/dev/null | awk '/interface:/ { print $2 }')
  case "$iface" in utun* | ipsec* | ppp*) iface="" ;; esac
  for candidate in "$iface" en0 en1; do
    [ -n "$candidate" ] || continue
    ip=$(ipconfig getifaddr "$candidate" 2>/dev/null || true)
    if [ -n "$ip" ]; then printf '%s' "$ip"; return 0; fi
  done
  return 1
}

# Cloudflare hızlı tünelinin adresi konteynerin logunda yazar; konteyner yeniden oluşturulunca değişir.
tunnel_url() {
  compose logs --no-color tunnel 2>/dev/null | grep -o 'https://[a-z0-9-]*\.trycloudflare\.com' | tail -1 || true
}

# Yeni tünel adresinin DNS kaydı birkaç saniye sonra oluşur; Mac o arada "bulunamadı" yanıtını önbelleğe alırsa
# adres çalıştığı hâlde yanıt vermiyor görünür. Bu yüzden önce DNS-over-HTTPS (1.1.1.1) ile denenir.
tunnel_check() {
  curl -fsS -m 10 --doh-url https://1.1.1.1/dns-query "$1/health/ready" >/dev/null 2>&1 ||
    curl -fsS -m 10 "$1/health/ready" >/dev/null 2>&1
}

wait_tunnel_url() { # adres konteyner açıldıktan birkaç saniye sonra loga düşer (en fazla 60 sn beklenir)
  local i url
  for i in $(seq 1 30); do
    url=$(tunnel_url)
    if [ -n "$url" ]; then printf '%s' "$url"; return 0; fi
    sleep 2
  done
  return 1
}

tunnel_reachable() { # yeni adresin DNS kaydı birkaç saniye sonra oluşur
  local i
  sleep 5
  for i in $(seq 1 20); do
    tunnel_check "$1" && return 0
    sleep 3
  done
  return 1
}

# Tünel adresini bulup doğrular (sonuç TUNNEL değişkeninde). Hızlı tünel bağlantısı uzun süre koparsa Cloudflare onu
# siler ("Tunnel not found") ve konteyner boşuna yeniden dener; o durumda tünel bir kez yeniden oluşturulur (yeni adres).
ensure_tunnel() { # $1 = "recreate" ise doğrudan yeni tünel açılır
  if [ "${1:-}" = "recreate" ]; then
    compose up -d --force-recreate --no-deps tunnel
  fi
  TUNNEL=$(wait_tunnel_url || true)
  if [ -n "$TUNNEL" ] && tunnel_reachable "$TUNNEL"; then return 0; fi
  if [ "${1:-}" != "recreate" ]; then
    echo "  Tünel yanıt vermiyor; yeni tünel açılıyor (adres değişecek)…"
    compose up -d --force-recreate --no-deps tunnel
    TUNNEL=$(wait_tunnel_url || true)
    if [ -n "$TUNNEL" ] && tunnel_reachable "$TUNNEL"; then return 0; fi
  fi
  echo "  UYARI: Tünel çalışmıyor. Son loglar:"
  compose logs --no-color --tail 15 tunnel || true
  TUNNEL=""
  return 1
}

print_addresses() {
  local ip url
  ip=$(lan_ip || true)
  url=$(tunnel_url)
  echo
  echo "  Web paneli:               $PANEL_URL"
  echo "  API (bu Mac):             $API_LOCAL"
  if [ -n "$ip" ]; then
    echo "  API (aynı Wi-Fi):         http://$ip:$LAN_PORT"
  else
    echo "  API (aynı Wi-Fi):         bulunamadı (Mac'in yerel ağ IP'si yok; Wi-Fi bağlı mı?)"
  fi
  if [ -n "$url" ]; then
    echo "  API (her yerden, HTTPS):  $url"
    printf '%s\n' "$url" > "$LOG_DIR/tunnel-url"
  fi
  echo
  echo "  Telefonda: giriş ekranının altındaki \"Sunucu: …\" yazısına dokun, adresi yaz, \"Bağlantıyı test et\" ve"
  echo "  \"Kaydet\". HTTPS adresi mobil veride ve başka ağlarda da çalışır; Mac açık ve uyanık kalmalı. Bu adres"
  echo "  tünel her yeniden başladığında (Mac yeniden başlayınca, Docker kapanıp açılınca) değişir."
}

# --- Komutlar ------------------------------------------------------------------------------------
case "$COMMAND" in
  up) ;;
  status)
    exec > >(tee "$LOG_DIR/backend-status.log") 2>&1
    ensure_docker
    [ -d "$DEPLOY" ] || fail "Backend henüz kurulmamış: bash scripts/mac/backend.sh"
    compose ps
    if curl -fsS -m 3 "$API_LOCAL/health/ready" >/dev/null 2>&1; then echo "API: hazır"; else echo "API: hazır değil"; fi
    print_addresses
    URL=$(tunnel_url)
    if [ -n "$URL" ]; then
      echo
      if tunnel_check "$URL"; then
        echo "  Tünel yanıt veriyor."
      else
        echo "  UYARI: Tünel yanıt vermiyor. Yeni adres için: \"Tüneli yenile\" görevi (bash scripts/mac/backend.sh tunnel)."
        echo "  Son loglar:"
        compose logs --no-color --tail 15 tunnel || true
      fi
    fi
    exit 0
    ;;
  tunnel)
    exec > >(tee "$LOG_DIR/backend-status.log") 2>&1
    ensure_docker
    [ -d "$DEPLOY" ] || fail "Backend henüz kurulmamış: bash scripts/mac/backend.sh"
    curl -fsS -m 3 "$API_LOCAL/health/ready" >/dev/null 2>&1 || fail "API çalışmıyor; önce: bash scripts/mac/backend.sh"
    echo "Yeni tünel açılıyor (adres değişecek)…"
    rm -f "$LOG_DIR/tunnel-url"
    if ensure_tunnel recreate; then echo "  $TUNNEL yanıt veriyor."; fi
    print_addresses
    exit 0
    ;;
  stop)
    ensure_docker
    compose stop
    echo "Durduruldu. Veriler duruyor; tekrar başlatmak için: bash scripts/mac/backend.sh"
    exit 0
    ;;
  logs)
    ensure_docker
    compose logs --no-color --tail 200 api
    exit 0
    ;;
  *)
    echo "Kullanım: bash scripts/mac/backend.sh [up|status|tunnel|stop|logs]"
    exit 2
    ;;
esac

LOG="$LOG_DIR/backend.log"
: > "$LOG"
exec > >(tee -a "$LOG") 2>&1
echo "DURUM: CALISIYOR" > "$STATUS"
on_exit() {
  local code=$?
  [ "$code" -eq 0 ] || echo "DURUM: BASARISIZ (çıkış kodu $code)" > "$STATUS"
}
trap on_exit EXIT
caffeinate -i -w $$ &

step "Docker"
ensure_docker
docker version --format '  Docker {{.Server.Version}}' 2>/dev/null || true

step "Backend kaynak kodu: $BACKEND"
if [ -d "$BACKEND/.git" ]; then
  echo "  Mevcut kopya kullanılıyor: $(git -C "$BACKEND" log -1 --format='%h %s (%cd)' --date=short)"
else
  [ -e "$BACKEND" ] && fail "$BACKEND var ama bir git deposu değil. SCOOTLY_BACKEND_DIR ile başka bir klasör ver."
  git clone "$REPO_URL" "$BACKEND"
fi
[ -f "$DEPLOY/docker-compose.yml" ] || fail "$DEPLOY/docker-compose.yml bulunamadı."

step "Gizli değerler (deploy/.env)"
ENV_CREATED=0
if [ -f "$ENV_FILE" ]; then
  echo "  Mevcut deploy/.env kullanılıyor."
  [ -f "$STAGED_ENV" ] && echo "  Not: .local-build/backend.env kullanılmadı (deploy/.env zaten var)."
elif [ -f "$STAGED_ENV" ]; then
  cp "$STAGED_ENV" "$ENV_FILE"
  rm -f "$STAGED_ENV"
  ENV_CREATED=1
  echo "  .local-build/backend.env → deploy/.env taşındı."
else
  cp "$DEPLOY/.env.example" "$ENV_FILE"
  ENV_CREATED=1
  for key in $REQUIRED_SECRETS; do
    value=$(env_value "$key")
    if [ -z "$value" ] || [ "$value" = "degistir" ]; then set_env "$key" "$(openssl rand -hex 32)"; fi
  done
  echo "  Anahtarlar rastgele üretildi. Panel için ilk yönetici hesabını belirle:"
  read -r -p "  Yönetici e-postası: " FM_EMAIL
  while true; do
    read -r -s -p "  Parola (en az 8 karakter; büyük harf, küçük harf ve rakam): " FM_PASS
    echo
    valid_password "$FM_PASS" && break
    echo "  Parola kurala uymuyor, tekrar dene."
  done
  set_env BOOTSTRAP_FLEET_MANAGER_EMAIL "$FM_EMAIL"
  set_env BOOTSTRAP_FLEET_MANAGER_PASSWORD "$FM_PASS"
fi
chmod 600 "$ENV_FILE"

MISSING=""
for key in $REQUIRED_SECRETS; do
  value=$(env_value "$key")
  if [ -z "$value" ] || [ "$value" = "degistir" ]; then MISSING="$MISSING $key"; fi
done
[ -z "$MISSING" ] || fail "deploy/.env içinde boş değerler var:$MISSING"

if [ "$ENV_CREATED" -eq 1 ] && docker volume inspect "$VOLUME" >/dev/null 2>&1; then
  echo "  UYARI: Bu Mac'te daha önce oluşturulmuş bir Scootly veritabanı var ($VOLUME). Veritabanı ilk"
  echo "  kurulduğu parolayı kullanır; yeni .env'deki POSTGRES_PASSWORD farklıysa migrator bağlanamaz."
fi

# Örnek araçların konumu: yalnızca ilk kurulumda sorulur (sonradan araç varsa hiç eklenmez).
CENTER_FILE="$LOG_DIR/demo-center"
if [ "$ENV_CREATED" -eq 1 ] && [ ! -f "$CENTER_FILE" ]; then
  echo
  echo "  Telefonda haritayı açınca scooter'ları hemen görmek için örnekler senin çevrene eklenecek."
  echo "  Google Haritalar'da bulunduğun yere uzun bas, üstte çıkan koordinatı buraya yapıştır."
  read -r -p "  Konum (enlem, boylam) [boş: Adana merkez]: " ANSWER || ANSWER=""
  printf '%s\n' "$ANSWER" > "$CENTER_FILE"
fi

step "Bu Mac'e özel Compose ayarları (deploy/docker-compose.override.yml)"
# Compose bu dosyayı kendiliğinden okur. Takımın dosyalarına dokunulmaz; buradaki dosyalar git'e de girmez.
OVERRIDE="$DEPLOY/docker-compose.override.yml"
MIGRATOR_DOCKERFILE="$DEPLOY/migrator.local.Dockerfile"
if [ "$(uname -m)" = "arm64" ]; then
  # src/Scootly.Api/Dockerfile migration paketini sabit "-r linux-x64" ile üretiyor; Apple Silicon'da imaj arm64
  # olduğu için o dosya çalışmaz. Aynı aşama, paketi imajın mimarisi için üreterek burada yeniden tanımlanır.
  cat > "$MIGRATOR_DOCKERFILE" <<'EOF'
# syntax=docker/dockerfile:1
# scootly-mobile/scripts/mac/backend.sh oluşturdu (yalnızca bu Mac, git'e girmez).
# src/Scootly.Api/Dockerfile'daki "migrator" aşamasının aynısı; tek fark, EF Core migration paketinin sabit
# linux-x64 yerine imajın mimarisi (Apple Silicon'da linux-arm64) için üretilmesi.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Scootly.Domain/Scootly.Domain.csproj src/Scootly.Domain/
COPY src/Scootly.Application/Scootly.Application.csproj src/Scootly.Application/
COPY src/Scootly.Infrastructure/Scootly.Infrastructure.csproj src/Scootly.Infrastructure/
COPY src/Scootly.Api/Scootly.Api.csproj src/Scootly.Api/
RUN dotnet restore src/Scootly.Api/Scootly.Api.csproj
COPY src/ src/
RUN dotnet tool install --global dotnet-ef --version 10.0.12
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN case "$TARGETARCH" in arm64) rid=linux-arm64 ;; *) rid=linux-x64 ;; esac \
    && dotnet ef migrations bundle \
        --project src/Scootly.Infrastructure \
        --startup-project src/Scootly.Api \
        --configuration Release \
        --self-contained -r "$rid" \
        --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=build /app/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]
EOF
  MIGRATOR_OVERRIDE="
  # Apple Silicon: migration paketi arm64 için üretilsin (bkz. migrator.local.Dockerfile).
  migrator:
    build:
      dockerfile: deploy/migrator.local.Dockerfile"
  echo "  Apple Silicon: migrator imajı arm64 için derlenecek (takımın Dockerfile'ı linux-x64 sabitliyor)."
else
  MIGRATOR_OVERRIDE=""
fi
if [ "${SCOOTLY_TUNNEL:-1}" != "0" ]; then
  TUNNEL_OVERRIDE="
  # Yerel ağ dışından (mobil veri, başka Wi-Fi) erişim: Cloudflare hızlı tüneli. Hesap gerektirmez; adres
  # https://<rastgele>.trycloudflare.com biçimindedir ve konteyner yeniden oluşturulunca değişir.
  # Kapatmak için: SCOOTLY_TUNNEL=0 bash scripts/mac/backend.sh
  tunnel:
    image: cloudflare/cloudflared:$CLOUDFLARED_VERSION
    profiles: [\"app\"]
    command: tunnel --no-autoupdate --url http://api:8080
    restart: unless-stopped
    depends_on:
      api:
        condition: service_healthy"
else
  TUNNEL_OVERRIDE=""
fi
cat > "$OVERRIDE" <<EOF
# scootly-mobile/scripts/mac/backend.sh her çalıştığında yeniden yazar (yalnızca bu Mac, git'e girmez).
services:
  # API'yi yerel ağa da açar: telefon http://<bu Mac'in IP'si>:$LAN_PORT adresine bağlanır.
  # 127.0.0.1:5016 olduğu gibi kalır.
  api:
    ports:
      - "$LAN_PORT:8080"
$MIGRATOR_OVERRIDE
$TUNNEL_OVERRIDE
EOF
echo "  API ayrıca $LAN_PORT portundan yerel ağa açık (5016 yalnızca bu Mac'e açık kalır)."
[ -n "$TUNNEL_OVERRIDE" ] && echo "  İnternetten erişim için Cloudflare hızlı tüneli de açılacak (HTTPS)."
mkdir -p "$BACKEND/.git/info"
for ignored in deploy/docker-compose.override.yml deploy/migrator.local.Dockerfile; do
  grep -qx "$ignored" "$BACKEND/.git/info/exclude" 2>/dev/null || echo "$ignored" >> "$BACKEND/.git/info/exclude"
done

show_failure_logs() {
  compose ps -a || true
  local service
  for service in migrator db-init api; do
    echo "--- $service (son 40 satır)"
    compose logs --no-color --tail 40 "$service" || true
  done
}

step "Aynı adlı eski konteyner kontrolü"
# Takımın Compose dosyası altyapı servislerine sabit konteyner adı veriyor (scootly-postgres, scootly-rabbitmq, …).
# Başka bir Compose projesinden (önceki bir kurulum, "-p scootly-prod" ile açılan yığın vb.) kalan aynı adlı
# konteyner varsa yığın "container name is already in use" hatasıyla başlayamaz.
CONFLICTS=$(docker ps -a --filter "name=scootly-" \
  --format '{{.Names}}|{{.Label "com.docker.compose.project"}}|{{.Status}}' |
  awk -F'|' '$1 ~ /^scootly-/ && $2 != "deploy"')
if [ -z "$CONFLICTS" ]; then
  echo "  Çakışan konteyner yok."
else
  echo "  Başka bir kurulumdan kalan aynı adlı konteynerler var:"
  printf '%s\n' "$CONFLICTS" | awk -F'|' '{ printf "    %-24s proje: %-16s %s\n", $1, ($2 == "" ? "-" : $2), $3 }'
  echo "  Bunlar kaldırılmadan yığın başlayamaz. Yalnızca konteynerler kaldırılır; adlandırılmış volume'lardaki"
  echo "  veriler (ör. veritabanı) silinmez, o kurulum gerektiğinde kendi Compose komutuyla yeniden açılabilir."
  printf "  Kaldırılsın mı? (e/h) "
  read -r ANSWER || ANSWER=""
  printf '%s\n' "$ANSWER" >> "$LOG"
  yes_answer "$ANSWER" || fail "Eski konteynerler kaldırılmadı; yığın başlatılamıyor."
  printf '%s\n' "$CONFLICTS" | cut -d'|' -f1 | while read -r name; do
    docker rm -f "$name" >/dev/null && echo "  Kaldırıldı: $name"
  done
fi

step "Servisler derlenip başlatılıyor (ilk seferde imaj indirme + derleme ~15-20 dk)"
if ! compose up -d --build; then
  show_failure_logs
  fail "Servisler başlatılamadı (yukarıdaki loglara bakın)."
fi

step "API'nin hazır olması bekleniyor"
READY=0
for i in $(seq 1 60); do
  if curl -fsS -m 3 "$API_LOCAL/health/ready" >/dev/null 2>&1; then READY=1; break; fi
  [ $((i % 6)) -eq 0 ] && echo "  hâlâ bekleniyor ($((i * 5)) sn)…"
  sleep 5
done
if [ "$READY" -ne 1 ]; then
  show_failure_logs
  fail "API 5 dakikada hazır olmadı (yukarıdaki loglara bakın)."
fi
echo "  API hazır: $API_LOCAL/health/ready"

# --- Örnek veri ----------------------------------------------------------------------------------
api() { # $1 = metot, $2 = yol, $3 = gövde (isteğe bağlı). Yanıt gövdesi + son satırda HTTP kodu.
  local args=(-sS -m 15 -X "$1" -H "Accept: application/json" -w '\n%{http_code}')
  [ -n "${TOKEN:-}" ] && args+=(-H "Authorization: Bearer $TOKEN")
  [ -n "${3:-}" ] && args+=(-H "Content-Type: application/json" --data "$3")
  curl "${args[@]}" "$API_LOCAL$2"
}
status_of() { printf '%s\n' "$1" | tail -1; }

FM_EMAIL=$(env_value BOOTSTRAP_FLEET_MANAGER_EMAIL)
FM_PASS=$(env_value BOOTSTRAP_FLEET_MANAGER_PASSWORD)
if [ -z "$FM_EMAIL" ] || [ -z "$FM_PASS" ]; then
  echo
  echo "Not: deploy/.env'de yönetici hesabı (BOOTSTRAP_FLEET_MANAGER_*) yok; örnek araçlar eklenmedi."
else
  step "Örnek veri"
  TOKEN=""
  RESPONSE=$(api POST /api/auth/login "{\"email\":\"$(json_escape "$FM_EMAIL")\",\"password\":\"$(json_escape "$FM_PASS")\"}")
  if [ "$(status_of "$RESPONSE")" = "200" ]; then
    TOKEN=$(printf '%s' "$RESPONSE" | sed -n 's/.*"token"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  fi
  if [ -z "$TOKEN" ]; then
    echo "  Yönetici girişi başarısız (HTTP $(status_of "$RESPONSE")); örnek veri eklenmedi."
    echo "  Hesap bu e-postayla daha önce açılmışsa API ona yönetici yetkisi vermez (ilk yönetici kuralı)."
  else
    RESPONSE=$(api GET "/api/v2/vehicles?pageSize=1")
    COUNT=$(printf '%s' "$RESPONSE" | sed -n 's/.*"totalCount"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' | head -1)
    if [ "$(status_of "$RESPONSE")" != "200" ] || [ -z "$COUNT" ]; then
      echo "  Araç listesi alınamadı (HTTP $(status_of "$RESPONSE")); örnek veri eklenmedi."
    elif [ "$COUNT" != "0" ]; then
      echo "  Veritabanında $COUNT araç var; örnek veri eklenmedi."
    else
      CENTER=$(tr -d ' \t\r' < "$CENTER_FILE" 2>/dev/null || true)
      [[ $CENTER =~ ^-?[0-9]+(\.[0-9]+)?,-?[0-9]+(\.[0-9]+)?$ ]] || CENTER="$DEFAULT_CENTER"
      LAT=${CENTER%,*}
      LNG=${CENTER#*,}

      add_area() { # $1 = ad, $2 = enlem, $3 = boylam, $4 = yarı genişlik (derece)
        local body
        body=$(awk -v n="$1" -v la="$2" -v lo="$3" -v d="$4" 'BEGIN {
          printf "{\"name\":\"%s\",\"boundary\":[", n
          printf "{\"latitude\":%.6f,\"longitude\":%.6f},", la - d, lo - d
          printf "{\"latitude\":%.6f,\"longitude\":%.6f},", la + d, lo - d
          printf "{\"latitude\":%.6f,\"longitude\":%.6f},", la + d, lo + d
          printf "{\"latitude\":%.6f,\"longitude\":%.6f}]}", la - d, lo + d }')
        echo "  Hizmet bölgesi \"$1\": HTTP $(status_of "$(api POST /api/v1/service-areas "$body")")"
      }
      add_vehicle() { # $1 = marka, $2 = menzil, $3 = enlem, $4 = boylam, $5 = batarya
        local body
        body=$(awk -v b="$1" -v r="$2" -v la="$3" -v lo="$4" -v p="$5" 'BEGIN {
          printf "{\"brand\":\"%s\",\"rangeKm\":%d,\"latitude\":%.6f,\"longitude\":%.6f,\"batteryPercentage\":%d}", b, r, la, lo, p }')
        echo "  $1 (%$5): HTTP $(status_of "$(api POST /api/v1/vehicles "$body")")"
      }
      offset() { awk -v base="$1" -v delta="$2" 'BEGIN { printf "%.6f", base + delta }'; }

      echo "  Senin çevren:"
      add_area "Yakın Çevre" "$LAT" "$LNG" 0.012
      add_vehicle "Segway Ninebot" 40 "$(offset "$LAT" 0.0015)" "$(offset "$LNG" 0.0010)" 92
      add_vehicle "Xiaomi Pro 2" 30 "$(offset "$LAT" -0.0012)" "$(offset "$LNG" 0.0025)" 76
      add_vehicle "Segway Ninebot" 40 "$(offset "$LAT" 0.0030)" "$(offset "$LNG" -0.0018)" 58
      add_vehicle "Xiaomi Pro 2" 30 "$(offset "$LAT" -0.0025)" "$(offset "$LNG" -0.0022)" 34
      add_vehicle "Segway Ninebot" 40 "$(offset "$LAT" 0.0005)" "$(offset "$LNG" -0.0035)" 15
      add_vehicle "Xiaomi Pro 2" 30 "$(offset "$LAT" -0.0035)" "$(offset "$LNG" 0.0005)" 7
      echo "  Panelin varsayılan harita merkezi (İstanbul):"
      add_area "İstanbul Merkez" 41.0005 29.0000 0.0075
      add_vehicle "Segway Ninebot" 40 41.0025 29.0015 86
      add_vehicle "Xiaomi Pro 2" 30 40.9985 29.0040 54
      add_vehicle "Segway Ninebot" 40 41.0045 28.9975 17
      add_vehicle "Xiaomi Pro 2" 30 40.9960 28.9950 7
    fi
  fi
fi

step "Yerel ağdan erişim kontrolü"
IP=$(lan_ip || true)
if [ -n "$IP" ] && curl -fsS -m 5 "http://$IP:$LAN_PORT/health/ready" >/dev/null 2>&1; then
  echo "  http://$IP:$LAN_PORT yanıt veriyor."
else
  echo "  UYARI: API yerel ağ adresinden yanıt vermedi. Mac'te VPN (WARP, ProtonVPN) açıksa kapatıp tekrar dene."
fi
if /usr/libexec/ApplicationFirewall/socketfilterfw --getglobalstate 2>/dev/null | grep -qi enabled; then
  echo "  macOS güvenlik duvarı açık: Docker için 'gelen bağlantılara izin ver' sorusu çıkarsa izin ver."
fi

rm -f "$LOG_DIR/tunnel-url"
if [ -n "$TUNNEL_OVERRIDE" ]; then
  step "İnternetten erişim (Cloudflare hızlı tüneli)"
  if ensure_tunnel; then echo "  $TUNNEL yanıt veriyor."; fi
fi

ELAPSED=$(( SECONDS / 60 ))
step "Backend çalışıyor (${ELAPSED} dk)"
print_addresses
echo
echo "  Panel girişi: $FM_EMAIL (parola deploy/.env → BOOTSTRAP_FLEET_MANAGER_PASSWORD)"
echo "  Durdurmak için: bash scripts/mac/backend.sh stop   (veriler kalır)"
echo "DURUM: TAMAM" > "$STATUS"
