"""Render the unassigned KIR 215-220 energy-ring candidate with additive keying."""

from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "energy_ring_candidate_275_280_preview.gif"
CANVAS = (192, 176)
ANCHOR = (96, 88)


def key_additive(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        if green > 180 and green - max(red, blue) > 55:
            pixels.append((red, green, blue, 0))
            continue
        energy = max(red, green, blue) / 255.0
        blend = max(0.0, min(1.0, (energy - 0.035) / 0.085))
        blend = blend * blend * (3.0 - 2.0 * blend)
        pixels.append((red, green, blue, round(alpha * blend)))
    rgba.putdata(pixels)
    return rgba


def composite_additive(background: Image.Image, drawing: Image.Image, position: tuple[int, int]) -> Image.Image:
    layer = Image.new("RGBA", background.size, (0, 0, 0, 0))
    layer.alpha_composite(drawing, position)
    premultiplied = Image.composite(layer.convert("RGB"), Image.new("RGB", background.size), layer.getchannel("A"))
    return ImageChops.add(background.convert("RGB"), premultiplied).convert("RGBA")


def main() -> None:
    frames = []
    for number in range(275, 281):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = key_additive(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        canvas = composite_additive(canvas, drawing,
                                    (ANCHOR[0] - drawing.width // 2, ANCHOR[1] - drawing.height // 2))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=[33] * len(frames),
                   loop=0, disposal=2, optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
