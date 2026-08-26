"""Extract the detached sword above Kamui's head from source drawing 094."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct" / "094.bmp"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Sword" / "kamui_sword.png"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "overhead_sword_094.png"


def main() -> None:
    # In drawing 094 the detached sword occupies the isolated top strip; the
    # character begins below it. This keeps the source pixels exactly intact.
    image = Image.open(SOURCE).convert("RGBA").crop((0, 0, 84, 28))
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, _ = pixels[x, y]
            if green > 180 and green > red * 1.8 and green > blue * 1.8:
                pixels[x, y] = (red, green, blue, 0)
    bounds = image.getbbox()
    if bounds is None:
        raise RuntimeError("No sword pixels found in source drawing 094")
    sword = image.crop(bounds)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    sword.save(OUTPUT)
    sword.resize((sword.width * 6, sword.height * 6), Image.Resampling.NEAREST).save(REVIEW)
    print(REVIEW)


if __name__ == "__main__":
    main()
