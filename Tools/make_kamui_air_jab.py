"""Build Kamui's air jab by splicing existing source pixels only."""

from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
KAMUI = ROOT / "Assets" / "TestFighter" / "BigBangBeatRevolve" / "Kamui"
FRAMES = KAMUI / "Frames"
OUTPUT = FRAMES / "Authored" / "AirJab"
REVIEW = KAMUI / "Review" / "anim_air_jab_splice.gif"
JAB_IDS = (72, 73, 74, 75, 76, 77, 78)
HOLDS = (1, 3, 3, 5, 1, 2, 2)
DASH_IDS = (26, 27, 28, 29)
LOWER_SPLICE_Y = 184
UPPER_SOURCE_CUT_Y = 172
UPPER_OFFSET = (20, 19)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    composites = []
    jab_timeline = [frame_id for frame_id, hold in zip(JAB_IDS, HOLDS) for _ in range(hold)]
    dash_timeline = [DASH_IDS[(tick // 3) % len(DASH_IDS)] for tick in range(len(jab_timeline))]
    for index, (frame_id, dash_id) in enumerate(zip(jab_timeline, dash_timeline)):
        # The user's construction places the jab torso over the exact forward-
        # dash silhouette; both halves retain their original pixel orientation.
        lower = Image.open(FRAMES / f"frame_{dash_id:04d}.png").convert("RGBA")
        lower_alpha = lower.getchannel("A")
        ImageDraw.Draw(lower_alpha).rectangle((0, 0, lower.width, LOWER_SPLICE_Y - 1), fill=0)
        lower.putalpha(lower_alpha)
        upper = Image.open(FRAMES / f"frame_{frame_id:04d}.png").convert("RGBA")
        upper_alpha = upper.getchannel("A")
        ImageDraw.Draw(upper_alpha).rectangle((0, UPPER_SOURCE_CUT_Y, upper.width, upper.height), fill=0)
        upper.putalpha(upper_alpha)
        composite = Image.new("RGBA", upper.size)
        composite.alpha_composite(lower)
        composite.alpha_composite(upper, UPPER_OFFSET)
        path = OUTPUT / f"air_jab_{index:02d}.png"
        composite.save(path)
        composites.append(composite)

    crop = (90, 90, 235, 275)
    preview = [image.crop(crop).resize((290, 370), Image.Resampling.NEAREST)
               for image in composites]
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview[0].save(REVIEW, save_all=True, append_images=preview[1:],
                    duration=round(1000 / 60), loop=0, disposal=2)
    print(REVIEW)


if __name__ == "__main__":
    main()
