#!/usr/bin/env python3
"""
Android duman testi: APK'yı emülatöre kurar, sahte API'ye (e2e/mock-api.py) bağlar ve ana akışları ekrandaki
metinlere dokunarak gezer: sürücü (rezervasyon, QR yerine elle kod, ödeme kartı, sürüş, ödeme, rezervasyon geçmişi)
ve filo yöneticisi (filo haritası, araç listesi). Her adımda ekran görüntüsü alır; hata olursa ekranı, arayüz ağacını
ve çökme kayıtlarını bırakır.

    python3 e2e/android-smoke.py --apk scootly.apk --out smoke --api http://10.0.2.2:5999

Gereken: PATH'te `adb`, açık bir emülatör (10.0.2.2 emülatörde bilgisayarın kendisidir).
"""
import argparse
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

PACKAGE = "com.scootly.mobile"
EMAIL = "surucu@scootly.test"
FLEET_EMAIL = "yonetici@scootly.test"
PASSWORD = "Scootly2026"
# Emülatörün konumuna en yakın kiralanabilir araç (%86); plakasındaki kod
NEAREST_CODE = "3F2B8C1A"


class SmokeFailure(Exception):
    pass


def adb(*args, check=True, capture=True, timeout=60):
    result = subprocess.run(["adb", *args], capture_output=capture, timeout=timeout)
    if check and result.returncode != 0:
        raise SmokeFailure(f"adb {' '.join(args)} başarısız: {result.stderr.decode(errors='replace')}")
    return result.stdout if capture else b""


def norm(text):
    """Büyük/küçük harf ve Türkçe I/İ farkını yok sayarak karşılaştırmak için."""
    return text.replace("İ", "I").replace("ı", "i").upper().replace("İ", "I")


class Node:
    def __init__(self, element):
        self.text = element.get("text") or ""
        self.desc = element.get("content-desc") or ""
        self.cls = element.get("class") or ""
        self.focused = element.get("focused") == "true"
        match = re.match(r"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", element.get("bounds") or "")
        x1, y1, x2, y2 = (int(v) for v in match.groups()) if match else (0, 0, 0, 0)
        self.center = ((x1 + x2) // 2, (y1 + y2) // 2)
        self.area = max(0, x2 - x1) * max(0, y2 - y1)

    def label(self):
        return f"{self.cls} text={self.text!r} desc={self.desc!r}"


class Device:
    def __init__(self, out: Path):
        self.out = out
        self.last_xml = ""
        self.step = 0

    def dump(self):
        for _ in range(3):
            raw = adb("exec-out", "uiautomator", "dump", "/dev/tty", check=False, timeout=40).decode("utf-8", "replace")
            start, end = raw.find("<?xml"), raw.rfind("</hierarchy>")
            if start != -1 and end != -1:
                self.last_xml = raw[start:end + len("</hierarchy>")]
                return [Node(e) for e in ET.fromstring(self.last_xml).iter("node")]
            time.sleep(1)
        return []

    def find(self, predicate, timeout=30, what="öğe"):
        deadline = time.time() + timeout
        while time.time() < deadline:
            nodes = [n for n in self.dump() if n.area > 0 and predicate(n)]
            if nodes:
                return nodes[0]
            time.sleep(1)
        raise SmokeFailure(f"Ekranda bulunamadı: {what}")

    def find_text(self, wanted, timeout=30, exact=False):
        key = norm(wanted)

        def matches(node):
            values = [norm(node.text), norm(node.desc)]
            return any(v == key for v in values) if exact else any(key in v for v in values)

        return self.find(matches, timeout, f"'{wanted}'")

    def wait_gone(self, wanted, timeout=30):
        key = norm(wanted)
        deadline = time.time() + timeout
        while time.time() < deadline:
            if not any(key in norm(n.text) or key in norm(n.desc) for n in self.dump()):
                return
            time.sleep(1)
        raise SmokeFailure(f"Ekrandan kalkmadı: '{wanted}'")

    def tap(self, node):
        adb("shell", "input", "tap", str(node.center[0]), str(node.center[1]))
        time.sleep(0.8)

    def tap_text(self, wanted, timeout=30, exact=False):
        node = self.find_text(wanted, timeout, exact)
        self.tap(node)
        return node

    def type_into(self, label, value, clear=0, submit=True):
        field = self.find(lambda n: n.cls.endswith("EditText") and norm(label) in norm(n.desc),
                          what=f"'{label}' alanı")
        self.tap(field)
        if clear:
            adb("shell", "input", "keyevent", "KEYCODE_MOVE_END")
            adb("shell", "input", "keyevent", *(["KEYCODE_DEL"] * clear))
        adb("shell", "input", "text", value.replace(" ", "%s"))
        if submit:
            adb("shell", "input", "keyevent", "KEYCODE_ENTER")
        time.sleep(0.8)

    def screenshot(self, name):
        self.step += 1
        path = self.out / f"{self.step:02d}-{name}.png"
        path.write_bytes(adb("exec-out", "screencap", "-p", timeout=30))
        print(f"  ekran görüntüsü: {path.name}", flush=True)


def run(args):
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    device = Device(out)

    print("APK kuruluyor...", flush=True)
    adb("install", "-r", "-g", args.apk, timeout=240)
    adb("shell", "pm", "grant", PACKAGE, "android.permission.ACCESS_FINE_LOCATION", check=False)
    adb("shell", "pm", "grant", PACKAGE, "android.permission.ACCESS_COARSE_LOCATION", check=False)
    adb("shell", "pm", "grant", PACKAGE, "android.permission.CAMERA", check=False)
    # Sahte API'deki araçların yanına (panelin varsayılan merkezi). Sıra: boylam, enlem.
    adb("emu", "geo", "fix", "29.0005", "41.0005", check=False)
    adb("logcat", "-c", check=False)

    print("Uygulama açılıyor...", flush=True)
    adb("shell", "monkey", "-p", PACKAGE, "-c", "android.intent.category.LAUNCHER", "1")
    device.find_text("Giriş yap", timeout=90)
    device.screenshot("giris")

    print("Sunucu adresi ayarlanıyor...", flush=True)
    device.tap_text("Sunucu ayarları")
    device.find_text("API adresi")
    device.type_into("API adresi", args.api, clear=60)
    device.find_text("Bağlantı başarılı", timeout=20)
    device.screenshot("sunucu-ayarlari")
    device.tap_text("Kaydet", exact=True)

    print("Giriş yapılıyor...", flush=True)
    device.find_text("Giriş yap")
    device.type_into("E-posta", EMAIL)
    device.type_into("Parola", PASSWORD)
    device.find_text("müsait scooter", timeout=45)
    time.sleep(6)  # harita karoları
    device.screenshot("harita")

    print("Araç seçiliyor...", flush=True)
    # Harita WebGL ile çizildiği için işaretler erişilebilirlik ağacında yok; en yakın scooter düğmesi seçer.
    # Emülatörün konumuna en yakın kiralanabilir araç %86 bataryalı olan.
    device.tap_text("En yakını göster", exact=True)
    device.find_text("Rezerve et", exact=True)
    device.find_text("Batarya yüzde 86")
    device.screenshot("arac-karti")

    print("Rezerve ediliyor...", flush=True)
    device.tap_text("Rezerve et", exact=True)
    device.find_text("Rezervasyonun", timeout=20)
    device.screenshot("rezervasyon")

    print("QR ekranında kod elle giriliyor...", flush=True)
    device.tap_text("QR okut ve başlat", exact=True)
    device.find_text("Kod okunmuyor mu?", timeout=20)
    device.screenshot("qr")
    device.tap_text("Kodu elle gir", exact=True)
    device.type_into("Scooter kodu", NEAREST_CODE)

    print("Ödeme kartı ekleniyor...", flush=True)
    device.find_text("Ödeme yöntemi", timeout=20)
    device.screenshot("surise-basla")
    device.tap(device.find(lambda n: norm(n.desc) == norm("Kart ekle") or norm(n.text) == norm("Kart ekle"),
                           what="Kart ekle"))
    device.tap_text("Test kartını doldur", exact=True)
    device.tap_text("Kartı kaydet", exact=True)

    print("Sürüş başlatılıyor...", flush=True)
    device.tap_text("Kilidi aç ve başla", exact=True, timeout=20)
    device.find_text("Sürüştesin", timeout=20, exact=True)
    device.screenshot("surus")

    print("Sürüş bitiriliyor...", flush=True)
    device.tap_text("Sürüşü bitir", exact=True)
    device.tap(device.find(lambda n: n.cls.endswith("Button") and norm(n.text) == norm("Bitir"), what="Bitir onayı"))
    device.find_text("Toplam", timeout=30)
    device.find_text("İyi yolculuklar!", timeout=45)
    device.screenshot("odeme")

    print("Rezervasyon geçmişi, sürüşler ve hesap...", flush=True)
    device.tap_text("Haritaya dön", exact=True)
    device.find_text("müsait scooter", timeout=20)
    device.tap_text("Rezervasyonlar", exact=True)
    device.find_text("Sürüşe dönüştü", timeout=20)
    device.screenshot("rezervasyonlar")
    device.tap_text("Sürüşlerim", exact=True)
    device.find_text("₺32,50", timeout=20)
    device.screenshot("suruslerim")
    device.tap_text("Hesap", exact=True)
    device.find_text("Parolayı değiştir", timeout=20)
    device.screenshot("hesap")

    print("Filo yöneticisi...", flush=True)
    device.tap_text("Çıkış yap", exact=True)
    device.tap(device.find(lambda n: n.cls.endswith("Button") and norm(n.text) == norm("Çıkış yap"),
                           what="Çıkış onayı"))
    device.find_text("Giriş yap", timeout=20)
    device.type_into("E-posta", FLEET_EMAIL, clear=40)
    device.type_into("Parola", PASSWORD)
    device.find_text("araç bu alanda", timeout=45)
    time.sleep(4)
    device.screenshot("filo-harita")
    device.tap_text("Araçlar", exact=True)
    device.find_text("Kayıp", timeout=20)
    device.screenshot("filo-araclar")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apk", required=True)
    parser.add_argument("--out", default="smoke")
    parser.add_argument("--api", default="http://10.0.2.2:5999")
    args = parser.parse_args()
    out = Path(args.out)
    failure = None

    try:
        run(args)
    except Exception as error:  # noqa: BLE001 — her hatada teşhis bilgisi bırak
        failure = error
        print(f"HATA: {error}", file=sys.stderr, flush=True)
        try:
            out.mkdir(parents=True, exist_ok=True)
            (out / "hata-ekrani.png").write_bytes(adb("exec-out", "screencap", "-p", check=False, timeout=30))
            device_xml = adb("exec-out", "uiautomator", "dump", "/dev/tty", check=False, timeout=40)
            (out / "hata-arayuz.xml").write_bytes(device_xml)
        except Exception as extra:  # noqa: BLE001
            print(f"Teşhis bilgisi alınamadı: {extra}", file=sys.stderr)

    crashes = adb("logcat", "-d", "-b", "crash", check=False).decode("utf-8", "replace").strip()
    app_log = adb("logcat", "-d", "-v", "time", "ReactNativeJS:V", "ReactNative:V", "chromium:V", "AndroidRuntime:E", "*:S",
                  check=False).decode("utf-8", "replace")
    out.mkdir(parents=True, exist_ok=True)
    (out / "logcat-uygulama.txt").write_text(app_log, encoding="utf-8")
    if crashes:
        (out / "logcat-cokme.txt").write_text(crashes, encoding="utf-8")
        print("Çökme kaydı bulundu:\n" + crashes[-3000:], file=sys.stderr)
        failure = failure or SmokeFailure("Uygulama çöktü")

    if failure:
        sys.exit(1)
    print("Duman testi başarılı.", flush=True)


if __name__ == "__main__":
    main()
