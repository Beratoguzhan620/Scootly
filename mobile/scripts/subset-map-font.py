#!/usr/bin/env python3
"""
Harita işaretlerindeki rakamlar için Barlow Condensed Bold'un küçük bir alt kümesini üretir.

Harita sayfası (WebView) uygulamanın yazı tiplerini göremez; işaretlerdeki pil yüzdesi ve küme sayıları da
uygulamadaki rakamlarla aynı görünsün diye yalnızca rakamları içeren ~3 KB'lık bir yazı tipi sayfaya gömülür.

    pip install fonttools
    python3 scripts/subset-map-font.py
"""
import base64
import io
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "node_modules/@expo-google-fonts/barlow-condensed/700Bold/BarlowCondensed_700Bold.ttf"
TARGET = ROOT / "src/features/map/mapFont.generated.ts"
TEXT = "0123456789%+"

font = TTFont(SOURCE)
options = subset.Options()
options.layout_features = ["tnum", "kern"]
options.notdef_outline = True
options.hinting = False
options.name_IDs = [0, 1, 2, 3, 4, 5, 6]
subsetter = subset.Subsetter(options=options)
subsetter.populate(text=TEXT)
subsetter.subset(font)
buffer = io.BytesIO()
font.save(buffer)
data = base64.b64encode(buffer.getvalue()).decode()

TARGET.write_text(
    "// Bu dosya scripts/subset-map-font.py ile üretildi; elle düzenlemeyin.\n"
    "// Barlow Condensed Bold — SIL Open Font License 1.1 (c) 2017 The Barlow Project Authors\n"
    f"// Yalnızca şu karakterler: {TEXT}\n\n"
    f"export const MAP_NUMERAL_FONT_TTF_BASE64 =\n  '{data}';\n"
)
print(f"{TARGET.relative_to(ROOT)}: {len(data) // 1024} KB")
