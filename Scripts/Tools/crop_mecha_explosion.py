from __future__ import annotations

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_m_heita_pct"
OUTPUT = ROOT / "Assets" / "Effects" / "BigBangMechaExplosion"


def is_green_key(red: int, green: int, blue: int) -> bool:
    return green >= 115 and green - max(red, blue) >= 50


def main() -> None:
    images = [Image.open(SOURCE / f"{frame}.png").convert("RGBA") for frame in range(433, 442)]
    width, height = images[0].size
    center_x, center_y = width / 2.0, height / 2.0
    visible = []
    for image in images:
        for y in range(height):
            for x in range(width):
                red, green, blue, _ = image.getpixel((x, y))
                if not is_green_key(red, green, blue):
                    visible.append((x, y))

    extent_x = max(abs(x - center_x) for x, _ in visible) + 1
    extent_y = max(abs(y - center_y) for _, y in visible) + 1
    left = max(0, int(center_x - extent_x))
    top = max(0, int(center_y - extent_y))
    right = min(width, int(center_x + extent_x))
    bottom = min(height, int(center_y + extent_y))

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for frame, image in zip(range(433, 442), images):
        pixels = image.load()
        for y in range(height):
            for x in range(width):
                red, green, blue, alpha = pixels[x, y]
                if is_green_key(red, green, blue):
                    pixels[x, y] = (red, green, blue, 0)
        image.crop((left, top, right, bottom)).save(OUTPUT / f"{frame}.png", optimize=False)


if __name__ == "__main__":
    main()
