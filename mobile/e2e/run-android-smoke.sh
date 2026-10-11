#!/usr/bin/env bash
# CI'da emülatör açıldıktan sonra çalışır (reactivecircus/android-emulator-runner `script`).
# Kullanım: bash e2e/run-android-smoke.sh <apk> <çıktı klasörü>
set -euo pipefail

APK="$1"
OUT="$2"

adb wait-for-device
# Açılış animasyonları ve yazım denetimi testleri yavaşlatmasın.
adb shell settings put global window_animation_scale 0 || true
adb shell settings put global transition_animation_scale 0 || true
adb shell settings put global animator_duration_scale 0 || true

python3 e2e/android-smoke.py --apk "$APK" --out "$OUT" --api "http://10.0.2.2:5999"
