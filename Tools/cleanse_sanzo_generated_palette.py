"""Normalize the generated Sanzou sweep to his source palette, scale, and baseline."""

from collections import Counter
from pathlib import Path
from statistics import median
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
FRAME_DIR = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Frames"
GENERATED_DIR = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "crouching_heavy_kick"
NORMALIZED_DIR = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "crouching_heavy_kick_normalized"
SHEET_PATH = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "sanzo_crouching_heavy_kick_sheet_normalized.png"
PREVIEW_PATH = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "sanzo_crouching_heavy_kick_preview.gif"

CANVAS_SIZE = (320, 384)
SANDAL_BASELINE_Y = 249
ALPHA_THRESHOLD = 16
# These are the authored crouching HP, crouching LP, and crouching MP groups.
CROUCHING_ATTACK_GROUPS = (17, 18, 19)

# The source sheet contains a few rare grayscale cleanup colors. They are valid
# somewhere in the character, but using all of them across generated trousers
# creates noisy, over-rendered fabric. The four values below are Sanzou's actual
# high-frequency charcoal ramp.
CORE_CLOTH_COLORS = ((20, 24, 28), (44, 47, 51), (68, 70, 75), (93, 93, 99))
CLOTH_SOURCE_COLORS = {
    (16, 16, 16), (20, 24, 28), (32, 32, 32), (44, 47, 51),
    (48, 48, 48), (64, 64, 64), (68, 70, 75), (93, 93, 99), (96, 96, 96),
}

# Six broad values retain Sanzou's warm skin ramp while avoiding the generated
# frame's extra micro-gradations. Internal single-pixel noise is collapsed below.
CORE_SKIN_COLORS = (
    (51, 20, 10), (127, 77, 52), (175, 95, 70),
    (197, 145, 96), (230, 190, 143), (243, 220, 185),
)
SKIN_SOURCE_COLORS = {
    (51, 20, 10), (103, 60, 35), (127, 77, 52), (175, 95, 70),
    (181, 108, 50), (197, 145, 96), (214, 171, 96),
    (230, 190, 143), (243, 220, 185),
}

# Per-frame left edge of the trouser region after normalization. This avoids
# flattening the cape even where it shares the same charcoal colors.
PANTS_LEFT_X = (140, 125, 125, 125, 120, 140)
PANTS_TOP_Y = 160


def visible_bounds(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.convert("RGBA").getchannel("A")
    return alpha.point(lambda value: 255 if value > ALPHA_THRESHOLD else 0).getbbox()


def source_palette() -> list[tuple[int, int, int]]:
    """Return every established opaque color used by Sanzou's 281 source frames."""
    counts: Counter[tuple[int, int, int]] = Counter()
    for path in FRAME_DIR.glob("group_*.png"):
        image = Image.open(path).convert("RGBA")
        counts.update((red, green, blue) for red, green, blue, alpha in image.getdata() if alpha > 127)
    # Ignore one-off contamination while retaining antialias shades used by the source art.
    return [color for color, count in counts.most_common() if count >= 10]


def target_crouching_attack_height() -> int:
    heights: list[int] = []
    for group in CROUCHING_ATTACK_GROUPS:
        for path in FRAME_DIR.glob(f"group_{group:02d}_*.png"):
            bounds = visible_bounds(Image.open(path))
            if bounds is not None:
                heights.append(bounds[3] - bounds[1])
    if not heights:
        raise RuntimeError("Could not measure Sanzou's authored crouching attacks.")
    return round(median(heights))


def nearest(color: tuple[int, int, int], palette: list[tuple[int, int, int]]) -> tuple[int, int, int]:
    red, green, blue = color
    # Weight green most strongly so perceived lightness and skin/cloth ramps stay stable.
    return min(
        palette,
        key=lambda item: 2 * (red - item[0]) ** 2 + 4 * (green - item[1]) ** 2 + (blue - item[2]) ** 2,
    )


def remap_palette(image: Image.Image, palette: list[tuple[int, int, int]], cache: dict) -> Image.Image:
    pixels = []
    for red, green, blue, alpha in image.convert("RGBA").getdata():
        if alpha == 0:
            pixels.append((0, 0, 0, 0))
            continue
        color = (red, green, blue)
        mapped = cache.setdefault(color, nearest(color, palette))
        pixels.append((*mapped, alpha))
    result = Image.new("RGBA", image.size, (0, 0, 0, 0))
    result.putdata(pixels)
    return result


def nearest_unweighted(color: tuple[int, int, int], ramp: tuple[tuple[int, int, int], ...]) -> tuple[int, int, int]:
    return min(ramp, key=lambda item: sum((color[channel] - item[channel]) ** 2 for channel in range(3)))


def smooth_material_clusters(
    pixels: list[tuple[int, int, int, int]],
    width: int,
    height: int,
    material: set[tuple[int, int, int]],
    eligible,
    passes: int,
    radius: int,
) -> list[tuple[int, int, int, int]]:
    """Remove isolated shade pixels without blurring silhouettes or alpha edges."""
    for _ in range(passes):
        source = pixels[:]
        for y in range(radius, height - radius):
            for x in range(radius, width - radius):
                index = y * width + x
                red, green, blue, alpha = source[index]
                color = (red, green, blue)
                if alpha == 0 or color not in material or not eligible(x, y):
                    continue
                neighbors: list[tuple[int, int, int]] = []
                for offset_y in range(-radius, radius + 1):
                    for offset_x in range(-radius, radius + 1):
                        if offset_x == 0 and offset_y == 0:
                            continue
                        neighbor = source[(y + offset_y) * width + x + offset_x]
                        neighbor_color = neighbor[:3]
                        if neighbor[3] > 0 and neighbor_color in material:
                            neighbors.append(neighbor_color)
                sample_area = (radius * 2 + 1) ** 2 - 1
                if len(neighbors) < sample_area // 2 + 1:
                    continue
                # A per-channel median collapses one-pixel lines and speckles even
                # when the surrounding shade cluster contains two adjacent ramp
                # values. Re-quantizing keeps every result on the canonical ramp.
                median_color = tuple(round(median(item[channel] for item in neighbors)) for channel in range(3))
                smoothed = nearest_unweighted(median_color, tuple(material))
                if smoothed != color:
                    pixels[index] = (*smoothed, alpha)
    return pixels


def simplify_material_detail(image: Image.Image, frame_index: int) -> Image.Image:
    pixels = list(image.convert("RGBA").getdata())
    width, height = image.size

    pants_left = PANTS_LEFT_X[frame_index]
    for index, (red, green, blue, alpha) in enumerate(pixels):
        if alpha == 0:
            continue
        x = index % width
        y = index // width
        color = (red, green, blue)
        if x >= pants_left and y >= PANTS_TOP_Y and color in CLOTH_SOURCE_COLORS:
            pixels[index] = (*nearest_unweighted(color, CORE_CLOTH_COLORS), alpha)
        elif color in SKIN_SOURCE_COLORS:
            pixels[index] = (*nearest_unweighted(color, CORE_SKIN_COLORS), alpha)

    cloth = set(CORE_CLOTH_COLORS)
    skin = set(CORE_SKIN_COLORS)
    pixels = smooth_material_clusters(
        pixels, width, height, cloth,
        lambda x, y: x >= pants_left and y >= PANTS_TOP_Y,
        passes=2,
        radius=2,
    )
    pixels = smooth_material_clusters(
        pixels, width, height, skin,
        lambda _x, _y: True,
        passes=1,
        radius=2,
    )
    result = Image.new("RGBA", image.size, (0, 0, 0, 0))
    result.putdata(pixels)
    return result


def normalize_frame(source: Path, frame_index: int, target_height: int, palette: list[tuple[int, int, int]], cache: dict) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    bounds = visible_bounds(image)
    if bounds is None:
        return Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    crop = image.crop(bounds)
    scale = target_height / crop.height
    target_width = round(crop.width * scale)
    resized = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    resized = remap_palette(resized, palette, cache)

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    paste_x = (CANVAS_SIZE[0] - target_width) // 2
    paste_y = SANDAL_BASELINE_Y - target_height + 1
    canvas.alpha_composite(resized, (paste_x, paste_y))
    return simplify_material_detail(canvas, frame_index)


def main() -> None:
    sources = sorted(GENERATED_DIR.glob("crouching_heavy_kick_*.png"))
    if len(sources) != 6:
        raise RuntimeError(f"Expected six generated sweep frames, found {len(sources)}.")

    palette = source_palette()
    target_height = target_crouching_attack_height()
    color_cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    NORMALIZED_DIR.mkdir(parents=True, exist_ok=True)
    normalized: list[Image.Image] = []
    for index, source in enumerate(sources):
        frame = normalize_frame(source, index, target_height, palette, color_cache)
        normalized_path = NORMALIZED_DIR / f"crouching_heavy_kick_{index:02d}.png"
        frame.save(normalized_path, format="PNG")
        normalized.append(frame)

    sheet = Image.new("RGBA", (CANVAS_SIZE[0] * len(normalized), CANVAS_SIZE[1]), (0, 0, 0, 0))
    for index, frame in enumerate(normalized):
        sheet.alpha_composite(frame, (index * CANVAS_SIZE[0], 0))
    sheet.save(SHEET_PATH, format="PNG")
    normalized[0].save(
        PREVIEW_PATH,
        save_all=True,
        append_images=normalized[1:],
        duration=100,
        loop=0,
        disposal=2,
        transparency=0,
    )
    print(
        f"Normalized {len(normalized)} sweep frames to {target_height}px character height, "
        f"the {len(palette)}-color Sanzou source palette, and baseline y={SANDAL_BASELINE_Y}."
    )


if __name__ == "__main__":
    main()
