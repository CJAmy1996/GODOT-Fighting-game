from pathlib import Path
from PIL import Image


SOURCES = [
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-5d80796f-cc6a-4111-aad8-f3d789b11f24.png"),
    Path(r"C:\Users\casey\AppData\Local\Temp\codex-clipboard-21fc753d-08bd-4200-abd2-53017fb651ce.png"),
]
OUTPUT = Path("Assets/Effects/MechaOpticBeam")


def extract_glow(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGB")
    pixels = []
    for red, green, blue in image.getdata():
        alpha = max(red, green, blue)
        if alpha <= 2:
            pixels.append((0, 0, 0, 0))
            continue
        # The supplied art is composited over black. Un-premultiply it so the
        # original magenta glow remains clean when Godot draws it over a stage.
        scale = 255.0 / alpha
        pixels.append((
            min(255, round(red * scale)),
            min(255, round(green * scale)),
            min(255, round(blue * scale)),
            alpha,
        ))
    rgba = Image.new("RGBA", image.size)
    rgba.putdata(pixels)
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError(f"No visible beam pixels found in {source}")
    left, top, right, bottom = bounds
    padding = 2
    rgba = rgba.crop((max(0, left - padding), max(0, top - padding),
                      min(image.width, right + padding), min(image.height, bottom + padding)))
    return rgba


OUTPUT.mkdir(parents=True, exist_ok=True)
frames = [extract_glow(source) for source in SOURCES]
canvas_width = max(frame.width for frame in frames)
canvas_height = max(frame.height for frame in frames)
for index, frame in enumerate(frames):
    canvas = Image.new("RGBA", (canvas_width, canvas_height))
    canvas.alpha_composite(frame, ((canvas_width - frame.width) // 2, (canvas_height - frame.height) // 2))
    canvas.save(OUTPUT / f"optic_beam_{index:02d}.png")
