from pathlib import Path
import sys
from PIL import Image


def keep_largest_component(frame: Image.Image) -> Image.Image:
    alpha = frame.getchannel("A")
    pixels = alpha.load()
    visited: set[tuple[int, int]] = set()
    components: list[list[tuple[int, int]]] = []
    for y in range(frame.height):
        for x in range(frame.width):
            if pixels[x, y] < 16 or (x, y) in visited:
                continue
            stack = [(x, y)]
            visited.add((x, y))
            component: list[tuple[int, int]] = []
            while stack:
                px, py = stack.pop()
                component.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if 0 <= nx < frame.width and 0 <= ny < frame.height and pixels[nx, ny] >= 16 and (nx, ny) not in visited:
                        visited.add((nx, ny))
                        stack.append((nx, ny))
            components.append(component)
    if not components:
        return frame
    keep = set(max(components, key=len))
    cleaned = frame.copy()
    data = cleaned.load()
    for y in range(cleaned.height):
        for x in range(cleaned.width):
            if (x, y) not in keep:
                data[x, y] = (0, 0, 0, 0)
    return cleaned


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
            raise RuntimeError(f"generated spinning sweep cell {index} is empty")
        frame = frame.crop(bbox)
        target_height = 126 if index in (1, 2) else 122
        scale = target_height / frame.height
        frame = frame.resize((max(1, round(frame.width * scale)), target_height), Image.Resampling.NEAREST)
        frame = keep_largest_component(frame)
        canvas = Image.new("RGBA", (320, 384), (0, 0, 0, 0))
        canvas.alpha_composite(frame, ((canvas.width - frame.width) // 2, 330 - frame.height))
        canvas.save(output / f"mecha_crouching_spin_sweep_{index}.png")


if __name__ == "__main__":
    main()
