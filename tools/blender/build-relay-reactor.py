"""Relay Station reactor crown: hero meshes for the suspended induction stages.

Each crown sector is a tapered ceramic blade with an asymmetric, wing-like section and a sharp
upper edge. The blade is broken into three pieces by stabilised fracture gaps (each piece shifted a
few centimetres), while a continuous graphite spine runs behind it through the gaps: the load path
continues where the ceramic does not. Alloy clamps bridge each gap without touching the ceramic.
One thin amber service line runs along the underside of the spine.

Mounts (Unity metres, Y up, +Z forward, sector centred on +Z):
  CrownInner / CrownMid / CrownOuter   one 96 degree sector at the stage radius
  AnchorStrut                          tapered graphite strut with a ceramic fairing, in foot space

blender --background --factory-startup --python-exit-code 1 --python tools/blender/build-relay-reactor.py -- [--preview <dir>]
"""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art  # noqa: E402

SWEEP = 96.0
STEP = 2.0
# radius, blade height, blade thickness
STAGES = {"CrownInner": (4.75, .78, .26), "CrownMid": (7.4, 1.02, .33), "CrownOuter": (9.55, 1.22, .39)}
# Fracture positions along the sweep (0..1), and the shift of the following piece (radial, vertical).
FRACTURES = ((.36, .045, .03), (.68, -.03, -.045))


def blade_profile(h, t):
    """Closed section in (radial, vertical): convex glazed outer face, sharp upper edge, hollowed inner face."""
    return [(.12 * t, .5 * h), (.50 * t, .30 * h), (.58 * t, .02 * h), (.40 * t, -.33 * h),
            (.05 * t, -.5 * h), (-.24 * t, -.36 * h), (-.34 * t, -.02 * h), (-.20 * t, .34 * h)]


def taper(u):
    """Width/height scale along the sweep: full in the middle, drawn to a point at both tips."""
    edge = min(u, 1 - u) / .2
    return .22 + .78 * (1 - (1 - min(1, edge)) ** 2)


def sweep(name, radius, profile, a0, a1, finish, shift=(0, 0), tip=True, bevel=.012, curl=0.0):
    steps = max(2, math.ceil((a1 - a0) / STEP))
    verts, faces = [], []
    n = len(profile)
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        u = (a + SWEEP / 2) / SWEEP
        s = taper(u) if tip else 1.0
        lift = curl * (1 - min(1, min(u, 1 - u) / .2)) ** 2
        ang = math.radians(a)
        for dr, dy in profile:
            r = radius + shift[0] + dr * s
            verts.append((math.sin(ang) * r, dy * s + shift[1] + lift, math.cos(ang) * r))
    for i in range(steps):
        for j in range(n):
            a, b = i * n + j, i * n + (j + 1) % n
            faces.append((a, a + n, b + n, b))
    faces.append(tuple(reversed(range(n))))
    faces.append(tuple(steps * n + j for j in range(n)))
    art.surface(name, verts, faces, finish, bevel, segments=2)


def crown(key, radius, h, t):
    art.group = key
    half = SWEEP / 2
    cuts = [-half] + [-half + SWEEP * f for f, _, _ in FRACTURES] + [half]
    shifts = [(0, 0)] + [(dr, dy) for _, dr, dy in FRACTURES]
    gap = math.degrees(.11 / radius)
    profile = blade_profile(h, t)
    for k in range(3):
        a0 = cuts[k] + (gap / 2 if k > 0 else 0)
        a1 = cuts[k + 1] - (gap / 2 if k < 2 else 0)
        sweep(f"Ceramic blade piece {k + 1}", radius, profile, a0, a1, "Ceramic", shifts[k], curl=h * .16)
    # Continuous graphite spine behind the blade, inset from the hollow face.
    spine = [(-.30 * t, .16 * h), (-.30 * t, -.18 * h), (-.62 * t, -.2 * h), (-.66 * t, .18 * h)]
    sweep("Graphite load spine", radius, spine, -half * .9, half * .9, "Graphite", tip=True, bevel=.01)
    # Underside service line, recessed into the spine.
    line = [(-.40 * t, -.205 * h), (-.40 * t, -.225 * h), (-.56 * t, -.225 * h), (-.56 * t, -.205 * h)]
    sweep("Amber service line", radius, line, -half * .55, half * .55, "Amber", tip=False, bevel=0)
    # Alloy clamps bridge each fracture around the spine, clear of the ceramic.
    for f, _, _ in FRACTURES:
        a = math.radians(-half + SWEEP * f)
        span = math.degrees(.42 / radius)
        clamp = [(-.27 * t, .26 * h), (-.27 * t, -.28 * h), (-.74 * t, -.30 * h), (-.78 * t, .27 * h)]
        sweep("Alloy fracture clamp", radius, clamp, math.degrees(a) - span / 2, math.degrees(a) + span / 2,
              "Alloy", tip=False, bevel=.008)
        # Two retaining pins per clamp read as the mechanical bridge at combat distance.
        for side in (-1, 1):
            pa = a + side * math.radians(span * .3)
            pr = radius - .52 * t
            x, z = math.sin(pa) * pr, math.cos(pa) * pr
            art.band("Clamp retaining pin", (x, -.0, z), [(.31 * h, .035, .035, 0), (-.33 * h, .035, .035, 0)], "Alloy", sides=10)


def strut():
    """Anchor strut from the deck foot (0,-7.3,6.6) to the crown seat (0,-2.8,3.8), in foot space."""
    art.group = "AnchorStrut"
    a = (0.0, -7.3, 6.6)
    b = (0.0, -2.8, 3.8)
    d = [b[i] - a[i] for i in range(3)]
    length = math.sqrt(sum(c * c for c in d))
    fwd = [c / length for c in d]
    up = (0.0, fwd[2], -fwd[1])  # perpendicular in the YZ plane
    side = (1.0, 0.0, 0.0)

    def section(t, w, hgt, chamfer):
        c = [a[i] + d[i] * t for i in range(3)]
        pts = [(-w + chamfer, -hgt), (w - chamfer, -hgt), (w, -hgt + chamfer), (w, hgt - chamfer),
               (w - chamfer, hgt), (-w + chamfer, hgt), (-w, hgt - chamfer), (-w, -hgt + chamfer)]
        return [tuple(c[i] + side[i] * x + up[i] * y for i in range(3)) for x, y in pts]

    def loft(name, rings, finish, bevel):
        verts = [p for ring in rings for p in ring]
        n = len(rings[0])
        faces = [tuple(reversed(range(n)))]
        for r in range(len(rings) - 1):
            for j in range(n):
                p, q = r * n + j, r * n + (j + 1) % n
                faces.append((p, q, q + n, p + n))
        faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
        art.surface(name, verts, faces, finish, bevel, segments=2)

    loft("Tapered graphite strut", [section(0, .52, .46, .16), section(.12, .46, .42, .14),
                                    section(.86, .27, .27, .08), section(1, .22, .22, .06)], "Graphite", .02)
    # Ceramic fairing on the outer face, separated from the strut by a visible gap.
    fairing = []
    for t, w in ((.18, .30), (.3, .36), (.72, .24), (.82, .16)):
        c = [a[i] + d[i] * t for i in range(3)]
        lift = .47 - .2 * t
        fairing.append([tuple(c[i] + side[i] * x + up[i] * y for i in range(3))
                        for x, y in ((-w, lift), (w, lift), (w * .8, lift + .1), (-w * .8, lift + .1))])
    loft("Ceramic strut fairing", fairing, "Ceramic", .015)
    # Alloy foot collar where the strut meets the deck.
    c = a
    art.band("Alloy foot collar", (c[0], c[1] - .05, c[2]), [(.0, .66, .6, 0), (.35, .6, .55, 0), (.42, .5, .46, 0)], "Alloy", sides=8, corner=.6)


def main():
    art.reset()
    for key, (radius, h, t) in STAGES.items():
        crown(key, radius, h, t)
    strut()
    display = {"CrownInner": (0, 4, 0), "CrownMid": (0, 2, 0), "CrownOuter": (0, 0, 0), "AnchorStrut": (0, 7.3, -2)}
    art.finish("RelayReactor", "RelayReactor/RelayReactor.fbx", display, camera_at=(14, -16, 9),
               camera_target=(0, 0, 1.5), camera_width=24, resolution=(1600, 1100))


(Path(art.PROJECT) / "Assets/Resources/TRIAS/RelayReactor").mkdir(parents=True, exist_ok=True)
main()
