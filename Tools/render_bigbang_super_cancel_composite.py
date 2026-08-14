"""Render the layered BBB super-cancel composite used by the runtime scene."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = SOURCE.parent / "Review" / "bigbang_super_cancel_composite.gif"
CANVAS = (512, 512)


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    cleaned = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        if green > 180 and green - max(red, blue) > 55:
            cleaned.append((red, green, blue, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    rgba.putdata(cleaned)
    return rgba


def load(number: int) -> Image.Image:
    with Image.open(SOURCE / f"{number:03d}.png") as source:
        return remove_green(source)


def center(canvas: Image.Image, drawing: Image.Image, scale: float = 1.0) -> None:
    if scale != 1.0:
        drawing = drawing.resize(
            (round(drawing.width * scale), round(drawing.height * scale)),
            Image.Resampling.NEAREST,
        )
    canvas.alpha_composite(
        drawing,
        ((CANVAS[0] - drawing.width) // 2, (CANVAS[1] - drawing.height) // 2),
    )


def main() -> None:
    frames: list[Image.Image] = []
    for tick in range(20):
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        if tick < 16:
            lightning = tick // 2
            center(canvas, load(lightning))
            center(canvas, load(lightning + 8), 0.5)
        center(canvas, load(tick + 17))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=[16 if index % 3 < 2 else 17 for index in range(20)],
        loop=0,
        disposal=2,
        optimize=False,
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
