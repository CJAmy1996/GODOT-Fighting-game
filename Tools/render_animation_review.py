"""Render an imported fighter animation as a GIF and numbered contact sheet."""

from __future__ import annotations

import argparse
import csv
import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve"
OUTPUT_ROOT = ROOT / ".codex-temp" / "animation_review"
TICKS_PER_SECOND = 60


def checkerboard(size: tuple[int, int], cell: int = 16) -> Image.Image:
    image = Image.new("RGBA", size, (31, 35, 45, 255))
    draw = ImageDraw.Draw(image)
    alternate = (43, 48, 61, 255)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, min(x + cell - 1, size[0] - 1), min(y + cell - 1, size[1] - 1)), fill=alternate)
    return image


def union_bounds(images: list[Image.Image]) -> tuple[int, int, int, int]:
    bounds = [image.getchannel("A").getbbox() for image in images]
    bounds = [box for box in bounds if box is not None]
    if not bounds:
        return 0, 0, images[0].width, images[0].height
    left = min(box[0] for box in bounds)
    top = min(box[1] for box in bounds)
    right = max(box[2] for box in bounds)
    bottom = max(box[3] for box in bounds)
    padding = 12
    return (
        max(0, left - padding),
        max(0, top - padding),
        min(images[0].width, right + padding),
        min(images[0].height, bottom + padding),
    )


def load_catalog_row(character: str, animation: str) -> tuple[Path, dict[str, str]]:
    character_dir = ASSET_ROOT / character
    catalog = character_dir / "animation_catalog.csv"
    with catalog.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            if row["animation"] == animation:
                return character_dir, row
    raise ValueError(f"{animation} was not found in {catalog}")


def render(character: str, animation: str, start: int = 0, end: int | None = None) -> tuple[Path, Path]:
    if character == "MechaHeita" and animation in {
            "booster_forward", "booster_up_forward", "booster_down_forward", "booster_back"}:
        character_dir = ASSET_ROOT / character
        images = [
            Image.open(path).convert("RGBA")
            for path in sorted((character_dir / "Frames" / "DirectionalFlight").glob(f"{animation}_*.png"))
        ]
        source_ids = [f"authored {animation} composite {index}" for index in range(len(images))]
        resolved_ids = list(source_ids)
        hold_ticks = [2] * len(images)
    elif character == "MechaHeita" and animation in {"fly_up", "booster_loop"}:
        character_dir = ASSET_ROOT / character
        frame_dir = character_dir / "Frames" / "FlyUp"
        pattern = "fly_up_*.png"
        images = [
            Image.open(path).convert("RGBA")
            for path in sorted(frame_dir.glob(pattern))
        ]
        source_ids = [f"body {407 + index % 2} + jet {409 + index}" for index in range(len(images))]
        resolved_ids = list(source_ids)
        hold_ticks = [2] * len(images)
    else:
        character_dir, row = load_catalog_row(character, animation)
        source_ids = row["source_frames"].split()
        resolved_ids = row["resolved_frames"].split()
        hold_ticks = [int(value) for value in row["hold_ticks"].split()]
        if not (len(source_ids) == len(resolved_ids) == len(hold_ticks)):
            raise ValueError(f"{animation} has mismatched frame metadata")
        images = [
            Image.open(character_dir / "Frames" / f"frame_{int(frame_id):04d}.png").convert("RGBA")
            for frame_id in resolved_ids
        ]
    original_count = len(images)
    end = original_count if end is None else min(end, original_count)
    if start < 0 or start >= end:
        raise ValueError(f"invalid drawing range {start}:{end} for {animation} ({original_count} drawings)")
    drawing_indices = list(range(original_count))[start:end]
    images = images[start:end]
    source_ids = source_ids[start:end]
    resolved_ids = resolved_ids[start:end]
    hold_ticks = hold_ticks[start:end]
    crop = union_bounds(images)
    scale = 2
    label_height = 42
    cropped_size = (crop[2] - crop[0], crop[3] - crop[1])
    presentation_size = (cropped_size[0] * scale, cropped_size[1] * scale + label_height)

    presentation_frames: list[Image.Image] = []
    for index, (drawing_index, image, source_id, resolved_id, ticks) in enumerate(
            zip(drawing_indices, images, source_ids, resolved_ids, hold_ticks)):
        sprite = image.crop(crop).resize((cropped_size[0] * scale, cropped_size[1] * scale), Image.Resampling.NEAREST)
        presentation = checkerboard(presentation_size)
        presentation.alpha_composite(sprite, (0, 0))
        draw = ImageDraw.Draw(presentation)
        draw.rectangle((0, presentation_size[1] - label_height, presentation_size[0], presentation_size[1]), fill=(12, 14, 20, 245))
        replacement = "" if source_id == resolved_id else f" -> {resolved_id}"
        draw.text((8, presentation_size[1] - 35),
                  f"{character} {animation} | drawing {drawing_index}/{original_count - 1}",
                  fill=(245, 245, 248, 255))
        draw.text((8, presentation_size[1] - 20), f"source {source_id}{replacement} | hold {ticks}f @ 60 Hz", fill=(255, 211, 91, 255))
        presentation_frames.append(presentation.convert("P", palette=Image.Palette.ADAPTIVE))

    output_dir = OUTPUT_ROOT / character
    output_dir.mkdir(parents=True, exist_ok=True)
    suffix = "" if start == 0 and end == original_count else f"_frames_{start}_{end - 1}"
    gif_path = output_dir / f"{animation}{suffix}.gif"
    presentation_frames[0].save(
        gif_path,
        save_all=True,
        append_images=presentation_frames[1:],
        # Source scripts use 1000-tick sentinel holds for state-controlled poses.
        # Keep the real value in the label, but cap review playback so the GIF
        # remains useful and reaches the next loop promptly.
        duration=[max(17, round(min(ticks, 12) * 1000 / TICKS_PER_SECOND)) for ticks in hold_ticks],
        loop=0,
        disposal=2,
    )

    columns = min(4, len(images))
    rows = math.ceil(len(images) / columns)
    gap = 8
    tile_width, tile_height = presentation_size
    sheet = Image.new("RGBA", (columns * tile_width + (columns + 1) * gap,
                               rows * tile_height + (rows + 1) * gap), (18, 20, 27, 255))
    for index, frame in enumerate(presentation_frames):
        x = gap + (index % columns) * (tile_width + gap)
        y = gap + (index // columns) * (tile_height + gap)
        sheet.alpha_composite(frame.convert("RGBA"), (x, y))
    sheet_path = output_dir / f"{animation}{suffix}_sheet.png"
    sheet.save(sheet_path)
    return gif_path, sheet_path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("character", help="Asset directory, for example MechaHeita")
    parser.add_argument("animation", help="Catalog animation, for example anim_000")
    parser.add_argument("--start", type=int, default=0, help="First drawing index to include")
    parser.add_argument("--end", type=int, help="Exclusive drawing index to include")
    args = parser.parse_args()
    gif_path, sheet_path = render(args.character, args.animation, args.start, args.end)
    print(gif_path)
    print(sheet_path)


if __name__ == "__main__":
    main()
