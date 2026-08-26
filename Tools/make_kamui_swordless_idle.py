"""Create Kamui's swordless idle by erasing source pixels only.

No pixels are painted or synthesized. The mask follows the baked sword in
source drawing 001; pixels occluded by the weapon become transparent.
"""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/TestFighter/BigBangBeatRevolve/Kamui/Frames/frame_0001.png"
OUTPUT_DIR = ROOT / "Assets/TestFighter/BigBangBeatRevolve/Kamui/Swordless/Idle"
OUTPUT = OUTPUT_DIR / "frame_0001_swordless.png"
PREVIEW = OUTPUT_DIR / "kamui_swordless_idle_5f.gif"
SWORD = OUTPUT_DIR / "kamui_idle_sword_asset.png"
SWORD_PREVIEW = OUTPUT_DIR / "kamui_idle_sword_asset_preview.png"


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    image = source.copy()
    alpha = image.getchannel("A")

    # Drawing 001 occupies source-space (121,127)-(200,250). This polygon hugs
    # the sword below the hand, including its crossguard. It deliberately does
    # not reconstruct the few pixels that the weapon covered.
    erase = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(erase)
    draw.polygon(
        [
            (117, 245), (116, 231), (120, 218), (126, 204),
            (132, 190), (138, 178), (148, 175), (156, 178),
            (158, 194), (151, 203), (145, 216), (139, 229),
            (132, 242), (126, 247),
        ],
        fill=255,
    )
    # The horizontal gold guard immediately beneath the left glove.
    draw.polygon([(137, 181), (154, 181), (155, 194), (135, 194)], fill=255)

    alpha.paste(0, mask=erase)
    image.putalpha(alpha)

    # Sprite only the newly exposed relaxed glove, using colors sampled from
    # Kamui's opposite glove. This is the sole newly drawn region.
    hand = ImageDraw.Draw(image)
    outline = (5, 0, 15, 255)
    shadow = (31, 20, 48, 255)
    mid = (57, 39, 75, 255)
    light = (75, 55, 93, 255)
    hand.polygon(
        [(149, 176), (153, 176), (156, 179), (156, 183),
         (154, 187), (151, 190), (148, 187), (146, 183), (147, 179)],
        fill=outline,
    )
    hand.polygon(
        [(150, 178), (153, 178), (154, 180), (154, 183),
         (152, 186), (151, 187), (149, 186), (148, 183), (149, 180)],
        fill=shadow,
    )
    hand.polygon([(150, 179), (152, 179), (153, 181), (152, 184), (150, 186), (149, 183)], fill=mid)
    hand.point((150, 180), fill=light)

    # Preserve the removed weapon as its own exact source-pixel layer.
    sword = source.copy()
    sword_mask = Image.new("L", source.size, 0)
    sword_draw = ImageDraw.Draw(sword_mask)
    sword_draw.polygon(
        [(120, 243), (118, 234), (123, 220), (129, 207),
         (135, 194), (140, 185), (146, 178), (155, 178),
         (155, 193), (148, 200), (143, 210), (137, 223),
         (132, 235), (126, 244)],
        fill=255,
    )
    sword_draw.polygon([(137, 181), (155, 181), (155, 194), (135, 194)], fill=255)
    sword_alpha = Image.composite(source.getchannel("A"), Image.new("L", source.size, 0), sword_mask)
    sword.putalpha(sword_alpha)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT)
    sword.save(SWORD)
    # Review GIF: composite only for visibility because some viewers display
    # transparent RGB remnants. The project PNG above retains real alpha.
    review = Image.new("RGBA", (99, 143), (32, 37, 47, 255))
    review.alpha_composite(image.crop((111, 117, 210, 260)))
    review = review.resize((396, 572), Image.Resampling.NEAREST)
    review.save(
        PREVIEW,
        save_all=True,
        append_images=[review] * 4,
        duration=[1000 // 60] * 5,
        loop=0,
        disposal=2,
    )
    sword_review = Image.new("RGBA", (47, 77), (32, 37, 47, 255))
    sword_review.alpha_composite(sword.crop((114, 171, 161, 248)))
    sword_review.resize((282, 462), Image.Resampling.NEAREST).save(SWORD_PREVIEW)
    print(OUTPUT.relative_to(ROOT))
    print(PREVIEW.relative_to(ROOT))
    print(SWORD.relative_to(ROOT))
    print(SWORD_PREVIEW.relative_to(ROOT))


if __name__ == "__main__":
    main()
