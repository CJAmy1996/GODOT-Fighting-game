"""Render one Big Bang Beat common DXA source section for user approval.

The renderer reads the CSV produced by ``catalog_bigbang_common.py`` and the
original ``_common_pct`` drawings.  It applies the same green-key rule as the
Godot runtime, places every drawing around its source-authored origin, and
produces both a real-time GIF and a numbered contact sheet.
"""

from __future__ import annotations

import argparse
import csv
import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Assets" / "Effects" / "BigBangCommon" / "common_animation_catalog.csv"
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
SCRIPT = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_scr" / "script.txt"
OUTPUT = ROOT / "Docs" / "BigBangBeatRevolveCommonPreviews"
TICKS_PER_SECOND = 60


def green_key(image: Image.Image) -> Image.Image:
    """Match BigBangCommonEffect's dominant-green transparency rule."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, alpha = pixels[x, y]
            if green >= 115 and green - max(red, blue) >= 56:
                pixels[x, y] = (red, green, blue, 0)
            else:
                pixels[x, y] = (red, green, blue, alpha)
    return rgba


def crop_visible(image: Image.Image, origin_x: int, origin_y: int) -> tuple[Image.Image, int, int]:
    """Remove transparent canvas while preserving the source-space anchor."""
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0)), origin_x, origin_y
    left, top, right, bottom = bounds
    return image.crop(bounds), origin_x - left, origin_y - top


def load_row(source_section: int) -> dict[str, str]:
    with CATALOG.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            if int(row["source_section"]) == source_section:
                return row
    raise ValueError(f"source section {source_section} was not found in {CATALOG}")


def load_drawing_commands(source_section: int) -> list[dict[str, float | int | bool]]:
    """Read stateful scale and per-drawing flip fields from the original script."""
    text = SCRIPT.read_text(encoding="cp932", errors="replace")
    sections = [section.strip() for section in text.split("------") if section.strip()]
    lines = [line for line in sections[source_section].splitlines() if line.strip()]
    scale_x = scale_y = 1.0
    growth_x = growth_y = 0.0
    drawings: list[dict[str, float | int | bool]] = []
    for line in lines[1:]:
        fields = line.split("\t")
        command = fields[0].strip()
        if command == "大" and len(fields) >= 11:
            scale_x = int(fields[1]) / 100.0
            scale_y = int(fields[4]) / 100.0
            growth_x = int(fields[7]) / 100.0
            growth_y = int(fields[10]) / 100.0
        elif command == "Ｉ" and len(fields) >= 5:
            drawings.append({
                "hold": max(1, int(fields[1])),
                "frame": int(fields[2]),
                "origin_x": int(fields[3]),
                "origin_y": int(fields[4]),
                "flip_x": len(fields) > 5 and fields[5] == "1",
                "scale_x": scale_x,
                "scale_y": scale_y,
                "growth_x": growth_x,
                "growth_y": growth_y,
            })
            scale_x += growth_x * max(1, int(fields[1]))
            scale_y += growth_y * max(1, int(fields[1]))
    return drawings


def checkerboard(size: tuple[int, int], cell: int = 12) -> Image.Image:
    image = Image.new("RGBA", size, (29, 32, 41, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(43, 47, 59, 255))
    return image


def render(source_section: int) -> tuple[Path, Path]:
    row = load_row(source_section)
    source_id = row["source_id"]
    frame_files = row["source_frames"].split()
    frame_ids = [int(Path(value).stem) for value in frame_files]
    kir_indices = [int(value) for value in row["kir_indices"].split()]
    holds = [int(value) for value in row["hold_ticks"].split()]
    origins_x = [int(value) for value in row["origin_x"].split()]
    origins_y = [int(value) for value in row["origin_y"].split()]
    if not (len(frame_ids) == len(kir_indices) == len(holds) == len(origins_x) == len(origins_y)):
        raise ValueError(f"{source_id} has mismatched source metadata")
    if not frame_ids:
        raise ValueError(f"{source_id} has no I drawing timeline to render")

    commands = load_drawing_commands(source_section)
    if len(commands) != len(frame_ids):
        raise ValueError(f"{source_id} has {len(frame_ids)} catalog drawings but {len(commands)} script drawings")
    for index, command in enumerate(commands):
        expected = (kir_indices[index], holds[index], origins_x[index], origins_y[index])
        actual = (command["frame"], command["hold"], command["origin_x"], command["origin_y"])
        if actual != expected:
            raise ValueError(f"{source_id} drawing {index} differs between CSV {expected} and script {actual}")

    missing_drawings: set[int] = set()
    images: list[Image.Image] = []
    for drawing, frame_id in enumerate(frame_ids):
        source_path = next((candidate for candidate in (
            SOURCE / f"{frame_id:03d}.png",
            SOURCE / f"{frame_id}.png",
            SOURCE / f"{frame_id:03d}.bmp",
            SOURCE / f"{frame_id}.bmp",
        ) if candidate.exists()), None)
        if source_path is not None:
            keyed = green_key(Image.open(source_path))
            cropped, corrected_x, corrected_y = crop_visible(
                keyed, int(commands[drawing]["origin_x"]), int(commands[drawing]["origin_y"])
            )
            images.append(cropped)
            commands[drawing]["origin_x"] = corrected_x
            commands[drawing]["origin_y"] = corrected_y
        else:
            # Keep the authored timeline visible without inventing missing art.
            images.append(Image.new("RGBA", (32, 32), (0, 0, 0, 0)))
            missing_drawings.add(drawing)
    bounds = []
    for image, command in zip(images, commands):
        alpha_bounds = image.getchannel("A").getbbox() or (0, 0, image.width, image.height)
        for tick in range(int(command["hold"])):
            scale_x = float(command["scale_x"]) + float(command["growth_x"]) * tick
            scale_y = float(command["scale_y"]) + float(command["growth_y"]) * tick
            x0 = (alpha_bounds[0] - int(command["origin_x"])) * scale_x
            x1 = (alpha_bounds[2] - int(command["origin_x"])) * scale_x
            if bool(command["flip_x"]):
                x0, x1 = -x1, -x0
            y0 = (alpha_bounds[1] - int(command["origin_y"])) * scale_y
            y1 = (alpha_bounds[3] - int(command["origin_y"])) * scale_y
            bounds.append((min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)))
    padding = 12
    left = math.floor(min(bound[0] for bound in bounds)) - padding
    top = math.floor(min(bound[1] for bound in bounds)) - padding
    right = math.ceil(max(bound[2] for bound in bounds)) + padding
    bottom = math.ceil(max(bound[3] for bound in bounds)) + padding
    scale = max(2, min(4, 360 // max(1, right - left)))
    art_size = ((right - left) * scale, (bottom - top) * scale)
    label_height = 58
    tile_size = (art_size[0], art_size[1] + label_height)
    anchor = (-left * scale, -top * scale)

    def make_tile(image: Image.Image, frame_id: int, drawing: int, tick: int,
                  command: dict[str, float | int | bool]) -> Image.Image:
        scale_x = float(command["scale_x"]) + float(command["growth_x"]) * tick
        scale_y = float(command["scale_y"]) + float(command["growth_y"]) * tick
        tile = checkerboard(tile_size)
        resized = image.resize((max(1, round(image.width * scale_x * scale)),
                                max(1, round(image.height * scale_y * scale))), Image.Resampling.NEAREST)
        if bool(command["flip_x"]):
            resized = resized.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            source_left = int(command["origin_x"]) * scale_x - image.width * scale_x
        else:
            source_left = -int(command["origin_x"]) * scale_x
        source_top = -int(command["origin_y"]) * scale_y
        x = round((source_left - left) * scale)
        y = round((source_top - top) * scale)
        tile.alpha_composite(resized, (x, y))
        draw = ImageDraw.Draw(tile)
        draw.line((anchor[0] - 8, anchor[1], anchor[0] + 8, anchor[1]), fill=(255, 66, 66, 255), width=2)
        draw.line((anchor[0], anchor[1] - 8, anchor[0], anchor[1] + 8), fill=(255, 66, 66, 255), width=2)
        label_top = art_size[1]
        draw.rectangle((0, label_top, tile_size[0], tile_size[1]), fill=(10, 12, 17, 248))
        draw.text((7, label_top + 6),
                  f"drawing {drawing:02d} | source {frame_id:03d}.png | hold {int(command['hold'])}f @ 60 Hz",
                  fill=(250, 222, 117, 255))
        draw.text((7, label_top + 25),
                  f"crop-corrected origin ({int(command['origin_x'])}, {int(command['origin_y'])}) | red cross = effect anchor",
                  fill=(230, 233, 240, 255))
        growth = "" if float(command["growth_x"]) == 0 and float(command["growth_y"]) == 0 else (
            f" + ({float(command['growth_x']):.0%}, {float(command['growth_y']):.0%})/tick")
        draw.text((7, label_top + 42),
                  f"scale ({scale_x:.0%}, {scale_y:.0%}){growth} | flip X: {bool(command['flip_x'])}",
                  fill=(160, 202, 255, 255))
        if drawing in missing_drawings:
            draw.rectangle((8, 8, tile_size[0] - 8, art_size[1] - 8), outline=(255, 75, 75, 255), width=3)
            draw.text((16, 18), f"MISSING SOURCE IMAGE {frame_id:03d}.png", fill=(255, 100, 100, 255))
        return tile

    frames = [make_tile(image, frame_id, drawing, 0, command)
              for drawing, (image, frame_id, command) in enumerate(zip(images, frame_ids, commands))]
    timeline_frames = [make_tile(image, frame_id, drawing, tick, command)
                       for drawing, (image, frame_id, command) in enumerate(zip(images, frame_ids, commands))
                       for tick in range(int(command["hold"]))]

    OUTPUT.mkdir(parents=True, exist_ok=True)
    slug = row["implemented_role"].replace(" / ", "_").replace(" ", "_")
    child_sections = row["child_source_sections"].split()
    scope = "primary_timeline_children_listed" if child_sections else "source_direct"
    stem = f"{source_id}_{slug}_{scope}"
    gif_path = OUTPUT / f"{stem}.gif"
    timeline_frames[0].save(gif_path, save_all=True, append_images=timeline_frames[1:],
                   # GIF delays are centiseconds. Alternating 20/20/10 ms
                   # preserves the 60 Hz average instead of Pillow rounding a
                   # nominal 17 ms delay down to 10 ms (incorrectly 100 FPS).
                   duration=[(20, 20, 10)[index % 3] for index in range(len(timeline_frames))],
                   loop=0, disposal=2)

    columns = min(4, len(frames))
    rows = math.ceil(len(frames) / columns)
    gap = 8
    header = 86
    sheet = Image.new("RGBA", (columns * tile_size[0] + (columns + 1) * gap,
                               header + rows * tile_size[1] + (rows + 1) * gap), (16, 18, 24, 255))
    sheet_draw = ImageDraw.Draw(sheet)
    sheet_draw.text((gap, 7), f"{source_id} | raw DXA source section | {row['implemented_role']}",
                    fill=(245, 247, 252, 255))
    sheet_draw.text((gap, 25), f"{len(frame_ids)} drawings | {row['timeline_ticks']} source ticks @ 60 Hz",
                    fill=(250, 222, 117, 255))
    dependency_text = (f"DRAWING TIMELINE ONLY | O child source sections: {' '.join(child_sections)}"
                       if child_sections else "DIRECT DRAWING TIMELINE | no O child sections")
    sheet_draw.text((gap, 43), dependency_text,
                    fill=(255, 133, 133, 255) if child_sections else (151, 231, 178, 255))
    sheet_draw.text((gap, 61),
                    f"Source commands: {row['command_types']} | preview simulates I timing/origin and 大 scale",
                    fill=(184, 194, 216, 255))
    for index, frame in enumerate(frames):
        x = gap + (index % columns) * (tile_size[0] + gap)
        y = header + gap + (index // columns) * (tile_size[1] + gap)
        sheet.alpha_composite(frame, (x, y))
    sheet_path = OUTPUT / f"{stem}_sheet.png"
    sheet.save(sheet_path)
    return gif_path, sheet_path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source_section", type=int,
                        help="Raw zero-based section from _common_scr/script.txt, for example 20")
    args = parser.parse_args()
    for output in render(args.source_section):
        print(output)


if __name__ == "__main__":
    main()
