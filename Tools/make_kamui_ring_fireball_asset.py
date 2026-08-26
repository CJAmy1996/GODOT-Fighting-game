"""Rebuild Kamui's ring fireball directly from BBBR source actions 152/153."""
from __future__ import annotations
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_scr" / "backup.txt"
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "RingFireball"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "ring_fireball.gif"
AUDIT = OUTPUT / "source_actions_152_153.json"

def action(action_id: int) -> list[list[str]]:
    sections = [part.strip() for part in SCRIPT.read_text(encoding="cp932", errors="replace").split("------")]
    for section in sections:
        lines = [line.split("\t") for line in section.splitlines() if line.strip()]
        if lines and len(lines[0]) > 3 and lines[0][3] == str(action_id):
            return lines
    raise ValueError(f"Kamui source action {action_id} was not found")

def first(lines: list[list[str]], command: str) -> list[str]:
    return next(fields for fields in lines[1:] if fields[0].strip() == command)

def drawings(lines: list[list[str]]) -> list[list[str]]:
    return [fields for fields in lines[1:] if fields[0].strip() == "Ｉ"]

def keyed_source(drawing_id: int) -> Image.Image:
    source_path = SOURCE / f"{drawing_id:03d}.bmp"
    if not source_path.exists():
        source_path = SOURCE / f"{drawing_id:03d}.png"
    image = Image.open(source_path).convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, _ = pixels[x, y]
            if green > 180 and green > red * 1.8 and green > blue * 1.8:
                pixels[x, y] = (red, green, blue, 0)
                continue
            energy = max(red, green, blue)
            if energy <= 26:
                alpha = 0
            elif energy >= 72:
                alpha = 255
            else:
                alpha = round((energy - 26) * 255 / 46)
            pixels[x, y] = (red, green, blue, alpha)
    bounds = image.getbbox()
    if bounds is None:
        raise ValueError(f"source drawing {drawing_id} became empty after keying")
    return image.crop(bounds)

def parse() -> dict[str, int | float]:
    cast, projectile, decoration = action(150), action(152), action(153)
    cast_commands = cast[1:]
    spawn_index = next(index for index, fields in enumerate(cast_commands) if fields[0].strip() == "Ｏ")
    spawn = cast_commands[spawn_index]
    cast_startup = sum(int(fields[1]) for fields in cast_commands[:spawn_index] if fields[0].strip() == "Ｉ")
    cast_lifetime = sum(int(fields[1]) for fields in drawings(cast))
    projectile_drawings = drawings(projectile)
    decoration_drawing = drawings(decoration)[0]
    motion, color, scale = first(projectile, "Ｍ"), first(decoration, "色"), first(decoration, "大")
    return {
        "cast_action": 150, "cast_startup_frames": cast_startup,
        "cast_lifetime_frames": cast_lifetime,
        "projectile_spawn_offset_x": int(spawn[1]), "projectile_spawn_offset_y": int(spawn[2]),
        "projectile_action": 152, "decoration_action": 153,
        "core_drawing": int(projectile_drawings[0][2]),
        "core_origin_x": int(projectile_drawings[0][3]), "core_origin_y": int(projectile_drawings[0][4]),
        "core_hold_frames": int(projectile_drawings[0][1]),
        "projectile_lifetime_frames": sum(int(fields[1]) for fields in projectile_drawings),
        "initial_speed_px_per_second": float(motion[1]),
        "speed_delta_px_per_second_per_frame": float(motion[5]),
        "ring_drawing": int(decoration_drawing[2]),
        "ring_origin_x": int(decoration_drawing[3]), "ring_origin_y": int(decoration_drawing[4]),
        "ring_lifetime_frames": int(decoration_drawing[1]),
        "ring_initial_opacity": int(color[5]), "ring_opacity_delta_per_frame": int(color[6]),
        "ring_initial_scale": int(scale[1]) / 100.0,
        "ring_scale_growth_per_frame": int(scale[7]) / 100.0,
    }

def render_review(core: Image.Image, ring: Image.Image, source: dict[str, int | float]) -> None:
    frames, rings = [], []
    position, velocity = 90.0, float(source["initial_speed_px_per_second"])
    interval, lifetime = int(source["core_hold_frames"]), int(source["ring_lifetime_frames"])
    alpha_start, alpha_delta = int(source["ring_initial_opacity"]), int(source["ring_opacity_delta_per_frame"])
    initial_scale, scale_growth = float(source["ring_initial_scale"]), float(source["ring_scale_growth_per_frame"])
    for tick in range(int(source["projectile_lifetime_frames"])):
        if tick % interval == 0:
            rings.append((position, 0))
        canvas = Image.new("RGBA", (1024, 180), (26, 30, 39, 255))
        live = []
        for ring_x, age in rings:
            alpha = max(0, alpha_start + alpha_delta * age)
            if age < lifetime and alpha > 0:
                scale = initial_scale + scale_growth * age
                size = (max(1, round(ring.width * scale)), max(1, round(ring.height * scale)))
                drawing = ring.resize(size, Image.Resampling.NEAREST)
                drawing.putalpha(drawing.getchannel("A").point(lambda value, a=alpha: value * a // 255))
                canvas.alpha_composite(drawing, (round(ring_x - size[0] / 2), round(90 - size[1] / 2)))
                live.append((ring_x, age + 1))
        rings = live
        canvas.alpha_composite(core, (round(position - core.width / 2), round(90 - core.height / 2)))
        frames.append(canvas.resize((512, 90), Image.Resampling.NEAREST))
        position += velocity / 60.0
        velocity += float(source["speed_delta_px_per_second_per_frame"])
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(REVIEW, save_all=True, append_images=frames[1:], duration=17, loop=0, disposal=2)

def main() -> None:
    source = parse()
    core, ring = keyed_source(int(source["core_drawing"])), keyed_source(int(source["ring_drawing"]))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    core.save(OUTPUT / "fireball_core.png")
    ring.save(OUTPUT / "fireball_ring_only.png")
    AUDIT.write_text(json.dumps(source, indent=2) + "\n", encoding="utf-8")
    render_review(core, ring, source)
    print(json.dumps(source, indent=2))
    print(REVIEW)

if __name__ == "__main__":
    main()
