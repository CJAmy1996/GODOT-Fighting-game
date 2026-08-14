from __future__ import annotations

import colorsys
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2] / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = ROOT / "PerfectBlock"
YELLOW_HUE = 55.0 / 360.0


def recolor_pixel(pixel: tuple[int, ...]) -> tuple[int, ...]:
    red, green, blue = (channel / 255.0 for channel in pixel[:3])
    # Preserve the exact chroma-key field used by the runtime shader.
    if green >= 0.45 and green - max(red, blue) >= 0.22:
        return pixel

    hue, saturation, value = colorsys.rgb_to_hsv(red, green, blue)
    if saturation >= 0.10:
        red, green, blue = colorsys.hsv_to_rgb(YELLOW_HUE, saturation, value)
    recolored = tuple(round(channel * 255.0) for channel in (red, green, blue))
    return recolored + pixel[3:]


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for frame in range(237, 245):
        source = ROOT / f"{frame}.png"
        destination = OUTPUT / source.name
        with Image.open(source) as image:
            mode = "RGBA" if "A" in image.mode else "RGB"
            converted = image.convert(mode)
            converted.putdata([recolor_pixel(pixel) for pixel in converted.getdata()])
            converted.save(destination, optimize=False)


if __name__ == "__main__":
    main()
