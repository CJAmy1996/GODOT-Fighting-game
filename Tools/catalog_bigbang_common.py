"""Catalog BIGBANG BEAT Revolve's common archive by raw DXA source section.

The source section is the stable identity used by the script's ``O`` child
commands. A prior catalog numbered only sections containing ``I`` drawings;
that index drifted after deleted/drawing-less sections and is retained only as
an explicitly labelled legacy value.
"""

from __future__ import annotations

import csv
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from import_bigbang_characters import EXTRACTION, ROOT


SCRIPT = EXTRACTION / "_common_scr" / "script.txt"
KIR = EXTRACTION / "_common_scr" / "kir.txt"
SOURCE_IMAGES = EXTRACTION / "_common_pct"
OUTPUT = ROOT / "Assets" / "Effects" / "BigBangCommon"
ACTION_CATALOG = OUTPUT / "common_animation_catalog.csv"
RESOURCE_CATALOG = OUTPUT / "common_resource_usage.csv"

# Runtime roles are keyed by authoritative script section, never by a filtered
# visual index.
IMPLEMENTED_ROLES = {
    0: "regular hit spark (weak)",
    2: "grounded jump-start effect",
    3: "regular hit spark (medium)",
    4: "regular hit spark (strong)",
    11: "dust / smoke",
    12: "mirrored dust / smoke",
    20: "block / guard impact",
    26: "blood burst controller",
    27: "blood droplet child particle",
}


@dataclass(frozen=True)
class SourceDrawing:
    hold_ticks: int
    image_id: int
    origin_x: int
    origin_y: int


@dataclass(frozen=True)
class KirEntry:
    filename: str
    origin_x: int
    origin_y: int
    width: int | None
    height: int | None


def kir_entries() -> list[KirEntry]:
    """Return the image table addressed by the script's I command.

    The I command stores an index into kir.txt, not a PNG filename.  Missing
    filenames in the archive make those two number spaces diverge.
    """
    entries: list[KirEntry] = []
    for raw_line in KIR.read_text(encoding="cp932", errors="replace").splitlines():
        if not raw_line.strip():
            continue
        fields = raw_line.split("\t")
        filename = fields[0].strip()
        try:
            origin_x = int(fields[1]) if len(fields) > 1 and fields[1] else 0
            origin_y = int(fields[2]) if len(fields) > 2 and fields[2] else 0
            width = int(fields[3]) if len(fields) > 3 and fields[3] else None
            height = int(fields[4]) if len(fields) > 4 and fields[4] else None
        except ValueError:
            origin_x = origin_y = 0
            width = height = None
        entries.append(KirEntry(filename, origin_x, origin_y, width, height))
    return entries


def source_sections() -> list[list[str]]:
    text = SCRIPT.read_text(encoding="cp932", errors="replace")
    return [
        [line for line in section.strip().splitlines() if line.strip()]
        for section in text.split("------")
        if section.strip()
    ]


def section_drawings(lines: list[str]) -> list[SourceDrawing]:
    drawings: list[SourceDrawing] = []
    for line in lines[1:]:
        fields = line.split("\t")
        if not fields or fields[0].strip() != "\uff29" or len(fields) < 3:
            continue
        try:
            drawings.append(SourceDrawing(
                max(1, int(fields[1])),
                int(fields[2]),
                int(fields[3]) if len(fields) > 3 else 0,
                int(fields[4]) if len(fields) > 4 else 0,
            ))
        except ValueError:
            continue
    return drawings


def command_summary(lines: list[str]) -> tuple[str, str, str, str]:
    command_types: list[str] = []
    child_source_sections: list[str] = []
    system_refs: list[str] = []
    sounds: list[str] = []
    for line in lines[1:]:
        fields = line.split("\t")
        command = fields[0].strip() if fields else ""
        if command and command not in command_types:
            command_types.append(command)
        try:
            if command == "\uff2f" and len(fields) > 3:
                child_source_sections.append(fields[3])
            elif command in {"DS", "SG"} and len(fields) > 2:
                system_refs.append(f"{command}:{fields[2]}")
            elif command == "SE" and len(fields) > 1:
                sounds.append(fields[1])
        except (ValueError, IndexError):
            pass
    return (
        " ".join(command_types),
        " ".join(child_source_sections),
        " ".join(system_refs),
        " ".join(sounds),
    )


def source_image_path(image_index: int, entries: list[KirEntry]) -> Path | None:
    if image_index < 0 or image_index >= len(entries):
        return None
    filename = entries[image_index].filename
    if not filename or filename.lower() == "null.bmp":
        return None
    candidate = SOURCE_IMAGES / filename
    if candidate.exists():
        return candidate
    for suffix in (".png", ".bmp"):
        fallback = SOURCE_IMAGES / f"{Path(filename).stem}{suffix}"
        if fallback.exists():
            return fallback
    return None


def write_action_catalog() -> None:
    sections = source_sections()
    entries = kir_entries()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with ACTION_CATALOG.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow((
            "source_id", "source_section", "legacy_visual_id", "source_action", "implemented_role",
            "drawing_count", "timeline_ticks", "kir_indices", "source_frames", "hold_ticks",
            "origin_x", "origin_y", "missing_frames", "command_types",
            "child_source_sections", "system_refs", "sound_refs",
        ))
        legacy_visual_index = 0
        for source_section, lines in enumerate(sections):
            drawings = section_drawings(lines)
            legacy_visual_id = f"common_anim_{legacy_visual_index:03d}" if drawings else ""
            if drawings:
                legacy_visual_index += 1
            command_types, children, system_refs, sounds = command_summary(lines)
            resolved = [source_image_path(drawing.image_id, entries) for drawing in drawings]
            source_frames = [path.name if path is not None else "MISSING" for path in resolved]
            missing = [drawing.image_id for drawing, path in zip(drawings, resolved) if path is None]
            writer.writerow((
                f"common_section_{source_section:03d}",
                source_section,
                legacy_visual_id,
                lines[0].split("\t")[0],
                IMPLEMENTED_ROLES.get(source_section, "reference / not runtime-wired"),
                len(drawings),
                sum(drawing.hold_ticks for drawing in drawings),
                " ".join(str(drawing.image_id) for drawing in drawings),
                " ".join(source_frames),
                " ".join(str(drawing.hold_ticks) for drawing in drawings),
                " ".join(str(drawing.origin_x) for drawing in drawings),
                " ".join(str(drawing.origin_y) for drawing in drawings),
                " ".join(str(frame) for frame in missing),
                command_types,
                children,
                system_refs,
                sounds,
            ))


def numeric_resource_files() -> dict[int, list[Path]]:
    files: dict[int, list[Path]] = defaultdict(list)
    for path in sorted(SOURCE_IMAGES.iterdir()):
        if not path.is_file() or path.suffix.lower() not in {".png", ".bmp"}:
            continue
        try:
            image_id = int(path.stem)
        except ValueError:
            continue
        files[image_id].append(path)
    return files


def write_resource_catalog() -> None:
    sections = source_sections()
    entries = kir_entries()
    usage: dict[int, list[str]] = defaultdict(list)
    holds: dict[int, list[str]] = defaultdict(list)
    roles_by_image: dict[int, set[str]] = defaultdict(set)
    for source_section, lines in enumerate(sections):
        source_name = lines[0].split("\t")[0]
        role = IMPLEMENTED_ROLES.get(source_section)
        for drawing_index, drawing in enumerate(section_drawings(lines)):
            path = source_image_path(drawing.image_id, entries)
            if path is None:
                continue
            try:
                resource_id = int(path.stem)
            except ValueError:
                continue
            usage[resource_id].append(
                f"common_section_{source_section:03d}:{source_name}:drawing_{drawing_index}"
            )
            holds[resource_id].append(str(drawing.hold_ticks))
            if role:
                roles_by_image[resource_id].add(role)

    files = numeric_resource_files()
    referenced_ids = set(usage)
    all_ids = sorted(set(files) | referenced_ids)
    with RESOURCE_CATALOG.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow((
            "resource_id", "source_files", "width", "height", "referenced",
            "source_section_usage", "drawing_holds", "runtime_role",
        ))
        for image_id in all_ids:
            source_files = files.get(image_id, [])
            width = height = ""
            if source_files:
                with Image.open(source_files[0]) as image:
                    width, height = image.size
            writer.writerow((
                image_id,
                " ".join(path.name for path in source_files),
                width,
                height,
                "yes" if image_id in referenced_ids else "no",
                " | ".join(usage.get(image_id, ())),
                " ".join(holds.get(image_id, ())),
                " | ".join(sorted(roles_by_image.get(image_id, ()))),
            ))


def main() -> None:
    write_action_catalog()
    write_resource_catalog()
    print(ACTION_CATALOG.relative_to(ROOT))
    print(RESOURCE_CATALOG.relative_to(ROOT))


if __name__ == "__main__":
    main()
