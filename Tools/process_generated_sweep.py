from pathlib import Path
import sys
from PIL import Image


def main() -> None:
    source = Image.open(sys.argv[1]).convert("RGBA")
    output = Path(sys.argv[2])
    output.mkdir(parents=True, exist_ok=True)
    # The generated strip has generous but not perfectly equal gutters. These
    # normalized boundaries prevent the extended middle sandal entering frame 3.
    boundaries = ((0.0, 0.31), (0.31, 0.715), (0.715, 1.0))
    target_heights = (122, 126, 122)
    for index in range(3):
        left = round(source.width * boundaries[index][0])
        right = round(source.width * boundaries[index][1])
        frame = source.crop((left, 0, right, source.height))
        bbox = frame.getbbox()
        if bbox is None:
            raise RuntimeError(f"generated sweep cell {index} is empty")
        frame = frame.crop(bbox)
        scale = target_heights[index] / frame.height
        frame = frame.resize((max(1, round(frame.width * scale)), target_heights[index]), Image.Resampling.NEAREST)
        canvas = Image.new("RGBA", (320, 384), (0, 0, 0, 0))
        x = (canvas.width - frame.width) // 2
        y = 330 - frame.height
        canvas.alpha_composite(frame, (x, y))
        canvas.save(output / f"mecha_crouching_sweep_{index}.png")


if __name__ == "__main__":
    main()
