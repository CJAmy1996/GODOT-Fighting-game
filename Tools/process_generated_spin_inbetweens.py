from pathlib import Path
import sys
from PIL import Image
from process_generated_spin_sweep import keep_largest_component


def main() -> None:
    source = Image.open(sys.argv[1]).convert("RGBA")
    output = Path(sys.argv[2])
    output.mkdir(parents=True, exist_ok=True)
    for index in range(4):
        left = round(source.width * index / 4)
        right = round(source.width * (index + 1) / 4)
        frame = source.crop((left, 0, right, source.height))
        bbox = frame.getbbox()
        if bbox is None:
            raise RuntimeError(f"generated spin in-between cell {index} is empty")
        frame = frame.crop(bbox)
        scale = 126 / frame.height
        frame = frame.resize((max(1, round(frame.width * scale)), 126), Image.Resampling.NEAREST)
        frame = keep_largest_component(frame)
        canvas = Image.new("RGBA", (320, 384), (0, 0, 0, 0))
        canvas.alpha_composite(frame, ((canvas.width - frame.width) // 2, 330 - frame.height))
        canvas.save(output / f"mecha_crouching_spin_inbetween_{index}.png")


if __name__ == "__main__":
    main()
