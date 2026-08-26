from pathlib import Path

from PIL import Image


EFFECT_DIR = Path(__file__).parents[1] / "Assets" / "Effects" / "BigBangCommon"


def convert_frame(path: Path) -> None:
    loaded = Image.open(path).convert("RGBA")
    # This also makes the utility safe to rerun on its own output: recover the
    # original black-backed RGB before rebuilding the alpha channel.
    source_pixels = []
    for red, green, blue, alpha in loaded.getdata():
        if alpha < 255:
            source_pixels.append(
                (
                    round(red * alpha / 255),
                    round(green * alpha / 255),
                    round(blue * alpha / 255),
                )
            )
        else:
            source_pixels.append((red, green, blue))

    output = Image.new("RGBA", loaded.size)

    converted = []
    for red, green, blue in source_pixels:
        # The captured background has a small constant red pedestal (7, 0, 0),
        # rather than mathematical RGB black.
        red = max(0, red - 7)
        alpha = max(red, green, blue)
        if alpha == 0:
            converted.append((0, 0, 0, 0))
            continue

        # The source was rendered over black, so its RGB is premultiplied by
        # coverage. Recover straight-alpha RGB to retain the soft fire glow
        # without carrying a black fringe into normal alpha compositing.
        converted.append(
            (
                min(255, round(red * 248 / alpha)),
                min(255, round(green * 248 / alpha)),
                min(255, round(blue * 248 / alpha)),
                min(255, round(alpha * 255 / 248)),
            )
        )

    output.putdata(converted)
    output.save(path, optimize=True)


for frame_number in range(440, 447):
    convert_frame(EFFECT_DIR / f"{frame_number}.png")
