"""Preserve Kamui's legacy 113-120 rock breakup as a reusable Godot effect."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_kamui_pct"
OUTPUT = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Effects" / "RockBreak"
REVIEW = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui" / "Review" / "rock_break_113_120.gif"


def remove_green(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, _ = pixels[x, y]
            if green > 180 and green > red * 1.8 and green > blue * 1.8:
                pixels[x, y] = (red, green, blue, 0)
    return image


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for source_id in range(113, 121):
        frame = remove_green(Image.open(SOURCE / f"{source_id:03}.bmp"))
        frame.save(OUTPUT / f"rock_break_{source_id:03}.png")
        frames.append(frame)

    width = max(frame.width for frame in frames)
    height = max(frame.height for frame in frames)
    previews = []
    for frame in frames:
        canvas = Image.new("RGBA", (width, height))
        # Preserve source registration: bottom-center all pieces on one effect origin.
        canvas.alpha_composite(frame, ((width - frame.width) // 2, height - frame.height))
        previews.append(canvas.resize((width * 3, height * 3), Image.Resampling.NEAREST))
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    previews[0].save(REVIEW, save_all=True, append_images=previews[1:], duration=50, loop=0, disposal=2)

    resources = []
    frame_rows = []
    for index, source_id in enumerate(range(113, 121), 1):
        resources.append(
            f'[ext_resource type="Texture2D" path="res://Assets/TestFighter/BigBangBeatRevolve/Kamui/Effects/RockBreak/rock_break_{source_id:03}.png" id="{index}"]'
        )
        frame_rows.append(f'{{"duration": 3.0, "texture": ExtResource("{index}")}}')
    tres = "[gd_resource type=\"SpriteFrames\" load_steps=9 format=3]\n\n"
    tres += "\n".join(resources)
    tres += "\n\n[resource]\nanimations = [{\n"
    tres += '\"frames\": [' + ", ".join(frame_rows) + '],\n'
    tres += '\"loop\": false,\n\"name\": &\"rock_break\",\n\"speed\": 60.0\n}]\n'
    (OUTPUT / "rock_break_sprite_frames.tres").write_text(tres, encoding="utf-8")
    print(REVIEW)


if __name__ == "__main__":
    main()
