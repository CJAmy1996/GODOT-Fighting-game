"""Prepare the source-labeled horizontal slash hit spark with transparency."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct" / "347.png"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "SwordHitSpark" / "slash_horizontal.png"

image = Image.open(SOURCE).convert("RGBA")
pixels = image.load()
for y in range(image.height):
    for x in range(image.width):
        red, green, blue, _ = pixels[x, y]
        if green > 220 and red < 40 and blue < 40:
            pixels[x, y] = (red, green, blue, 0)
image = image.rotate(-90, resample=Image.Resampling.NEAREST, expand=True)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUTPUT)
print(OUTPUT)
