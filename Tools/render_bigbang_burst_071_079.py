"""Render BBB common action 91, Burst, using its source 60 Hz origins."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = SOURCE.parent / "Review" / "burst_071_079_preview.gif"
CANVAS = (384, 384)
ANCHOR = (128, 320)
ORIGINS = ((-41, 272), (-22, 305), (1, 314), (-25, 211), (-29, 234),
           (-34, 264), (-36, 284), (-41, 297), (-46, 301))


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
    for number, origin in zip(range(71, 80), ORIGINS):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        canvas.alpha_composite(drawing, (ANCHOR[0] - origin[0], ANCHOR[1] - origin[1]))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=[17 for _ in frames],
        loop=0,
        disposal=2,
        optimize=False,
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
