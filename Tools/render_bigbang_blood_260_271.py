"""Render BBB blood burst 260-270 plus its fourteen source droplet children."""

from pathlib import Path
import random

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "blood_260_271_preview.gif"
CANVAS = (256, 208)
ANCHOR = (128, 126)
ORIGINS = ((4, 36), (10, 36), (17, 26), (24, 18), (29, 11), (23, -32),
           (24, -38), (22, -68), (22, -70), (22, -74), (24, -64))
HOLDS = (3, 2, 3, 4, 3, 3, 3, 3, 3, 3, 3)


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        keyed = green > 180 and green - max(red, blue) > 55
        pixels.append((red, green, blue, 0 if keyed else alpha))
    rgba.putdata(pixels)
    return rgba


def main() -> None:
    drawings = []
    for number in range(260, 271):
        with Image.open(SOURCE / f"{number:03d}.png") as source:
            drawings.append(remove_green(source))
    with Image.open(SOURCE / "271.png") as source:
        droplet = remove_green(source)

    rng = random.Random(271)
    particles = [(rng.uniform(-500, 500), rng.uniform(-1000, -150)) for _ in range(14)]
    frames = []
    drawing_index = 0
    drawing_end = HOLDS[0]
    for tick in range(sum(HOLDS)):
        while tick >= drawing_end and drawing_index < len(drawings) - 1:
            drawing_index += 1
            drawing_end += HOLDS[drawing_index]
        canvas = Image.new("RGBA", CANVAS, (17, 17, 26, 255))
        seconds = tick / 60.0
        main_y = -100 * seconds + 0.5 * 240 * seconds * seconds
        origin = ORIGINS[drawing_index]
        canvas.alpha_composite(drawings[drawing_index],
                               (ANCHOR[0] - origin[0], round(ANCHOR[1] - origin[1] + main_y)))
        particle_scale = max(0.0, 1.0 - (tick + 1) * 0.02)
        if particle_scale > 0:
            particle_image = droplet.resize((max(1, round(droplet.width * particle_scale)),
                                             max(1, round(droplet.height * particle_scale))),
                                            Image.Resampling.NEAREST)
            for vx, vy in particles:
                x = ANCHOR[0] + vx * seconds
                y = ANCHOR[1] + vy * seconds + 0.5 * 1800 * seconds * seconds
                canvas.alpha_composite(particle_image, (round(x - 2), round(y - 5)))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=[17] * len(frames),
                   loop=0, disposal=2, optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
