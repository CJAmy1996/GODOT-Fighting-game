"""Extract the exact source-timed Ikazuchi lightning drawings 146/147."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "Ikazuchi"


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for source_id in (146, 147):
        image = Image.open(SOURCE / f"{source_id}.png").convert("RGBA")
        pixels = image.load()
        for y in range(image.height):
            for x in range(image.width):
                red, green, blue, _ = pixels[x, y]
                if green > 180 and green > red * 1.8 and green > blue * 1.8:
                    pixels[x, y] = (red, green, blue, 0)
        image.save(OUTPUT / f"ikazuchi_{source_id}.png")


if __name__ == "__main__":
    main()
