"""Extract the exact BBB 1st Impression HUD sprites from a MUGEN SFF v2 reference."""

from __future__ import annotations

import argparse
import json
import struct
import zlib
from pathlib import Path


TARGETS = {
    (0, 0): "life_bg_p1",
    (0, 1): "life_bg_p2",
    (0, 2): "life_team_p1",
    (0, 3): "life_team_p2",
    (1, 0): "life_overlay_p1",
    (1, 1): "life_overlay_p2",
    **{(2, frame): f"life_segment_{frame}" for frame in range(9)},
    (3, 0): "life_delayed_red",
    (4, 0): "life_current_green",
    (5, 0): "bpower_background",
    (6, 0): "bpower_frame",
    (7, 0): "bpower_fill",
	**{(group, 0): f"life_stock_marker_{group}" for group in range(100, 109)},
}


def png_chunk(kind: bytes, payload: bytes) -> bytes:
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)


def write_png(path: Path, width: int, height: int, rgba: bytes) -> None:
    rows = b"".join(b"\0" + rgba[y * width * 4 : (y + 1) * width * 4] for y in range(height))
    data = b"\x89PNG\r\n\x1a\n"
    data += png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    data += png_chunk(b"IDAT", zlib.compress(rows, 9))
    data += png_chunk(b"IEND", b"")
    path.write_bytes(data)


def decode_rle8(raw: bytes, pixel_count: int) -> bytes:
    stream = memoryview(raw)[4:]
    pixels = bytearray()
    cursor = 0
    while len(pixels) < pixel_count:
        value = stream[cursor]
        cursor += 1
        if value & 0xC0 == 0x40:
            run = value & 0x3F
            value = stream[cursor]
            cursor += 1
            pixels.extend([value] * run)
        else:
            pixels.append(value)
    if len(pixels) != pixel_count:
        raise ValueError("RLE8 output exceeded declared dimensions")
    return bytes(pixels)


def alpha_bbox(rgba: bytes, width: int, height: int) -> tuple[int, int, int, int] | None:
    points = [(i % width, i // width) for i in range(width * height) if rgba[i * 4 + 3]]
    if not points:
        return None
    xs, ys = zip(*points)
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def crop_rgba(rgba: bytes, width: int, bbox: tuple[int, int, int, int]) -> bytes:
    x0, y0, x1, y1 = bbox
    return b"".join(rgba[(y * width + x0) * 4 : (y * width + x1) * 4] for y in range(y0, y1))


def extract_sff(sff_path: Path, output: Path) -> list[dict]:
    data = sff_path.read_bytes()
    sprite_table, sprite_count = struct.unpack_from("<II", data, 36)
    palette_table, palette_count = struct.unpack_from("<II", data, 44)
    literal_base = struct.unpack_from("<I", data, 52)[0]
    translated_base = struct.unpack_from("<I", data, 60)[0]

    palettes = []
    for index in range(palette_count):
        group, number, colors, linked, offset, length = struct.unpack_from("<HHHHII", data, palette_table + index * 16)
        palettes.append({"group": group, "number": number, "colors": colors, "linked": linked, "offset": offset, "length": length})

    def resolve_palette(index: int) -> bytes:
        seen = set()
        while palettes[index]["length"] == 0:
            if index in seen:
                raise ValueError("palette link cycle")
            seen.add(index)
            index = palettes[index]["linked"]
        entry = palettes[index]
        start = literal_base + entry["offset"]
        return data[start : start + entry["length"]]

    records = []
    for index in range(sprite_count):
        fields = struct.unpack_from("<HHHHhhHBBIIHH", data, sprite_table + index * 28)
        group, image, width, height, axis_x, axis_y, linked, fmt, depth, offset, length, palette_index, flags = fields
        key = (group, image)
        if key not in TARGETS:
            continue
        if length == 0:
            raise ValueError(f"target {key} links sprite {linked}; linked extraction not implemented")
        base = translated_base if flags & 1 else literal_base
        raw = data[base + offset : base + offset + length]
        if fmt != 2:
            raise ValueError(f"target {key} uses format {fmt}, expected RLE8")
        indexed = decode_rle8(raw, width * height)
        palette = resolve_palette(palette_index)
        rgba = bytearray()
        for pixel in indexed:
            start = pixel * 4
            rgba.extend(palette[start : start + 4])
        name = TARGETS[key]
        full_path = output / f"{name}_full.png"
        write_png(full_path, width, height, bytes(rgba))
        bbox = alpha_bbox(rgba, width, height)
        crop_path = None
        if bbox:
            cropped = crop_rgba(rgba, width, bbox)
            crop_path = output / f"{name}.png"
            write_png(crop_path, bbox[2] - bbox[0], bbox[3] - bbox[1], cropped)
        records.append({
            "group": group, "image": image, "name": name, "size": [width, height],
            "axis": [axis_x, axis_y], "alpha_bbox": list(bbox) if bbox else None,
            "full_png": full_path.name, "cropped_png": crop_path.name if crop_path else None,
        })
    return records


def extract_power_font(fnt_path: Path, output: Path) -> dict:
    data = fnt_path.read_bytes()
    pcx_offset, pcx_length, text_offset = struct.unpack_from("<III", data, 16)
    pcx = data[pcx_offset : pcx_offset + pcx_length]
    if pcx[:4] != b"\x0a\x05\x01\x08":
        raise ValueError("BBB-Pow.fnt does not contain the expected 8-bit PCX")
    xmin, ymin, xmax, ymax = struct.unpack_from("<HHHH", pcx, 4)
    width, height = xmax - xmin + 1, ymax - ymin + 1
    planes = pcx[65]
    bytes_per_line = struct.unpack_from("<H", pcx, 66)[0]
    needed = height * planes * bytes_per_line
    decoded = bytearray()
    cursor = 128
    while len(decoded) < needed:
        value = pcx[cursor]
        cursor += 1
        if value & 0xC0 == 0xC0:
            run = value & 0x3F
            value = pcx[cursor]
            cursor += 1
            decoded.extend([value] * run)
        else:
            decoded.append(value)
    palette_marker = pcx.rfind(b"\x0c")
    palette = pcx[palette_marker + 1 : palette_marker + 769]
    rgba = bytearray()
    for y in range(height):
        row = decoded[y * bytes_per_line : y * bytes_per_line + width]
        for pixel in row:
            start = pixel * 3
            rgba.extend(palette[start : start + 3])
            rgba.append(0 if pixel == 0 else 255)
    path = output / "bpower_numbers_0_to_9.png"
    write_png(path, width, height, bytes(rgba))
    definition = data[text_offset:].decode("cp1252", errors="replace")
    glyph_starts = [0, 28, 57, 85, 114, 143, 172, 201, 233, 264]
    glyph_widths = [28] * 10
    digit_files = []
    for digit, (start, glyph_width) in enumerate(zip(glyph_starts, glyph_widths)):
        digit_rgba = crop_rgba(bytes(rgba), width, (start, 0, start + glyph_width, height))
        digit_path = output / f"bpower_number_{digit}.png"
        write_png(digit_path, glyph_width, height, digit_rgba)
        digit_files.append(digit_path.name)
    return {
        "size": [width, height], "png": path.name, "digits": digit_files,
        "glyph_starts": glyph_starts, "glyph_widths": glyph_widths,
        "definition": definition,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("sff", type=Path)
    parser.add_argument("font", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "source_sff": str(args.sff),
        "source_font": str(args.font),
        "sprites": extract_sff(args.sff, args.output),
        "power_font": extract_power_font(args.font, args.output),
    }
    (args.output / "hud_asset_manifest.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"BBB1I_HUD_EXTRACT_PASS: {len(report['sprites'])} sprites + power-number atlas")


if __name__ == "__main__":
    main()
