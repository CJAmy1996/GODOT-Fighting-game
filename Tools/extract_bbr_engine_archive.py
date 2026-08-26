"""Extract Big Bang Beat Revolve's encrypted DxLib v3 engine archives.

Unlike the character picture archives, system.dat and define.dat use engine-
level paths and were omitted from the original project extraction.  DxLib v3's
12-byte XOR key can be reconstructed from the archive's known header fields.
"""

from __future__ import annotations

import argparse
import struct
from pathlib import Path


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def i32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<i", data, offset)[0]


def decrypt(data: bytes, key: bytes, position: int) -> bytes:
    return bytes(value ^ key[(position + index) % len(key)] for index, value in enumerate(data))


def recover_v3_key(archive: bytes) -> bytes:
    key = bytearray(archive[:12])
    key[0] ^= ord("D")
    key[1] ^= ord("X")
    key[2] ^= 3
    key[8] ^= 0x18
    key0 = u32(key, 0)
    index_offset = u32(archive, 12) ^ key0
    if not 0x18 < index_offset < len(archive):
        raise ValueError("archive does not have a recoverable DxLib v3 header")
    index_size = len(archive) - index_offset
    key[4] ^= index_size & 0xFF
    key[5] ^= (index_size >> 8) & 0xFF
    key[6] ^= (index_size >> 16) & 0xFF
    header = decrypt(archive[:0x18], key, 0)
    if header[:4] != b"DX\x03\x00":
        raise ValueError("recovered key did not produce a DxLib v3 header")
    return bytes(key)


def unpack_dxa(data: bytes) -> bytes:
    if len(data) < 9:
        raise ValueError("truncated compressed entry")
    output_size, compressed_size = struct.unpack_from("<II", data, 0)
    marker = data[8]
    source = 9
    source_end = min(len(data), compressed_size)
    output = bytearray()
    while source < source_end:
        value = data[source]
        source += 1
        if value != marker:
            output.append(value)
            continue
        flags = data[source]
        source += 1
        if flags == marker:
            output.append(marker)
            continue
        if flags > marker:
            flags -= 1
        count = flags >> 3
        if flags & 4:
            count |= data[source] << 5
            source += 1
        count += 4
        mode = flags & 3
        if mode == 0:
            distance = data[source]
            source += 1
        elif mode == 1:
            distance = struct.unpack_from("<H", data, source)[0]
            source += 2
        elif mode == 2:
            distance = struct.unpack_from("<H", data, source)[0] | (data[source + 2] << 16)
            source += 3
        else:
            raise ValueError("invalid DxLib back-reference")
        distance += 1
        for _ in range(count):
            output.append(output[-distance])
    if len(output) != output_size:
        raise ValueError(f"decoded {len(output)} bytes; expected {output_size}")
    return bytes(output)


def extract(archive_path: Path, output_dir: Path) -> list[Path]:
    archive = archive_path.read_bytes()
    key = recover_v3_key(archive)
    header = decrypt(archive[:0x18], key, 0)
    index_size = u32(header, 4)
    base_offset = u32(header, 8)
    index_offset = u32(header, 12)
    file_table = u32(header, 16)
    directory_table = u32(header, 20)
    index = decrypt(archive[index_offset:index_offset + index_size], key, index_offset)
    written: list[Path] = []

    def name_at(table_offset: int) -> str:
        text_offset = struct.unpack_from("<H", index, table_offset)[0] * 4 + 4
        start = table_offset + text_offset
        end = index.find(b"\0", start)
        return index[start:end].decode("cp932")

    def visit(directory_offset: int, parent: Path) -> None:
        position = directory_table + directory_offset
        own_file, parent_directory, count, first_file = struct.unpack_from("<iiii", index, position)
        current = parent
        if own_file != -1 and parent_directory != -1:
            current /= name_at(u32(index, file_table + own_file))
        entry_position = file_table + first_file
        for _ in range(count):
            name_offset = u32(index, entry_position)
            attributes = u32(index, entry_position + 4)
            data_offset = u32(index, entry_position + 0x20)
            if attributes & 0x10:
                visit(data_offset, current)
            else:
                size = u32(index, entry_position + 0x24)
                packed_size = i32(index, entry_position + 0x28)
                stored_size = size if packed_size == -1 else packed_size
                absolute_offset = base_offset + data_offset
                payload = decrypt(
                    archive[absolute_offset:absolute_offset + stored_size], key, absolute_offset
                )
                if packed_size != -1:
                    payload = unpack_dxa(payload)
                destination = output_dir / current / name_at(name_offset)
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(payload)
                written.append(destination)
            entry_position += 0x2C

    visit(0, Path())
    return written


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    files = extract(args.archive, args.output)
    print(f"extracted {len(files)} files from {args.archive.name}")
    for path in files:
        # Windows' legacy console encoding cannot necessarily represent the
        # Japanese filenames stored in Revolve's Shift-JIS archive index.
        print(str(path).encode("ascii", errors="backslashreplace").decode("ascii"))


if __name__ == "__main__":
    main()
