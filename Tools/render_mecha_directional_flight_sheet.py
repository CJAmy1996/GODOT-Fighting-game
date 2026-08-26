"""Render the first frame of Mecha Heita's nine flight directions."""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
FRAME_ROOT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "MechaHeita" / "Frames"
OUTPUT = ROOT / ".codex-temp" / "mecha_jet_alignment" / "directional_flight_sheet.png"

TILE_WIDTH = 300
TILE_HEIGHT = 300
LABEL_HEIGHT = 34
PADDING = 14

GRID = (
    (("UP BACK", "booster_up_back"), ("UP", "booster_up"), ("UP FORWARD", "booster_up_forward")),
    (("BACK", "booster_back"), ("NEUTRAL", "neutral"), ("FORWARD", "booster_forward")),
    (("DOWN BACK", "booster_down_back"), ("DOWN", "booster_down"), ("DOWN FORWARD", "booster_down_forward")),
)


def frame_path(animation: str) -> Path:
    if animation == "neutral":
        return FRAME_ROOT / "FlyUp" / "fly_up_00.png"
    return FRAME_ROOT / "DirectionalFlight" / f"{animation}_00.png"


def composite_sprite(canvas: Image.Image, sprite: Image.Image, x: int, y: int) -> None:
    bounds = sprite.getbbox()
    if bounds is None:
        return

    cropped = sprite.crop(bounds)
    available_width = TILE_WIDTH - PADDING * 2
    available_height = TILE_HEIGHT - LABEL_HEIGHT - PADDING * 2
    scale = min(available_width / cropped.width, available_height / cropped.height, 2.0)
    size = (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale)))
    resized = cropped.resize(size, Image.Resampling.NEAREST)
    draw_x = x + (TILE_WIDTH - resized.width) // 2
    draw_y = y + LABEL_HEIGHT + (TILE_HEIGHT - LABEL_HEIGHT - resized.height) // 2
    canvas.alpha_composite(resized, (draw_x, draw_y))


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGBA", (TILE_WIDTH * 3, TILE_HEIGHT * 3), (19, 19, 29, 255))
    draw = ImageDraw.Draw(sheet)

    for row_index, row in enumerate(GRID):
        for column_index, (label, animation) in enumerate(row):
            x = column_index * TILE_WIDTH
            y = row_index * TILE_HEIGHT
            draw.rectangle((x, y, x + TILE_WIDTH - 1, y + TILE_HEIGHT - 1), outline=(70, 70, 88, 255), width=2)
            label_bounds = draw.textbbox((0, 0), label)
            label_width = label_bounds[2] - label_bounds[0]
            draw.text((x + (TILE_WIDTH - label_width) // 2, y + 10), label, fill=(245, 245, 255, 255))
            with Image.open(frame_path(animation)) as image:
                composite_sprite(sheet, image.convert("RGBA"), x, y)

    sheet.convert("RGB").save(OUTPUT, format="PNG", optimize=False)
    print(OUTPUT)


if __name__ == "__main__":
    main()
