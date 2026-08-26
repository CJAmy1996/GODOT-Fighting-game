"""Convert the legacy Sanzo frame dump into transparent Godot-ready assets."""

from pathlib import Path
from PIL import Image
from catalog_sanzo_animations import write_catalog


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "TestFighter" / "Sanzo" / "sanzou_kongoumaru"
OUTPUT = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Frames"
RESOURCE = ROOT / "Assets" / "TestFighter" / "Sanzo" / "sanzo_sprite_frames.tres"
CANVAS_SIZE = (320, 384)
SANDAL_BASELINE_Y = 250
TIMELINE_FPS = 60.0
# The legacy art was authored at approximately 10 drawings per second. Each
# drawing therefore occupies six deterministic 60 Hz simulation ticks.
SOURCE_FRAME_HOLD_TICKS = 6.0
ANIMATION_HOLD_TICKS = {
    "crouch_start": 1.0,
    "crouch_end": 1.0,
    "crouch_hold": 5.0,
    # Match the 165 px/s runtime movement closely enough that planted sandals
    # do not skate while the CharacterBody advances.
    "walk": 3.0,
    "walk_back": 3.0,
    "win_start": 5.0,
    "win": 5.0,
    "win_loop": 5.0,
    "trait_2": 3.0,
    "spd_air_grab": 4.0,
}
FRAME_HOLD_OVERRIDES = {
    # BIGBANG BEAT Revolve script.txt, neutral action, images 104-115.
    "idle": {
        0: 4.0, 1: 4.0, 2: 4.0, 3: 4.0, 4: 4.0, 5: 4.0,
        6: 5.0, 7: 6.0, 8: 6.0, 9: 6.0, 10: 6.0, 11: 5.0,
    },
    # BIGBANG BEAT Revolve [System] Start action, including the post-144
    # continuation into images 254-262. Image 142 is intentionally absent.
    "intro": {
        0: 130.0, 1: 5.0, 2: 5.0, 3: 5.0, 4: 5.0, 5: 5.0,
        6: 5.0, 7: 5.0, 8: 5.0, 9: 6.0, 10: 6.0, 11: 30.0,
        12: 4.0, 13: 4.0, 14: 3.0, 15: 4.0, 16: 13.0, 17: 5.0,
        18: 5.0,
    },
	# Group 25 standing jab: four unique drawings across a ten-tick move.
	"light_punch": {0: 2.0, 1: 2.0, 2: 2.0, 3: 4.0},
	"group_25": {0: 2.0, 1: 2.0, 2: 2.0, 3: 4.0},
    # Group 17, zero-based: 0-4 share startup, 5-8 are active, 9-11 recover.
    "crouching_heavy_punch": {
        0: 2.0, 1: 2.0, 2: 2.0, 3: 1.0, 4: 1.0,
        5: 1.0, 6: 1.0, 7: 1.0, 8: 11.0,
        9: 4.0, 10: 3.0, 11: 8.0,
    },
    # Group 18, zero-based: 0-1 start up, 2-3 are active, and 4 recovers.
    "crouching_light_punch": {0: 2.0, 1: 2.0, 2: 1.0, 3: 1.0, 4: 4.0},
    # The former Group 18 crouching-LK placeholder is now the crouching medium.
    "crouching_medium_punch": {0: 2.0, 1: 2.0, 2: 1.0, 3: 1.0, 4: 4.0},
    # Group 19's former crouching medium is now the down-forward heavy punch.
    "down_forward_heavy_punch": {
        0: 2.0, 1: 2.0,
        2: 1.0, 3: 1.0, 4: 1.0, 5: 1.0,
        6: 2.0, 7: 2.0, 8: 2.0,
    },
    # New five-drawing crouching light kick: 4 startup, 4 active, 21 recovery.
    "crouching_light_kick": {0: 2.0, 1: 2.0, 2: 2.0, 3: 2.0, 4: 21.0},
    # Special-1 SPD: five ticks of standing HP startup, followed by the
    # cape-only airborne grab loop used after a successful capture.
    "spd_grab": {0: 3.0, 1: 2.0, 2: 4.0, 3: 4.0, 4: 4.0, 5: 4.0},
    # Group 20, zero-based: 0-2 start up, 3-4 are active, 5-7 recover.
    "air_heavy_kick": {0: 3.0, 1: 2.0, 2: 2.0, 3: 1.0, 4: 1.0, 5: 2.0, 6: 2.0, 7: 1.0},
    # Group 21 uses zero-based source indices: 0 starts up, 1-2 are active,
    # and 3 is recovery.
    "air_light_punch": {0: 4.0, 1: 1.0, 2: 1.0, 3: 4.0},
    # Group 22 body splash: 0-2 start up, 3-4 stay active for 30 ticks,
    # and 5-6 use the authored 14-frame recovery.
    "air_heavy_punch": {0: 2.0, 1: 1.0, 2: 1.0, 3: 15.0, 4: 15.0, 5: 7.0, 6: 7.0},
    "body_splash": {0: 2.0, 1: 1.0, 2: 1.0, 3: 15.0, 4: 15.0, 5: 7.0, 6: 7.0},
    # Group 23: frame 0 reaches the grab, 1-8 carry the victim, and 9 settles.
    "throw": {0: 10.0, 1: 2.0, 2: 2.0, 3: 2.0, 4: 2.0, 5: 2.0, 6: 2.0, 7: 2.0, 8: 2.0, 9: 10.0},
    # Group 24 standing heavy: 0-4 share 15 startup ticks, 5-8 are active
    # for one tick each, and 9-11 share 34 recovery ticks.
    "heavy_punch": {
        0: 3.0, 1: 3.0, 2: 3.0, 3: 3.0, 4: 3.0,
        5: 1.0, 6: 1.0, 7: 1.0, 8: 1.0,
        9: 12.0, 10: 11.0, 11: 11.0,
    },
    # Group 26: frame 2 bridges the final startup tick into the active phase.
    # Frames 2-6 cover eight active ticks; 7-9 cover 15 recovery ticks.
    "qcf_power_punch": {0: 7.0, 1: 7.0, 2: 3.0, 3: 2.0, 4: 2.0, 5: 1.0, 6: 1.0, 7: 5.0, 8: 5.0, 9: 5.0},
    "fireball": {0: 7.0, 1: 7.0, 2: 3.0, 3: 2.0, 4: 2.0, 5: 1.0, 6: 1.0, 7: 5.0, 8: 5.0, 9: 5.0},
    # Group 27: 0-4 share eight startup ticks, 5-8 are active, 9-10 recover.
    "qcf_power_punch_rekka": {0: 2.0, 1: 2.0, 2: 2.0, 3: 1.0, 4: 1.0, 5: 1.0, 6: 1.0, 7: 1.0, 8: 1.0, 9: 6.0, 10: 6.0},
    # Group 28: 0-3 share 13 startup ticks, 4-6 are active, 7-9 recover.
    "standing_heavy_kick": {0: 4.0, 1: 3.0, 2: 3.0, 3: 3.0, 4: 1.0, 5: 1.0, 6: 1.0, 7: 5.0, 8: 5.0, 9: 4.0},
    # Group 29 block reflector: frame 6 bridges startup into activation.
    "reflector_cast": {0: 1.0, 1: 1.0, 2: 1.0, 3: 1.0, 4: 1.0, 5: 1.0, 6: 2.0, 7: 1.0, 8: 1.0, 9: 1.0, 10: 1.0, 11: 1.0, 12: 1.0},
    # Group 30: 0-2 start up, 3-4 rise-hit, 5-6 bridge the arc, 7-9 stomp,
    # and 10-11 recover.
    "stomp_special": {0: 1.0, 1: 1.0, 2: 1.0, 3: 1.0, 4: 1.0, 5: 1.0, 6: 1.0, 7: 1.0, 8: 1.0, 9: 1.0, 10: 6.0, 11: 6.0},
}

# Keep the crouching jab's hips planted while its arms change the silhouette's
# width. Without these small corrections, per-frame centering makes the body
# appear to slide sideways even though the CharacterBody remains stationary.
GROUP_FRAME_X_OFFSETS = {
    # BIGBANG BEAT Revolve script.txt uses half-scale X coordinates
    # 20,22,28,34,34,32,28,28,24,20,18,22 for neutral images 104-115.
    # Preserve those authored coordinates relative to the first drawing.
    12: [0, 1, 4, 7, 7, 6, 4, 4, 2, 0, -1, 1],
    # Source victory action, images 126-132, relative to neutral image 104.
    14: [-10, -10, -4, 9, 16, 9, 9],
    # Revolve [BB] Big Bang Mode activation, images 146-149, relative to
    # neutral image 104.
    16: [-24, -20, -19, -17],
    18: [0, 0, 6, 6, 2],
}

GROUP_FRAME_Y_OFFSETS = {
    # Source Y anchors are 9.5,9.5,8,8,8,8,8 versus neutral's 5.
    14: [4, 4, 3, 3, 3, 3, 3],
    16: [-1, -1, -1, -1],
}

GROUPS = [
    (0, 5), (7, 13), (15, 20), (22, 29), (31, 35), (37, 41), (43, 48),
    (50, 57), (59, 67), (69, 76), (78, 89), (91, 102), (104, 115),
    (117, 124), (126, 132), (134, 144), (146, 149), (151, 162),
    (164, 168), (170, 178), (180, 187), (189, 192), (194, 200),
    (202, 211), (213, 224), (226, 229), (231, 240), (242, 252),
    (254, 263), (265, 277), (279, 290), (292, 311),
]

# Extracted Revolve script.txt [System] Start action. Offsets are the source
# anchors relative to neutral image 104, rounded only where the legacy
# half-scale renderer lands between physical pixels.
INTRO_SOURCE_DRAWINGS = [
    (134, -21, 0), (135, -18, 0), (136, -14, 0), (137, -16, 0),
    (138, -14, 0), (139, -22, 0), (140, -8, 0), (141, -7, 0),
    (143, -3, 0), (144, 0, 0), (254, -23, 0), (255, -35, 0),
    (256, -33, 0), (257, -38, 0), (258, -16, 0), (259, -14, 0),
    (260, -16, 0), (261, -15, 0), (262, -13, 0),
]

# User-confirmed gameplay aliases. Values are (group, start, end); omitted slice
# bounds select the complete group. Original group_00 ... group_31 entries remain
# available in the editor for later tuning.
ALIASES = {
    # Reactions and defense.
    "crouch_hit": (0, None, None),
    "knockdown": (1, None, None),
    "ground_bounce": (1, None, None),
    "get_up": (2, None, None),
    "tumble": (3, None, None),
    "air_hitstun": (3, None, None),
    "hitstun_light": (4, None, None),
    "hitstun_medium": (4, None, None),
    "hitstun_heavy": (5, None, None),
    "hitstun_heavy_air": (5, None, None),
    "stand_block": (6, 0, 1),
    "stand_block_impact": (6, 1, 2),
    "crouch_block": (6, 2, 3),
    "crouch_block_impact": (6, 3, 4),
    "air_block": (6, 4, 5),
    "air_block_impact": (6, 5, 6),

    # Movement and presentation.
    "crouch_start": (7, 0, 5),
    "crouch_hold": (13, None, None),
    "crouch_end": (7, 0, 5),
    "neutral_jump": (8, None, None),
    "forward_jump_start": (8, 0, 3),
    "forward_jump_loop": (8, 3, None),
    "fall": (8, 4, None),
    "forward_dash": (9, 0, 4),
    "back_dash": (9, 4, 8),
    "walk_back": (10, None, None),
    "walk": (11, None, None),
    "idle": (12, None, None),
    "win_start": (14, 0, 3),
    "win": (14, 3, 7),
    "win_loop": (14, 3, 7),
    "intro": (15, None, None),
    "trait_2": (16, None, None),

    # Normals and grabs.
    "crouching_heavy_punch": (17, None, None),
    "crouching_light_punch": (18, None, None),
    "crouching_medium_punch": (18, None, None),
    "down_forward_heavy_punch": (19, None, None),
    "air_heavy_kick": (20, None, None),
    "air_light_punch": (21, None, None),
    "air_heavy_punch": (22, None, None),
    "body_splash": (22, None, None),
    "throw": (23, None, None),
    "heavy_punch": (24, 0, 11),
    "light_punch": (25, None, None),
    "standing_heavy_kick": (28, None, None),

    # Specials and supers.
    "qcf_power_punch": (26, None, None),
    "fireball": (26, None, None),
    "qcf_power_punch_rekka": (27, None, None),
    "reflector_cast": (29, None, None),
    "super_fireball": (29, None, None),
    "stomp_special": (30, None, None),
    "command_run": (31, 0, 8),
    "command_run_punch": (31, 8, None),
    "run": (31, 0, 8),

    # Safe placeholders for framework inputs without a confirmed unique group.
    "forward_heavy_punch": (24, None, None),
    "standing_light_kick": (25, None, None),
    "forward_light_kick": (25, None, None),
    "air_light_kick": (21, None, None),
    "air_up_heavy_kick": (22, None, None),
    "attack": (24, None, None),
    "super_one_finisher": (29, None, None),
}


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.getdata():
        # The source uses pure/nearly pure green as its transparency key.
        if green > 220 and red < 45 and blue < 45:
            pixels.append((red, green, blue, 0))
        else:
            pixels.append((red, green, blue, alpha))
    rgba.putdata(pixels)
    return rgba


def align_to_sandal_baseline(image: Image.Image) -> Image.Image:
    """Center a frame horizontally and put its lowest visible pixel on one floor line.

    Godot centers AnimatedSprite2D textures. The source dump contains tightly
    cropped frames of different sizes, so using them directly makes Sanzou jump
    around. A shared canvas makes the origin stable while preserving every pixel.
    """
    rgba = image.convert("RGBA")
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        return Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    horizontal_center = (bounds[0] + bounds[2]) // 2
    paste_x = CANVAS_SIZE[0] // 2 - horizontal_center
    paste_y = SANDAL_BASELINE_Y - bounds[3]
    canvas.alpha_composite(rgba, (paste_x, paste_y))
    return canvas


def shift_canvas(image: Image.Image, offset_x: int, offset_y: int) -> Image.Image:
    if offset_x == 0 and offset_y == 0:
        return image
    shifted = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    shifted.alpha_composite(image, (offset_x, offset_y))
    return shifted


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    group_paths = []
    for group_index, (start, end) in enumerate(GROUPS):
        paths = []
        for drawing_index, frame_number in enumerate(range(start, end + 1)):
            source = SOURCE / f"{frame_number}.bmp"
            if not source.exists():
                continue
            destination = OUTPUT / f"group_{group_index:02d}_{frame_number:03d}.png"
            aligned = align_to_sandal_baseline(remove_green(Image.open(source)))
            offsets = GROUP_FRAME_X_OFFSETS.get(group_index, [])
            offset_x = offsets[drawing_index] if drawing_index < len(offsets) else 0
            y_offsets = GROUP_FRAME_Y_OFFSETS.get(group_index, [])
            offset_y = y_offsets[drawing_index] if drawing_index < len(y_offsets) else 0
            shift_canvas(aligned, offset_x, offset_y).save(destination)
            paths.append(destination)
        group_paths.append(paths)

    generated_sweep_paths = []
    # Keep the authored pre-clean sweep. The later resized/recolored variant is
    # retained only as an archive and must not replace the live animation.
    generated_sweep_source = (
        ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "crouching_heavy_kick"
    )
    for source in sorted(generated_sweep_source.glob("*.png")):
        destination = OUTPUT / f"generated_{source.name}"
        align_to_sandal_baseline(Image.open(source)).save(destination)
        generated_sweep_paths.append(destination)
    generated_light_kick_paths = []
    generated_light_kick_source = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "crouching_light_kick"
    for source in sorted(generated_light_kick_source.glob("*.png")):
        destination = OUTPUT / f"generated_{source.name}"
        align_to_sandal_baseline(Image.open(source)).save(destination)
        generated_light_kick_paths.append(destination)
    generated_spd_paths = []
    generated_spd_source = ROOT / "Assets" / "TestFighter" / "Sanzo" / "Generated" / "spd_air_grab"
    for source in sorted(generated_spd_source.glob("spd_air_grab_*.png")):
        destination = OUTPUT / f"generated_{source.name}"
        # The SPD art is already authored on the live 320x384 canvas. Preserve
        # its exact placement so only the two loose cape tips animate.
        Image.open(source).convert("RGBA").save(destination)
        generated_spd_paths.append(destination)
    intro_paths = []
    for frame_number, offset_x, offset_y in INTRO_SOURCE_DRAWINGS:
        source = SOURCE / f"{frame_number}.bmp"
        destination = OUTPUT / f"source_intro_{frame_number:03d}.png"
        aligned = align_to_sandal_baseline(remove_green(Image.open(source)))
        shift_canvas(aligned, offset_x, offset_y).save(destination)
        intro_paths.append(destination)
    generated_animations = {
        "crouching_heavy_kick": generated_sweep_paths,
        "crouching_light_kick": generated_light_kick_paths,
        "spd_air_grab": generated_spd_paths,
    }
    all_paths = [path for paths in group_paths for path in paths]
    all_paths.extend(path for paths in generated_animations.values() for path in paths)
    all_paths.extend(intro_paths)
    ext_ids = {path: index + 1 for index, path in enumerate(all_paths)}
    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(all_paths) + 1} format=3]', ""]
    for path in all_paths:
        relative = path.relative_to(ROOT).as_posix()
        lines.append(f'[ext_resource type="Texture2D" path="res://{relative}" id="{ext_ids[path]}"]')

    animations = [(f"group_{index:02d}", group_paths[index]) for index in range(len(group_paths))]
    for name, (group_index, start, end) in ALIASES.items():
        selected_paths = intro_paths if name == "intro" else group_paths[group_index][slice(start, end)]
        if name == "trait_2":
            # Source order: 146,147,148,149 repeated twice, then 146,147.
            selected_paths = selected_paths + selected_paths + selected_paths[:2]
        if name == "crouch_end":
            selected_paths = list(reversed(selected_paths))
        elif name == "heavy_punch" and selected_paths:
            # Source group frame 11 is empty; retain the final visible recovery
            # pose as drawing 11 so the fighter never disappears.
            selected_paths = selected_paths + [selected_paths[-1]]
        animations.append((name, selected_paths))
    animations.extend(generated_animations.items())
    animations.append(("spd_grab", group_paths[24][:2] + generated_spd_paths))
    lines.extend(["", "[resource]", "animations = ["])
    for animation_index, (name, frames) in enumerate(animations):
        loop = name in {"idle", "walk", "walk_back", "run", "command_run", "crouch_hold", "win", "win_loop", "spd_air_grab"} or name.startswith("group_")
        hold_ticks = ANIMATION_HOLD_TICKS.get(name, SOURCE_FRAME_HOLD_TICKS)
        frame_entries = ", ".join(
            '{"duration": %.1f, "texture": ExtResource("%d")}'
            % (FRAME_HOLD_OVERRIDES.get(name, {}).get(frame_index, hold_ticks), ext_ids[path])
            for frame_index, path in enumerate(frames)
        )
        comma = "," if animation_index < len(animations) - 1 else ""
        lines.append(
            '{"frames": [%s], "loop": %s, "name": &"%s", "speed": %.1f}%s'
            % (frame_entries, str(loop).lower(), name, TIMELINE_FPS, comma)
        )
    lines.append("]")
    RESOURCE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    write_catalog(RESOURCE)
    print(f"Imported {len(all_paths)} Sanzo frames into {OUTPUT}")


if __name__ == "__main__":
    main()
