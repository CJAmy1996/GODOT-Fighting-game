"""Extract the original 104-108 spinning-sword pixels for Kamui's standing heavy."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "SpinningSword"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "standing_heavy_spinning_sword.gif"


def extract(source_id: int) -> Image.Image:
    image = Image.open(SOURCE / f"{source_id:03}.bmp").convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, _ = pixels[x, y]
            if green > 180 and green > red * 1.8 and green > blue * 1.8:
                pixels[x, y] = (red, green, blue, 0)
    # The source effect is stored horizontally. Kamui presents it on the
    # requested 45-degree axis in front of his head; keep nearest-neighbour
    # sampling so no source pixels are softened or redrawn.
    return image.rotate(-45, resample=Image.Resampling.NEAREST, expand=True)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for source_id in range(104, 109):
        frame = extract(source_id)
        frame.save(OUTPUT / f"spinning_sword_{source_id:03}.png")
        frames.append(frame)
    previews = [frame.resize((frame.width * 4, frame.height * 4), Image.Resampling.NEAREST) for frame in frames]
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    previews[0].save(REVIEW, save_all=True, append_images=previews[1:], duration=50, loop=0, disposal=2)
    print(REVIEW)


if __name__ == "__main__":
    main()
