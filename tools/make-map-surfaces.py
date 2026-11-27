"""Generate tileable stone, timber, deck wear and refractory material textures."""
import math
from pathlib import Path
import random
import struct
import zlib

SIZE = 256
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "unity-vfx-preview/Assets/Resources/TRIAS/SurfaceDetail"


def field(columns, rows, seed):
    rng = random.Random(seed)
    values = [[rng.random() for _ in range(columns)] for _ in range(rows)]

    def sample(u, v):
        x, y = u * columns, v * rows
        i, j = math.floor(x), math.floor(y)
        tx, ty = x - i, y - j
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        row_a, row_b = values[j % rows], values[(j + 1) % rows]
        a = row_a[i % columns] * (1 - tx) + row_a[(i + 1) % columns] * tx
        b = row_b[i % columns] * (1 - tx) + row_b[(i + 1) % columns] * tx
        return a * (1 - ty) + b * ty

    return sample


def chunk(label, data):
    return struct.pack(">I", len(data)) + label + data + struct.pack(">I", zlib.crc32(label + data))


def save(name, material):
    image = bytearray()
    for y in range(SIZE):
        image.append(0)
        for x in range(SIZE):
            rgb = material(x / SIZE, y / SIZE)
            image.extend(max(0, min(255, round(value * 255))) for value in rgb)
    header = struct.pack(">2I5B", SIZE, SIZE, 8, 2, 0, 0, 0)
    (OUT / f"{name}.png").write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) +
                                   chunk(b"IDAT", zlib.compress(image, 9)) + chunk(b"IEND", b""))


broad = field(6, 6, 12)
medium = field(24, 21, 21)
fine = field(113, 109, 9)
long_grain = field(73, 4, 17)
soft_grain = field(36, 7, 31)
scratches = field(27, 207, 41)


def grey(value, tint):
    return tuple(value * channel for channel in tint)


def stone(u, v):
    weather = broad(u, v)
    mineral = medium(u, v)
    grain = fine(u, v)
    vein = abs(math.sin(math.tau * (v * 5 + .11 * math.sin(math.tau * u) + weather * .74)))
    color = .57 + .21 * mineral + .14 * grain + .10 * weather - .09 * (1 - vein) ** 9
    return grey(color, (1.0, .98, .93))


def timber(u, v):
    bend = (broad(u, v) - .5) * .047
    grain = long_grain(u + bend, v)
    splitting = max(0, (.24 - soft_grain(u + bend, v)) * 2.8)
    wood = .57 + .38 * grain + .10 * fine(u, v) - .17 * splitting
    return grey(wood, (1.0, .94, .85))


def deck(u, v):
    worn = broad(u, v)
    scratch = scratches(u, v)
    steel = .56 + worn * .26 + fine(u, v) * .095 + max(0, scratch - .62) * .28
    return grey(steel, (.96, .98, 1.0))


def ceramic(u, v):
    ash = broad(u, v)
    pitting = max(0, medium(u, v) * .7 + fine(u, v) * .3 - .63)
    surface = .55 + ash * .32 + fine(u, v) * .12 - pitting * .65
    return grey(surface, (1.0, .94, .89))


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for name, material in (("fractured-stone", stone), ("weathered-timber", timber),
                           ("brushed-deck", deck), ("refractory", ceramic)):
        save(name, material)
