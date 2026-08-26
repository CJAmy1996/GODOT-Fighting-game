"""Render the provisional 039-045 block-effect candidate at 60 Hz."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = SOURCE.parent / "Review" / "block_candidate_039_045_preview.gif"
CANVAS = (192, 192)


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        if green > 180 and green - max(red, blue) > 55:
            pixels.append((red, green, blue, 0))
        else:
            pixels.append((red, green, blue, alpha))
    rgba.putdata(pixels)
    return rgba


def main() -> None:
    frames: list[Image.Image] = []
    for number in range(39, 46):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        canvas.alpha_composite(
            drawing,
            ((CANVAS[0] - drawing.width) // 2, (CANVAS[1] - drawing.height) // 2),
        )
        # Runtime holds every provisional drawing for two 60 Hz ticks.
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=[33 for _ in frames],
        loop=0,
        disposal=2,
        optimize=False,
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
