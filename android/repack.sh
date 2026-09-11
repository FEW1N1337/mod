#!/usr/bin/env bash
# FEW1N Mod — Android repackage
# Oyun APK'sına libfew1nmod.so'yu gömer + UnityPlayerActivity.onCreate'e System.loadLibrary
# enjekte eder, yeniden derleyip imzalar. ROOT GEREKMEZ (PairIP'siz APK şart!).
#
# Kullanim:  ./repack.sh game.apk libfew1nmod.so [cikti.apk]
# Gerekenler: apktool, zipalign, apksigner (Android build-tools), keytool, python3
set -euo pipefail

APK="${1:?kullanim: repack.sh game.apk libfew1nmod.so [cikti.apk]}"
SO="${2:?libfew1nmod.so yolu gerekli}"
OUT="${3:-few1n-modded.apk}"
WORK="$(mktemp -d)"
LIBNAME="few1nmod"   # System.loadLibrary("few1nmod") -> libfew1nmod.so

echo "==> apktool decompile"
apktool d -f -o "$WORK/dec" "$APK"

# PairIP guvenlik kontrolu: hedef gercekten PairIP'siz mi?
if grep -rqi "com/pairip" "$WORK/dec/smali"* 2>/dev/null; then
  echo "!! UYARI: APK'da com.pairip izi var — PairIP korumali olabilir, repackage COKEBILIR."
  echo "!! PairIP'siz bir taban kullanmalisin. Yine de devam icin ENTER, iptal icin Ctrl-C."
  read -r _
fi

echo "==> libfew1nmod.so ekleniyor (lib/arm64-v8a)"
mkdir -p "$WORK/dec/lib/arm64-v8a"
cp "$SO" "$WORK/dec/lib/arm64-v8a/lib${LIBNAME}.so"

echo "==> smali yamasi: UnityPlayerActivity.onCreate -> System.loadLibrary(\"$LIBNAME\")"
ACT=$(find "$WORK/dec" -path "*com/unity3d/player/UnityPlayerActivity.smali" | head -1)
if [ -z "$ACT" ]; then
  echo "!! UnityPlayerActivity.smali bulunamadi — manifest'teki launcher activity'yi elle yamalaman gerekir."
  echo "   Aktivite adi icin: aapt dump badging \"$APK\" | grep launchable-activity"
  exit 1
fi

python3 - "$ACT" "$LIBNAME" <<'PY'
import re, sys
path, lib = sys.argv[1], sys.argv[2]
s = open(path, encoding='utf-8').read()

inject = (
    '    const-string v0, "%s"\n'
    '    invoke-static {v0}, Ljava/lang/System;->loadLibrary(Ljava/lang/String;)V\n' % lib
)

# onCreate metodunu bul
m = re.search(r'(\.method\s+(?:public|protected)[^\n]*onCreate\([^\n]*\n)', s)
if not m:
    print("HATA: onCreate bulunamadi"); sys.exit(1)
start = m.end()
# .locals N satirini bul (register alani), gerekiyorsa v0 zaten var, .locals >=1 yeter
loc = re.search(r'(\.locals\s+)(\d+)', s[start:start+200])
if loc:
    n = int(loc.group(2))
    if n < 1:
        s = s[:start] + s[start:].replace(loc.group(0), '.locals 1', 1)
    # enjeksiyonu .locals satirindan hemen sonra koy
    abs_loc = start + loc.start()
    line_end = s.index('\n', abs_loc) + 1
    s = s[:line_end] + inject + s[line_end:]
else:
    # .registers formati (nadir) — onCreate govdesinin basina ekle
    s = s[:start] + inject + s[start:]

open(path, 'w', encoding='utf-8').write(s)
print("OK: loadLibrary enjekte edildi ->", path)
PY

echo "==> apktool build"
apktool b -o "$WORK/unsigned.apk" "$WORK/dec"

echo "==> zipalign"
zipalign -p -f 4 "$WORK/unsigned.apk" "$WORK/aligned.apk"

echo "==> imza (debug keystore uretilir)"
KS="$WORK/few1n.keystore"
if [ ! -f "$KS" ]; then
  keytool -genkey -v -keystore "$KS" -alias few1n -keyalg RSA -keysize 2048 \
    -validity 10000 -storepass few1n123 -keypass few1n123 \
    -dname "CN=FEW1N, OU=Mod, O=FEW1N, L=NA, S=NA, C=NA" >/dev/null 2>&1
fi
apksigner sign --ks "$KS" --ks-pass pass:few1n123 --key-pass pass:few1n123 \
  --out "$OUT" "$WORK/aligned.apk"

echo "==> BITTI: $OUT"
echo "   Kur: eski oyunu KALDIR (imza farkli) -> $OUT'i yukle -> 3 parmak dokun = menu"
rm -rf "$WORK"
