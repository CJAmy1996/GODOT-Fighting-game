"""Render BBB common action 41, wall bounce / wall hit, at source 60 Hz timing."""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "wall_hit_198_204_preview.gif"
CANVAS = (220, 200)
WALL_CONTACT = (44, 166)
ORIGINS = ((0, 80), (10, 132), (10, 136), (14, 141), (14, 138), (14, 134), (12, 130))


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
    for number, origin in zip(range(198, 205), ORIGINS):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        guide = ImageDraw.Draw(canvas)
        guide.line((WALL_CONTACT[0], 8, WALL_CONTACT[0], CANVAS[1] - 8), fill=(74, 79, 104, 255), width=2)
        canvas.alpha_composite(drawing, (WALL_CONTACT[0] - origin[0], WALL_CONTACT[1] - origin[1]))
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
