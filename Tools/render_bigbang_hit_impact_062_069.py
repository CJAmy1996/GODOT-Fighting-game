"""Render BBB strike-hit drawings 062-069 using the source action timing/origins."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = SOURCE.parent / "Review" / "hit_impact_062_069_preview.gif"
CANVAS = (256, 256)
CONTACT = (128, 128)
ORIGINS = ((1, 24), (64, 93), (77, 98), (76, 88), (68, 82), (74, 85), (76, 82), (76, 34))
HOLDS = (1, 2, 2, 2, 2, 2, 2, 2)


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
    durations: list[int] = []
    for number, origin, hold in zip(range(62, 70), ORIGINS, HOLDS):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawing = remove_green(source)
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        canvas.alpha_composite(drawing, (CONTACT[0] - origin[0], CONTACT[1] - origin[1]))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))
        durations.append(17 if hold == 1 else 33)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
        optimize=False,
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
