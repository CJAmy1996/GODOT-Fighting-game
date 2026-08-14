"""Render Mecha Heita's source-authored wall-bounce reaction as a composite.

The body timeline comes from Mecha Heita's generated animation/anchor CSVs.
The wall-impact overlay comes from the common-effect CSV.  Motion, overlay
offset, and sound are read back from the original Shift-JIS action scripts so
the review cannot silently substitute a similarly shaped effect.

This is review tooling only.  It does not change the Godot runtime resources.
"""

from __future__ import annotations

import csv
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MECHA_ASSETS = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "MechaHeita"
MECHA_SCRIPT = ROOT / "Extraction" / "BigBangBeatRevolve" / "_m_heita_scr" / "script.txt"
COMMON_SCRIPT = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_scr" / "script.txt"
COMMON_IMAGES = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
COMMON_CATALOG = ROOT / "Assets" / "Effects" / "BigBangCommon" / "common_animation_catalog.csv"
OUTPUT = ROOT / "Docs" / "BigBangBeatRevolveCommonPreviews"

FPS = 60
BODY_CANVAS_CENTER_X = 160
BODY_CANVAS_BASELINE_Y = 250
PANEL_SIZE = (800, 740)
WALL_X = 120
FLOOR_Y = 710
# This review height keeps the complete strong arc in frame.  The 293-pixel
# start-to-floor gap also lets the weak action reach its state-controlled final
# drawing at approximately the same tick as floor contact.
START_ORIGIN = (190.0, 417.0)


@dataclass(frozen=True)
class Motion:
    velocity_x: float
    velocity_y: float
    delta_x_per_tick: float
    delta_y_per_tick: float


@dataclass(frozen=True)
class SourceAction:
    name: str
    motion: Motion
    child_offset: tuple[int, int]
    child_section: int
    sound: int


def load_csv_row(path: Path, key: str, value: str) -> dict[str, str]:
    with path.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            if row[key] == value:
                return row
    raise ValueError(f"{value!r} was not found in {path}")


def source_section(path: Path, section_index: int) -> list[str]:
    text = path.read_text(encoding="cp932", errors="replace")
    sections = [section.strip() for section in text.split("------") if section.strip()]
    return [line for line in sections[section_index].splitlines() if line.strip()]


def parse_source_action(section: list[str]) -> SourceAction:
    name = section[0].split("\t")[0]
    motion: Motion | None = None
    child_offset: tuple[int, int] | None = None
    child_section = -1
    sound = -1
    for line in section[1:]:
        fields = line.split("\t")
        command = fields[0].strip()
        if command == "Ｍ" and motion is None:
            # M stores initial pixels/second in fields 1/2.  Fields 5/6 change
            # those velocities once per authored 60 Hz tick.  This is the same
            # mapping used by the existing DP port (1000, -800 and -40/tick).
            motion = Motion(float(fields[1]), float(fields[2]), float(fields[5]), float(fields[6]))
        elif command == "Ｏ" and child_offset is None:
            child_offset = (int(fields[1]), int(fields[2]))
            child_section = int(fields[3])
        elif command == "SE" and sound < 0:
            sound = int(fields[1])
    if motion is None or child_offset is None:
        raise ValueError(f"{name} does not contain its required M/O commands")
    return SourceAction(name, motion, child_offset, child_section, sound)


def number_list(row: dict[str, str], field: str, number_type=int) -> list:
    return [number_type(value) for value in row[field].split()]


def resolve_drawing(tick: int, holds: list[int]) -> int:
    cursor = 0
    for index, hold in enumerate(holds):
        cursor += hold
        if tick < cursor:
            return index
    return len(holds) - 1


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        dominant_green = green >= 115 and green - max(red, blue) >= 56
        pixels.append((red, green, blue, 0 if dominant_green else alpha))
    rgba.putdata(pixels)
    return rgba


def background(title: str, action: SourceAction) -> Image.Image:
    image = Image.new("RGBA", PANEL_SIZE, (12, 15, 24, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, PANEL_SIZE[1], 24):
        color = (17, 22, 34, 255) if (y // 24) % 2 == 0 else (20, 25, 39, 255)
        draw.rectangle((0, y, PANEL_SIZE[0], min(y + 23, PANEL_SIZE[1])), fill=color)
    draw.rectangle((0, 0, WALL_X, FLOOR_Y), fill=(28, 33, 49, 255))
    draw.line((WALL_X, 54, WALL_X, FLOOR_Y), fill=(116, 154, 228, 255), width=4)
    draw.line((WALL_X, FLOOR_Y, PANEL_SIZE[0], FLOOR_Y), fill=(116, 154, 228, 255), width=4)
    draw.text((18, 14), title, fill=(255, 231, 138, 255))
    draw.text((18, 32),
              f"M {action.motion.velocity_x:g} {action.motion.velocity_y:g} | velocity delta/tick "
              f"({action.motion.delta_x_per_tick:g}, {action.motion.delta_y_per_tick:g}) | SE {action.sound}",
              fill=(210, 220, 241, 255))
    return image


def render_variant(animation: str, label: str) -> tuple[Path, Path]:
    body_row = load_csv_row(MECHA_ASSETS / "animation_catalog.csv", "animation", animation)
    anchor_row = load_csv_row(MECHA_ASSETS / "animation_anchor_catalog.csv", "animation", animation)
    effect_row = load_csv_row(COMMON_CATALOG, "source_id", "common_section_021")

    body_frames = number_list(body_row, "resolved_frames")
    body_source_frames = number_list(body_row, "source_frames")
    body_holds = number_list(body_row, "hold_ticks")
    body_anchor_x = number_list(anchor_row, "godot_anchor_x", float)
    body_anchor_y = number_list(anchor_row, "godot_anchor_y", float)
    effect_frames = number_list(effect_row, "source_frames")
    effect_holds = number_list(effect_row, "hold_ticks")
    effect_origin_x = number_list(effect_row, "origin_x")
    effect_origin_y = number_list(effect_row, "origin_y")

    action = parse_source_action(source_section(MECHA_SCRIPT, int(body_row["source_section"])))
    common = source_section(COMMON_SCRIPT, int(effect_row["source_section"]))
    common_name = common[0].split("\t")[0]
    if action.child_section != int(effect_row["source_section"]):
        raise ValueError(
            f"{action.name} spawns section {action.child_section}, but the selected CSV row is "
            f"section {effect_row['source_section']}")
    if common_name != effect_row["source_action"]:
        raise ValueError("common CSV name does not match the original source section")

    body_images = [
        Image.open(MECHA_ASSETS / "Frames" / f"frame_{frame_id:04d}.png").convert("RGBA")
        for frame_id in body_frames
    ]
    effect_images: list[Image.Image | None] = []
    for frame_id in effect_frames:
        path = COMMON_IMAGES / f"{frame_id:03d}.png"
        effect_images.append(remove_green(Image.open(path)) if path.exists() else None)

    total_body_ticks = sum(body_holds[:-1])
    position_x, position_y = START_ORIGIN
    velocity_x = action.motion.velocity_x
    velocity_y = action.motion.velocity_y
    timeline: list[tuple[int, float, float, int, int | None]] = []
    landed = False
    tick = 0
    # The final 1000-tick drawing is state-controlled.  End this review when
    # the authored trajectory first touches the floor instead of inventing a
    # thousand-frame pause that the game exits through collision/state logic.
    while tick < 120:
        body_drawing = resolve_drawing(tick, body_holds)
        effect_drawing = resolve_drawing(tick, effect_holds) if tick < sum(effect_holds) else None
        if tick >= total_body_ticks and position_y >= FLOOR_Y:
            position_y = FLOOR_Y
            timeline.append((tick, position_x, position_y, body_drawing, effect_drawing))
            landed = True
            break
        timeline.append((tick, position_x, position_y, body_drawing, effect_drawing))
        position_x += velocity_x / FPS
        position_y += velocity_y / FPS
        velocity_x += action.motion.delta_x_per_tick
        velocity_y += action.motion.delta_y_per_tick
        tick += 1
    if not landed:
        raise RuntimeError(f"{label} preview did not reach the floor")

    rendered: list[Image.Image] = []
    # O creates the common effect at the wall-contact position on tick zero.
    # It is a separate spawned visual, so it remains at that world position
    # while the victim's M command launches the body away from the wall.
    spawned_effect_anchor_x = START_ORIGIN[0] + action.child_offset[0]
    spawned_effect_anchor_y = START_ORIGIN[1] + action.child_offset[1]
    for tick, origin_x, origin_y, body_drawing, effect_drawing in timeline:
        frame = background(f"MECHA HEITA — {label} WALL BOUNCE (source composite)", action)
        if effect_drawing is not None:
            effect = effect_images[effect_drawing]
            if effect is not None:
                frame.alpha_composite(effect, (
                    round(spawned_effect_anchor_x - effect_origin_x[effect_drawing]),
                    round(spawned_effect_anchor_y - effect_origin_y[effect_drawing]),
                ))

        body = body_images[body_drawing]
        frame.alpha_composite(body, (
            round(origin_x - BODY_CANVAS_CENTER_X + body_anchor_x[body_drawing]),
            round(origin_y - BODY_CANVAS_BASELINE_Y + body_anchor_y[body_drawing]),
        ))

        draw = ImageDraw.Draw(frame)
        effect_text = "ended"
        if effect_drawing is not None:
            effect_id = effect_frames[effect_drawing]
            effect_text = f"{effect_id:03d}" if effect_images[effect_drawing] is not None else f"{effect_id:03d} MISSING"
        draw.rectangle((0, PANEL_SIZE[1] - 44, PANEL_SIZE[0], PANEL_SIZE[1]), fill=(6, 8, 13, 242))
        draw.text((16, PANEL_SIZE[1] - 36),
                  f"tick {tick:02d} | body {body_source_frames[body_drawing]:03d} "
                  f"({body_holds[body_drawing]}f) | common wall effect {effect_text} | "
                  f"O ({action.child_offset[0]}, {action.child_offset[1]}) -> section {action.child_section}",
                  fill=(238, 241, 248, 255))
        rendered.append(frame.convert("P", palette=Image.Palette.ADAPTIVE))

    # Briefly hold the collision frame for review; this is presentation only
    # and is not counted as part of the source action in the on-frame label.
    rendered.extend([rendered[-1].copy() for _ in range(5)])

    OUTPUT.mkdir(parents=True, exist_ok=True)
    slug = animation.replace("anim_", "")
    gif_path = OUTPUT / f"mecha_heita_wall_bounce_{label.lower()}_source_composite_{slug}.gif"
    rendered[0].save(
        gif_path,
        save_all=True,
        append_images=rendered[1:],
        # GIF delays are centiseconds, so alternate 20/20/10 ms.  That is
        # exactly 50 ms for every three source ticks (the 60 Hz average) rather
        # than silently playing the review at 50 or 100 FPS.
        duration=[(20, 20, 10)[index % 3] for index in range(len(rendered))],
        loop=0,
        disposal=2,
    )

    sample_ticks = sorted(set([0, 4, 8, 12, 16, 20, 24, 28, 32, len(timeline) - 1]))
    samples = [rendered[min(index, len(timeline) - 1)].convert("RGBA") for index in sample_ticks]
    columns = 2
    rows = math.ceil(len(samples) / columns)
    gap = 8
    sheet = Image.new("RGBA", (
        columns * PANEL_SIZE[0] + (columns + 1) * gap,
        rows * PANEL_SIZE[1] + (rows + 1) * gap,
    ), (8, 10, 16, 255))
    for index, sample in enumerate(samples):
        x = gap + (index % columns) * (PANEL_SIZE[0] + gap)
        y = gap + (index // columns) * (PANEL_SIZE[1] + gap)
        sheet.alpha_composite(sample, (x, y))
    sheet_path = OUTPUT / f"mecha_heita_wall_bounce_{label.lower()}_source_composite_{slug}_sheet.png"
    sheet.save(sheet_path)
    return gif_path, sheet_path


def main() -> None:
    for animation, label in (("anim_052", "STRONG"), ("anim_053", "WEAK")):
        for path in render_variant(animation, label):
            print(path.relative_to(ROOT))


if __name__ == "__main__":
    main()
