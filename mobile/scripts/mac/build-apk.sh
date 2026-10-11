#!/usr/bin/env bash
# Scootly APK'sını macOS'ta derler. Eksik araçları yönetici parolası istemeden kullanıcı klasörlerine kurar:
#
#   Node.js 22    ~/.scootly-build/node                          (yalnızca sistemde uygun sürüm yoksa)
#   JDK 17        ~/Library/Java/JavaVirtualMachines/temurin-17.jdk  (yalnızca JDK 17 yoksa)
#   Android SDK   ~/Library/Android/sdk                         (Android Studio'nun da kullandığı yer)
#
# Kullanım (VS Code: Terminal → Run Build Task… ya da ⇧⌘B):
#   bash scripts/mac/build-apk.sh
#   ABIS=arm64-v8a bash scripts/mac/build-apk.sh     # yalnızca 64 bit telefonlar: daha hızlı
#
# Çıktı: proje kökünde scootly.apk. Tüm çıktı .local-build/build.log dosyasına da yazılır.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
LOG_DIR="$ROOT/.local-build"
LOG="$LOG_DIR/build.log"
mkdir -p "$LOG_DIR"
: > "$LOG"
exec > >(tee -a "$LOG") 2>&1

STARTED=$(date +%s)
step() { printf '\n==> %s\n' "$*"; }
fail() {
  printf '\nHATA: %s\n' "$*"
  echo "DURUM: BASARISIZ" > "$LOG_DIR/status"
  exit 1
}
on_exit() {
  local code=$?
  [ "$code" -eq 0 ] && return
  echo "DURUM: BASARISIZ (çıkış kodu $code)" > "$LOG_DIR/status"
  printf '\nDerleme durdu (çıkış kodu %s). Ayrıntılar: %s\n' "$code" "$LOG"
}
trap on_exit EXIT
echo "DURUM: CALISIYOR" > "$LOG_DIR/status"

yes_answer() { case "$1" in e|E|evet|Evet|EVET|y|Y|yes|Yes) return 0 ;; *) return 1 ;; esac; }

download() { # $1 = adres, $2 = hedef dosya
  if [ -f "$2" ]; then echo "Önbellekte: $(basename "$2")"; return 0; fi
  echo "İndiriliyor: $1"
  curl -fL --retry 3 --connect-timeout 20 --progress-bar -o "$2.part" "$1"
  mv "$2.part" "$2"
}

[ "$(uname -s)" = "Darwin" ] || fail "Bu betik macOS içindir."
case "$(uname -m)" in
  arm64) NODE_ARCH=arm64; JDK_ARCH=aarch64 ;;
  x86_64) NODE_ARCH=x64; JDK_ARCH=x64 ;;
  *) fail "Desteklenmeyen işlemci: $(uname -m)" ;;
esac

TOOLS="$HOME/.scootly-build"
CACHE="$TOOLS/cache"
SDK="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-$HOME/Library/Android/sdk}}"
ABIS="${ABIS:-arm64-v8a,armeabi-v7a}"
mkdir -p "$CACHE" "$SDK"

step "Sistem"
sw_vers 2>/dev/null | sed 's/^/  /' || true
RAM_GB=$(( $(sysctl -n hw.memsize) / 1073741824 ))
FREE_GB=$(df -g "$HOME" | awk 'NR==2 {print $4}')
echo "  İşlemci: $(uname -m), bellek: ${RAM_GB} GB, boş disk: ${FREE_GB} GB"
NEEDED_GB=4
[ -d "$SDK/ndk" ] || NEEDED_GB=10
[ "$FREE_GB" -ge "$NEEDED_GB" ] ||
  fail "En az ${NEEDED_GB} GB boş disk gerekli (Android SDK + NDK + Gradle önbelleği ilk kurulumda ~8 GB)."

# --- Node.js -------------------------------------------------------------------------------------
step "Node.js"
[ -x "$TOOLS/node/bin/node" ] && export PATH="$TOOLS/node/bin:$PATH"
node_ok() {
  command -v node >/dev/null 2>&1 || return 1
  local major minor
  major=$(node -p 'process.versions.node.split(".")[0]')
  minor=$(node -p 'process.versions.node.split(".")[1]')
  [ "$major" -ge 22 ] || { [ "$major" -eq 20 ] && [ "$minor" -ge 19 ]; }
}
if ! node_ok; then
  echo "Uygun Node.js yok (20.19+ ya da 22+ gerekli); Node.js 22 kullanıcı klasörüne kuruluyor."
  SHASUMS=$(curl -fsSL https://nodejs.org/dist/latest-v22.x/SHASUMS256.txt)
  NODE_FILE=$(printf '%s\n' "$SHASUMS" | awk -v suffix="-darwin-$NODE_ARCH.tar.gz" '{ if (substr($2, length($2) - length(suffix) + 1) == suffix) print $2 }' | head -1)
  NODE_SUM=$(printf '%s\n' "$SHASUMS" | awk -v f="$NODE_FILE" '$2 == f { print $1 }')
  [ -n "$NODE_FILE" ] || fail "Node.js indirme dosyası bulunamadı."
  download "https://nodejs.org/dist/latest-v22.x/$NODE_FILE" "$CACHE/$NODE_FILE"
  echo "$NODE_SUM  $CACHE/$NODE_FILE" | shasum -a 256 -c - >/dev/null || fail "Node.js dosyasının sağlaması tutmadı."
  rm -rf "$TOOLS/node"
  mkdir -p "$TOOLS/node"
  tar -xzf "$CACHE/$NODE_FILE" -C "$TOOLS/node" --strip-components 1
  export PATH="$TOOLS/node/bin:$PATH"
fi
echo "  node $(node -v), npm $(npm -v)"

step "Bağımlılıklar (npm ci)"
cd "$ROOT"
STAMP="$LOG_DIR/package-lock.sha"
LOCK_SUM=$(shasum -a 256 package-lock.json | cut -d' ' -f1)
if [ -d node_modules ] && [ -f "$STAMP" ] && [ "$(cat "$STAMP")" = "$LOCK_SUM" ]; then
  echo "  node_modules güncel."
else
  npm ci --no-audit --no-fund
  echo "$LOCK_SUM" > "$STAMP"
fi

VERSIONS="$ROOT/node_modules/react-native/gradle/libs.versions.toml"
toml() { sed -n "s/^$1 = \"\\(.*\\)\"/\\1/p" "$VERSIONS"; }
COMPILE_SDK=$(toml compileSdk)
BUILD_TOOLS=$(toml buildTools)
NDK_VERSION=$(toml ndkVersion)
echo "  React Native'in beklediği: compileSdk $COMPILE_SDK, build-tools $BUILD_TOOLS, NDK $NDK_VERSION"

# --- JDK 17 --------------------------------------------------------------------------------------
step "JDK 17"
JDK_DIR="$HOME/Library/Java/JavaVirtualMachines/temurin-17.jdk"
if [ -x "$JDK_DIR/Contents/Home/bin/java" ]; then
  JAVA_HOME="$JDK_DIR/Contents/Home"
elif /usr/libexec/java_home -v 17 >/dev/null 2>&1; then
  JAVA_HOME="$(/usr/libexec/java_home -v 17)"
else
  echo "JDK 17 yok; Eclipse Temurin 17 kullanıcı klasörüne kuruluyor."
  JDK_JSON=$(curl -fsSL "https://api.adoptium.net/v3/assets/latest/17/hotspot?architecture=$JDK_ARCH&image_type=jdk&os=mac&vendor=eclipse")
  JDK_INFO=$(node -e '
    const assets = JSON.parse(require("fs").readFileSync(0, "utf8"));
    const p = assets[0].binary.package;
    console.log([p.link, p.checksum, p.name].join(" "));' <<<"$JDK_JSON")
  read -r JDK_URL JDK_SUM JDK_FILE <<<"$JDK_INFO"
  download "$JDK_URL" "$CACHE/$JDK_FILE"
  echo "$JDK_SUM  $CACHE/$JDK_FILE" | shasum -a 256 -c - >/dev/null || fail "JDK dosyasının sağlaması tutmadı."
  rm -rf "$JDK_DIR"
  mkdir -p "$JDK_DIR"
  tar -xzf "$CACHE/$JDK_FILE" -C "$JDK_DIR" --strip-components 1
  JAVA_HOME="$JDK_DIR/Contents/Home"
fi
export JAVA_HOME
export PATH="$JAVA_HOME/bin:$PATH"
java -version 2>&1 | head -1 | sed 's/^/  /'

# --- Android SDK ---------------------------------------------------------------------------------
step "Android SDK: $SDK"
SDKMANAGER="$SDK/cmdline-tools/latest/bin/sdkmanager"
if [ ! -x "$SDKMANAGER" ]; then
  echo "Android komut satırı araçları kuruluyor."
  REPO_XML=$(curl -fsSL https://dl.google.com/android/repository/repository2-3.xml)
  CLT_REV=$(printf '%s\n' "$REPO_XML" | grep -o 'commandlinetools-mac-[0-9]*_latest\.zip' | sed 's/[^0-9]//g' | sort -n | tail -1)
  [ -n "$CLT_REV" ] || fail "Android komut satırı araçlarının sürümü bulunamadı."
  CLT_FILE="commandlinetools-mac-${CLT_REV}_latest.zip"
  CLT_SHA1=$(printf '%s\n' "$REPO_XML" | tr -d '\n' | grep -o "<checksum type=\"sha1\">[0-9a-f]*</checksum>[[:space:]]*<url>$CLT_FILE</url>" | grep -o '[0-9a-f]\{40\}' | head -1 || true)
  download "https://dl.google.com/android/repository/$CLT_FILE" "$CACHE/$CLT_FILE"
  if [ -n "$CLT_SHA1" ]; then
    echo "$CLT_SHA1  $CACHE/$CLT_FILE" | shasum -a 1 -c - >/dev/null || fail "Komut satırı araçlarının sağlaması tutmadı."
  fi
  rm -rf "$CACHE/cmdline-tools" "$SDK/cmdline-tools/latest"
  unzip -q "$CACHE/$CLT_FILE" -d "$CACHE"
  mkdir -p "$SDK/cmdline-tools"
  mv "$CACHE/cmdline-tools" "$SDK/cmdline-tools/latest"
fi

if [ ! -f "$SDK/licenses/android-sdk-license" ]; then
  echo
  echo "Android SDK bileşenlerini indirmek için Google'ın Android SDK lisans koşullarını kabul etmen gerekiyor:"
  echo "  https://developer.android.com/studio/terms"
  printf "Kabul ediyor musun? (e/h) "
  read -r ANSWER
  yes_answer "$ANSWER" || fail "Lisans kabul edilmedi; kurulum durduruldu."
  { yes || true; } | "$SDKMANAGER" --sdk_root="$SDK" --licenses >/dev/null
fi

PACKAGES=("platform-tools" "platforms;android-$COMPILE_SDK" "build-tools;$BUILD_TOOLS" "ndk;$NDK_VERSION" "cmake;3.22.1")
MISSING=()
for package in "${PACKAGES[@]}"; do
  [ -d "$SDK/$(printf '%s' "$package" | tr ';' '/')" ] || MISSING+=("$package")
done
if [ ${#MISSING[@]} -gt 0 ]; then
  echo "Kuruluyor: ${MISSING[*]} (NDK ~1 GB, birkaç dakika sürebilir)"
  "$SDKMANAGER" --sdk_root="$SDK" --install "${MISSING[@]}" | grep -v '^\[' || true
  for package in "${MISSING[@]}"; do
    [ -d "$SDK/$(printf '%s' "$package" | tr ';' '/')" ] || fail "Kurulamadı: $package"
  done
else
  echo "  Gerekli SDK bileşenleri kurulu."
fi
export ANDROID_HOME="$SDK"
export PATH="$SDK/platform-tools:$PATH"

# --- Derleme -------------------------------------------------------------------------------------
step "Android projesi (expo prebuild)"
EXPO_NO_GIT_STATUS=1 EXPO_NO_TELEMETRY=1 CI=1 npx expo prebuild --platform android --no-install --clean
printf 'sdk.dir=%s\n' "$SDK" > android/local.properties
HEAP=2560m
[ "$RAM_GB" -ge 16 ] && HEAP=4g
sed -i '' "s/^org.gradle.jvmargs=.*/org.gradle.jvmargs=-Xmx$HEAP -XX:MaxMetaspaceSize=1g -Dfile.encoding=UTF-8/" android/gradle.properties

step "APK derleniyor ($ABIS) — ilk derleme 10-20 dakika sürebilir"
(cd android && ./gradlew assembleRelease --no-daemon -PreactNativeArchitectures="$ABIS")

APK="$ROOT/android/app/build/outputs/apk/release/app-release.apk"
[ -f "$APK" ] || fail "APK oluşmadı."
cp "$APK" "$ROOT/scootly.apk"
ELAPSED=$(( ($(date +%s) - STARTED) / 60 ))
step "Hazır: $ROOT/scootly.apk ($(du -h "$ROOT/scootly.apk" | cut -f1), ${ELAPSED} dk)"
echo "DURUM: TAMAM" > "$LOG_DIR/status"

# --- Telefona kurulum (isteğe bağlı) -------------------------------------------------------------
if "$SDK/platform-tools/adb" devices 2>/dev/null | awk 'NR > 1 && $2 == "device"' | grep -q .; then
  printf "\nUSB ile bağlı bir Android cihaz bulundu. APK kurulsun mu? (e/h) "
  read -r ANSWER
  if yes_answer "$ANSWER"; then
    "$SDK/platform-tools/adb" install -r "$ROOT/scootly.apk"
  fi
else
  echo
  echo "Telefona kurmak için: scootly.apk dosyasını telefona gönderip açın"
  echo "ya da telefonda USB hata ayıklamayı açıp USB ile bağlayın ve: adb install -r scootly.apk"
fi
