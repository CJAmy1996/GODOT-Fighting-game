"""Render the benched PNG 282-290 fire loop without assigning gameplay behavior."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "fire_candidate_282_290_preview.gif"
CANVAS = (144, 128)
GROUND_ANCHOR = (72, 104)


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
    for number in range(282, 291):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        # No recovered action provides origins, so hold the flame's visual base
        # steady instead of allowing differently cropped source canvases to slide.
        position = (GROUND_ANCHOR[0] - drawing.width // 2, GROUND_ANCHOR[1] - drawing.height)
        canvas.alpha_composite(drawing, position)
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=[33] * len(frames),
                   loop=0, disposal=2, optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
