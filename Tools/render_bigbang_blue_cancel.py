from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "blue_cancel_360_363_preview.gif"
ORDER = (360, 361, 362, 363)
HOLDS = (2, 2, 3, 3)


def black_key(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, alpha = pixels[x, y]
            luminance = max(red, green, blue)
            if luminance < 8:
                pixels[x, y] = (red, green, blue, 0)
            else:
                pixels[x, y] = (red, green, blue, alpha)
    return rgba


def main() -> None:
    frames = []
    durations = []
    for number, hold in zip(ORDER, HOLDS):
        effect = black_key(Image.open(SOURCE / f"{number}.png"))
        canvas = Image.new("RGBA", (256, 224), (18, 19, 27, 255))
        # Source origin is (0, 128): anchor the bottom-left of the 128px frame.
        canvas.alpha_composite(effect, (64, 48))
        frames.append(canvas.convert("RGB"))
        durations.append(round(hold * 1000 / 60))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=durations,
                   loop=0, optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
