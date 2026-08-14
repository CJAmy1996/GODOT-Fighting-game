"""Write Sanzou's live SpriteFrames timelines to the shared animation CSV schema.

Unlike the BBBR imports, Sanzou has no decoded action script. His current
SpriteFrames resource is the authoritative result of the frame-by-frame design
pass, so this tool catalogs it without changing or benching any assignments.
"""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RESOURCE = ROOT / "Assets" / "TestFighter" / "Sanzo" / "sanzo_sprite_frames.tres"
CATALOG = ROOT / "Assets" / "TestFighter" / "Sanzo" / "animation_catalog.csv"

EXT_RESOURCE_RE = re.compile(r'^\[ext_resource .*path="([^"]+)".* id="([^"]+)"\]$')
ANIMATION_NAME_RE = re.compile(r'"name": &"([^"]+)"')
FRAME_RE = re.compile(r'\{"duration": ([0-9.]+), "texture": ExtResource\("([^"]+)"\)\}')
GROUP_FRAME_RE = re.compile(r"group_(\d+)_(\d+)$")


GROUP_DESCRIPTIONS = (
    "crouching hit reaction",
    "ground impact and ground bounce",
    "get up from knockdown",
    "launched airborne reaction",
    "standing light hit reaction",
    "standing heavy hit reaction",
    "standing crouching and air block pairs",
    "crouch entry and exit transition",
    "jump sequence",
    "forward and backward air-dash source pool",
    "backward walk",
    "forward walk",
    "idle",
    "held crouch",
    "victory pose",
    "intro",
    "command-grab active catch",
    "crouching heavy punch",
    "crouching jab source",
    "down-forward heavy command normal source",
    "jumping heavy kick",
    "jumping jab",
    "body splash",
    "normal throw",
    "standing heavy punch",
    "standing jab",
    "QCF power punch",
    "QCF power-punch rekka",
    "standing heavy kick",
    "reflector cast and reflector super",
    "charge down-up stomp",
    "charge back-forward command run and punch follow-up",
)

ATTACK_ANIMATIONS = {
    "command_grab_active", "crouching_heavy_punch", "crouching_light_punch",
    "crouching_medium_punch", "down_forward_heavy_punch", "air_heavy_kick",
    "air_light_punch", "air_heavy_punch", "body_splash", "throw",
    "heavy_punch", "light_punch", "standing_heavy_kick", "forward_heavy_punch",
    "standing_light_kick", "forward_light_kick", "air_light_kick",
    "air_up_heavy_kick", "attack", "crouching_heavy_kick",
    "crouching_light_kick", "spd_air_grab", "spd_grab",
}
SPECIAL_ANIMATIONS = {
    "qcf_power_punch", "fireball", "qcf_power_punch_rekka", "reflector_cast",
    "stomp_special", "command_run", "command_run_punch",
}
SUPER_ANIMATIONS = {"super_fireball", "super_one_finisher"}


@dataclass(frozen=True)
class CatalogAnimation:
    name: str
    loop: bool
    durations: tuple[int, ...]
    texture_paths: tuple[str, ...]


def parse_resource(path: Path) -> list[CatalogAnimation]:
    textures: dict[str, str] = {}
    animations: list[CatalogAnimation] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        ext_match = EXT_RESOURCE_RE.match(line)
        if ext_match:
            textures[ext_match.group(2)] = ext_match.group(1)
            continue
        name_match = ANIMATION_NAME_RE.search(line)
        if not name_match:
            continue
        frames = FRAME_RE.findall(line)
        animations.append(CatalogAnimation(
            name=name_match.group(1),
            loop='"loop": true' in line,
            durations=tuple(max(1, round(float(duration))) for duration, _ in frames),
            texture_paths=tuple(textures[resource_id] for _, resource_id in frames),
        ))
    return animations


def category(name: str) -> str:
    if name.startswith("group_"):
        group = int(name.removeprefix("group_"))
        if 16 <= group <= 25:
            return "attack"
        if 26 <= group <= 31:
            return "special"
        return "state_or_system"
    if name in SUPER_ANIMATIONS:
        return "super"
    if name in SPECIAL_ANIMATIONS:
        return "special"
    if name in ATTACK_ANIMATIONS:
        return "attack"
    return "state_or_system"


def source_section(animation: CatalogAnimation) -> str:
    if animation.name.startswith("group_"):
        return animation.name.removeprefix("group_")
    groups = {
        match.group(1)
        for path in animation.texture_paths
        if (match := GROUP_FRAME_RE.match(Path(path).stem))
    }
    return " ".join(sorted(groups, key=int)) if groups else "generated"


def source_action(name: str) -> str:
    if name.startswith("group_"):
        group = int(name.removeprefix("group_"))
        return GROUP_DESCRIPTIONS[group]
    return name.replace("_", " ")


def frame_labels(animation: CatalogAnimation) -> list[str]:
    labels = []
    for path in animation.texture_paths:
        stem = Path(path).stem
        match = GROUP_FRAME_RE.match(stem)
        labels.append(match.group(2) if match else stem)
    return labels


def write_catalog(resource: Path = RESOURCE, catalog: Path = CATALOG) -> tuple[int, int]:
    animations = parse_resource(resource)
    catalog.parent.mkdir(parents=True, exist_ok=True)
    with catalog.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow(("animation", "source_section", "source_action", "category", "assignment",
                         "drawing_count", "timeline_ticks", "source_frames", "resolved_frames",
                         "hold_ticks", "offset_x", "offset_y", "missing_frames"))
        for animation in animations:
            labels = frame_labels(animation)
            writer.writerow((
                animation.name,
                source_section(animation),
                source_action(animation.name),
                category(animation.name),
                "SOURCE_POOL" if animation.name.startswith("group_") else "CURRENT_ASSIGNMENT",
                len(animation.durations),
                sum(animation.durations),
                " ".join(labels),
                " ".join(labels),
                " ".join(str(duration) for duration in animation.durations),
                " ".join("0" for _ in animation.durations),
                " ".join("0" for _ in animation.durations),
                "",
            ))
    return len(animations), sum(len(animation.durations) for animation in animations)


def main() -> None:
    animation_count, drawing_slots = write_catalog()
    print(f"Cataloged {animation_count} Sanzou animations and {drawing_slots} drawing slots -> {CATALOG.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
