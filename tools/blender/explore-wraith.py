"""Render competing Wraith (Přízrak) helmet designs side by side. Exploration only; nothing is exported.

  Blender --background --python tools/blender/explore-wraith.py -- <output.png>

The winning design is copied into build-pilots.py. Coordinates follow the pilot helmet mount:
Unity metres, head centre at the origin, face towards +z.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band, fastener, panel, plate, vents


def face(name, outline, front, depth, finish, bevel=.006, side=1):
    points = [(side * x, y, z + front) for x, y, z in outline]
    plate(name, points if side == 1 else list(reversed(points)), 0, depth, finish, bevel)


def current():
    """Baseline: the shipped Wraith helmet."""
    band("Narrow graphite cap", (0, 0, -.029), [
        (-.106, .058, .075, .016), (-.039, .094, .108, .003), (.030, .098, .112, -.010),
        (.080, .084, .094, -.020), (.112, .054, .060, -.032), (.124, .024, .030, -.036)], "Graphite", 22, .8)
    face("Bullnose ceramic mask", [
        (-.079, -.036, .069), (-.040, -.102, .125), (.032, -.077, .129), (.083, -.008, .083),
        (.073, .057, .087), (-.053, .081, .099)], 0, .037, "Ceramic", .010)
    face("Slanted apertured mask", [
        (-.026, -.074, .133), (.012, -.072, .141), (.040, .036, .122), (.007, .057, .119)], 0, .019, "Visor", .004)
    face("Phase slit", [(-.012, -.066, .143), (.002, -.067, .145), (.025, .038, .129), (.013, .040, .128)], 0, .006, "Signal", .0018)
    band("Swept receiver", (-.100, .020, .010), [(-.040, .024, .050, .010), (.000, .030, .060, 0), (.040, .022, .048, -.016)], "Alloy", 14, .8)
    panel("Split antenna", -.110, .096, -.052, .016, .120, .030, "Graphite", -.30, .004)
    panel("Antenna terminal", -.128, .150, -.064, .016, .026, .026, "Faction", -.30)
    panel("Right filter grill", .060, -.042, .125, .043, .039, .017, "Faction", .26)


def cowl():
    """A: an infiltrator's cowl. A soft fabric hood frames a narrow fired-ceramic blade with one
    off-centre vertical optic; the hood's trailing edge gives a silhouette no other pilot has."""
    band("Cowl skull", (0, 0, -.030), [
        (-.118, .070, .082, .018), (-.060, .100, .112, .004), (.020, .106, .120, -.008),
        (.076, .094, .106, -.022), (.112, .064, .074, -.036), (.128, .030, .040, -.044)], "Graphite", 24, .78)
    band("Fabric hood", (0, 0, -.046), [
        (-.150, .090, .070, -.030), (-.090, .118, .110, -.006), (-.010, .124, .128, -.004),
        (.062, .116, .124, -.018), (.112, .086, .098, -.034), (.142, .040, .060, -.050)], "Fabric", 24, .72)
    # Trailing hood edge down the back of the neck.
    plate("Hood tail", [(-.070, -.020, None), (.070, -.020, None), (.050, -.200, None), (-.050, -.200, None)], -.130, .030, "Fabric", .010)
    face("Ceramic face blade", [
        (-.050, -.118, .112), (.050, -.118, .112), (.066, -.020, .118), (.058, .070, .108),
        (.000, .094, .114), (-.058, .070, .108), (-.066, -.020, .118)][::-1], 0, .040, "Ceramic", .010)
    face("Blade chin facet", [(-.040, -.124, .116), (.040, -.124, .116), (.020, -.150, .090), (-.020, -.150, .090)][::-1], 0, .030, "Ceramic", .006)
    face("Vertical optic", [(.010, -.020, .156), (.030, -.020, .156), (.030, .058, .148), (.010, .058, .148)][::-1], 0, .012, "Visor", .003)
    face("Optic core", [(.016, .005, .160), (.024, .005, .160), (.024, .048, .153), (.016, .048, .153)][::-1], 0, .004, "Signal", .001)
    face("Blade seam", [(-.004, -.110, .154), (.002, -.110, .154), (.002, .080, .150), (-.004, .080, .150)][::-1], 0, .004, "Graphite", .001)
    vents("Breath slots", -.026, -.086, .155, 3, .012, .024, "Graphite")
    panel("Faction tab", -.074, .040, .080, .020, .050, .030, "Faction", -.2)


def cowl_faceted():
    """A2: the cowl with a three-facet fired-ceramic blade, a lit hood rim and a respirator ridge.
    The blade folds back at its edges like a keel, so it catches light as a form rather than a slab."""
    band("Cowl skull", (0, 0, -.030), [
        (-.118, .070, .082, .018), (-.060, .100, .112, .004), (.020, .106, .120, -.008),
        (.076, .094, .106, -.022), (.112, .064, .074, -.036), (.128, .030, .040, -.044)], "Graphite", 24, .78)
    band("Fabric hood", (0, 0, -.050), [
        (-.150, .092, .070, -.034), (-.090, .120, .110, -.008), (-.010, .126, .128, -.006),
        (.062, .118, .124, -.020), (.112, .088, .098, -.036), (.144, .040, .060, -.052)], "Fabric", 24, .72)
    plate("Hood tail", [(-.072, -.020, None), (.072, -.020, None), (.046, -.215, None), (-.046, -.215, None)], -.132, .030, "Fabric", .010)
    # Tried and rejected: a lofted hood rim read as a flat beige board framing the face.
    centre = [(-.026, -.124, .140), (.026, -.124, .140), (.030, .060, .136), (.000, .092, .128), (-.030, .060, .136)]
    face("Blade centre facet", centre[::-1], 0, .030, "Ceramic", .006)
    for side in (-1, 1):
        face("Blade keel facet", [(.026, -.124, .136), (.062, -.096, .104), (.074, -.010, .100), (.062, .060, .098), (.030, .060, .134)][::-1],
             0, .030, "Ceramic", .006, side)
    face("Blade chin facet", [(-.028, -.126, .138), (.028, -.126, .138), (.018, -.154, .100), (-.018, -.154, .100)][::-1], 0, .026, "Ceramic", .006)
    face("Vertical optic", [(.032, -.030, .142), (.052, -.034, .126), (.054, .046, .122), (.034, .050, .139)][::-1], 0, .010, "Visor", .003)
    face("Optic core", [(.038, -.010, .145), (.046, -.012, .139), (.047, .036, .136), (.039, .038, .142)][::-1], 0, .003, "Signal", .001)
    face("Respirator ridge", [(-.020, -.092, .150), (.020, -.092, .150), (.016, -.114, .148), (-.016, -.114, .148)][::-1], 0, .010, "Graphite", .003)
    panel("Faction tab", -.086, .030, .074, .020, .050, .030, "Faction", -.2)


def split_mask():
    """B: a split ceramic mask. Two fired halves are held apart by a phase gap and offset in depth
    and height; the visor shows only through the gap. Swept rear fins carry the silhouette."""
    band("Low skull", (0, 0, -.030), [
        (-.110, .064, .078, .016), (-.040, .096, .110, .002), (.030, .100, .114, -.010),
        (.082, .084, .096, -.022), (.112, .052, .060, -.034)], "Graphite", 22, .8)
    face("Visor behind gap", [(-.030, -.060, .124), (.030, -.060, .124), (.030, .050, .118), (-.030, .050, .118)][::-1], 0, .020, "Visor", .004)
    face("Left mask half", [(-.012, -.112, .128), (-.012, .072, .118), (-.060, .070, .100), (-.088, .000, .080), (-.070, -.090, .090)][::-1],
         0, .036, "Ceramic", .009)
    face("Right mask half", [(.016, -.096, .140), (.074, -.074, .102), (.094, .010, .090), (.068, .090, .108), (.016, .092, .132)],
         0, .036, "Ceramic", .009)
    face("Phase gap light", [(-.010, -.100, .138), (-.002, -.100, .140), (.010, .086, .134), (.002, .086, .132)][::-1], 0, .004, "Signal", .001)
    for side in (-1, 1):
        plate("Swept rear fin", [(side * .060, .080, None), (side * .074, .086, None), (side * .110, .030, None), (side * .100, .020, None)][::side or 1],
              -.050, .060, "Secondary", .006)
    panel("Faction strip", 0, .098, .010, .020, .010, .150, "Faction")


def hunter():
    """C: a hunter's optic head. A smooth graphite shell, a ceramic jaw and a three-lens rangefinder
    cluster mounted asymmetrically over the right eye; the left side stays deliberately quiet."""
    band("Hunter shell", (0, 0, -.022), [
        (-.100, .066, .080, .020), (-.040, .098, .114, .006), (.030, .102, .118, -.008),
        (.080, .088, .098, -.020), (.116, .056, .062, -.032), (.130, .024, .028, -.038)], "Graphite", 24, .78)
    face("Ceramic jaw", [(-.080, -.040, .080), (-.040, -.120, .108), (.040, -.120, .108), (.080, -.040, .080),
                         (.050, -.020, .118), (-.050, -.020, .118)][::-1], 0, .040, "Ceramic", .010)
    face("Brow visor", [(-.082, .000, .104), (.020, -.004, .126), (.020, .040, .122), (-.076, .044, .100)][::-1], 0, .016, "Visor", .004)
    for i, (x, y, r, l) in enumerate(((.052, .020, .026, .060), (.088, .044, .015, .040), (.084, -.004, .012, .036))):
        band("Lens barrel", (x, y, .090), [(0, r, r, 0), (.001, r, r, l)], "Alloy", 16, 1.0)
    for x, y, r, l in ((.052, .020, .019, .062), (.088, .044, .010, .042), (.084, -.004, .008, .038)):
        face("Lens", [(x + math.cos(a) * r, y + math.sin(a) * r, .090 + l) for a in (i * math.tau / 10 for i in range(10))], 0, .004, "Signal", .001)
    panel("Faction collar", -.070, -.060, .070, .030, .040, .020, "Faction", .2)


art.reset()
designs = [("Current", current), ("A cowl", cowl), ("A2 cowl faceted", cowl_faceted)]
for i, (label, build) in enumerate(designs):
    art.group = label
    build()
    band("Neck pressure seal", (0, -.128, -.021), [(-.052, .044, .043, 0), (.01, .055, .057, 0), (.032, .059, .055, 0)], "Fabric", 14)
    for item in art.parts[label]:
        item.location.x += (i - (len(designs) - 1) / 2) * .42

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1800, 760
scene.world = bpy.data.worlds.new("Studio")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.16, .18, .22, 1)
scene.world.node_tree.nodes["Background"].inputs[1].default_value = .5


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


view = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = view[0] if view else "/tmp/wraith.png"
angle = float(view[1]) if len(view) > 1 else 28
camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
scene.collection.objects.link(camera)
r = 2.2
camera.location = (math.sin(math.radians(angle)) * r * .55, -math.cos(math.radians(angle)) * r, .12)
aim(camera, (0, 0, 0))
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.75
scene.camera = camera
for name, loc, power, color in (("Key", (-1.5, -2.2, 1.6), 260, (1, .88, .74)), ("Fill", (1.8, -1.4, .3), 90, (.6, .75, 1)),
                                ("Rim", (.3, 2.0, 1.4), 300, (.6, .8, 1))):
    light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
    light.data.energy, light.data.color, light.data.size = power, color, 1.2
    scene.collection.objects.link(light)
    light.location = loc
    aim(light, (0, 0, 0))
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("WRAITH EXPLORATION", out)
