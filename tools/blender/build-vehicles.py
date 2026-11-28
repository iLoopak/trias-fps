"""Model the TRIAS Skimmer and Bastion vehicles. Run with Blender --background --python.

Visual meshes only: server movement, collision and the turret mount (`Weapon housing`) are unchanged.
Coordinates are Unity-style metres (x right, y up, z forward) with the vehicle root on the ground.
Team colour is limited to one stripe per side; the hull reads as graphite and fired ceramic.
"""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band


def slab(name, outline, x, half_width, finish, chamfer=.04, bevel=.012):
    """Extrude a side profile [(z, y), ...] across x with chamfered side faces."""
    pts = [(y, z) for z, y in outline]
    if sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(pts, pts[1:] + pts[:1])) < 0:
        pts.reverse()
    n = len(pts)

    def inset(c):
        out = []
        for i in range(n):
            p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
            normals = []
            for a, b in ((p0, p1), (p1, p2)):
                dy, dz = b[0] - a[0], b[1] - a[1]
                length = math.hypot(dy, dz) or 1
                normals.append((-dz / length, dy / length))
            my, mz = normals[0][0] + normals[1][0], normals[0][1] + normals[1][1]
            scale = c / max(.35, 1 + normals[0][0] * normals[1][0] + normals[0][1] * normals[1][1])
            out.append((p1[0] + my * scale, p1[1] + mz * scale))
        return out

    rings = [(x - half_width, inset(chamfer)), (x - half_width + chamfer, pts),
             (x + half_width - chamfer, pts), (x + half_width, inset(chamfer))]
    verts = [(xx, y, z) for xx, ring in rings for y, z in ring]
    faces = [tuple(reversed(range(n)))]
    for r in range(3):
        for i in range(n):
            a, b = r * n + i, r * n + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple(3 * n + i for i in range(n)))
    art.surface(name, verts, faces, finish, bevel)


def wheel(name, x, y, z, radius, width, side):
    """Tyre and recessed alloy hub on an axle along x."""
    sides = 28
    verts, faces = [], []
    sections = ((-.5, .86), (-.42, 1), (.42, 1), (.5, .86), (.5, .56), (.40, .5), (-.40, .5), (-.5, .56))
    for offset, r in sections:
        for i in range(sides):
            a = i * math.tau / sides
            verts.append((x + offset * width, y + math.sin(a) * radius * r, z + math.cos(a) * radius * r))
    rings = len(sections)
    for ring in range(rings):
        nxt = (ring + 1) % rings
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((ring * sides + i, ring * sides + j, nxt * sides + j, nxt * sides + i))
    art.surface(name + " tyre", verts, faces, "Fabric", .006)
    hub = x + side * width * .26
    verts = []
    for offset, r in ((-.18, .48), (.18, .48), (.18, .20), (-.18, .20)):
        for i in range(12):
            a = i * math.tau / 12
            verts.append((hub + offset * width, y + math.sin(a) * radius * r, z + math.cos(a) * radius * r))
    faces = [(ring * 12 + i, ring * 12 + (i + 1) % 12, ((ring + 1) % 4) * 12 + (i + 1) % 12, ((ring + 1) % 4) * 12 + i)
             for ring in range(4) for i in range(12)]
    art.surface(name + " hub", verts, faces, "Alloy", .004)


def skimmer():
    """Light skimmer: a low wedge chassis, a fired-ceramic rear shell split from the canopy by one
    phase gap, and a turret pedestal standing in that gap."""
    art.group = "Skimmer"
    slab("Graphite chassis", [(-1.62, .30), (-1.56, .62), (-1.10, .76), (.90, .74), (1.48, .56), (1.62, .38),
                              (1.44, .24), (-1.40, .22)], 0, .80, "Graphite", .07, .015)
    slab("Ceramic rear shell", [(-1.52, .72), (-1.44, 1.00), (-.62, 1.10), (-.40, 1.02), (-.40, .72)], 0, .74, "Ceramic", .09, .018)
    slab("Ceramic nose", [(.88, .72), (.88, .84), (1.36, .66), (1.58, .46), (1.50, .40), (1.10, .56)], 0, .76, "Ceramic", .08, .016)
    slab("Canopy", [(.30, .74), (.34, 1.02), (.62, 1.06), (.92, .84), (.92, .74)], 0, .50, "Visor", .10, .012)
    slab("Canopy frame", [(.26, .72), (.26, 1.04), (.34, 1.08), (.34, .72)], 0, .54, "Graphite", .03, .008)
    slab("Phase gap seam", [(-.38, .74), (-.38, .96), (-.34, .96), (-.34, .74)], 0, .66, "Signal", .01, 0)
    slab("Turret pedestal", [(-.30, .74), (-.26, 1.30), (.24, 1.30), (.28, .74)], 0, .34, "Graphite", .05, .012)
    band("Turret bearing", (0, 1.28, 0), [(0, .30, .30, 0), (.10, .27, .27, 0)], "Alloy", 24, 1.0)
    for side in (-1, 1):
        slab("Team stripe", [(-1.30, .50), (-1.30, .58), (1.10, .55), (1.10, .47)], side * .81, .012, "Faction", .004, 0)
        for z in (-1.05, 1.05):
            wheel("Skimmer wheel", side * .86, .30, z, .30, .26, side)
            slab("Wheel fender", [(z - .42, .52), (z - .32, .66), (z + .32, .66), (z + .42, .52)], side * .86, .16, "Graphite", .03, .01)
        slab("Rear lamp", [(-1.60, .60), (-1.60, .66), (-1.56, .66), (-1.56, .60)], side * .55, .12, "Signal", .01, 0)
        # Raked tail fin: one per side, it breaks the box read of the rear shell from every angle.
        slab("Raked tail fin", [(-1.50, 1.00), (-1.66, 1.30), (-1.56, 1.32), (-1.10, 1.06)], side * .60, .03, "Graphite", .01, .006)
        slab("Side intake", [(.62, .56), (.62, .70), (.86, .72), (.86, .56)], side * .80, .05, "Fabric", .015, .004)
        slab("Intake lip", [(.60, .70), (.60, .74), (.88, .76), (.88, .72)], side * .81, .06, "Alloy", .01, .003)
        slab("Headlamp", [(1.50, .50), (1.56, .56), (1.58, .54), (1.53, .48)], side * .52, .10, "Signal", .01, 0)


def bastion():
    """Heavy bastion: a tall graphite hull, three fractured ceramic side plates held apart by
    visible gaps, a raked ceramic glacis and six wheels."""
    art.group = "Bastion"
    slab("Graphite hull", [(-2.34, .42), (-2.30, 1.26), (-1.92, 1.42), (1.56, 1.42), (2.30, .98), (2.34, .48),
                           (1.94, .32), (-1.94, .32)], 0, 1.18, "Graphite", .12, .02)
    slab("Ceramic glacis", [(1.50, 1.36), (1.56, 1.46), (2.40, .98), (2.42, .86), (2.30, .86)], 0, 1.12, "Ceramic", .10, .02)
    slab("Vision slit", [(1.62, 1.36), (1.66, 1.40), (1.98, 1.22), (1.94, 1.18)], 0, .64, "Visor", .02, .006)
    slab("Rear door", [(-2.38, .55), (-2.38, 1.18), (-2.30, 1.18), (-2.30, .55)], 0, .62, "Ceramic", .03, .01)
    band("Turret ring", (0, 1.40, -.15), [(0, .68, .68, 0), (.16, .64, .64, 0), (.30, .56, .56, 0)], "Alloy", 28, 1.0)
    for side in (-1, 1):
        for z0, z1 in ((-2.10, -.86), (-.74, .46), (.58, 1.92)):
            slab("Fractured side plate", [(z0, .60), (z0 + .04, 1.28), (z1 - .04, 1.28), (z1, .60)], side * 1.24, .07, "Ceramic", .03, .012)
        slab("Team stripe", [(-.70, 1.12), (-.70, 1.20), (.42, 1.20), (.42, 1.12)], side * 1.315, .01, "Faction", .004, 0)
        slab("Plate gap glow", [(-.81, .80), (-.81, 1.10), (-.79, 1.10), (-.79, .80)], side * 1.22, .04, "Signal", .004, 0)
        for z in (-1.60, 0, 1.60):
            wheel("Bastion wheel", side * 1.30, .42, z, .42, .36, side)
        slab("Track guard", [(-2.05, .86), (-2.05, .92), (2.05, .92), (2.05, .86)], side * 1.30, .22, "Graphite", .02, .008)
        slab("Headlamp", [(2.28, 1.00), (2.33, 1.06), (2.36, 1.03), (2.31, .97)], side * .78, .12, "Signal", .01, 0)
        slab("Tow hook", [(2.30, .44), (2.30, .56), (2.46, .56), (2.46, .44)], side * .62, .06, "Alloy", .01, .004)
        # Smoke dischargers on the turret ring cheeks, three tubes per side angled forward.
        for i in range(3):
            band("Smoke discharger", (side * (.62 + i * .1), 1.60, .52 - i * .07), [(0, .045, .045, 0), (.20, .045, .045, .06)], "Alloy", 10, 1.0)
        slab("Tail lamp", [(-2.36, 1.10), (-2.36, 1.16), (-2.30, 1.16), (-2.30, 1.10)], side * .86, .08, "Amber", .01, 0)
    # Roof: three fired-ceramic armour plates held apart by gaps, a commander's hatch off the axis,
    # rear stowage and two whip antennas, so the large hull top has a large-medium-small rhythm.
    for z0, z1 in ((-1.86, -.92), (-.80, .34), (.86, 1.50)):
        slab("Roof armour plate", [(z0, 1.42), (z0 + .03, 1.49), (z1 - .03, 1.49), (z1, 1.42)], 0, .98, "Ceramic", .03, .01)
    slab("Roof gap light", [(-.88, 1.42), (-.88, 1.46), (-.84, 1.46), (-.84, 1.42)], 0, .80, "Signal", .004, 0)
    band("Commander hatch", (.62, 1.49, -1.30), [(0, .26, .26, 0), (.08, .24, .24, 0), (.12, .16, .16, 0)], "Graphite", 16, 1.0)
    slab("Stowage bin", [(-2.24, 1.42), (-2.24, 1.72), (-1.90, 1.74), (-1.88, 1.42)], 0, .90, "Secondary", .04, .012)
    for x in (-.58, .58):
        slab("Stowage strap", [(-2.26, 1.40), (-2.26, 1.76), (-2.22, 1.76), (-2.22, 1.40)], x, .05, "Graphite", .005, 0)
    for x, z in ((-.96, -1.70), (.96, -1.80)):
        band("Antenna base", (x, 1.49, z), [(0, .05, .05, 0), (.10, .04, .04, 0)], "Alloy", 10, 1.0)
        band("Whip antenna", (x, 1.59, z), [(0, .012, .012, 0), (1.15, .006, .006, -.18)], "Graphite", 6, 1.0)


art.reset()
skimmer()
bastion()
art.finish("Vehicles", "Vehicles/Vehicles.fbx", {"Skimmer": (-2.0, 0, 0), "Bastion": (2.2, 0, 0)},
           camera_at=(9.5, -8.5, 6.5), camera_target=(0, 0, .9), camera_width=9.5, resolution=(1400, 900))
print("VEHICLES EXPORT", len(art.parts), "mounts")
