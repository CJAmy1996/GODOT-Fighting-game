"""Audit Big Bang Beat Revolve's extracted HUD mappings and source GI layout."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
COMMON = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_scr"
PICTURES = ROOT / "Extraction" / "BigBangBeatRevolve" / "_common_pct"


def main() -> None:
    # KiriKiri assigns drawing IDs by zero-based line position.  A line may
    # optionally contain crop metadata after the filename.
    mappings = {}
    for drawing, line in enumerate(
        (COMMON / "kir.txt").read_bytes().decode("cp932", errors="replace").splitlines()
    ):
        mappings[drawing] = line.split("\t", 1)[0].strip()

    script = (COMMON / "script.txt").read_bytes().decode("cp932", errors="replace").splitlines()
    gauge_commands = [line for line in script if line.startswith("GI\t") and "\t384\t" in line]
    expected = {
        380: "gauge1p.png",
        381: "gauge2p.png",
        382: "gauge_b1p.png",
        383: "gauge_b2p.png",
        384: "gauge_in.png",
    }
    for drawing, filename in expected.items():
        if mappings.get(drawing) != filename or not (PICTURES / filename).is_file():
            raise RuntimeError(f"missing Revolve HUD drawing {drawing}: {filename}")
    if len(gauge_commands) != 2:
        raise RuntimeError("expected source GI commands for both players")

    print("REVOLVE_HUD_AUDIT_PASS")
    for drawing, filename in expected.items():
        print(f"drawing {drawing}: {filename}")
    for command in gauge_commands:
        print(command)


if __name__ == "__main__":
    main()
