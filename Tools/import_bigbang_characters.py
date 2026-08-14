"""Import extracted BIGBANG BEAT Revolve fighters as editable Godot characters.

The legacy archives keep animation order in Shift-JIS ``script.txt`` files.
This importer preserves every visual action as ``anim_###``, writes a UTF-8
catalog with the original Japanese action names, and adds conservative aliases
so a fighter can enter the test arena before its move-by-move pass is complete.
"""

from __future__ import annotations

import csv
import shutil
from dataclasses import dataclass
from math import cos, lcm, radians, sin
from pathlib import Path
from typing import Callable, Iterable

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[1]
EXTRACTION = ROOT / "Extraction" / "BigBangBeatRevolve"
ASSET_ROOT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve"
DATA_ROOT = ROOT / "Data" / "Characters" / "BigBangBeatRevolve"
SCENE_ROOT = ROOT / "Scenes" / "TestCharacters"

CANVAS_SIZE = (320, 384)
FLOOR_BASELINE_Y = 250
TIMELINE_FPS = 60.0
MECHA_BOOSTER_JET_OFFSET = (-61, 24)
# Forward flight uses anim_103. Keep the reference frame-89 placement and
# follow the authored backpack bob on its two alternate drawings.
MECHA_FLIGHT_BODY_JET_ADJUSTMENTS = {
    89: (0, 0),
    90: (0, -4),
    91: (0, -2),
}
MECHA_DIRECTIONAL_JET_CROP = (102, 157, 218, 250)
MECHA_DIRECTIONAL_FLIGHT_SPECS = {
    # Measured directly from the user's pixel-placement references.
    # animation: (body source, jet rotation, transformed jet top-left, body rotation)
    "booster_up": (70, 55, (92, 159), 0),
    "booster_up_forward": (80, 11, (58, 158), 0),
    "booster_forward": (89, -36, (43, 129), 0),
    "booster_down_forward": (89, -63, (48, 107), -38),
    "booster_down": (70, -125, (87, 37), 0),
    "booster_down_back": (61, -172, (131, 84), 0),
    "booster_back": (92, 154, (133, 126), 0),
    "booster_up_back": (61, 103, (134, 138), 0),
}


def rotate_screen_offset(offset: tuple[int, int], angle: int) -> tuple[int, int]:
    """Rotate a pixel offset using Pillow's screen-coordinate angle convention."""
    theta = radians(angle)
    return (round(cos(theta) * offset[0] + sin(theta) * offset[1]),
            round(-sin(theta) * offset[0] + cos(theta) * offset[1]))


@dataclass(frozen=True)
class FighterSpec:
    archive_id: str
    display_name: str
    scene_stem: str

    @property
    def slug(self) -> str:
        return self.archive_id


@dataclass(frozen=True)
class Drawing:
    hold_ticks: int
    image_id: int
    offset_x: int
    offset_y: int


@dataclass
class Action:
    visual_index: int
    source_section: int
    source_name: str
    drawings: list[Drawing]


FIGHTERS = (
    FighterSpec("kinako", "Kinako", "Kinako"),
    FighterSpec("senna", "Senna", "Senna"),
    FighterSpec("m_heita", "Mecha Heita", "MechaHeita"),
    FighterSpec("kunagi", "Kunagi", "Kunagi"),
    FighterSpec("daigo", "Daigo", "Daigo"),
    FighterSpec("rouga", "Rouga", "Rouga"),
    FighterSpec("kamui", "Kamui", "Kamui"),
    FighterSpec("heita", "Heita", "Heita"),
    FighterSpec("agito", "Agito", "Agito"),
)

# Confirmed source-action roles from the character-by-character design pass.
SOURCE_ASSIGNMENTS: dict[str, dict[int, str]] = {
    "m_heita": {
        0: "NEUTRAL",
        1: "WALK_FORWARD",
        2: "WALK_BACKWARD",
        3: "NEUTRAL_JUMP",
        4: "FORWARD_JUMP",
        5: "BACKWARD_JUMP",
        6: "FALL",
        7: "CROUCH_START",
        8: "FULL_CROUCH",
        9: "CROUCH_END",
        10: "CROUCH_HITSTUN",
        11: "CROUCH_HITSTUN_2",
        12: "BOOSTER",
        13: "WIN_TAUNT",
        14: "FULL_CROUCH_HITSTUN",
        15: "FULL_CROUCH_HITSTUN_2",
        16: "SUPER_JUMP_NEUTRAL",
        17: "SUPER_JUMP_FORWARD",
        18: "SUPER_JUMP_BACKWARD",
        19: "FULL_CROUCH_2",
        20: "STANDING_HITSTUN_TO_IDLE",
        21: "STANDING_LIGHT_HITSTUN_TO_IDLE",
        22: "STANDING_MEDIUM_HITSTUN_TO_IDLE",
        23: "STANDING_BIG_HITSTUN_TO_IDLE_2",
        24: "STANDING_BIG_HITSTUN_TO_IDLE_3",
        25: "STANDING_MID_HITSTUN_TO_IDLE",
        26: "STANDING_LIGHT_MID_HITSTUN_TO_IDLE",
        27: "STANDING_LIGHT_MID_HITSTUN_TO_IDLE_2",
        28: "STANDING_MID_HITSTUN_TO_IDLE_2",
        29: "STANDING_MID_HITSTUN_TO_IDLE_3",
        30: "CROUCHING_HEAVY_HITSTUN",
        31: "CROUCHING_LIGHT_HITSTUN",
        32: "CROUCHING_MID_HITSTUN",
        33: "CROUCHING_MID_HITSTUN_2",
        34: "CROUCHING_MID_HITSTUN_3",
        35: "LAUNCHED_KNOCKED_AWAY",
        36: "LAUNCHED_HITSTUN",
        37: "LAUNCHED_FAR",
        38: "SHORT_LAUNCH",
        39: "LIGHT_LAUNCH",
        40: "BLOW_AWAY_HORIZONTAL",
        41: "BLOW_AWAY_VERTICAL_WEAK",
        42: "BLOW_AWAY_VERTICAL_MEDIUM",
        43: "BLOW_AWAY_VERTICAL_STRONG",
        44: "BLOW_AWAY_DIAGONAL_WEAK",
        45: "BLOW_AWAY_DIAGONAL_MEDIUM",
        46: "BLOW_AWAY_DIAGONAL_STRONG",
        47: "BLOW_AWAY_DOWNWARD_WEAK",
        48: "BLOW_AWAY_DOWNWARD_MEDIUM",
        49: "BLOW_AWAY_DOWNWARD_STRONG",
        50: "STUMBLE",
        51: "BLOW_AWAY_DIAGONAL_DOWN",
        52: "WALL_BOUNCE_STRONG",
        53: "WALL_BOUNCE_WEAK",
        54: "HIT_FALL",
        55: "KNOCKDOWN_DOWNED",
        56: "WAKEUP",
        57: "GROUND_BOUNCE_WEAK",
        58: "GROUND_BOUNCE_MEDIUM",
        59: "GROUND_BOUNCE_STRONG",
        60: "STAND_BLOCK_WEAK",
        61: "STAND_BLOCK_MEDIUM",
        62: "STAND_BLOCK_STRONG",
        63: "STAND_BLOCK_SPECIAL_STRONG",
        64: "CROUCH_BLOCK_WEAK",
        65: "CROUCH_BLOCK_MEDIUM",
        66: "CROUCH_BLOCK_STRONG",
        67: "CROUCH_BLOCK_SPECIAL_STRONG",
        68: "AIR_BLOCK_WEAK",
        69: "AIR_BLOCK_MEDIUM",
        70: "AIR_BLOCK_STRONG",
        71: "AIR_BLOCK_SPECIAL_STRONG",
        72: "SPECIAL_REACTION_STAGGER",
        73: "SPECIAL_REACTION_SLIDE_DOWN_HORIZONTAL",
        74: "SPECIAL_REACTION_SLIDE_DOWN_DIAGONAL",
        75: "SPECIAL_REACTION_SLIDE_DOWNED",
        78: "REACTION_DIAGONAL_BOUNCE",
        79: "SPECIAL_REACTION_PULLBACK_WEAK",
        80: "SPECIAL_REACTION_PULLBACK_STRONG",
        81: "SPECIAL_GUARD_PULLBACK_WEAK",
        82: "SPECIAL_GUARD_PULLBACK_STRONG",
        83: "SPECIAL_REACTION_PULLBACK_AIR",
        84: "SPECIAL_GUARD_PULLBACK_AIR",
        85: "ALPHA_COUNTER",
        86: "BACKDASH_HOP_ESCAPE_LEFT",
        87: "ESCAPE_LANDING",
        88: "JET_FORWARD_ESCAPE_RIGHT",
        89: "BENCHED",
        90: "BENCHED_AIR_ATTACK_LANDING",
        91: "FACE_ASSET",
        92: "FACE_ASSET",
        93: "NAMEPLATE_ASSET",
        94: "FACE_ASSET",
        95: "FACE_ASSET",
        96: "BENCHED_VERTICAL_AIR_UKEMI",
        97: "BENCHED_FORWARD_AIR_UKEMI",
        98: "BENCHED_BACKWARD_AIR_UKEMI",
        99: "DOUBLE_JUMP_VERTICAL",
        100: "DOUBLE_JUMP_FORWARD",
        101: "DOUBLE_JUMP_BACKWARD",
        102: "DOUBLE_JUMP_FALL",
        103: "FORWARD_FLIGHT_BODY_LOOP",
        104: "BENCHED_FRICTION_STOP",
        105: "BACK_FLIGHT_BODY_LOOP",
        106: "FLIGHT_LANDING",
        107: "LANDING",
        108: "BENCHED_AIR_DASH",
        109: "BENCHED_AIR_BACKDASH",
        110: "FLIGHT_FALL",
        111: "AIR_INTERPOLATION_FALL",
        112: "STANDING_LIGHT_PUNCH",
        113: "STANDING_MEDIUM_PUNCH_BACK",
        114: "STANDING_HEAVY_KICK",
        115: "CROUCHING_LIGHT_PUNCH",
        116: "CROUCHING_LIGHT_KICK_AND_DOWN_BACK_MEDIUM_KICK",
        117: "CROUCHING_HEAVY_PUNCH",
        118: "JUMPING_LIGHT_KICK",
        119: "JUMPING_MEDIUM_PUNCH_BACK_LP",
        120: "JUMPING_HEAVY_PUNCH",
        121: "BENCHED_PREVIOUS_STANDING_LIGHT_KICK",
        123: "THROW_STARTUP",
        124: "FORWARD_THROW",
        125: "BACK_THROW",
        126: "BACK_THROW_STARTUP",
        127: "BENCHED_CUT_IN_EFFECT",
        128: "JUMPING_HEAVY_KICK",
        129: "QCF_LASER_ACTIVATION",
        130: "THIRTEEN_HIT_HELICOPTER_DP",
        131: "SPECIAL_MOVE_LANDING_RECOVERY",
        132: "AIRBORNE_HELICOPTER_DP",
        133: "MISSILE_ACTIVATION",
        134: "LIGHT_BACK_MISSILE_PROJECTILE",
        135: "HEAVY_BACK_MISSILE_PROJECTILE",
        136: "JUMPING_MEDIUM_KICK_BACK_LK",
        137: "JUMPING_MEDIUM_KICK_LANDING",
        138: "BENCHED",
        139: "BENCHED_SOURCE_REPURPOSED_FOR_GENERATED_CROUCHING_SWEEP",
        140: "STANDING_LIGHT_KICK",
        141: "ROBOT_THEMED_FORWARD_RUN",
        142: "ACTIONABLE_RUN_STOP_FRICTION",
        143: "BENCHED_PREVIOUS_SHINRYUKEN",
        144: "STANDING_HEAVY_PUNCH",
        145: "QCF_LK_HK_SEVENTEEN_HIT_RISING_SPIN",
        146: "FORWARD_HEAVY_KICK",
        76: "BLOW_AWAY_DOWNWARD_NO_BOUNCE",
        77: "BLOW_AWAY_DIAGONAL_DOWN_NO_BOUNCE",
        151: "FLY_UP",
        152: "FLY_UP_JET_EFFECT",
    },
}


def parse_actions(path: Path) -> list[Action]:
    text = path.read_text(encoding="cp932", errors="replace")
    sections = [section.strip() for section in text.split("------") if section.strip()]
    actions: list[Action] = []
    for source_section, section in enumerate(sections):
        lines = [line for line in section.splitlines() if line.strip()]
        if not lines:
            continue
        drawings: list[Drawing] = []
        for line in lines[1:]:
            fields = line.split("\t")
            if not fields or fields[0].strip() != "\uff29" or len(fields) < 3:
                continue
            try:
                hold = max(1, int(fields[1]))
                image_id = int(fields[2])
                offset_x = int(fields[3]) if len(fields) > 3 else 0
                offset_y = int(fields[4]) if len(fields) > 4 else 0
            except ValueError:
                continue
            drawings.append(Drawing(hold, image_id, offset_x, offset_y))
        if drawings:
            actions.append(Action(len(actions), source_section, lines[0].split("\t")[0], drawings))
    return actions


def source_image(source_dir: Path, image_id: int) -> Path | None:
    # PNG takes precedence because IDs such as 1000 have both a palette BMP and
    # a full-color cut-in PNG in the original archive.
    for suffix in (".png", ".bmp"):
        for stem in (f"{image_id:03d}", str(image_id)):
            candidate = source_dir / f"{stem}{suffix}"
            if candidate.exists():
                return candidate
    return None


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in rgba.get_flattened_data():
        if green > 220 and red < 45 and blue < 45:
            pixels.append((red, green, blue, 0))
        else:
            pixels.append((red, green, blue, alpha))
    rgba.putdata(pixels)
    return rgba


def align_character_frame(image: Image.Image) -> Image.Image:
    rgba = remove_green(image)
    bounds = rgba.getchannel("A").getbbox()
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    if bounds is None:
        return canvas
    visible_width = bounds[2] - bounds[0]
    visible_height = bounds[3] - bounds[1]
    if visible_width > CANVAS_SIZE[0] or visible_height > CANVAS_SIZE[1]:
        # This is presentation/effect art rather than a tightly cropped fighter
        # drawing. Preserve it without clipping; raw actions remain reviewable.
        return rgba
    center_x = (bounds[0] + bounds[2]) // 2
    paste_x = CANVAS_SIZE[0] // 2 - center_x
    paste_y = FLOOR_BASELINE_Y - bounds[3]
    canvas.alpha_composite(rgba, (paste_x, paste_y))
    return canvas


def normalize_name(name: str) -> str:
    return name.replace(" ", "").replace("（", "(").replace("）", ")")


def find_action(actions: Iterable[Action], predicate: Callable[[str], bool]) -> Action | None:
    for action in actions:
        if predicate(normalize_name(action.source_name)):
            return action
    return None


def choose_aliases(actions: list[Action]) -> dict[str, Action]:
    fallback = actions[0]

    def find(predicate: Callable[[str], bool], default: Action | None = None) -> Action:
        return find_action(actions, predicate) or default or fallback

    idle = find(lambda n: n.endswith("通常立ち"))
    walk = find(lambda n: "前歩き" in n and "しゃがみ" not in n, idle)
    walk_back = find(lambda n: "後歩き" in n and "しゃがみ" not in n, walk)
    neutral_jump = find(lambda n: "ジャンプ垂直" in n and "ハイ" not in n and "二段" not in n, idle)
    forward_jump = find(lambda n: "ジャンプ前" in n and "ハイ" not in n and "二段" not in n, neutral_jump)
    backward_jump = find(lambda n: "ジャンプ後" in n and "ハイ" not in n and "二段" not in n, neutral_jump)
    fall = find(lambda n: "空中落下" in n and "ヒット" not in n, neutral_jump)
    crouch_start = find(lambda n: n.endswith("しゃがみ中"), idle)
    crouch_hold = find(lambda n: n.endswith("しゃがみ") and "中" not in n and "歩き" not in n, crouch_start)
    crouch_end = find(lambda n: "しゃがみからの立上り" in n, crouch_start)
    run = find(lambda n: "ダッシュ" in n and "空中" not in n and "バック" not in n and "ボツ" not in n, walk)
    back_dash = find(lambda n: "バックステップ" in n and "終了" not in n, walk_back)
    air_dash = find(lambda n: "空中ダッシュ" in n and "バック" not in n and "落下" not in n, forward_jump)

    stand_hit_light = find(lambda n: "[ヒット]" in n and "地上_上段_弱" in n, idle)
    stand_hit_medium = find(lambda n: "[ヒット]" in n and "地上_上段_中" in n, stand_hit_light)
    stand_hit_heavy = find(lambda n: "[ヒット]" in n and "地上_上段_強" in n, stand_hit_medium)
    crouch_hit = find(lambda n: "[ヒット]" in n and "屈_弱" in n, crouch_hold)
    air_hit = find(lambda n: "[ヒット]" in n and "空中_弱" in n, neutral_jump)
    tumble = find(lambda n: "吹っ飛び_斜め_強" in n, air_hit)
    knockdown = find(lambda n: "[やられ]ダウン" in n, tumble)
    get_up = find(lambda n: "[やられ]起き上がり" in n, crouch_end)

    stand_block = find(lambda n: "[ガード]" in n and "立ち_弱" in n, stand_hit_light)
    crouch_block = find(lambda n: "[ガード]" in n and "屈_弱" in n, crouch_hit)
    air_block = find(lambda n: "[ガード]" in n and "空中_弱" in n, air_hit)

    stand_light = find(lambda n: "[攻撃][立]弱" in n, idle)
    stand_medium = find(lambda n: "[攻撃][立]中" in n, stand_light)
    stand_heavy = find(lambda n: "[攻撃][立]強" in n, stand_medium)
    crouch_light = find(lambda n: "[攻撃][屈]弱" in n, stand_light)
    crouch_medium = find(lambda n: "[攻撃][屈]中" in n, crouch_light)
    crouch_heavy = find(lambda n: "[攻撃][屈]強" in n, crouch_medium)
    air_light = find(lambda n: "[攻撃][空]弱" in n, stand_light)
    air_medium = find(lambda n: "[攻撃][空]中" in n, air_light)
    air_heavy = find(lambda n: "[攻撃][空]強" in n, air_medium)
    command_normal = find(lambda n: "[攻撃]レバー" in n, stand_medium)
    throw = find(lambda n: "[攻撃]投げ_始動" in n, stand_heavy)
    special = find(lambda n: "[必殺]" in n, stand_heavy)
    super_move = find(lambda n: "[超必殺]" in n, special)
    fly_up_body = find(lambda n: "[必殺]ブースト" in n, forward_jump)
    fly_up_jet_effect = find(lambda n: "[エフェクト]ブースト" in n, fly_up_body)
    intro = find(lambda n: "開始時" in n, idle)
    win = find(lambda n: n.endswith("勝ち"), idle)

    aliases = {
        "idle": idle,
        "walk": walk,
        "walk_back": walk_back,
        "neutral_jump": neutral_jump,
        "forward_jump_start": forward_jump,
        "forward_jump_loop": forward_jump,
        "backward_jump": backward_jump,
        "fall": fall,
        "crouch_start": crouch_start,
        "crouch_hold": crouch_hold,
        "crouch_end": crouch_end,
        "run": run,
        "forward_dash": run,
        "back_dash": back_dash,
        "air_dash": air_dash,
        "hitstun_light": stand_hit_light,
        "hitstun_medium": stand_hit_medium,
        "hitstun_heavy": stand_hit_heavy,
        "hitstun_heavy_air": stand_hit_heavy,
        "crouch_hit": crouch_hit,
        "air_hitstun": air_hit,
        "tumble": tumble,
        "knockdown": knockdown,
        "ground_bounce": knockdown,
        "get_up": get_up,
        "stand_block": stand_block,
        "stand_block_impact": stand_block,
        "crouch_block": crouch_block,
        "crouch_block_impact": crouch_block,
        "air_block": air_block,
        "air_block_impact": air_block,
        "attack": stand_heavy,
        "light_punch": stand_light,
        "standing_light_kick": stand_medium,
        "heavy_punch": stand_heavy,
        "standing_heavy_kick": stand_heavy,
        "crouching_light_punch": crouch_light,
        "crouching_light_kick": crouch_light,
        "crouching_medium_punch": crouch_medium,
        "crouching_heavy_punch": crouch_heavy,
        "crouching_heavy_kick": crouch_heavy,
        "down_forward_heavy_punch": command_normal,
        "forward_light_kick": command_normal,
        "forward_heavy_punch": command_normal,
        "air_light_punch": air_light,
        "air_light_kick": air_light,
        "air_heavy_punch": air_heavy,
        "air_heavy_kick": air_heavy,
        "air_up_heavy_kick": air_heavy,
        "body_splash": air_heavy,
        "throw": throw,
        "fireball": special,
        "qcf_power_punch": special,
        "qcf_power_punch_rekka": special,
        "super_fireball": super_move,
        "super_one_finisher": super_move,
        "intro": intro,
        "win": win,
        "win_loop": win,
        "fly_up_body": fly_up_body,
        "fly_up_jet_effect": fly_up_jet_effect,
    }
    # Imported combat groups remain available as anim_### entries but are not
    # assigned to gameplay buttons. Designers deliberately choose them later
    # in the move/hitbox editor. Neutral placeholders keep staging scenes safe.
    benched_combat_aliases = {
        "attack", "light_punch", "standing_light_kick", "heavy_punch",
        "standing_heavy_kick", "crouching_light_punch", "crouching_light_kick",
        "crouching_medium_punch", "crouching_heavy_punch", "crouching_heavy_kick",
        "down_forward_heavy_punch", "forward_light_kick", "forward_heavy_punch",
        "air_light_punch", "air_light_kick", "air_heavy_punch", "air_heavy_kick",
        "air_up_heavy_kick", "body_splash", "throw", "fireball",
        "qcf_power_punch", "qcf_power_punch_rekka", "super_fireball",
        "super_one_finisher",
    }
    for alias in benched_combat_aliases:
        aliases[alias] = idle
    return aliases


def is_looping_source(name: str) -> bool:
    normalized = normalize_name(name)
    return any(token in normalized for token in ("通常立ち", "前歩き", "後歩き", "しゃがみ", "ダッシュ"))


def action_category(action: Action) -> str:
    name = action.source_name
    if "[超必殺]" in name:
        return "super"
    if "[必殺]" in name:
        return "special"
    if "[攻撃]" in name:
        return "attack"
    if "[エフェクト]" in name:
        return "effect"
    return "state_or_system"


def resolve_action_textures(action: Action, texture_paths: dict[int, Path], fallback_id: int) -> list[int]:
    """Keep every authored drawing slot, even if its source file is absent.

    Legacy scripts occasionally reference a drawing omitted from the character
    archive. The closest available drawing in that same action is the least
    destructive visual placeholder and preserves the original 60 Hz duration.
    The catalog still records the missing source ID for later replacement.
    """
    available_indices = [
        index for index, drawing in enumerate(action.drawings)
        if drawing.image_id in texture_paths
    ]
    resolved: list[int] = []
    for index, drawing in enumerate(action.drawings):
        if drawing.image_id in texture_paths:
            resolved.append(drawing.image_id)
            continue
        if not available_indices:
            resolved.append(fallback_id)
            continue
        nearest = min(available_indices, key=lambda candidate: (abs(candidate - index), candidate > index))
        resolved.append(action.drawings[nearest].image_id)
    return resolved


def write_catalog(path: Path, actions: list[Action], missing_by_action: dict[int, list[int]],
                  resolved_by_action: dict[int, list[int]], spec: FighterSpec) -> None:
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow(("animation", "source_section", "source_action", "category", "assignment",
                         "drawing_count", "timeline_ticks", "source_frames", "resolved_frames",
                         "hold_ticks", "offset_x", "offset_y", "missing_frames"))
        for action in actions:
            category = action_category(action)
            assignment = SOURCE_ASSIGNMENTS.get(spec.archive_id, {}).get(action.visual_index)
            if assignment is None:
                assignment = "BENCHED" if category in {"attack", "special", "super"} else "REFERENCE"
            writer.writerow((
                f"anim_{action.visual_index:03d}",
                action.source_section,
                action.source_name,
                category,
                assignment,
                len(action.drawings),
                sum(item.hold_ticks for item in action.drawings),
                " ".join(str(item.image_id) for item in action.drawings),
                " ".join(str(item) for item in resolved_by_action[action.visual_index]),
                " ".join(str(item.hold_ticks) for item in action.drawings),
                " ".join(str(item.offset_x) for item in action.drawings),
                " ".join(str(item.offset_y) for item in action.drawings),
                " ".join(str(item) for item in missing_by_action.get(action.visual_index, ())),
            ))


def write_sprite_frames(path: Path, actions: list[Action], aliases: dict[str, Action],
                        texture_paths: dict[int, Path], resolved_by_action: dict[int, list[int]],
                        spec: FighterSpec,
                        composite_animations: dict[str, list[Path]] | None = None) -> None:
    composite_animations = composite_animations or {}
    composite_paths = list(dict.fromkeys(
        composite
        for composites in composite_animations.values()
        for composite in composites
    ))
    ordered_ids = sorted(texture_paths)
    ext_ids = {image_id: index + 1 for index, image_id in enumerate(ordered_ids)}
    composite_ext_ids = {
        composite: len(ext_ids) + index + 1
        for index, composite in enumerate(composite_paths)
    }
    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(ordered_ids) + len(composite_paths) + 1} format=3]', ""]
    for image_id in ordered_ids:
        relative = texture_paths[image_id].relative_to(ROOT).as_posix()
        lines.append(f'[ext_resource type="Texture2D" path="res://{relative}" id="{ext_ids[image_id]}"]')
    for composite in composite_paths:
        relative = composite.relative_to(ROOT).as_posix()
        lines.append(f'[ext_resource type="Texture2D" path="res://{relative}" id="{composite_ext_ids[composite]}"]')

    animations: list[tuple[str, Action, bool, list[int]]] = []
    for action in actions:
        animations.append((f"anim_{action.visual_index:03d}", action,
                           is_looping_source(action.source_name), list(range(len(action.drawings)))))
    for alias, action in aliases.items():
        drawing_indices = list(range(len(action.drawings)))
        if spec.archive_id == "m_heita" and alias == "idle":
            # The source idle begins with an arm-cross flourish, then settles
            # into the five-drawing neutral cycle. Keep the flourish available
            # separately until its delayed runtime trigger is designed.
            drawing_indices = list(range(7, 12))
        elif spec.archive_id == "m_heita" and alias in {
            "full_crouch_hitstun", "full_crouch_hitstun_2"
        }:
            # Source actions 14 and 15 open on an unrelated standing pose.
            # Preserve it in anim_###, but gameplay begins on the crouch reaction.
            drawing_indices = list(range(1, len(action.drawings)))
        elif spec.archive_id == "m_heita" and alias in {"super_jump_neutral", "super_jump_forward"}:
            # Preserve takeoff through the apex; gameplay hands off to the
            # ordinary fall state as soon as vertical velocity turns downward.
            drawing_indices = list(range(0, 6))
        elif spec.archive_id == "m_heita" and alias == "super_jump_backward":
            drawing_indices = list(range(0, 7))
        elif spec.archive_id == "m_heita" and alias == "knocked_away":
            # The first four drawings are the straight knock-away reaction;
            # drawing four begins the airborne tumble used by the launch state.
            drawing_indices = list(range(0, 4))
        animations.append((alias, action,
                           alias in {"idle", "walk", "walk_back", "run", "crouch_hold", "win_loop",
                                     "fly_up_jet_effect", "booster_jet_fire"},
                           drawing_indices))
    if spec.archive_id == "m_heita":
        idle_action = aliases["idle"]
        animations.append(("idle_flourish", idle_action, False, list(range(0, 7))))
        fly_up_action = aliases["fly_up_body"]
        animations.append(("fly_up_start", fly_up_action, False, list(range(0, 3))))
        animations.append(("fly_up_end", fly_up_action, False, list(range(11, 17))))
        animations.append(("booster_start", fly_up_action, False, list(range(0, 3))))
        animations.append(("booster_recovery", fly_up_action, False, list(range(11, 17))))

    custom_animations: list[tuple[str, bool, list[tuple[float, int]]]] = [
        (name, True, [(2.0, composite_ext_ids[composite]) for composite in composites])
        for name, composites in composite_animations.items()
    ]

    lines.extend(("", "[resource]", "animations = ["))
    total_animations = len(animations) + len(custom_animations)
    for animation_index, (name, action, loop, drawing_indices) in enumerate(animations):
        frames = []
        resolved_ids = resolved_by_action[action.visual_index]
        for drawing_index in drawing_indices:
            drawing = action.drawings[drawing_index]
            resolved_id = resolved_ids[drawing_index]
            frames.append('{"duration": %.1f, "texture": ExtResource("%d")}' %
                          (float(drawing.hold_ticks), ext_ids[resolved_id]))
        comma = "," if animation_index < total_animations - 1 else ""
        lines.append('{"frames": [%s], "loop": %s, "name": &"%s", "speed": %.1f}%s' %
                     (", ".join(frames), str(loop).lower(), name, TIMELINE_FPS, comma))
    for custom_index, (name, loop, custom_frames) in enumerate(custom_animations):
        frames = [
            '{"duration": %.1f, "texture": ExtResource("%d")}' % (duration, ext_id)
            for duration, ext_id in custom_frames
        ]
        animation_index = len(animations) + custom_index
        comma = "," if animation_index < total_animations - 1 else ""
        lines.append('{"frames": [%s], "loop": %s, "name": &"%s", "speed": %.1f}%s' %
                     (", ".join(frames), str(loop).lower(), name, TIMELINE_FPS, comma))
    lines.append("]")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_mecha_composite_animations(spec: FighterSpec, asset_dir: Path,
                                     aliases: dict[str, Action], texture_paths: dict[int, Path],
                                     resolved_by_action: dict[int, list[int]]) -> dict[str, list[Path]]:
    """Layer the authored boost flame behind booster and directional escape bodies."""
    if spec.archive_id != "m_heita":
        return {}
    fly_up_body_action = aliases["fly_up_body"]
    forward_flight_body_action = aliases["forward_flight_body"]
    back_flight_body_action = aliases["back_flight_body"]
    jet_action = aliases["fly_up_jet_effect"]
    body_indices = list(range(3, 11))
    if len(jet_action.drawings) != len(body_indices):
        raise ValueError("Mecha Heita boost body and jet effect no longer have matching cycles")

    output_dir = asset_dir / "Frames" / "FlyUp"
    output_dir.mkdir(parents=True, exist_ok=True)
    fly_up_body_ids = resolved_by_action[fly_up_body_action.visual_index]
    forward_flight_body_ids = resolved_by_action[forward_flight_body_action.visual_index]
    back_flight_body_ids = resolved_by_action[back_flight_body_action.visual_index]
    jet_ids = resolved_by_action[jet_action.visual_index]
    fly_up_composites: list[Path] = []
    for cycle_index, (body_index, jet_id) in enumerate(zip(body_indices, jet_ids)):
        with Image.open(texture_paths[jet_id]) as jet_image, Image.open(texture_paths[fly_up_body_ids[body_index]]) as body_image:
            composite = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
            composite.alpha_composite(jet_image.convert("RGBA"), MECHA_BOOSTER_JET_OFFSET)
            composite.alpha_composite(body_image.convert("RGBA"))
            destination = output_dir / f"fly_up_{cycle_index:02d}.png"
            composite.save(destination)
            fly_up_composites.append(destination)

    escape_output_dir = asset_dir / "Frames" / "JetEscapes"
    escape_output_dir.mkdir(parents=True, exist_ok=True)
    escape_composites: dict[str, list[Path]] = {}
    escape_specs = {
        # Escape right/left share the exact forward/back reference placement.
        "jet_escape_right": (aliases["escape_right"], -36, (43, 129)),
        "jet_escape_left": (aliases["escape_left"], 154, (133, 126)),
    }
    for animation_name, (escape_body, jet_angle, jet_position) in escape_specs.items():
        body_id = resolved_by_action[escape_body.visual_index][0]
        animation_frames: list[Path] = []
        for cycle_index, jet_id in enumerate(jet_ids):
            with Image.open(texture_paths[jet_id]) as jet_image, Image.open(texture_paths[body_id]) as body_image:
                jet_layer = jet_image.convert("RGBA").crop(MECHA_DIRECTIONAL_JET_CROP)
                jet_layer = jet_layer.rotate(jet_angle, resample=Image.Resampling.NEAREST, expand=True)
                composite = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
                composite.alpha_composite(jet_layer, jet_position)
                composite.alpha_composite(body_image.convert("RGBA"))
                destination = escape_output_dir / f"{animation_name}_{cycle_index:02d}.png"
                composite.save(destination)
                animation_frames.append(destination)
        escape_composites[animation_name] = animation_frames

    directional_output_dir = asset_dir / "Frames" / "DirectionalFlight"
    directional_output_dir.mkdir(parents=True, exist_ok=True)
    directional_composites: dict[str, list[Path]] = {}
    canvas_center = (CANVAS_SIZE[0] // 2, CANVAS_SIZE[1] // 2)
    for animation_name, (body_id, jet_angle, jet_position, body_angle) in MECHA_DIRECTIONAL_FLIGHT_SPECS.items():
        animation_frames: list[Path] = []
        forward_body_flight = animation_name in {"booster_forward", "booster_down_forward"}
        back_body_flight = animation_name == "booster_back"
        animated_flight = forward_body_flight or back_body_flight
        body_cycle = (forward_flight_body_ids if forward_body_flight else
                      back_flight_body_ids if back_body_flight else [body_id])
        cycle_length = (lcm(len(body_cycle), len(jet_ids))
                        if animated_flight else len(jet_ids))
        for cycle_index in range(cycle_length):
            cycle_body_id = body_cycle[cycle_index % len(body_cycle)]
            jet_id = jet_ids[cycle_index % len(jet_ids)]
            jet_adjustment = (MECHA_FLIGHT_BODY_JET_ADJUSTMENTS.get(cycle_body_id, (0, 0))
                              if forward_body_flight else (0, 0))
            if body_angle and jet_adjustment != (0, 0):
                jet_adjustment = rotate_screen_offset(jet_adjustment, body_angle)
            resolved_jet_position = (jet_position[0] + jet_adjustment[0],
                                     jet_position[1] + jet_adjustment[1])
            with Image.open(texture_paths[cycle_body_id]) as body_source, \
                    Image.open(texture_paths[jet_id]) as jet_source:
                body_layer = body_source.convert("RGBA")
                if body_angle:
                    body_layer = body_layer.rotate(body_angle, resample=Image.Resampling.NEAREST,
                                                   center=canvas_center, expand=False)
                rotated_jet = jet_source.convert("RGBA").crop(MECHA_DIRECTIONAL_JET_CROP)
                rotated_jet = rotated_jet.rotate(jet_angle, resample=Image.Resampling.NEAREST,
                                                  expand=True)
                composite = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
                composite.alpha_composite(rotated_jet, resolved_jet_position)
                composite.alpha_composite(body_layer)
                destination = directional_output_dir / f"{animation_name}_{cycle_index:02d}.png"
                composite.save(destination)
                animation_frames.append(destination)
        directional_composites[animation_name] = animation_frames

    return {
        "fly_up": fly_up_composites,
        "booster_loop": fly_up_composites,
        **escape_composites,
        **directional_composites,
    }


def write_character_data(spec: FighterSpec) -> Path:
    data_dir = DATA_ROOT / spec.scene_stem
    data_dir.mkdir(parents=True, exist_ok=True)
    gauges = data_dir / f"{spec.slug}_gauges.tres"
    normals = data_dir / f"{spec.slug}_normal_moves.tres"
    specials = data_dir / f"{spec.slug}_special_moves.tres"
    states = data_dir / f"{spec.slug}_state_boxes.tres"
    definition = data_dir / f"{spec.slug}_definition.tres"
    booster = data_dir / f"{spec.slug}_booster.tres"
    jet_escape_right = data_dir / f"{spec.slug}_jet_escape_right.tres"
    jet_escape_left = data_dir / f"{spec.slug}_jet_escape_left.tres"

    def write_once(path: Path, content: str) -> None:
        # Once designers begin assigning moves and boxes, re-importing art must
        # never erase their character-specific work.
        if not path.exists():
            path.write_text(content, encoding="utf-8")

    gauge_content = """[gd_resource type="Resource" script_class="FighterGaugeData" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Core/FighterGaugeData.cs" id="1"]

[resource]
script = ExtResource("1")
MaxLife = 1000
StartingLife = 1000
SpecialMeterName = "GAS"
MaxSpecialMeter = 100
StartingSpecialMeter = 100
SpecialMeterRecoveryPerSecond = 15.0
SpecialMeterRecoveryDelayFrames = 30
LifeColor = Color(0.28, 0.88, 0.32, 1)
SpecialMeterColor = Color(1, 0.58, 0.12, 1)
""" if spec.archive_id == "m_heita" else """[gd_resource type="Resource" script_class="FighterGaugeData" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Core/FighterGaugeData.cs" id="1"]

[resource]
script = ExtResource("1")
MaxLife = 1000
StartingLife = 1000
SpecialMeterName = "SUPER"
MaxSpecialMeter = 300
StartingSpecialMeter = 0
LifeColor = Color(0.28, 0.88, 0.32, 1)
SpecialMeterColor = Color(0.15, 0.58, 1, 1)
"""
    write_once(gauges, gauge_content)
    normal_move_content = """[gd_resource type="Resource" script_class="NormalMoveSet" load_steps=49 format=3]

[ext_resource type="Script" path="res://Scripts/Core/FighterBoxFrame.cs" id="1_box"]
[ext_resource type="Script" path="res://Scripts/Core/NormalMoveData.cs" id="2_move"]
[ext_resource type="Script" path="res://Scripts/Core/NormalMoveSet.cs" id="3_set"]

[sub_resource type="Resource" id="MechaAirHeavyHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-48, -104, 96, 104)
Tag = "air-heavy-kick-body"

[sub_resource type="Resource" id="MechaAirHeavyHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 3
EndFrame = 20
LocalRect = Rect2(8, -102, 132, 64)
Tag = "air-heavy-kick"

[sub_resource type="Resource" id="MechaAirHeavyKick"]
script = ExtResource("2_move")
AttackName = "HEAVY KICK"
AnimationName = "air_heavy_kick"
Stance = 3
StartupFrames = 3
ActiveFrames = 18
RecoveryFrames = 15
SuppressFallbackHitbox = true
AnimationSourceTimeline = PackedInt32Array(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 1027, 1028, 1029, 1030, 1031, 1032)
AnimationDrawingOffsets = PackedVector2Array(0, 0, 34, -29.5, 31, -29.5, 31, -29.5, 31, -29.5, 31, -29.5, 31, -29.5, 21, -28.5, 12, -12.5, 8, -10.5, 6, -2, 0, 0, 4.5, 1.5)
CanChainToSpecial = true
Damage = 100
BoxTimeline = [SubResource("MechaAirHeavyHurtbox"), SubResource("MechaAirHeavyHitbox")]

[sub_resource type="Resource" id="MechaStandingLightPunchHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-38, -112, 76, 112)
Tag = "standing-light-punch-body"

[sub_resource type="Resource" id="MechaStandingLightPunchHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 3
EndFrame = 6
LocalRect = Rect2(12, -92, 88, 42)
Tag = "standing-light-punch"

[sub_resource type="Resource" id="MechaStandingLightPunch"]
script = ExtResource("2_move")
AttackName = "LIGHT PUNCH"
AnimationName = "light_punch"
Stance = 1
StartupFrames = 3
ActiveFrames = 4
RecoveryFrames = 4
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, 1, 0, 14, 0, 11, 0, 0, 0, 0, 0)
CanChainToLight = true
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 35
BoxTimeline = [SubResource("MechaStandingLightPunchHurtbox"), SubResource("MechaStandingLightPunchHitbox")]

[sub_resource type="Resource" id="MechaStandingMediumPunchHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-42, -112, 84, 112)
Tag = "standing-medium-punch-body"

[sub_resource type="Resource" id="MechaStandingMediumPunchHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 9
EndFrame = 14
LocalRect = Rect2(18, -94, 108, 48)
Tag = "standing-medium-punch-back"
AttackLevel = 2

[sub_resource type="Resource" id="MechaStandingMediumPunch"]
script = ExtResource("2_move")
AttackName = "MEDIUM PUNCH BACK"
AnimationName = "medium_punch_back"
Stance = 1
StartupFrames = 9
ActiveFrames = 6
RecoveryFrames = 12
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, -5, -5, 4, -5, 21, -5, 19, -5, 13, -5, 3.5, -5, 3, -4, 0, 0)
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 60
BoxTimeline = [SubResource("MechaStandingMediumPunchHurtbox"), SubResource("MechaStandingMediumPunchHitbox")]

[sub_resource type="Resource" id="MechaStandingHeavyKickHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-44, -132, 88, 132)
Tag = "standing-heavy-kick-body"

[sub_resource type="Resource" id="MechaStandingHeavyKickHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 8
EndFrame = 16
LocalRect = Rect2(18, -132, 126, 62)
Tag = "standing-heavy-kick"
AttackLevel = 2

[sub_resource type="Resource" id="MechaStandingHeavyKick"]
script = ExtResource("2_move")
AttackName = "HEAVY KICK"
AnimationName = "heavy_kick"
Stance = 1
StartupFrames = 8
ActiveFrames = 9
RecoveryFrames = 13
SuppressFallbackHitbox = true
AnimationSourceTimeline = PackedInt32Array(0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10)
CanChainToSpecial = true
Damage = 100
BoxTimeline = [SubResource("MechaStandingHeavyKickHurtbox"), SubResource("MechaStandingHeavyKickHitbox")]

[sub_resource type="Resource" id="MechaCrouchingLightPunchHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-42, -78, 84, 78)
Tag = "crouching-light-punch-body"

[sub_resource type="Resource" id="MechaCrouchingLightPunchHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 4
EndFrame = 10
LocalRect = Rect2(16, -68, 98, 36)
Tag = "crouching-light-punch"
AttackLevel = 3

[sub_resource type="Resource" id="MechaCrouchingLightPunch"]
script = ExtResource("2_move")
AttackName = "LIGHT PUNCH"
AnimationName = "crouching_light_punch"
Stance = 2
StartupFrames = 4
ActiveFrames = 7
RecoveryFrames = 2
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, 2, 0, 20, 1, 18, 1, 5, 0, -1, 0)
CanChainToLight = true
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 30
BoxTimeline = [SubResource("MechaCrouchingLightPunchHurtbox"), SubResource("MechaCrouchingLightPunchHitbox")]

[sub_resource type="Resource" id="MechaCrouchingLightKickHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-44, -78, 88, 78)
Tag = "crouching-light-kick-body"

[sub_resource type="Resource" id="MechaCrouchingLightKickHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 3
EndFrame = 6
LocalRect = Rect2(18, -58, 112, 36)
Tag = "crouching-light-kick-fast"
AttackLevel = 3

[sub_resource type="Resource" id="MechaCrouchingLightKick"]
script = ExtResource("2_move")
AttackName = "LIGHT KICK"
AnimationName = "crouching_light_kick"
Stance = 2
StartupFrames = 3
ActiveFrames = 4
RecoveryFrames = 5
SuppressFallbackHitbox = true
AnimationSourceTimeline = PackedInt32Array(0, 1, 2, 3, 3, 3, 3, 4, 5, 5, 6, 6)
CanChainToLight = true
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 30
BoxTimeline = [SubResource("MechaCrouchingLightKickHurtbox"), SubResource("MechaCrouchingLightKickHitbox")]

[sub_resource type="Resource" id="MechaCrouchingMediumKickHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-44, -78, 88, 78)
Tag = "crouching-medium-kick-body"

[sub_resource type="Resource" id="MechaCrouchingMediumKickHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 4
EndFrame = 11
LocalRect = Rect2(18, -58, 112, 36)
Tag = "crouching-medium-kick-down-back"
AttackLevel = 3

[sub_resource type="Resource" id="MechaCrouchingMediumKick"]
script = ExtResource("2_move")
AttackName = "CROUCHING MEDIUM KICK"
AnimationName = "crouching_medium_kick"
Stance = 2
StartupFrames = 4
ActiveFrames = 8
RecoveryFrames = 12
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, 0, 0, 4, 3, 3, 1.5, 2, 0, -1, 0, -3, 0)
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 60
BoxTimeline = [SubResource("MechaCrouchingMediumKickHurtbox"), SubResource("MechaCrouchingMediumKickHitbox")]

[sub_resource type="Resource" id="MechaCrouchingHeavyPunchHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-46, -102, 92, 102)
Tag = "crouching-heavy-punch-body"

[sub_resource type="Resource" id="MechaCrouchingHeavyPunchHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 10
EndFrame = 24
LocalRect = Rect2(8, -158, 72, 132)
Tag = "crouching-heavy-punch-rising"
AttackLevel = 2

[sub_resource type="Resource" id="MechaCrouchingHeavyPunch"]
script = ExtResource("2_move")
AttackName = "HEAVY PUNCH CROUCHING"
AnimationName = "crouching_heavy_punch"
Stance = 2
StartupFrames = 10
ActiveFrames = 15
RecoveryFrames = 9
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, 18.5, 0, 21, 0, 27, 0, 24, 0, 22, 0, 22, 0, 15.5, 0, 7, 1, 4, 0)
CanChainToSpecial = true
Damage = 100
Launches = true
LaunchSpeed = 1265.0
LaunchPushback = 180.0
LaunchHitstunFrames = 30
JumpCancelWindowFrames = 30
ChaseJumpSpeed = 1265.0
ChaseForwardSpeed = 360.0
BoxTimeline = [SubResource("MechaCrouchingHeavyPunchHurtbox"), SubResource("MechaCrouchingHeavyPunchHitbox")]

[sub_resource type="Resource" id="MechaJumpingLightKickHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-46, -106, 92, 102)
Tag = "jumping-light-kick-body"

[sub_resource type="Resource" id="MechaJumpingLightKickHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 3
EndFrame = 8
LocalRect = Rect2(6, -108, 128, 62)
Tag = "jumping-light-kick"
AttackLevel = 2

[sub_resource type="Resource" id="MechaJumpingLightKick"]
script = ExtResource("2_move")
AttackName = "LIGHT KICK"
AnimationName = "air_light_kick"
Stance = 3
StartupFrames = 3
ActiveFrames = 6
RecoveryFrames = 9
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, 3.5, 15, 2.5, 15.5, -4, 14, -6.5, 16, -7, 3.5)
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 35
BoxTimeline = [SubResource("MechaJumpingLightKickHurtbox"), SubResource("MechaJumpingLightKickHitbox")]

[sub_resource type="Resource" id="MechaJumpingMediumPunchHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-46, -116, 92, 112)
Tag = "jumping-medium-punch-body"

[sub_resource type="Resource" id="MechaJumpingMediumPunchHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 8
EndFrame = 20
LocalRect = Rect2(2, -164, 92, 126)
Tag = "jumping-medium-punch-upward"
AttackLevel = 2

[sub_resource type="Resource" id="MechaJumpingMediumPunch"]
script = ExtResource("2_move")
AttackName = "MEDIUM PUNCH AIR BACK"
AnimationName = "air_medium_punch_back"
Stance = 3
StartupFrames = 8
ActiveFrames = 13
RecoveryFrames = 12
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, 1, 2, -4.5, 7, -1.5, 17.5, -10, 23, -11, 24, -15, 23, -7.5, 30, -3.5, 13, -3.5, -7)
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 60
Launches = true
LaunchSpeed = 1150.0
LaunchPushback = 70.0
LaunchHitstunFrames = 32
JumpCancelWindowFrames = 0
BoxTimeline = [SubResource("MechaJumpingMediumPunchHurtbox"), SubResource("MechaJumpingMediumPunchHitbox")]

[sub_resource type="Resource" id="MechaJumpingHeavyPunchHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-48, -114, 96, 110)
Tag = "jumping-heavy-punch-body"

[sub_resource type="Resource" id="MechaJumpingHeavyPunchHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 9
EndFrame = 20
LocalRect = Rect2(-18, -88, 104, 112)
Tag = "jumping-heavy-punch-downward"
AttackLevel = 2

[sub_resource type="Resource" id="MechaJumpingHeavyPunch"]
script = ExtResource("2_move")
AttackName = "HEAVY PUNCH AIR"
AnimationName = "air_heavy_punch"
Stance = 3
StartupFrames = 9
ActiveFrames = 12
RecoveryFrames = 12
SuppressFallbackHitbox = true
AnimationDrawingOffsets = PackedVector2Array(0, 0, -3, 41.5, -4, 42.5, 2, 21.5, 13.5, 16.5, 11.5, 16.5, 0.5, 17.5, -1.5, 25, 0, 14.5, 0.5, 6.5)
CanChainToSpecial = true
Damage = 100
BoxTimeline = [SubResource("MechaJumpingHeavyPunchHurtbox"), SubResource("MechaJumpingHeavyPunchHitbox")]

[sub_resource type="Resource" id="MechaStandingLightKickHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-42, -124, 84, 124)
Tag = "standing-light-kick-body"

[sub_resource type="Resource" id="MechaStandingLightKickHitbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 6
EndFrame = 11
LocalRect = Rect2(16, -152, 130, 72)
Tag = "standing-light-kick-fast"
AttackLevel = 2

[sub_resource type="Resource" id="MechaStandingLightKick"]
script = ExtResource("2_move")
AttackName = "LIGHT KICK"
AnimationName = "standing_light_kick"
Stance = 1
StartupFrames = 6
ActiveFrames = 6
RecoveryFrames = 9
SuppressFallbackHitbox = true
AnimationSourceTimeline = PackedInt32Array(0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8, 9, 9, 10, 10, 11, 11)
CanChainToLight = true
CanChainToHeavy = true
CanChainToSpecial = true
Damage = 35
BoxTimeline = [SubResource("MechaStandingLightKickHurtbox"), SubResource("MechaStandingLightKickHitbox")]

[sub_resource type="Resource" id="MechaThrowStartupHurtbox"]
script = ExtResource("1_box")
LocalRect = Rect2(-40, -116, 80, 116)
Tag = "throw-startup-body"

[sub_resource type="Resource" id="MechaThrowStartupGrabbox"]
script = ExtResource("1_box")
Kind = 1
StartFrame = 5
EndFrame = 8
LocalRect = Rect2(10, -96, 76, 84)
Tag = "throw-startup-grab"
Attributes = 4
AttackLevel = 0

[sub_resource type="Resource" id="MechaForwardThrowAnchorHold"]
script = ExtResource("1_box")
Kind = 7
StartFrame = 5
EndFrame = 34
LocalRect = Rect2(28, -118, 48, 72)
Tag = "forward-throw-victim-hold"

[sub_resource type="Resource" id="MechaForwardThrowAnchorTurn"]
script = ExtResource("1_box")
Kind = 7
StartFrame = 35
EndFrame = 55
LocalRect = Rect2(-20, -152, 54, 74)
Tag = "forward-throw-victim-turn"

[sub_resource type="Resource" id="MechaForwardThrowAnchorRelease"]
script = ExtResource("1_box")
Kind = 7
StartFrame = 56
EndFrame = 70
LocalRect = Rect2(38, -82, 58, 72)
Tag = "forward-throw-victim-release"

[sub_resource type="Resource" id="MechaBackThrowAnchorHold"]
script = ExtResource("1_box")
Kind = 7
StartFrame = 5
EndFrame = 34
LocalRect = Rect2(-76, -118, 48, 72)
Tag = "back-throw-victim-hold"

[sub_resource type="Resource" id="MechaBackThrowAnchorTurn"]
script = ExtResource("1_box")
Kind = 7
StartFrame = 35
EndFrame = 55
LocalRect = Rect2(-34, -152, 54, 74)
Tag = "back-throw-victim-turn"

[sub_resource type="Resource" id="MechaBackThrowAnchorRelease"]
script = ExtResource("1_box")
Kind = 7
StartFrame = 56
EndFrame = 70
LocalRect = Rect2(-96, -82, 58, 72)
Tag = "back-throw-victim-release"

[sub_resource type="Resource" id="MechaThrowStartup"]
script = ExtResource("2_move")
AttackName = "THROW"
AnimationName = "throw"
AnimationTailName = "forward_throw"
AnimationTailStartFrame = 5
ConnectedThrowRecoveryFrames = 78
Stance = 1
StartupFrames = 5
ActiveFrames = 4
RecoveryFrames = 22
SuppressFallbackHitbox = true
AnimationSourceTimeline = PackedInt32Array(0, 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5)
KnockdownType = 3
KnocksDown = true
KnockdownFrames = 42
BoxTimeline = [SubResource("MechaThrowStartupHurtbox"), SubResource("MechaThrowStartupGrabbox"), SubResource("MechaForwardThrowAnchorHold"), SubResource("MechaForwardThrowAnchorTurn"), SubResource("MechaForwardThrowAnchorRelease")]

[sub_resource type="Resource" id="MechaBackThrow"]
script = ExtResource("2_move")
AttackName = "BACK THROW"
AnimationName = "back_throw_startup"
AnimationTailName = "back_throw"
AnimationTailStartFrame = 5
ConnectedThrowRecoveryFrames = 78
Stance = 1
StartupFrames = 5
ActiveFrames = 4
RecoveryFrames = 22
SuppressFallbackHitbox = true
AnimationSourceTimeline = PackedInt32Array(0, 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5)
KnockdownType = 3
KnocksDown = true
KnockdownFrames = 42
BoxTimeline = [SubResource("MechaThrowStartupHurtbox"), SubResource("MechaThrowStartupGrabbox"), SubResource("MechaBackThrowAnchorHold"), SubResource("MechaBackThrowAnchorTurn"), SubResource("MechaBackThrowAnchorRelease")]

[resource]
script = ExtResource("3_set")
Rules = [SubResource("MechaAirHeavyKick"), SubResource("MechaStandingLightPunch"), SubResource("MechaStandingMediumPunch"), SubResource("MechaStandingHeavyKick"), SubResource("MechaCrouchingLightPunch"), SubResource("MechaCrouchingLightKick"), SubResource("MechaCrouchingMediumKick"), SubResource("MechaCrouchingHeavyPunch"), SubResource("MechaJumpingLightKick"), SubResource("MechaJumpingMediumPunch"), SubResource("MechaJumpingHeavyPunch"), SubResource("MechaStandingLightKick"), SubResource("MechaThrowStartup"), SubResource("MechaBackThrow")]
""" if spec.archive_id == "m_heita" else """[gd_resource type="Resource" script_class="NormalMoveSet" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Core/NormalMoveSet.cs" id="1"]

[resource]
script = ExtResource("1")
Rules = []
"""
    write_once(normals, normal_move_content)
    write_once(specials, """[gd_resource type="Resource" script_class="SpecialMoveSet" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Core/SpecialMoveSet.cs" id="1"]

[resource]
script = ExtResource("1")
Moves = []
""")
    if spec.archive_id == "m_heita":
        write_once(booster, """[gd_resource type="Resource" script_class="FlightAbility" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Movement/FlightAbility.cs" id="1_flight"]

[resource]
script = ExtResource("1_flight")
Id = "mecha_heita_booster"
Priority = 35
FlightSpeed = 420.0
Acceleration = 1900.0
CanStartGrounded = true
MaxFrames = 0
UseSpecial1Input = true
GasCostPerFrame = 0.5
UseDirectionalAnimations = true
DirectVelocityControl = true
BackwardFlightSpeedMultiplier = 0.3
UseDirectionalBoosts = true
BoostSpeed = 900.0
BoostFrames = 12
MaxAirBoosts = 3
BoostGasCost = 12.0
BoostCancelExtraGasCost = 4.0
BoostAttackDelayFrames = 7
BackwardBoostSpeedMultiplier = 0.5
BackwardBoostAirUseCost = 2
CommitAfterBackwardAirBoost = true
AllowNormalHitFlightCancel = true
AllowWhiffRecoveryFlightCancelNormals = true
AllowWhiffRecoveryFlightCancelSpecials = true
FlightCancelGasCost = 10.0
FlightCancelMinimumFrames = 15
RequireNeutralBeforeCancelledFlightMovement = true
RequireDirectionBeforeCancelledFlightAttack = true
LockAirNormalsDuringPostFlightFall = true
""")
        write_once(jet_escape_right, """[gd_resource type="Resource" script_class="JetEscapeAbility" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Movement/JetEscapeAbility.cs" id="1_escape"]

[resource]
script = ExtResource("1_escape")
Id = "mecha_heita_jet_escape_right"
Priority = 35
SuspendsInputBufferWhileActive = true
Direction = 0
ActiveFrames = 16
Speed = 650.0
VerticalSpeed = 280.0
GasCost = 20.0
InvulnerabilityFrames = 4
AnimationName = "jet_escape_right"
StateName = "STATE ESCAPE RIGHT / JET FORWARD DASH"
""")
        write_once(jet_escape_left, """[gd_resource type="Resource" script_class="JetEscapeAbility" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Movement/JetEscapeAbility.cs" id="1_escape"]

[resource]
script = ExtResource("1_escape")
Id = "mecha_heita_jet_escape_left"
Priority = 35
SuspendsInputBufferWhileActive = true
Direction = 1
ActiveFrames = 16
Speed = 650.0
VerticalSpeed = 280.0
GasCost = 20.0
InvulnerabilityFrames = 4
AnimationName = "jet_escape_left"
StateName = "STATE ESCAPE LEFT / JET BACK DASH"
""")

    write_once(states, """[gd_resource type="Resource" script_class="NormalMoveSet" load_steps=9 format=3]

[ext_resource type="Script" path="res://Scripts/Core/NormalMoveData.cs" id="1_move"]
[ext_resource type="Script" path="res://Scripts/Core/NormalMoveSet.cs" id="2_set"]

[sub_resource type="Resource" id="IdleState"]
script = ExtResource("1_move")
AttackName = "STATE IDLE"
AnimationName = "idle"
StartupFrames = 0
ActiveFrames = 60
RecoveryFrames = 0

[sub_resource type="Resource" id="CrouchState"]
script = ExtResource("1_move")
AttackName = "STATE CROUCH"
AnimationName = "crouch_hold"
Stance = 2
StartupFrames = 0
ActiveFrames = 60
RecoveryFrames = 0

[sub_resource type="Resource" id="WalkForwardState"]
script = ExtResource("1_move")
AttackName = "STATE WALK FORWARD"
AnimationName = "walk"
StartupFrames = 0
ActiveFrames = 60
RecoveryFrames = 0

[sub_resource type="Resource" id="WalkBackState"]
script = ExtResource("1_move")
AttackName = "STATE WALK BACK"
AnimationName = "walk_back"
StartupFrames = 0
ActiveFrames = 60
RecoveryFrames = 0

[sub_resource type="Resource" id="JumpRiseState"]
script = ExtResource("1_move")
AttackName = "STATE JUMP RISE"
AnimationName = "neutral_jump"
Stance = 3
StartupFrames = 0
ActiveFrames = 60
RecoveryFrames = 0

[sub_resource type="Resource" id="FallState"]
script = ExtResource("1_move")
AttackName = "STATE FALL"
AnimationName = "fall"
Stance = 3
StartupFrames = 0
ActiveFrames = 60
RecoveryFrames = 0

[resource]
script = ExtResource("2_set")
Rules = [SubResource("IdleState"), SubResource("CrouchState"), SubResource("WalkForwardState"), SubResource("WalkBackState"), SubResource("JumpRiseState"), SubResource("FallState")]
""")

    has_super_portrait = (ASSET_ROOT / spec.scene_stem / "super_portrait.png").exists()
    definition_load_steps = (17 if spec.archive_id == "m_heita" else 16) + (1 if has_super_portrait else 0)
    booster_ext = (f'\n[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/'
                   f'{spec.scene_stem}/{spec.slug}_booster.tres" id="15_booster"]'
                   f'\n[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/'
                   f'{spec.scene_stem}/{spec.slug}_jet_escape_right.tres" id="16_escape_right"]'
                   f'\n[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/'
                   f'{spec.scene_stem}/{spec.slug}_jet_escape_left.tres" id="17_escape_left"]') \
        if spec.archive_id == "m_heita" else ""
    portrait_ext = (f'\n[ext_resource type="Texture2D" path="res://Assets/TestFighter/BigBangBeatRevolve/'
                    f'{spec.scene_stem}/super_portrait.png" id="20_portrait"]') if has_super_portrait else ""
    portrait_property = '\nSuperPortrait = ExtResource("20_portrait")' if has_super_portrait else ""
    ability_list = ('ExtResource("16_escape_right"), ExtResource("17_escape_left"), '
                    'ExtResource("15_booster"), ExtResource("10_super_jump"), '
                    'ExtResource("3_neutral_jump"), ExtResource("4_forward_jump"), '
                    'ExtResource("5_backward_jump")') if spec.archive_id == "m_heita" else (
                    'ExtResource("10_super_jump"), ExtResource("3_neutral_jump"), '
                    'ExtResource("4_forward_jump"), ExtResource("5_backward_jump"), '
                    'ExtResource("6_run"), ExtResource("7_backdash"), '
                    'ExtResource("9_back_air_dash"), ExtResource("8_air_dash")')
    landing_tuning = "\nNonFlightLandingLagMultiplier = 0.5" if spec.archive_id == "m_heita" else ""
    fallback_move_property = "\nAllowLegacyFallbackMoves = false" if spec.archive_id == "m_heita" else ""
    definition_content = f"""[gd_resource type="Resource" script_class="FighterDefinition" load_steps={definition_load_steps} format=3]

[ext_resource type="Script" path="res://Scripts/Core/FighterDefinition.cs" id="1_def"]
[ext_resource type="Script" path="res://Scripts/Core/MovementTuning.cs" id="2_tuning"]
[ext_resource type="Resource" path="res://Data/Characters/Common/neutral_jump.tres" id="3_neutral_jump"]
[ext_resource type="Resource" path="res://Data/Characters/Common/forward_jump.tres" id="4_forward_jump"]
[ext_resource type="Resource" path="res://Data/Characters/Common/backward_jump.tres" id="5_backward_jump"]
[ext_resource type="Resource" path="res://Data/Characters/Common/forward_run.tres" id="6_run"]
[ext_resource type="Resource" path="res://Data/Characters/Common/backdash.tres" id="7_backdash"]
[ext_resource type="Resource" path="res://Data/Characters/Common/air_dash.tres" id="8_air_dash"]
[ext_resource type="Resource" path="res://Data/Characters/Common/backward_air_dash.tres" id="9_back_air_dash"]
[ext_resource type="Resource" path="res://Data/Characters/Common/super_jump.tres" id="10_super_jump"]
[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/{spec.scene_stem}/{spec.slug}_normal_moves.tres" id="11_normals"]
[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/{spec.scene_stem}/{spec.slug}_special_moves.tres" id="12_specials"]
[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/{spec.scene_stem}/{spec.slug}_gauges.tres" id="13_gauges"]
[ext_resource type="Resource" path="res://Data/Characters/BigBangBeatRevolve/{spec.scene_stem}/{spec.slug}_state_boxes.tres" id="14_states"]{booster_ext}{portrait_ext}

[sub_resource type="Resource" id="MovementTuning_imported"]
script = ExtResource("2_tuning")
WalkSpeed = 300.0
GroundAcceleration = 4200.0
GroundDeceleration = 5000.0
GroundFriction = 5800.0
Gravity = 2400.0
TerminalFallSpeed = 1900.0
AirSpeed = 340.0
AirAcceleration = 2200.0
AirDeceleration = 1500.0
AllowAirControl = false{landing_tuning}
InputBufferFrames = 4

[resource]
script = ExtResource("1_def")
FighterName = "{spec.display_name}"
{fallback_move_property.lstrip()}
{portrait_property.lstrip()}
Gauges = ExtResource("13_gauges")
Tuning = SubResource("MovementTuning_imported")
Abilities = [{ability_list}]
NormalMoves = ExtResource("11_normals")
SpecialMoves = ExtResource("12_specials")
StateBoxes = ExtResource("14_states")
SuperMoves = []
CancelRules = []
"""
    # Existing generated definitions receive the new state-box link once. Once
    # it exists, later imports preserve all designer-authored tuning.
    if not definition.exists() or "StateBoxes = ExtResource" not in definition.read_text(encoding="utf-8"):
        definition.write_text(definition_content, encoding="utf-8")
    return definition


def write_scene(spec: FighterSpec, sprite_frames: Path, definition: Path) -> Path:
    scene = SCENE_ROOT / f"{spec.scene_stem}Test.tscn"
    sprite_resource = sprite_frames.relative_to(ROOT).as_posix()
    definition_resource = definition.relative_to(ROOT).as_posix()
    if scene.exists():
        return scene
    scene.write_text(f"""[gd_scene load_steps=5 format=3]

[ext_resource type="Script" path="res://Scripts/Demo/SpriteTestFighter.cs" id="1_script"]
[ext_resource type="Resource" path="res://{definition_resource}" id="2_definition"]
[ext_resource type="SpriteFrames" path="res://{sprite_resource}" id="3_frames"]

[sub_resource type="RectangleShape2D" id="RectangleShape2D_body"]
size = Vector2(56, 100)

[node name="{spec.scene_stem}Test" type="CharacterBody2D" node_paths=PackedStringArray("CharacterSprite")]
collision_layer = 2
script = ExtResource("1_script")
CharacterSprite = NodePath("CharacterSprite")
Definition = ExtResource("2_definition")

[node name="CharacterSprite" type="AnimatedSprite2D" parent="."]
texture_filter = 1
position = Vector2(0, -58)
sprite_frames = ExtResource("3_frames")
animation = &"idle"
autoplay = "idle"

[node name="BodyCollision" type="CollisionShape2D" parent="."]
position = Vector2(0, -50)
shape = SubResource("RectangleShape2D_body")
""", encoding="utf-8")
    return scene


def import_fighter(spec: FighterSpec) -> tuple[int, int, Path]:
    source_dir = EXTRACTION / f"_{spec.archive_id}_pct"
    script_path = EXTRACTION / f"_{spec.archive_id}_scr" / "script.txt"
    if not source_dir.is_dir() or not script_path.is_file():
        raise FileNotFoundError(f"Missing extracted picture/script data for {spec.display_name}")

    actions = parse_actions(script_path)
    aliases = choose_aliases(actions)
    if spec.archive_id == "m_heita":
        # Confirmed during the character pass: source action 10 is repurposed
        # as Mecha Heita's dedicated crouching hit reaction.
        aliases["crouch_hit"] = next(action for action in actions if action.visual_index == 10)
        aliases["crouch_hit_2"] = next(action for action in actions if action.visual_index == 11)
        booster_body = next(action for action in actions if action.visual_index == 12)
        aliases["fly_up_body"] = booster_body
        aliases["booster_body"] = booster_body
        aliases["forward_flight_body"] = next(action for action in actions if action.visual_index == 103)
        aliases["back_flight_body"] = next(action for action in actions if action.visual_index == 105)
        aliases["flight_landing"] = next(action for action in actions if action.visual_index == 106)
        aliases["landing"] = next(action for action in actions if action.visual_index == 107)
        aliases["flight_fall"] = next(action for action in actions if action.visual_index == 110)
        aliases["air_interpolation_fall"] = next(action for action in actions if action.visual_index == 111)
        standing_light_punch = next(action for action in actions if action.visual_index == 112)
        aliases["light_punch"] = standing_light_punch
        aliases["standing_light_punch"] = standing_light_punch
        standing_medium_punch = next(action for action in actions if action.visual_index == 113)
        aliases["medium_punch_back"] = standing_medium_punch
        aliases["standing_medium_punch"] = standing_medium_punch
        standing_heavy_kick = next(action for action in actions if action.visual_index == 114)
        aliases["heavy_kick"] = standing_heavy_kick
        aliases["standing_heavy_kick"] = standing_heavy_kick
        crouching_light_punch = next(action for action in actions if action.visual_index == 115)
        aliases["crouching_light_punch"] = crouching_light_punch
        crouching_medium_kick = next(action for action in actions if action.visual_index == 116)
        aliases["crouching_light_kick"] = crouching_medium_kick
        aliases["crouching_medium_kick"] = crouching_medium_kick
        crouching_heavy_punch = next(action for action in actions if action.visual_index == 117)
        aliases["crouching_heavy_punch"] = crouching_heavy_punch
        jumping_light_kick = next(action for action in actions if action.visual_index == 118)
        aliases["air_light_kick"] = jumping_light_kick
        aliases["jumping_light_kick"] = jumping_light_kick
        jumping_medium_punch = next(action for action in actions if action.visual_index == 119)
        aliases["air_medium_punch_back"] = jumping_medium_punch
        aliases["jumping_medium_punch"] = jumping_medium_punch
        jumping_heavy_punch = next(action for action in actions if action.visual_index == 120)
        aliases["air_heavy_punch"] = jumping_heavy_punch
        aliases["jumping_heavy_punch"] = jumping_heavy_punch
        standing_light_kick = next(action for action in actions if action.visual_index == 140)
        aliases["standing_light_kick"] = standing_light_kick
        throw_startup = next(action for action in actions if action.visual_index == 123)
        aliases["throw"] = throw_startup
        aliases["throw_startup"] = throw_startup
        forward_throw = next(action for action in actions if action.visual_index == 124)
        aliases["forward_throw"] = forward_throw
        back_throw = next(action for action in actions if action.visual_index == 125)
        aliases["back_throw"] = back_throw
        back_throw_startup = next(action for action in actions if action.visual_index == 126)
        aliases["back_throw_startup"] = back_throw_startup
        aliases["cut_in_effect"] = next(action for action in actions if action.visual_index == 127)
        standing_heavy_punch = next(action for action in actions if action.visual_index == 144)
        aliases["heavy_punch"] = standing_heavy_punch
        aliases["standing_heavy_punch"] = standing_heavy_punch
        aliases["mecha_explosion"] = next(action for action in actions if action.visual_index == 147)
        aliases["helicopter_dp"] = next(action for action in actions if action.visual_index == 130)
        aliases["special_move_landing"] = next(action for action in actions if action.visual_index == 131)
        aliases["airborne_helicopter_dp"] = next(action for action in actions if action.visual_index == 132)
        # Compatibility alias for the initial test binding; the source action
        # is explicitly named 対空_空中版 (anti-air, airborne version).
        aliases["light_helicopter_dp"] = aliases["airborne_helicopter_dp"]
        aliases["missile_activation"] = next(action for action in actions if action.visual_index == 133)
        aliases["mecha_missile"] = next(action for action in actions if action.visual_index == 150)
        aliases["jumping_medium_kick"] = next(action for action in actions if action.visual_index == 136)
        aliases["jumping_medium_kick_landing"] = next(action for action in actions if action.visual_index == 137)
        aliases["run"] = next(action for action in actions if action.visual_index == 141)
        aliases["run_stop"] = next(action for action in actions if action.visual_index == 142)
        aliases["forward_heavy_kick"] = next(action for action in actions if action.visual_index == 146)
        aliases["booster_jet_fire"] = aliases["fly_up_jet_effect"]
        win_taunt = next(action for action in actions if action.visual_index == 13)
        aliases["win"] = win_taunt
        aliases["win_loop"] = win_taunt
        aliases["taunt"] = win_taunt
        aliases["full_crouch_hitstun"] = next(
            action for action in actions if action.visual_index == 14
        )
        aliases["full_crouch_hitstun_2"] = next(
            action for action in actions if action.visual_index == 15
        )
        aliases["super_jump_neutral"] = next(
            action for action in actions if action.visual_index == 16
        )
        aliases["super_jump_forward"] = next(
            action for action in actions if action.visual_index == 17
        )
        aliases["super_jump_backward"] = next(
            action for action in actions if action.visual_index == 18
        )
        aliases["full_crouch_2"] = next(
            action for action in actions if action.visual_index == 19
        )
        aliases["standing_hitstun_to_idle"] = next(
            action for action in actions if action.visual_index == 20
        )
        aliases["standing_light_hitstun_to_idle"] = next(
            action for action in actions if action.visual_index == 21
        )
        aliases["standing_medium_hitstun_to_idle"] = next(
            action for action in actions if action.visual_index == 22
        )
        aliases["standing_big_hitstun_to_idle_2"] = next(
            action for action in actions if action.visual_index == 23
        )
        aliases["standing_big_hitstun_to_idle_3"] = next(
            action for action in actions if action.visual_index == 24
        )
        aliases["standing_mid_hitstun_to_idle"] = next(
            action for action in actions if action.visual_index == 25
        )
        aliases["standing_light_mid_hitstun_to_idle"] = next(
            action for action in actions if action.visual_index == 26
        )
        aliases["standing_light_mid_hitstun_to_idle_2"] = next(
            action for action in actions if action.visual_index == 27
        )
        aliases["standing_mid_hitstun_to_idle_2"] = next(
            action for action in actions if action.visual_index == 28
        )
        aliases["standing_mid_hitstun_to_idle_3"] = next(
            action for action in actions if action.visual_index == 29
        )
        aliases["crouching_heavy_hitstun"] = next(
            action for action in actions if action.visual_index == 30
        )
        aliases["crouching_light_hitstun"] = next(
            action for action in actions if action.visual_index == 31
        )
        aliases["crouching_mid_hitstun"] = next(
            action for action in actions if action.visual_index == 32
        )
        aliases["crouching_mid_hitstun_2"] = next(
            action for action in actions if action.visual_index == 33
        )
        aliases["crouching_mid_hitstun_3"] = next(
            action for action in actions if action.visual_index == 34
        )
        launched_knocked_away = next(
            action for action in actions if action.visual_index == 35
        )
        aliases["knocked_away"] = launched_knocked_away
        aliases["launched"] = launched_knocked_away
        aliases["launched_hitstun"] = next(
            action for action in actions if action.visual_index == 36
        )
        aliases["launched_far"] = next(
            action for action in actions if action.visual_index == 37
        )
        aliases["short_launch"] = next(
            action for action in actions if action.visual_index == 38
        )
        aliases["light_launch"] = next(
            action for action in actions if action.visual_index == 39
        )
        # These are not redundant air-hit drawings. The source script exposes
        # them as distinct Japanese blow-away reaction channels so attacks can
        # choose direction, strength, and bounce behavior independently.
        blow_away_aliases = {
            "blow_away_horizontal": 40,
            "blow_away_vertical_weak": 41,
            "blow_away_vertical_medium": 42,
            "blow_away_vertical_strong": 43,
            "blow_away_diagonal_weak": 44,
            "blow_away_diagonal_medium": 45,
            "blow_away_diagonal_strong": 46,
            "blow_away_downward_weak": 47,
            "blow_away_downward_medium": 48,
            "blow_away_downward_strong": 49,
            "blow_away_diagonal_down": 51,
            "blow_away_downward_no_bounce": 76,
            "blow_away_diagonal_down_no_bounce": 77,
        }
        for alias, visual_index in blow_away_aliases.items():
            aliases[alias] = next(
                action for action in actions if action.visual_index == visual_index
            )
        aliases["stumble"] = next(
            action for action in actions if action.visual_index == 50
        )
        aliases["wall_bounce_strong"] = next(
            action for action in actions if action.visual_index == 52
        )
        aliases["wall_bounce_weak"] = next(
            action for action in actions if action.visual_index == 53
        )
        aliases["hit_fall"] = next(
            action for action in actions if action.visual_index == 54
        )
        aliases["ground_bounce_weak"] = next(
            action for action in actions if action.visual_index == 57
        )
        aliases["ground_bounce_medium"] = next(
            action for action in actions if action.visual_index == 58
        )
        aliases["ground_bounce_strong"] = next(
            action for action in actions if action.visual_index == 59
        )
        aliases["stand_block_weak"] = next(
            action for action in actions if action.visual_index == 60
        )
        aliases["stand_block_medium"] = next(
            action for action in actions if action.visual_index == 61
        )
        aliases["stand_block_strong"] = next(
            action for action in actions if action.visual_index == 62
        )
        aliases["stand_block_special_strong"] = next(
            action for action in actions if action.visual_index == 63
        )
        aliases["crouch_block_weak"] = next(
            action for action in actions if action.visual_index == 64
        )
        guard_aliases = {
            "crouch_block_medium": 65,
            "crouch_block_strong": 66,
            "crouch_block_special_strong": 67,
            "air_block_weak": 68,
            "air_block_medium": 69,
            "air_block_strong": 70,
            "air_block_special_strong": 71,
        }
        for alias, visual_index in guard_aliases.items():
            aliases[alias] = next(
                action for action in actions if action.visual_index == visual_index
            )
        special_reaction_aliases = {
            "special_stagger": 72,
            "slide_down_horizontal": 73,
            "slide_down_diagonal": 74,
            "slide_downed": 75,
            "diagonal_bounce": 78,
            "pullback_weak": 79,
            "pullback_strong": 80,
            "guard_pullback_weak": 81,
            "guard_pullback_strong": 82,
            "pullback_air": 83,
            "guard_pullback_air": 84,
        }
        for alias, visual_index in special_reaction_aliases.items():
            aliases[alias] = next(
                action for action in actions if action.visual_index == visual_index
            )
        aliases["alpha_counter"] = next(
            action for action in actions if action.visual_index == 85
        )
        escape_left = next(
            action for action in actions if action.visual_index == 86
        )
        aliases["escape_left"] = escape_left
        aliases["backdash_hop"] = escape_left
        aliases["back_dash"] = escape_left
        escape_landing = next(
            action for action in actions if action.visual_index == 87
        )
        aliases["escape_landing"] = escape_landing
        escape_right = next(
            action for action in actions if action.visual_index == 88
        )
        aliases["escape_right"] = escape_right
        aliases["forward_dash"] = escape_right
        # The source labels this B Burst, but the character design pass
        # repurposes its airborne split kick as Mecha Heita's heavy kick.
        aliases["air_heavy_kick"] = next(
            action for action in actions if action.visual_index == 128
        )
        aliases["jumping_heavy_kick"] = aliases["air_heavy_kick"]
        # Keep this provisional: it is currently the shared landing after an
        # aerial normal, but remains benched for replacement if a better pose
        # appears later in the source catalog.
        aliases["air_attack_landing"] = next(
            action for action in actions if action.visual_index == 90
        )
    asset_dir = ASSET_ROOT / spec.scene_stem
    frame_dir = asset_dir / "Frames"
    frame_dir.mkdir(parents=True, exist_ok=True)
    # Prefer the source cut-in drawing; two archives expose only their full-size
    # victory portrait, which is still the character's authored presentation art.
    portrait_source = source_dir / "1000.png"
    if not portrait_source.exists():
        portrait_source = source_dir / "victory.png"
    portrait_destination = asset_dir / "super_portrait.png"
    if portrait_source.exists() and not portrait_destination.exists():
        shutil.copy2(portrait_source, portrait_destination)

    referenced_ids = sorted({drawing.image_id for action in actions for drawing in action.drawings})
    texture_paths: dict[int, Path] = {}
    for image_id in referenced_ids:
        source = source_image(source_dir, image_id)
        if source is None:
            continue
        destination = frame_dir / f"frame_{image_id:04d}.png"
        if not destination.exists():
            with Image.open(source) as image:
                normalized = align_character_frame(image) if source.suffix.lower() == ".bmp" else remove_green(image)
                normalized.save(destination)
        texture_paths[image_id] = destination

    missing_by_action = {
        action.visual_index: sorted({drawing.image_id for drawing in action.drawings if drawing.image_id not in texture_paths})
        for action in actions
    }
    missing_by_action = {key: value for key, value in missing_by_action.items() if value}
    if not texture_paths:
        raise FileNotFoundError(f"No referenced picture data was found for {spec.display_name}")
    fallback_id = next(
        (drawing.image_id for drawing in aliases["idle"].drawings if drawing.image_id in texture_paths),
        min(texture_paths),
    )
    resolved_by_action = {
        action.visual_index: resolve_action_textures(action, texture_paths, fallback_id)
        for action in actions
    }
    catalog = asset_dir / "animation_catalog.csv"
    write_catalog(catalog, actions, missing_by_action, resolved_by_action, spec)
    composite_animations = build_mecha_composite_animations(
        spec, asset_dir, aliases, texture_paths, resolved_by_action
    )
    sprite_frames = asset_dir / f"{spec.slug}_sprite_frames.tres"
    write_sprite_frames(sprite_frames, actions, aliases, texture_paths, resolved_by_action, spec,
                        composite_animations)
    definition = write_character_data(spec)
    scene = write_scene(spec, sprite_frames, definition)
    return len(actions), len(texture_paths), scene


def main() -> None:
    for spec in FIGHTERS:
        action_count, texture_count, scene = import_fighter(spec)
        print(f"{spec.display_name}: {action_count} actions, {texture_count} textures -> {scene.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
