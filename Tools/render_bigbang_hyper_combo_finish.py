from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review"


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sequences = (
        ("hyper_combo_finish_explosion_preview.gif", tuple(range(440, 448)), (3, 3, 3, 3, 3, 3, 3, 60)),
        ("hyper_combo_finish_activation_preview.gif", (*range(460, 476), 460), (*([1] * 16), 60)),
    )
    for filename, order, holds in sequences:
        frames = []
        durations = []
        one_tick_phase = 0
        for number, hold in zip(order, holds):
            frame = Image.open(SOURCE / f"{number}.png").convert("RGB")
            if frame.size != (320, 240):
                raise ValueError(f"Unexpected frame size for {number}: {frame.size}")
            frames.append(frame)
            if hold == 1:
                durations.append((20, 20, 10)[one_tick_phase % 3])
                one_tick_phase += 1
            else:
                durations.append(round(hold * 1000 / 60))
        output = OUTPUT / filename
        frames[0].save(
            output,
            save_all=True,
            append_images=frames[1:],
            duration=durations,
            loop=0,
            optimize=False,
        )
        print(output)


if __name__ == "__main__":
    main()
