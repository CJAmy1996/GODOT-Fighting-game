"""Render BBB common Guard with the exact source timing, scale ramp, and origin."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "guard_237_244_preview.gif"
CANVAS = (208, 192)
ANCHOR = (104, 154)
ORIGINS = ((6, 66), (6, 108), (8, 114), (12, 118), (12, 118), (12, 118), (12, 118), (14, 118))
HOLDS = (8, 2, 2, 2, 2, 2, 2, 2)


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        keyed = green > 180 and green - max(red, blue) > 55
        pixels.append((red, green, blue, 0 if keyed else alpha))
    rgba.putdata(pixels)
    return rgba


def main() -> None:
    frames: list[Image.Image] = []
    durations: list[int] = []
    for index, (number, origin, hold) in enumerate(zip(range(237, 245), ORIGINS, HOLDS)):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        scale = min(1.0, 0.3 + index * 0.1) if index == 0 else 1.0
        # Drawing 237 grows from 30% to 100% across its eight held ticks.
        tick_scales = [0.3 + tick * 0.1 for tick in range(8)] if index == 0 else [scale] * hold
        for tick_scale in tick_scales:
            resized = drawing.resize((max(1, round(drawing.width * tick_scale)),
                                      max(1, round(drawing.height * tick_scale))), Image.Resampling.NEAREST)
            canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
            canvas.alpha_composite(resized, (round(ANCHOR[0] - (drawing.width - origin[0]) * tick_scale),
                                             round(ANCHOR[1] - origin[1] * tick_scale)))
            frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))
            durations.append(17)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=durations,
                   loop=0, disposal=2, optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
