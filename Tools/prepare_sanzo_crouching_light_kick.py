"""Build Sanzou's crouching light-kick frames from crouch references and generated poses."""

from pathlib import Path
from statistics import median
from PIL import Image

from cleanse_sanzo_generated_palette import (
    ALPHA_THRESHOLD,
    CANVAS_SIZE,
    FRAME_DIR,
    ROOT,
    SANDAL_BASELINE_Y,
    remap_palette,
    simplify_material_detail,
    source_palette,
    visible_bounds,
)


RAW_SHEET = (
    ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" /
    "crouching_light_kick_raw" / "crouching_light_kick_sheet.png"
)
OUTPUT_DIR = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "crouching_light_kick"
CONTACT_SHEET = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "sanzo_crouching_light_kick_sheet.png"
PREVIEW = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "sanzo_crouching_light_kick_preview.gif"
REFERENCE_START = FRAME_DIR / "group_13_117.png"
REFERENCE_SHIFT = FRAME_DIR / "group_13_118.png"


def target_crouch_height() -> int:
    heights: list[int] = []
    for path in FRAME_DIR.glob("group_13_*.png"):
        bounds = visible_bounds(Image.open(path))
        if bounds is not None:
            heights.append(bounds[3] - bounds[1])
    if not heights:
        raise RuntimeError("Could not measure Sanzou's crouch animation.")
    return round(median(heights))


def normalize_generated_pose(
    cell: Image.Image,
    frame_index: int,
    target_height: int,
    palette: list[tuple[int, int, int]],
    cache: dict,
) -> Image.Image:
    bounds = visible_bounds(cell)
    if bounds is None:
        raise RuntimeError(f"Generated crouching-light-kick pose {frame_index} is empty.")
    crop = cell.convert("RGBA").crop(bounds)
    scale = target_height / crop.height
    target_width = round(crop.width * scale)
    resized = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    resized = remap_palette(resized, palette, cache)
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    canvas.alpha_composite(
        resized,
        ((CANVAS_SIZE[0] - target_width) // 2, SANDAL_BASELINE_Y - target_height + 1),
    )
    return simplify_material_detail(canvas, min(frame_index + 1, 5))


def main() -> None:
    sheet = Image.open(RAW_SHEET).convert("RGBA")
    if sheet.size != (1536, 1024):
        raise RuntimeError(f"Expected 1536Ã—1024 generated sheet, got {sheet.size}.")
    palette = source_palette()
    cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    height = target_crouch_height()
    generated = [
        normalize_generated_pose(sheet.crop((index * 512, 0, (index + 1) * 512, 1024)), index, height, palette, cache)
        for index in range(3)
    ]

    # Exact authored crouch drawings cover neutral and weight shift. Only the two
    # extended-leg drawings come from the generated edit, minimizing style drift.
    crouch = Image.open(REFERENCE_START).convert("RGBA")
    shift = Image.open(REFERENCE_SHIFT).convert("RGBA")
    frames = [crouch, shift, generated[1], generated[2], crouch.copy()]
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for index, frame in enumerate(frames):
        frame.save(OUTPUT_DIR / f"crouching_light_kick_{index:02d}.png", format="PNG")
        frame.save(FRAME_DIR / f"generated_crouching_light_kick_{index:02d}.png", format="PNG")

    contact = Image.new("RGBA", (CANVAS_SIZE[0] * len(frames), CANVAS_SIZE[1]), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        contact.alpha_composite(frame, (index * CANVAS_SIZE[0], 0))
    contact.save(CONTACT_SHEET, format="PNG")
    frames[0].save(
        PREVIEW,
        save_all=True,
        append_images=frames[1:],
        duration=(33, 33, 33, 50, 350),
        loop=0,
        disposal=2,
        transparency=0,
    )
    print(
        f"Prepared {len(frames)} crouching-light-kick frames at {height}px crouch height, "
        f"baseline y={SANDAL_BASELINE_Y}, using Sanzou's {len(palette)}-color palette."
    )


if __name__ == "__main__":
    main()
