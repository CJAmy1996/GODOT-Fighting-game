"""Import selected Kung Fu Man sprite-sheet rows into Godot-ready animation frames.

Usage:
    python Tools/import_kfm_sprites.py "path/to/Kung Fu Man.png"

The source sheet is irregular rather than a fixed grid. Frames are discovered from
the magenta cell backgrounds within selected horizontal bands, color-keyed, and
placed on a stable bottom-centered canvas before a SpriteFrames .tres is written.
"""

from pathlib import Path
import shutil
import sys

from PIL import Image


PROJECT_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_ROOT = PROJECT_ROOT / "Assets" / "TestFighter" / "KungFuMan"
FRAME_ROOT = OUTPUT_ROOT / "Frames"
GREEN = (0, 229, 111)
MAGENTA = (255, 0, 255)
CANVAS_SIZE = (160, 128)
BOTTOM_MARGIN = 4

# animation: (inclusive y start, inclusive y end, maximum source x, fps, loop)
SEQUENCES = {
    "idle": (5, 110, 400, 8.0, True),
    "walk": (116, 227, 900, 12.0, True),
    "walk_back": (233, 344, 950, 12.0, True),
    "jump": (350, 456, 600, 10.0, False),
    "attack": (462, 579, 500, 14.0, False),
}

ANIMATION_SETTINGS = {
    **{name: (fps, loop) for name, (_, _, _, fps, loop) in SEQUENCES.items()},
    "run": (12.0, True),
    "neutral_jump": (14.0, False),
    "fall": (12.0, True),
    "forward_jump_start": (14.0, False),
    "forward_jump_loop": (14.0, True),
    "back_dash": (10.0, False),
    "crouch_start": (12.0, False),
    "crouch_hold": (8.0, True),
    "crouch_end": (12.0, False),
    "heavy_punch": (60.0, False),
    "crouching_heavy_punch": (30.0, False),
    "light_punch": (60.0, False),
    "air_heavy_punch": (60.0, False),
    "air_light_punch": (60.0, False),
    "crouching_light_punch": (60.0, False),
    "crouching_medium_punch": (60.0, False),
    "throw": (60.0, False),
    "forward_heavy_punch": (60.0, False),
    "standing_light_kick": (60.0, False),
    "forward_light_kick": (60.0, False),
    "standing_heavy_kick": (60.0, False),
    "air_up_heavy_kick": (60.0, False),
    "air_dash": (60.0, False),
    "air_light_kick": (60.0, False),
    "air_heavy_kick": (60.0, False),
    "crouching_light_kick": (60.0, False),
    "crouching_heavy_kick": (60.0, False),
    "super_one_finisher": (60.0, False),
    "super_fireball": (60.0, False),
    "fireball": (60.0, False),
}


def find_magenta_runs(image: Image.Image, y0: int, y1: int, max_x: int, min_width: int = 20):
    pixels = image.load()
    active_x = []
    for x in range(min(max_x, image.width)):
        if any(pixels[x, y][:3] == MAGENTA for y in range(y0, y1 + 1)):
            active_x.append(x)

    runs = []
    for x in active_x:
        # Normal cells have a five-pixel green gutter. Gaps of one to three pixels
        # can occur when an opaque sprite covers the full height of its magenta cell.
        if not runs or x > runs[-1][1] + 3:
            runs.append([x, x])
        else:
            runs[-1][1] = x
    return [(left, right) for left, right in runs if right - left >= min_width]


def find_magenta_bands(image: Image.Image):
    pixels = image.load()
    active_y = []
    for y in range(image.height):
        if any(pixels[x, y][:3] == MAGENTA for x in range(image.width)):
            active_y.append(y)

    bands = []
    for y in active_y:
        if not bands or y > bands[-1][1] + 1:
            bands.append([y, y])
        else:
            bands[-1][1] = y
    return [(top, bottom) for top, bottom in bands]


def color_key_and_align(frame: Image.Image) -> Image.Image:
    rgba = frame.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            if pixels[x, y][:3] in (GREEN, MAGENTA):
                pixels[x, y] = (0, 0, 0, 0)

    bounds = rgba.getbbox()
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    if bounds is None:
        return canvas
    sprite = rgba.crop(bounds)
    target_x = (CANVAS_SIZE[0] - sprite.width) // 2
    target_y = CANVAS_SIZE[1] - BOTTOM_MARGIN - sprite.height
    canvas.alpha_composite(sprite, (target_x, target_y))
    return canvas


def write_sprite_frames(frames_by_animation):
    entries = []
    resource_id = 1
    for animation, frames in frames_by_animation.items():
        for frame_path in frames:
            resource_name = f"{resource_id}_{animation}_{frame_path.stem.split('_')[-1]}"
            godot_path = frame_path.relative_to(PROJECT_ROOT).as_posix()
            entries.append((animation, frame_path, resource_name, godot_path))
            resource_id += 1

    lines = [
        f'[gd_resource type="SpriteFrames" load_steps={len(entries) + 1} format=3]',
        "",
    ]
    for _, _, resource_name, godot_path in entries:
        lines.append(f'[ext_resource type="Texture2D" path="res://{godot_path}" id="{resource_name}"]')
    lines.append("")
    lines.append("[resource]")
    lines.append("animations = [")

    for sequence_index, (animation, (fps, loop)) in enumerate(ANIMATION_SETTINGS.items()):
        animation_entries = [item for item in entries if item[0] == animation]
        lines.append("{")
        lines.append('"frames": [')
        for frame_index, (_, _, resource_name, _) in enumerate(animation_entries):
            comma = "," if frame_index < len(animation_entries) - 1 else ""
            lines.extend([
                "{",
                '"duration": 1.0,',
                f'"texture": ExtResource("{resource_name}")',
                f"}}{comma}",
            ])
        lines.append("],")
        lines.append(f'"loop": {str(loop).lower()},')
        lines.append(f'"name": &"{animation}",')
        lines.append(f'"speed": {fps}')
        lines.append("}" + ("," if sequence_index < len(ANIMATION_SETTINGS) - 1 else ""))
    lines.append("]")
    lines.append("")
    (OUTPUT_ROOT / "kung_fu_man_sprite_frames.tres").write_text("\n".join(lines), encoding="utf-8")


def write_all_lines_resource(line_records):
    entries = []
    resource_id = 1
    for record in line_records:
        for frame_path in record["frames"]:
            resource_name = f"{resource_id}_{record['name']}_{frame_path.stem.split('_')[-1]}"
            godot_path = frame_path.relative_to(PROJECT_ROOT).as_posix()
            entries.append((record["name"], resource_name, godot_path))
            resource_id += 1

    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(entries) + 1} format=3]', ""]
    for _, resource_name, godot_path in entries:
        lines.append(f'[ext_resource type="Texture2D" path="res://{godot_path}" id="{resource_name}"]')
    lines.extend(["", "[resource]", "animations = ["])
    for line_index, record in enumerate(line_records):
        animation_entries = [item for item in entries if item[0] == record["name"]]
        lines.extend(["{", '"frames": ['])
        for frame_index, (_, resource_name, _) in enumerate(animation_entries):
            comma = "," if frame_index < len(animation_entries) - 1 else ""
            lines.extend(["{", '"duration": 1.0,', f'"texture": ExtResource("{resource_name}")', f"}}{comma}"])
        lines.extend([
            "],",
            '"loop": true,',
            f'"name": &"{record["name"]}",',
            '"speed": 10.0',
            "}" + ("," if line_index < len(line_records) - 1 else ""),
        ])
    lines.extend(["]", ""])
    (OUTPUT_ROOT / "kung_fu_man_all_lines_sprite_frames.tres").write_text("\n".join(lines), encoding="utf-8")


def export_all_lines(source: Image.Image):
    line_root = OUTPUT_ROOT / "AnimationLines"
    preview_root = OUTPUT_ROOT / "LinePreviews"
    line_root.mkdir(parents=True, exist_ok=True)
    preview_root.mkdir(parents=True, exist_ok=True)
    records = []

    for line_index, (y0, y1) in enumerate(find_magenta_bands(source), start=1):
        line_name = f"line_{line_index:02d}"
        output_root = line_root / line_name
        output_root.mkdir(parents=True, exist_ok=True)
        frame_paths = []
        preview_frames = []
        runs = find_magenta_runs(source, y0, y1, source.width, min_width=3)
        for frame_index, (x0, x1) in enumerate(runs):
            raw_frame = source.crop((x0, y0, x1 + 1, y1 + 1))
            normalized = color_key_and_align(raw_frame)
            output_path = output_root / f"{line_name}_{frame_index:02d}.png"
            normalized.save(output_path)
            frame_paths.append(output_path)
            preview_frames.append(normalized)

        preview = Image.new("RGBA", (CANVAS_SIZE[0] * len(preview_frames), CANVAS_SIZE[1]), (0, 0, 0, 0))
        for frame_index, frame in enumerate(preview_frames):
            preview.alpha_composite(frame, (frame_index * CANVAS_SIZE[0], 0))
        preview.save(preview_root / f"{line_name}.png")
        records.append({"name": line_name, "y0": y0, "y1": y1, "frames": frame_paths})

    write_all_lines_resource(records)
    catalog = [
        "# Kung Fu Man animation-line catalog",
        "",
        "Every horizontal magenta-backed line from the supplied sheet is exported without assigning move names.",
        "Rename the animations after identifying them, or provide the line-number mapping for integration.",
        "",
        "| Animation | Source Y | Frames | Preview |",
        "|---|---:|---:|---|",
    ]
    for record in records:
        catalog.append(
            f"| `{record['name']}` | {record['y0']}–{record['y1']} | {len(record['frames'])} | "
            f"[PNG](LinePreviews/{record['name']}.png) |"
        )
    catalog.append("")
    (OUTPUT_ROOT / "ANIMATION_LINES.md").write_text("\n".join(catalog), encoding="utf-8")
    return records


def main():
    if len(sys.argv) != 2:
        raise SystemExit("Pass the source sprite-sheet path as the only argument.")
    source_path = Path(sys.argv[1]).resolve()
    if not source_path.is_file():
        raise SystemExit(f"Sprite sheet not found: {source_path}")

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    FRAME_ROOT.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path, OUTPUT_ROOT / "kung_fu_man_source.png")
    source = Image.open(source_path).convert("RGBA")
    frames_by_animation = {}

    for animation, (y0, y1, max_x, _, _) in SEQUENCES.items():
        animation_root = FRAME_ROOT / animation
        animation_root.mkdir(parents=True, exist_ok=True)
        frames = []
        for index, (x0, x1) in enumerate(find_magenta_runs(source, y0, y1, max_x)):
            raw_frame = source.crop((x0, y0, x1 + 1, y1 + 1))
            output_path = animation_root / f"{animation}_{index:02d}.png"
            color_key_and_align(raw_frame).save(output_path)
            frames.append(output_path)
        frames_by_animation[animation] = frames
        print(f"{animation}: {len(frames)} frames")

    line_records = export_all_lines(source)
    idle_forward = line_records[0]["frames"][1:6]
    if len(idle_forward) != 5:
        raise RuntimeError("Expected line_01 frames 01 through 05 for the idle animation.")
    # Intentional duplicated endpoints: 01..05, then 05..01 = 10 frames.
    frames_by_animation["idle"] = idle_forward + list(reversed(idle_forward))
    run_forward = line_records[3]["frames"][0:6]
    if len(run_forward) != 6:
        raise RuntimeError("Expected line_04 frames 00 through 05 for the run animation.")
    # Intentional duplicated endpoints: 00..05, then 05..00 = 12 frames.
    frames_by_animation["run"] = run_forward + list(reversed(run_forward))
    back_walk_forward = line_records[2]["frames"][0:16]
    if len(back_walk_forward) != 16:
        raise RuntimeError("Expected line_03 frames 00 through 15 for the back-walk animation.")
    frames_by_animation["walk_back"] = list(reversed(back_walk_forward))
    neutral_jump_frames = line_records[4]["frames"][0:7]
    fall_frames = line_records[4]["frames"][7:9]
    if len(neutral_jump_frames) != 7 or len(fall_frames) != 2:
        raise RuntimeError("Expected line_05 frames 00-06 for jump and 07-08 for falling.")
    frames_by_animation["neutral_jump"] = neutral_jump_frames
    frames_by_animation["fall"] = fall_frames
    forward_jump_start_frames = line_records[5]["frames"][0:2] + line_records[4]["frames"][2:5]
    forward_jump_loop_frames = line_records[5]["frames"][2:6]
    back_dash_frames = line_records[6]["frames"]
    if len(forward_jump_start_frames) != 5 or len(forward_jump_loop_frames) != 4 or len(back_dash_frames) != 2:
        raise RuntimeError("Forward-jump phase or back-dash source frames are incomplete.")
    frames_by_animation["forward_jump_start"] = forward_jump_start_frames
    frames_by_animation["forward_jump_loop"] = forward_jump_loop_frames
    frames_by_animation["back_dash"] = back_dash_frames
    crouch_source = line_records[7]["frames"][0:4]
    heavy_punch_source = line_records[10]["frames"]
    crouching_heavy_punch_source = line_records[11]["frames"]
    fireball_source = line_records[8]["frames"]
    light_punch_forward = line_records[9]["frames"][0:3]
    air_light_punch_source = line_records[12]["frames"]
    crouching_light_punch_source = line_records[13]["frames"]
    crouching_medium_punch_source = line_records[14]["frames"]
    throw_source = line_records[15]["frames"]
    forward_heavy_punch_source = line_records[16]["frames"]
    standing_light_kick_source = line_records[17]["frames"]
    forward_light_kick_source = line_records[18]["frames"]
    standing_heavy_kick_source = line_records[19]["frames"]
    air_up_heavy_kick_source = line_records[20]["frames"]
    air_dash_source = line_records[21]["frames"]
    air_light_kick_source = line_records[22]["frames"]
    air_heavy_kick_source = line_records[23]["frames"]
    crouching_light_kick_source = line_records[24]["frames"]
    crouching_heavy_kick_source = line_records[25]["frames"]
    super_one_finisher_source = line_records[26]["frames"]
    if len(crouch_source) != 4 or len(heavy_punch_source) != 6 or len(crouching_heavy_punch_source) != 10 or len(fireball_source) != 8 or len(light_punch_forward) != 3 or len(air_light_punch_source) != 5 or len(crouching_light_punch_source) != 2 or len(crouching_medium_punch_source) != 6 or len(throw_source) != 13 or len(forward_heavy_punch_source) != 7 or len(standing_light_kick_source) != 3 or len(forward_light_kick_source) != 6 or len(standing_heavy_kick_source) != 8 or len(air_up_heavy_kick_source) != 8 or len(air_dash_source) != 4 or len(air_light_kick_source) != 2 or len(air_heavy_kick_source) != 3 or len(crouching_light_kick_source) != 4 or len(crouching_heavy_kick_source) != 7 or len(super_one_finisher_source) != 9:
        raise RuntimeError("Crouch, heavy-punch, or light-punch source frames are incomplete.")
    crouch_transition = [crouch_source[0], crouch_source[1], crouch_source[3], crouch_source[2]]
    frames_by_animation["crouch_start"] = crouch_transition
    frames_by_animation["crouch_hold"] = [crouch_source[2]]
    frames_by_animation["crouch_end"] = list(reversed(crouch_transition))
    frames_by_animation["heavy_punch"] = [
        *[heavy_punch_source[0]] * 3,
        *[heavy_punch_source[1]] * 3,
        *[heavy_punch_source[2]] * 3,
        *[heavy_punch_source[3]] * 4,
        *[heavy_punch_source[4]] * 3,
        *[heavy_punch_source[5]] * 3,
        heavy_punch_source[2], heavy_punch_source[1], heavy_punch_source[0],
    ]
    frames_by_animation["crouching_heavy_punch"] = [
        crouching_heavy_punch_source[0], crouching_heavy_punch_source[0],
        crouching_heavy_punch_source[1], crouching_heavy_punch_source[1],
        crouching_heavy_punch_source[2], crouching_heavy_punch_source[2],
        crouching_heavy_punch_source[3], crouching_heavy_punch_source[4], crouching_heavy_punch_source[5],
        crouching_heavy_punch_source[6], crouching_heavy_punch_source[7],
        crouching_heavy_punch_source[8], crouching_heavy_punch_source[9],
        crouching_heavy_punch_source[8], crouching_heavy_punch_source[7],
        crouching_heavy_punch_source[6], crouching_heavy_punch_source[7],
        crouching_heavy_punch_source[8], crouching_heavy_punch_source[9],
        crouching_heavy_punch_source[8], crouching_heavy_punch_source[7],
        crouching_heavy_punch_source[6], crouching_heavy_punch_source[5],
        crouching_heavy_punch_source[4], crouching_heavy_punch_source[3],
        crouching_heavy_punch_source[2], crouching_heavy_punch_source[1], crouching_heavy_punch_source[0],
    ]
    fireball_frames = fireball_source + list(reversed(fireball_source[0:3]))
    frames_by_animation["fireball"] = [frame for frame in fireball_frames for _ in range(2)]
    # Super Fireball: 12f release startup, then its final three poses ping-pong
    # throughout the 2 active + 28 recovery frames.
    super_fireball_tail = [
        fireball_source[5], fireball_source[6], fireball_source[7], fireball_source[6]
    ]
    frames_by_animation["super_fireball"] = [
        *[fireball_source[0]] * 3,
        *[fireball_source[1]] * 3,
        *[fireball_source[2]] * 2,
        *[fireball_source[3]] * 2,
        *[fireball_source[4]] * 2,
        *[super_fireball_tail[index % len(super_fireball_tail)] for index in range(30)],
    ]
    # Jab timing at 60 Hz: 4f startup, source 10_02 active for 2f, 4f recovery.
    frames_by_animation["light_punch"] = [
        light_punch_forward[0], light_punch_forward[0],
        light_punch_forward[1], light_punch_forward[1],
        light_punch_forward[2], light_punch_forward[2],
        light_punch_forward[1], light_punch_forward[1],
        light_punch_forward[0], light_punch_forward[0],
    ]
    frames_by_animation["air_heavy_punch"] = [
        air_light_punch_source[0], air_light_punch_source[0],
        air_light_punch_source[1], air_light_punch_source[1],
        air_light_punch_source[3], air_light_punch_source[3],
        air_light_punch_source[2], air_light_punch_source[1],
        air_light_punch_source[1], air_light_punch_source[0],
    ]
    frames_by_animation["air_light_punch"] = [
        *[crouching_light_punch_source[0]] * 4,
        *[air_light_punch_source[4]] * 2,
        *[crouching_light_punch_source[0]] * 4,
    ]
    frames_by_animation["crouching_light_punch"] = [
        *[crouching_light_punch_source[0]] * 4,
        *[crouching_light_punch_source[1]] * 2,
        *[crouching_light_punch_source[0]] * 4,
    ]
    # Down-forward medium jab: line 15, with source 15_04 active on timeline frame 4.
    frames_by_animation["crouching_medium_punch"] = [
        *crouching_medium_punch_source[0:5],
        crouching_medium_punch_source[4],
        crouching_medium_punch_source[5],
        *reversed(crouching_medium_punch_source[0:5]),
    ]
    # Classic throw animation: line 16 at 30 source frames per second.
    frames_by_animation["throw"] = [frame for frame in throw_source for _ in range(2)]
    frames_by_animation["forward_heavy_punch"] = [frame for frame in forward_heavy_punch_source for _ in range(2)]
    frames_by_animation["standing_light_kick"] = [
        *[standing_light_kick_source[0]] * 4,
        *[standing_light_kick_source[1]] * 4,
        *[standing_light_kick_source[2]] * 2,
        *[standing_light_kick_source[1]] * 2,
        *[standing_light_kick_source[0]] * 2,
    ]
    frames_by_animation["forward_light_kick"] = [
        forward_light_kick_source[0], forward_light_kick_source[0],
        forward_light_kick_source[1], forward_light_kick_source[2],
        *[forward_light_kick_source[3]] * 4,
        *[forward_light_kick_source[4]] * 2,
        *[forward_light_kick_source[5]] * 2,
        forward_light_kick_source[2], forward_light_kick_source[0],
    ]
    frames_by_animation["standing_heavy_kick"] = [
        *standing_heavy_kick_source[0:4],
        *[standing_heavy_kick_source[4]] * 5,
        *[standing_heavy_kick_source[5]] * 2,
        *[standing_heavy_kick_source[6]] * 2,
        *[standing_heavy_kick_source[7]] * 2,
        *[standing_heavy_kick_source[6]] * 2,
        *[standing_heavy_kick_source[5]] * 2,
        *[standing_heavy_kick_source[0]] * 2,
    ]
    frames_by_animation["air_up_heavy_kick"] = [
        *air_up_heavy_kick_source[0:4],
        *[air_up_heavy_kick_source[4]] * 5,
        *[air_up_heavy_kick_source[5]] * 2,
        *[air_up_heavy_kick_source[6]] * 2,
        *[air_up_heavy_kick_source[7]] * 2,
        *[air_up_heavy_kick_source[6]] * 2,
        *[air_up_heavy_kick_source[5]] * 2,
        *[air_up_heavy_kick_source[0]] * 2,
    ]
    # Matches air_dash.tres: 11 active frames followed by 6 recovery frames.
    frames_by_animation["air_dash"] = [
        air_dash_source[0],
        *[air_dash_source[1]] * 10,
        *[air_dash_source[2]] * 3,
        *[air_dash_source[3]] * 3,
    ]
    frames_by_animation["air_light_kick"] = [
        *[air_light_kick_source[0]] * 4,
        *[air_light_kick_source[1]] * 4,
        *[air_light_kick_source[0]] * 8,
    ]
    frames_by_animation["air_heavy_kick"] = [
        *[air_heavy_kick_source[0]] * 2,
        *[air_heavy_kick_source[1]] * 2,
        *[air_heavy_kick_source[2]] * 5,
        *[air_heavy_kick_source[1]] * 6,
        *[air_heavy_kick_source[0]] * 6,
    ]
    frames_by_animation["crouching_light_kick"] = [
        *[crouching_light_kick_source[0]] * 2,
        *[crouching_light_kick_source[1]] * 2,
        *[crouching_light_kick_source[2]] * 4,
        *[crouching_light_kick_source[3]] * 2,
        *[crouching_light_kick_source[1]] * 2,
        *[crouching_light_kick_source[0]] * 2,
    ]
    frames_by_animation["crouching_heavy_kick"] = [
        crouching_heavy_kick_source[0], crouching_heavy_kick_source[1],
        crouching_heavy_kick_source[2], crouching_heavy_kick_source[2],
        *[crouching_heavy_kick_source[3]] * 5,
        *[crouching_heavy_kick_source[4]] * 2,
        *[crouching_heavy_kick_source[5]] * 2,
        *[crouching_heavy_kick_source[6]] * 2,
        *[crouching_heavy_kick_source[5]] * 2,
        *[crouching_heavy_kick_source[1]] * 2,
        *[crouching_heavy_kick_source[0]] * 2,
    ]
    frames_by_animation["super_one_finisher"] = super_one_finisher_source
    write_sprite_frames(frames_by_animation)
    print(f"animation lines: {len(line_records)}")
    print(f"Wrote assets to {OUTPUT_ROOT}")


if __name__ == "__main__":
    main()
