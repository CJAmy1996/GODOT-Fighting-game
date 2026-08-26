"""Prepare Sanzou's airborne SPD hold and a cape-only animation loop."""

from pathlib import Path

from PIL import Image

from cleanse_sanzo_generated_palette import (
    CANVAS_SIZE,
    SANDAL_BASELINE_Y,
    remap_palette,
    source_palette,
    visible_bounds,
)


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "spd_air_grab_raw" / "spd_air_grab_source.png"
OUTPUT_DIR = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "spd_air_grab"
OUTPUT = OUTPUT_DIR / "spd_air_grab_00.png"
LIVE_DIR = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Frames"
SHEET = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "sanzo_spd_air_grab_sheet.png"
PREVIEW = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "sanzo_spd_air_grab_preview.gif"
TARGET_HEIGHT = 190

# Tail tips stay attached at these x coordinates. The values are per-frame
# vertical shear slopes, giving a base -> lift -> crest -> settle loop.
CAPE_MOTION = ((0.0, 0.0), (0.06, 0.045), (0.13, 0.12), (0.06, 0.055))


def is_left_cape_tail(x: int, y: int, alpha: int) -> bool:
    """Select only the free left cape tip, away from Sanzou's body."""
    return alpha > 0 and x <= 105 and 126 <= y <= 171 and (x <= 97 or y <= 164)


def is_right_cape_tail(x: int, y: int, alpha: int) -> bool:
    """Select only the free right cape tip, away from Sanzou's body."""
    return alpha > 0 and x >= 215 and 128 <= y <= 184


def animate_cape(base: Image.Image, left_slope: float, right_slope: float) -> Image.Image:
    """Shear the two loose cape tips upward without touching any body pixel."""
    source = base.load()
    frame = base.copy()
    target = frame.load()
    moved: list[tuple[int, int, int, int, tuple[int, int, int, int]]] = []

    for y in range(base.height):
        for x in range(base.width):
            pixel = source[x, y]
            if is_left_cape_tail(x, y, pixel[3]):
                destination_y = y - round((105 - x) * left_slope)
            elif is_right_cape_tail(x, y, pixel[3]):
                destination_y = y - round((x - 215) * right_slope)
            else:
                continue
            moved.append((x, y, x, destination_y, pixel))
            target[x, y] = (0, 0, 0, 0)

    for _source_x, _source_y, destination_x, destination_y, pixel in moved:
        if 0 <= destination_y < frame.height:
            target[destination_x, destination_y] = pixel

    # Pixels outside the source/destination cape regions must remain bit-identical.
    affected = {(sx, sy) for sx, sy, _dx, _dy, _pixel in moved}
    affected.update((dx, dy) for _sx, _sy, dx, dy, _pixel in moved)
    for y in range(base.height):
        for x in range(base.width):
            if (x, y) not in affected and target[x, y] != source[x, y]:
                raise RuntimeError(f"Non-cape pixel changed at ({x}, {y}).")
    return frame


def save_animation(base: Image.Image) -> None:
    """Save source frames, live game copies, a contact sheet, and GIF preview."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    LIVE_DIR.mkdir(parents=True, exist_ok=True)
    frames = [animate_cape(base, left, right) for left, right in CAPE_MOTION]
    base_colors = {pixel[:3] for pixel in base.getdata() if pixel[3]}

    for index, frame in enumerate(frames):
        colors = {pixel[:3] for pixel in frame.getdata() if pixel[3]}
        if not colors.issubset(base_colors):
            raise RuntimeError(f"Cape frame {index} introduced colors outside Sanzou's palette.")
        frame.save(OUTPUT_DIR / f"spd_air_grab_{index:02d}.png", format="PNG")
        frame.save(LIVE_DIR / f"generated_spd_air_grab_{index:02d}.png", format="PNG")

    sheet = Image.new("RGBA", (CANVAS_SIZE[0] * len(frames), CANVAS_SIZE[1]), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, (CANVAS_SIZE[0] * index, 0))
    sheet.save(SHEET, format="PNG")

    frames[0].save(
        PREVIEW,
        save_all=True,
        append_images=frames[1:],
        duration=80,
        loop=0,
        disposal=2,
        transparency=0,
    )


def main() -> None:
    image = Image.open(SOURCE).convert("RGBA")
    bounds = visible_bounds(image)
    if bounds is None:
        raise RuntimeError("Generated SPD hold frame has no visible pixels.")

    crop = image.crop(bounds)
    scale = TARGET_HEIGHT / crop.height
    width = round(crop.width * scale)
    resized = crop.resize((width, TARGET_HEIGHT), Image.Resampling.LANCZOS)
    palette = source_palette()
    normalized = remap_palette(resized, palette, {})

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    x = (CANVAS_SIZE[0] - width) // 2
    y = SANDAL_BASELINE_Y - TARGET_HEIGHT + 1
    canvas.alpha_composite(normalized, (x, y))

    save_animation(canvas)
    print(
        f"Prepared four cape-only SPD frames in {OUTPUT_DIR.relative_to(ROOT)} at "
        f"{CANVAS_SIZE[0]}x{CANVAS_SIZE[1]}, "
        f"{TARGET_HEIGHT}px character height, baseline y={SANDAL_BASELINE_Y}, and {len(palette)} source colors."
    )


if __name__ == "__main__":
    main()
