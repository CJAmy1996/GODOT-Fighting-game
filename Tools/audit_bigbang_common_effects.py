"""Build a source-authoritative manifest and previews for common effects.

The source I command addresses an entry in ``_common_scr/kir.txt``.  It does
not address a PNG filename directly.  This audit deliberately resolves that
indirection before exporting timing, origins, flip, and scale information.
"""

from __future__ import annotations

import csv
import json
from pathlib import Path

from PIL import Image

from catalog_bigbang_common import ACTION_CATALOG, OUTPUT, ROOT, write_action_catalog, write_resource_catalog
from render_bigbang_common_animation_review import load_drawing_commands, render


MANIFEST = OUTPUT / "common_source_effect_manifest.json"
REPORT = OUTPUT / "common_source_effect_audit.csv"
CROPPED = OUTPUT / "Cropped"

# Source sections represented by the common art reviewed with the user.
REVIEW_SECTIONS = (0, 1, 2, 3, 4, 11, 12, 14, 15, 16, 19, 20, 21, 22, 23,
                   26, 39, 40, 63, 64, 66, 71, 73, 75, 76, 83, 89, 91, 92)


def green_key_and_crop(filename: str) -> tuple[Path, tuple[int, int, int, int]]:
    """Crop transparent/chroma padding without changing authored placement."""
    source = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct" / filename
    image = Image.open(source).convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            if green >= 115 and green - max(red, blue) >= 56:
                pixels[x, y] = (red, green, blue, 0)
            else:
                pixels[x, y] = (red, green, blue, alpha)
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        bounds = (0, 0, 1, 1)
        cropped = Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    else:
        cropped = image.crop(bounds)
    CROPPED.mkdir(parents=True, exist_ok=True)
    destination = CROPPED / filename
    cropped.save(destination)
    return destination, bounds


def main() -> None:
    write_action_catalog()
    write_resource_catalog()
    with ACTION_CATALOG.open(newline="", encoding="utf-8-sig") as handle:
        rows = {int(row["source_section"]): row for row in csv.DictReader(handle)}

    manifest: dict[str, object] = {
        "format": 1,
        "ticks_per_second": 60,
        "authority": "_common_scr/script.txt I commands resolved through _common_scr/kir.txt",
        "sections": {},
    }
    report_rows: list[dict[str, object]] = []
    crop_cache: dict[str, tuple[Path, tuple[int, int, int, int]]] = {}
    for section in REVIEW_SECTIONS:
        row = rows[section]
        commands = load_drawing_commands(section)
        files = row["source_frames"].split()
        drawings = []
        for filename, command in zip(files, commands):
            if filename not in crop_cache:
                crop_cache[filename] = green_key_and_crop(filename)
            cropped_path, bounds = crop_cache[filename]
            crop_left, crop_top, crop_right, crop_bottom = bounds
            drawings.append({
                "source_file": filename,
                "resource_path": f"res://{cropped_path.relative_to(ROOT).as_posix()}",
                "kir_index": int(command["frame"]),
                "hold_ticks": int(command["hold"]),
                "source_origin_x": int(command["origin_x"]),
                "source_origin_y": int(command["origin_y"]),
                "crop_left": crop_left,
                "crop_top": crop_top,
                "crop_width": crop_right - crop_left,
                "crop_height": crop_bottom - crop_top,
                "origin_x": int(command["origin_x"]) - crop_left,
                "origin_y": int(command["origin_y"]) - crop_top,
                "flip_x": bool(command["flip_x"]),
                "scale_x": float(command["scale_x"]),
                "scale_y": float(command["scale_y"]),
                "growth_x_per_tick": float(command["growth_x"]),
                "growth_y_per_tick": float(command["growth_y"]),
            })
        manifest["sections"][str(section)] = {
            "source_id": row["source_id"],
            "source_action": row["source_action"],
            "timeline_ticks": int(row["timeline_ticks"]),
            "loop": False,
            "drawings": drawings,
        }
        gif_path, sheet_path = render(section)
        report_rows.append({
            "source_section": section,
            "source_action": row["source_action"],
            "drawing_count": len(drawings),
            "timeline_ticks": row["timeline_ticks"],
            "alignment": "source I origin",
            "timing": "source I hold",
            "transform": "source flip/scale/growth",
            "gif": str(gif_path.relative_to(ROOT)),
            "sheet": str(sheet_path.relative_to(ROOT)),
        })

    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with REPORT.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=report_rows[0].keys())
        writer.writeheader()
        writer.writerows(report_rows)
    print(MANIFEST.relative_to(ROOT))
    print(REPORT.relative_to(ROOT))


if __name__ == "__main__":
    main()
