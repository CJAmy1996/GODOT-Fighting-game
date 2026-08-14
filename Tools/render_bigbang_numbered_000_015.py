"""Render the exact runtime grouping of common drawings 000-015 for review."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = SOURCE.parent / "Review" / "numbered_000_015_preview.gif"
CANVAS = (512, 512)


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    cleaned = []
    for red, green, blue, alpha in rgba.getdata():
        if green > 180 and green - max(red, blue) > 55:
            cleaned.append((red, green, blue, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    rgba.putdata(cleaned)
    return rgba


def main() -> None:
    frames: list[Image.Image] = []
    for number in range(16):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        canvas.alpha_composite(
            drawing,
            ((CANVAS[0] - drawing.width) // 2, (CANVAS[1] - drawing.height) // 2),
        )
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    # GIF delay precision is 10 ms. Alternating 30/30/40 ms averages the exact
    # two-tick (33.333 ms) 60 Hz hold used by the runtime resource.
    delays = [30 if index % 3 < 2 else 40 for index in range(len(frames))]
    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=delays,
        loop=0,
        disposal=2,
        optimize=False,
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
