"""Render the BBB technique/throw hit ring tail, PNG 161-168."""

from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "technique_hit_ring_161_168_preview.gif"
CANVAS = (320, 320)


def key_legacy_additive(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        energy = max(red, green, blue) / 255.0
        blend = max(0.0, min(1.0, (energy - 0.035) / (0.12 - 0.035)))
        blend = blend * blend * (3.0 - 2.0 * blend)
        pixels.append((red, green, blue, int(alpha * blend)))
    rgba.putdata(pixels)
    return rgba


def additive_composite(background: Image.Image, drawing: Image.Image) -> Image.Image:
    black = Image.new("RGB", background.size, (0, 0, 0))
    premultiplied = Image.composite(drawing.convert("RGB"), black, drawing.getchannel("A"))
    return ImageChops.add(background.convert("RGB"), premultiplied).convert("RGBA")


def main() -> None:
    frames: list[Image.Image] = []
    for number in range(161, 169):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            frame = key_legacy_additive(source)
        background = Image.new("RGBA", CANVAS, (36, 45, 64, 255))
        background = additive_composite(background, frame)
        frames.append(background.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

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
