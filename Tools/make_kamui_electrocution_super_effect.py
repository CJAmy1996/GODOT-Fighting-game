"""Rotate source effect 152 around the victim with its authored 1f flicker."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct" / "152.png"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "ElectrocutionSuper"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "electrocution_super.gif"


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    pixels = source.load()
    for y in range(source.height):
        for x in range(source.width):
            red, green, blue, _ = pixels[x, y]
            if green > 180 and green > red * 1.8 and green > blue * 1.8:
                pixels[x, y] = (red, green, blue, 0)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    blank = Image.new("RGBA", source.size)
    frames = []
    for tick in range(48):
        frame = source.rotate(-(tick // 2) * 15, Image.Resampling.NEAREST) if tick % 2 == 0 else blank.copy()
        frame.save(OUTPUT / f"electrocution_{tick:02d}.png")
        frames.append(frame.resize((600, 600), Image.Resampling.NEAREST))
    frames[0].save(REVIEW, save_all=True, append_images=frames[1:], duration=17, loop=0, disposal=2)
    print(REVIEW)


if __name__ == "__main__":
    main()
