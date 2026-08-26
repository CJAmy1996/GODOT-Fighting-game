"""Convert Kamui's source medium-punch BMP pieces into horizontal transparent assets."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "MediumPunch"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "medium_punch_asset_horizontal.gif"


def convert(source_id: int) -> Image.Image:
    image = Image.open(SOURCE / f"{source_id}.bmp").convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, _ = pixels[x, y]
            if green > 220 and red < 40 and blue < 40:
                pixels[x, y] = (red, green, blue, 0)
    # Source points upward; clockwise rotation points it toward facing-right.
    return image.rotate(-90, resample=Image.Resampling.NEAREST, expand=True)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for source_id in (111, 112):
        frame = convert(source_id)
        frame.save(OUTPUT / f"medium_punch_{source_id}.png")
        frames.append(frame)
    canvas_size = (max(frame.width for frame in frames), max(frame.height for frame in frames))
    previews = []
    for frame in frames:
        canvas = Image.new("RGBA", canvas_size)
        canvas.alpha_composite(frame, (0, (canvas.height - frame.height) // 2))
        previews.append(canvas.resize((canvas.width * 3, canvas.height * 3), Image.Resampling.NEAREST))
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    previews[0].save(REVIEW, save_all=True, append_images=previews[1:], duration=(120, 500), loop=0, disposal=2)
    print(REVIEW)


if __name__ == "__main__":
    main()
