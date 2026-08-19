from pathlib import Path

from PIL import Image


ROOT = Path(__file__).parents[1]
SOURCE = ROOT / "Assets" / "Effects" / "BigBangCommon"
OUTPUT = SOURCE / "HyperComboFinishNormal"

# Deep navy -> cobalt -> electric blue -> gold -> white. The source luminance
# chooses the stop, preserving every original ray and blur sample exactly.
PALETTE = (
    (0.00, (2, 7, 35)),
    (0.18, (4, 24, 105)),
    (0.42, (0, 91, 232)),
    (0.66, (15, 180, 255)),
    (0.82, (255, 211, 35)),
    (1.00, (255, 255, 255)),
)


def palette_color(value: float) -> tuple[int, int, int]:
    for index in range(1, len(PALETTE)):
        right_position, right_color = PALETTE[index]
        left_position, left_color = PALETTE[index - 1]
        if value <= right_position:
            amount = (value - left_position) / (right_position - left_position)
            return tuple(round(a + (b - a) * amount) for a, b in zip(left_color, right_color))
    return PALETTE[-1][1]


def recolor(frame_number: int) -> None:
    source = Image.open(SOURCE / f"{frame_number}.png").convert("RGBA")
    output = Image.new("RGBA", source.size)
    pixels = []
    for index, (red, green, blue, alpha) in enumerate(source.getdata()):
        # Normalize the source's red-channel energy range. Using peak energy
        # (rather than neutral luminance) retains the original orange hot rays
        # as yellow and white accents among the blue body of the tunnel.
        peak = max(red, green, blue)
        intensity = min(1.0, max(0.0, (peak - 72.0) / 172.0))
        out_red, out_green, out_blue = palette_color(intensity)

        # A compact white destination light with a warm halo occupies the
        # vanishing point, while the underlying animated texture remains.
        x = index % source.width
        y = index // source.width
        distance = ((x - (source.width - 1) * 0.5) ** 2 + (y - (source.height - 1) * 0.5) ** 2) ** 0.5
        light = min(1.0, max(0.0, (48.0 - distance) / 36.0))
        light = light * light * (3.0 - 2.0 * light)
        white = min(1.0, max(0.0, (26.0 - distance) / 18.0))
        target = tuple(round(255 + (channel - 255) * (1.0 - white)) for channel in (255, 211, 45))
        blend = 0.94 * light
        out_red = round(out_red + (target[0] - out_red) * blend)
        out_green = round(out_green + (target[1] - out_green) * blend)
        out_blue = round(out_blue + (target[2] - out_blue) * blend)
        pixels.append((out_red, out_green, out_blue, alpha))
    output.putdata(pixels)
    output.save(OUTPUT / f"{frame_number}.png", optimize=True)


OUTPUT.mkdir(parents=True, exist_ok=True)
for number in range(460, 476):
    recolor(number)
