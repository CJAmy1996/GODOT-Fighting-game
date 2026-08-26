from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"
OUTPUT = ROOT / "Extraction" / "BigBangBeatRevolve" / "Review" / "hyper_combo_finish_alt_preview.gif"
ORDER = tuple(range(480, 491))


def main() -> None:
    frames = []
    for number in ORDER:
        frame = Image.open(SOURCE / f"{number}.png").convert("RGB")
        if frame.size != (320, 240):
            raise ValueError(f"Unexpected frame size for {number}: {frame.size}")
        frames.append(frame)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=33,
        loop=0,
        optimize=False,
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
