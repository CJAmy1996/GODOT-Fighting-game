"""Prepare source drawings 104-108 as Kamui's low swinging-sword sweep."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "SweepSword"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "crouching_heavy_sword_sweep.gif"


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for index, source_id in enumerate(range(104, 109)):
        frame = Image.open(SOURCE / f"{source_id:03}.bmp").convert("RGBA")
        pixels = frame.load()
        for y in range(frame.height):
            for x in range(frame.width):
                red, green, blue, _ = pixels[x, y]
                if green > 180 and green > red * 1.8 and green > blue * 1.8:
                    pixels[x, y] = (red, green, blue, 0)
        frame.save(OUTPUT / f"sweep_sword_{index:02d}.png")
        frames.append(frame)
    size = max(max(frame.width, frame.height) for frame in frames)
    previews = []
    for frame in frames:
        canvas = Image.new("RGBA", (size, size))
        canvas.alpha_composite(frame, ((size - frame.width) // 2, (size - frame.height) // 2))
        previews.append(canvas.resize((size * 5, size * 5), Image.Resampling.NEAREST))
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    previews[0].save(REVIEW, save_all=True, append_images=previews[1:], duration=50, loop=0, disposal=2)
    print(REVIEW)


if __name__ == "__main__":
    main()
