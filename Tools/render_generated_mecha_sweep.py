from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets/TestFighter/BigBangBeatRevolve/MechaHeita/GeneratedSweep"
OUTPUT = ROOT / ".codex-temp/animation_review/MechaHeita/generated_crouching_spin_sweep.gif"


def checkerboard() -> Image.Image:
    canvas = Image.new("RGBA", (320, 384), (26, 27, 38, 255))
    pixels = canvas.load()
    colors = ((35, 37, 50, 255), (45, 48, 63, 255))
    for y in range(384):
        for x in range(320):
            pixels[x, y] = colors[((x // 16) + (y // 16)) & 1]
    return canvas


def main() -> None:
    main_poses = [Image.open(ASSETS / f"mecha_crouching_spin_sweep_{index}.png").convert("RGBA") for index in range(4)]
    inbetweens = [Image.open(ASSETS / f"mecha_crouching_spin_inbetween_{index}.png").convert("RGBA") for index in range(4)]
    # Twelve spin ticks, two distinct two-tick extension drawings, then four recovery ticks.
    poses = [main_poses[0], inbetweens[0], inbetweens[1], inbetweens[2], main_poses[1],
             inbetweens[3], main_poses[2], main_poses[1], main_poses[3]]
    order = tuple(range(len(poses)))
    holds = (2, 2, 2, 2, 4, 2, 2, 2, 2)
    frames = []
    durations = []
    for pose_index, hold in zip(order, holds):
        frame = checkerboard()
        frame.alpha_composite(poses[pose_index])
        frames.append(frame.convert("RGB"))
        durations.append(round(1000 * hold / 60))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2)
    print(OUTPUT)


if __name__ == "__main__":
    main()
