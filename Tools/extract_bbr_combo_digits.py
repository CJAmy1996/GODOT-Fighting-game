"""Cut Revolve's combo digits cleanly from the original system num.png sheet."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Hud" / "BigBangBeatRevolve" / "num.png"
OUTPUT = ROOT / "Assets" / "Hud" / "BigBangBeatRevolve" / "ComboDigits"

# The original sheet stores 0..4 on its first combo row and 5..9 on its
# second. Cells are deliberately broader than the visible glyph; alpha bounds
# below remove all transparent padding without touching authored pixels.
NORMAL_ROWS = ((123, 166), (187, 230))
FLASH_ROWS = ((249, 297), (314, 361))
CELL_WIDTH = 48


def extract_cell(sheet: Image.Image, digit: int, rows: tuple[tuple[int, int], tuple[int, int]]) -> tuple[Image.Image, tuple[int, int, int, int]]:
    row = 0 if digit < 5 else 1
    column = digit if digit < 5 else digit - 5
    y0, y1 = rows[row]
    x0 = column * CELL_WIDTH
    cell = sheet.crop((x0, y0, x0 + CELL_WIDTH, y1))
    alpha_bounds = cell.getchannel("A").getbbox()
    if alpha_bounds is None:
        raise RuntimeError(f"combo digit {digit} has no visible pixels")
    cropped = cell.crop(alpha_bounds)
    source_bounds = (
        x0 + alpha_bounds[0],
        y0 + alpha_bounds[1],
        x0 + alpha_bounds[2],
        y0 + alpha_bounds[3],
    )
    return cropped, source_bounds


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sheet = Image.open(SOURCE).convert("RGBA")
    manifest: dict[str, dict[str, object]] = {}
    normal_digits: dict[int, Image.Image] = {}
    for digit in range(10):
        normal, normal_bounds = extract_cell(sheet, digit, NORMAL_ROWS)
        flash, flash_bounds = extract_cell(sheet, digit, FLASH_ROWS)
        normal.save(OUTPUT / f"combo_{digit}.png")
        flash.save(OUTPUT / f"combo_{digit}_flash.png")
        normal_digits[digit] = normal
        manifest[str(digit)] = {
            "normal_source_bounds": normal_bounds,
            "normal_size": normal.size,
            "flash_source_bounds": flash_bounds,
            "flash_size": flash.size,
        }

    # A transparent, evenly-spaced review strip containing the exact 1..9 cuts.
    gap = 8
    label_height = 18
    cell_width = max(image.width for image in normal_digits.values())
    cell_height = max(image.height for image in normal_digits.values())
    review = Image.new("RGBA", ((cell_width + gap) * 9 + gap, cell_height + label_height + gap * 2), (20, 24, 34, 255))
    draw = ImageDraw.Draw(review)
    for index, digit in enumerate(range(1, 10)):
        image = normal_digits[digit]
        x = gap + index * (cell_width + gap) + (cell_width - image.width) // 2
        y = gap + label_height + cell_height - image.height
        review.alpha_composite(image, (x, y))
        draw.text((gap + index * (cell_width + gap) + cell_width // 2 - 3, 3), str(digit), fill=(190, 198, 214, 255))
    review.save(OUTPUT / "combo_digits_1_to_9_review.png")
    (OUTPUT / "combo_digit_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(OUTPUT / "combo_digits_1_to_9_review.png")


if __name__ == "__main__":
    main()
