"""Export Mecha Heita body/jet alignment references on an opaque white canvas."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
FRAME_ROOT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "MechaHeita" / "Frames"
OUTPUT_ROOT = ROOT / ".codex-temp" / "mecha_jet_alignment"

EXPORTS = {
    "super_jump_neutral_first_airborne.png": 70,
    "super_jump_forward_first_airborne.png": 80,
    "super_jump_backward_first_airborne.png": 61,
    "escape_left.png": 92,
    "escape_right.png": 89,
    "jet_asset_frame_0409.png": 409,
}


def main() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    for filename, source_id in EXPORTS.items():
        source = FRAME_ROOT / f"frame_{source_id:04d}.png"
        with Image.open(source) as image:
            sprite = image.convert("RGBA")
            white = Image.new("RGBA", sprite.size, (255, 255, 255, 255))
            white.alpha_composite(sprite)
            destination = OUTPUT_ROOT / filename
            white.convert("RGB").save(destination, format="PNG", optimize=False)
            print(destination)


if __name__ == "__main__":
    main()
