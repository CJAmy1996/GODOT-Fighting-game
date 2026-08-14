"""Reconstruct BIGBANG BEAT Revolve drawing anchors from its original data.

The original renderer does not center the visible fighter pixels independently.
It uses the full source bitmap's horizontal center and bottom ground edge, then
applies the ``Ｉ`` command's X/Y values in the game's half-scale coordinate
system.  The main importer used to discard both the bitmap canvas and these
authored offsets, which makes feet slide when differently-sized attack drawings
are played on one Godot origin.

This tool reads the extracted ``script.txt`` and original BMP canvases and emits
the absolute source anchor, the traditional action-relative offset, and the
neutral-relative offset used when an attack's first drawing does not share the
idle ground anchor. Half-pixel values are intentionally preserved for parity.
"""

from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
EXTRACTION = ROOT / "Extraction" / "BigBangBeatRevolve"
ASSETS = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve"


@dataclass(frozen=True)
class Drawing:
    hold: int
    image_id: int
    offset_x: int
    offset_y: int


def parse_actions(script_path: Path) -> list[tuple[int, str, list[Drawing]]]:
    text = script_path.read_text(encoding="cp932", errors="replace")
    sections = [section.strip() for section in text.split("------") if section.strip()]
    actions: list[tuple[int, str, list[Drawing]]] = []
    for source_section, section in enumerate(sections):
        lines = [line for line in section.splitlines() if line.strip()]
        drawings: list[Drawing] = []
        for line in lines[1:]:
            fields = line.split("\t")
            if len(fields) < 5 or fields[0].strip() != "Ｉ":
                continue
            drawings.append(Drawing(
                hold=max(1, int(fields[1])),
                image_id=int(fields[2]),
                offset_x=int(fields[3]),
                offset_y=int(fields[4]),
            ))
        if drawings:
            actions.append((source_section, lines[0].split("\t")[0], drawings))
    return actions


def source_picture(picture_dir: Path, image_id: int) -> Path | None:
    for suffix in (".png", ".bmp"):
        for stem in (f"{image_id:03d}", str(image_id)):
            candidate = picture_dir / f"{stem}{suffix}"
            if candidate.exists():
                return candidate
    return None


def visible_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    rgba = image.convert("RGBA")
    alpha = Image.new("L", rgba.size, 0)
    alpha_pixels = []
    for red, green, blue, source_alpha in rgba.get_flattened_data():
        keyed_green = green > 220 and red < 45 and blue < 45
        alpha_pixels.append(0 if keyed_green else source_alpha)
    alpha.putdata(alpha_pixels)
    return alpha.getbbox() or (0, 0, image.width, image.height)


def source_anchor(picture: Path, drawing: Drawing) -> tuple[float, float]:
    with Image.open(picture) as image:
        left, top, right, bottom = visible_bounds(image)
        # Preserve the original full-bitmap horizontal center and bottom-ground
        # anchor before applying the source game's half-scale Ｉ coordinates.
        canvas_x = ((left + right) - image.width) * 0.5
        canvas_y = bottom - image.height
    return canvas_x + drawing.offset_x * 0.5, canvas_y + drawing.offset_y * 0.5


def clean_number(value: float) -> str:
    rounded = round(value * 2.0) / 2.0
    return str(int(rounded)) if rounded.is_integer() else f"{rounded:.1f}"


def reconstruct(character_archive: str, character_assets: str) -> Path:
    script_path = EXTRACTION / f"_{character_archive}_scr" / "script.txt"
    picture_dir = EXTRACTION / f"_{character_archive}_pct"
    output = ASSETS / character_assets / "animation_anchor_catalog.csv"
    actions = parse_actions(script_path)

    reconstructed: list[tuple[int, str, list[Drawing], list[tuple[float, float]]]] = []
    for source_section, source_name, drawings in actions:
        absolute: list[tuple[float, float]] = []
        for drawing in drawings:
            picture = source_picture(picture_dir, drawing.image_id)
            absolute.append(source_anchor(picture, drawing) if picture else (0.0, 0.0))
        reconstructed.append((source_section, source_name, drawings, absolute))

    neutral_x, neutral_y = reconstructed[0][3][0]
    rows = []
    for visual_index, (source_section, source_name, drawings, absolute) in enumerate(reconstructed):
        base_x, base_y = absolute[0]
        relative = [(x - base_x, y - base_y) for x, y in absolute]
        neutral_relative = [(x - neutral_x, y - neutral_y) for x, y in absolute]
        rows.append({
            "animation": f"anim_{visual_index:03d}",
            "source_section": source_section,
            "source_action": source_name,
            "source_frames": " ".join(str(item.image_id) for item in drawings),
            "script_offset_x": " ".join(str(item.offset_x) for item in drawings),
            "script_offset_y": " ".join(str(item.offset_y) for item in drawings),
            "godot_anchor_x": " ".join(clean_number(x) for x, _ in relative),
            "godot_anchor_y": " ".join(clean_number(y) for _, y in relative),
            "godot_absolute_anchor_x": " ".join(clean_number(x) for x, _ in absolute),
            "godot_absolute_anchor_y": " ".join(clean_number(y) for _, y in absolute),
            "godot_neutral_anchor_x": " ".join(clean_number(x) for x, _ in neutral_relative),
            "godot_neutral_anchor_y": " ".join(clean_number(y) for _, y in neutral_relative),
        })

    with output.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys())
        writer.writeheader()
        writer.writerows(rows)
    return output


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", default="m_heita")
    parser.add_argument("--assets", default="MechaHeita")
    args = parser.parse_args()
    print(reconstruct(args.archive, args.assets).relative_to(ROOT))


if __name__ == "__main__":
    main()
