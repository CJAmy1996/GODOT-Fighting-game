"""Render the user-ordered, benched 317 -> 309-316 dark dust sequence."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "dust_candidate_317_309_316_preview.gif"
CANVAS = (144, 112)
GROUND_ANCHOR = (72, 88)
ORDER = (317, 309, 310, 311, 312, 313, 314, 315, 316)


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        keyed = green > 180 and green - max(red, blue) > 55
        pixels.append((red, green, blue, 0 if keyed else alpha))
    rgba.putdata(pixels)
    return rgba


def main() -> None:
    frames = []
    for number in ORDER:
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        position = (GROUND_ANCHOR[0] - drawing.width // 2, GROUND_ANCHOR[1] - drawing.height)
        canvas.alpha_composite(drawing, position)
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=[33] * len(frames),
                   loop=0, disposal=2, optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
