"""Pixel-reduce the user's exact swordless Kamui reference and isolate his sword."""

from pathlib import Path

from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
REFERENCE = Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-aeeb3707-6091-467a-891c-64989d8ec440.png")
SOURCE = ROOT / "Assets/TestFighter/BigBangBeatRevolve/Kamui/Frames/frame_0001.png"
OUTPUT_DIR = ROOT / "Assets/TestFighter/BigBangBeatRevolve/Kamui/Swordless/Idle"
OUTPUT = OUTPUT_DIR / "frame_0001_swordless.png"
PREVIEW = OUTPUT_DIR / "kamui_swordless_idle_5f.gif"
SWORD = OUTPUT_DIR / "kamui_idle_sword_asset.png"
SWORD_PREVIEW = OUTPUT_DIR / "kamui_idle_sword_asset_preview.png"


def reference_sprite() -> Image.Image:
    reference = Image.open(REFERENCE).convert("RGB")
    mask = Image.new("L", reference.size, 0)
    src = reference.load()
    dst = mask.load()
    for y in range(reference.height):
        for x in range(reference.width):
            red, green, blue = src[x, y]
            if min(red, green, blue) < 225 or max(red, green, blue) - min(red, green, blue) > 12:
                dst[x, y] = 255

    bounds = mask.getbbox()
    if bounds is None:
        raise RuntimeError("The supplied reference contains no detectable sprite")
    sprite = reference.crop(bounds)
    alpha = mask.crop(bounds)

    target_height = 123
    target_width = round(sprite.width * target_height / sprite.height)
    sprite = sprite.resize((target_width, target_height), Image.Resampling.NEAREST)
    alpha = alpha.resize((target_width, target_height), Image.Resampling.NEAREST)

    # Reduce the supplied image itself to a clean fighting-game palette without
    # inventing or repainting any forms.
    sprite = sprite.quantize(colors=32, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
    sprite.putalpha(alpha)

    canvas = Image.new("RGBA", (320, 384), (0, 0, 0, 0))
    canvas.alpha_composite(sprite, ((320 - target_width) // 2, 250 - target_height))
    return canvas


def exact_sword_layer() -> Image.Image:
    source = Image.open(SOURCE).convert("RGBA")
    gold = Image.new("L", source.size, 0)
    source_pixels = source.load()
    gold_pixels = gold.load()

    # Drawing 001 sword region. Start at the guard so Kamui's hand and cuff are
    # categorically excluded; retain gold/ivory weapon pixels only.
    for y in range(184, 246):
        for x in range(116, 157):
            red, green, blue, alpha = source_pixels[x, y]
            weapon_color = (
                alpha > 0
                and red >= 90
                and green >= 45
                and red > blue * 1.45
                and green > blue * 1.10
            )
            if weapon_color:
                gold_pixels[x, y] = 255

    # Include the sword's one-pixel dark contour, but no free-standing purple
    # hand/coat pixels from the source pose.
    contour = gold.filter(ImageFilter.MaxFilter(3))
    mask = Image.new("L", source.size, 0)
    mask_pixels = mask.load()
    contour_pixels = contour.load()
    for y in range(184, 246):
        for x in range(116, 157):
            red, green, blue, alpha = source_pixels[x, y]
            dark_outline = max(red, green, blue) < 95
            if gold_pixels[x, y] or (contour_pixels[x, y] and alpha > 0 and dark_outline):
                mask_pixels[x, y] = 255

    # A gold costume cuff shares the weapon palette but is disconnected from
    # the blade. Keep only the sword's largest connected component.
    remaining = {(x, y) for y in range(184, 246) for x in range(116, 157) if mask_pixels[x, y]}
    components: list[set[tuple[int, int]]] = []
    while remaining:
        component = {remaining.pop()}
        frontier = list(component)
        while frontier:
            x, y = frontier.pop()
            for neighbor in (
                (x - 1, y - 1), (x, y - 1), (x + 1, y - 1),
                (x - 1, y),                     (x + 1, y),
                (x - 1, y + 1), (x, y + 1), (x + 1, y + 1),
            ):
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    component.add(neighbor)
                    frontier.append(neighbor)
        components.append(component)
    sword_component = max(components, key=len)
    for y in range(184, 246):
        for x in range(116, 157):
            if (x, y) not in sword_component:
                mask_pixels[x, y] = 0

    sword = source.copy()
    sword.putalpha(mask)
    return sword


def review(sprite: Image.Image, crop: tuple[int, int, int, int], scale: int) -> Image.Image:
    clipped = sprite.crop(crop)
    background = Image.new("RGBA", clipped.size, (32, 37, 47, 255))
    background.alpha_composite(clipped)
    return background.resize((clipped.width * scale, clipped.height * scale), Image.Resampling.NEAREST)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    idle = reference_sprite()
    sword = exact_sword_layer()
    idle.save(OUTPUT)
    sword.save(SWORD)

    idle_review = review(idle, (115, 117, 205, 260), 4)
    idle_review.save(
        PREVIEW,
        save_all=True,
        append_images=[idle_review] * 4,
        duration=[1000 // 60] * 5,
        loop=0,
        disposal=2,
    )
    review(sword, (114, 177, 161, 248), 6).save(SWORD_PREVIEW)
    print(OUTPUT.relative_to(ROOT))
    print(PREVIEW.relative_to(ROOT))
    print(SWORD.relative_to(ROOT))
    print(SWORD_PREVIEW.relative_to(ROOT))


if __name__ == "__main__":
    main()
