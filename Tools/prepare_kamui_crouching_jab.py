"""Prepare the supplied Kamui crouching-jab pose for the in-game sprite canvas."""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCES = (
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-5bf999ae-1a69-4aad-bf03-ecfbcdbc06cf.png"),
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-7a449b51-776b-48e3-916c-f7449d3638ea.png"),
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-84940e02-f67f-452e-a06b-cc13f83c80af.png"),
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-1d05f284-66ab-43a8-8b82-69be078e146a.png"),
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-69a73866-703e-4f3f-8352-b9f4b31f3d5f.png"),
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-fa8b99ef-3ee5-461c-8fd3-fdac2e0f61ad.png"),
)
OUTPUT = (ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" /
          "Frames" / "Authored" / "CrouchingJab")

CANVAS_SIZE = (320, 384)
GROUND_Y = 250
BACKGROUND_CUTOFF = 10
SOURCE_SCALE = 0.18


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for index, path in enumerate(SOURCES):
        prepare_frame(path, OUTPUT / f"crouching_jab_{index:02d}.png")


def prepare_frame(source_path: Path, output_path: Path) -> None:
    source = Image.open(source_path).convert("RGBA")
    pixels = source.load()
    for y in range(source.height):
        for x in range(source.width):
            red, green, blue, _ = pixels[x, y]
            strength = max(red, green, blue)
            alpha = max(0, min(255, (strength - 2) * 255 // (BACKGROUND_CUTOFF - 2)))
            pixels[x, y] = (red, green, blue, alpha)

    bounds = source.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError("No foreground pixels found in crouching-jab source")
    pose = source.crop(bounds)
    pose = pose.resize((max(1, round(pose.width * SOURCE_SCALE)),
                        max(1, round(pose.height * SOURCE_SCALE))),
                       Image.Resampling.NEAREST)

    canvas = Image.new("RGBA", CANVAS_SIZE)
    left = (CANVAS_SIZE[0] - pose.width) // 2
    top = GROUND_Y - pose.height
    canvas.alpha_composite(pose, (left, top))
    canvas.save(output_path)
    print(output_path)


if __name__ == "__main__":
    main()
