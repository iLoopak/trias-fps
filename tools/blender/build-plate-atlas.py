"""TRIAS fractured-ceramic plate atlas.

Models a periodic field of bevelled ceramic plates in Blender and bakes it, through orthographic
data renders, into two tileable maps consumed by the Unity `TRIAS/Architecture` shader:

  <name>-normal.png  tangent-space normal (X right, Y up), non-colour
  <name>-data.png    R plate id (0-.04 hardware, .08-.86 plate, .92-1 phase plate)
                     G ambient occlusion, B edge (chamfer) mask, A height

The plate layout is an architectural panel grid with shared column lines (a masonry stagger\nread as brick in review). Some plates are split into tiles and
some are "phase plates": rotated, raised and isolated by a wider dark gap — the stabilised fracture
that gives TRIAS ceramics their identity at eye level. Geometry is repeated 3x3 so seams wrap.

blender --background --factory-startup --python-exit-code 1 --python tools/blender/build-plate-atlas.py -- \
  [--variant wall|deck] [--resolution 2048] [--preview <dir>] [--blend <file>]
"""
import argparse
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "unity-vfx-preview" / "Assets" / "Resources" / "TRIAS" / "Architecture"
P = 4.0

VARIANTS = {
    # Large wall cladding: tall rows, generous plates, sparse phase plates.
    "wall": dict(seed=11, rows=[.8, 1.0, 1.333], widths=[.8, 1.0, 1.2], merge=.35,
                 seam=.016, split=.12, phase=.09, inset=.14, fasteners=.18, bevel=.009),
    # Walking deck: square-ish tiles, finer rhythm, more fasteners, tighter seams.
    "deck": dict(seed=5, rows=[.5, .8, 1.0], widths=[.5, .8, 1.0], merge=.25,
                 seam=.012, split=.18, phase=.06, inset=.08, fasteners=.34, bevel=.006),
}


def arguments():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--variant", default="wall", choices=sorted(VARIANTS))
    parser.add_argument("--resolution", type=int, default=2048)
    parser.add_argument("--preview")
    parser.add_argument("--blend")
    return parser.parse_args(argv)


def partition(rng, total, choices, minimum):
    parts, used = [], 0.0
    while total - used > minimum + 1e-6:
        size = rng.choice(choices)
        if total - (used + size) < minimum:
            size = total - used
        parts.append(size)
        used += size
    if parts:
        parts[-1] += total - sum(parts)
    return parts


def layout(spec, rng):
    """Architectural panel grid: shared column lines (no masonry stagger), merged wide panels,
    occasional tile subdivisions and isolated phase plates."""
    columns = partition(rng, P, spec["widths"], min(spec["widths"]))
    edges = [0.0]
    for w in columns:
        edges.append(edges[-1] + w)
    plates = []
    y = 0.0
    for h in partition(rng, P, spec["rows"], min(spec["rows"])):
        c = 0
        while c < len(columns):
            span = 1
            while c + span < len(columns) and rng.random() < spec["merge"]:
                span += 1
            x, w = edges[c], edges[c + span] - edges[c]
            roll = rng.random()
            if roll < spec["split"]:
                cols = 2 if w < 1.6 else 3
                rows = 2 if h > .9 else 1
                for i in range(cols):
                    for j in range(rows):
                        plates.append(dict(x=x + w * i / cols, y=y + h * j / rows, w=w / cols, h=h / rows, kind="tile"))
            elif roll < spec["split"] + spec["phase"]:
                plates.append(dict(x=x, y=y, w=w, h=h, kind="phase"))
            else:
                plates.append(dict(x=x, y=y, w=w, h=h, kind="plate", inset=rng.random() < spec["inset"]))
            c += span
        y += h
    return plates


def material(name, color, roughness, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


def box(name, x0, y0, x1, y1, z0, z1, mat, bevel, segments=2):
    mesh = bpy.data.meshes.new(name)
    v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    mesh.from_pydata(v, [], f)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if bevel > 0:
        mod = obj.modifiers.new("Chamfer", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.harden_normals = True
    return obj


def cylinder(name, x, y, z0, z1, radius, mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=radius, depth=z1 - z0, location=(x, y, (z0 + z1) / 2))
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Chamfer", "BEVEL")
    mod.width = radius * .3
    mod.segments = 2
    return obj


def build(spec, rng):
    ceramic = material("Ceramic", (.80, .76, .69), .34)
    graphite = material("Graphite", (.03, .035, .04), .7)
    alloy = material("Alloy", (.62, .63, .64), .32, 1.0)
    depth = .035
    objects = []
    base = box("Seam floor", -P, -P, 2 * P, 2 * P, -depth - .01, -depth, graphite, 0)
    base["plate_id"] = .06
    for index, plate in enumerate(layout(spec, rng)):
        seam = spec["seam"] * (2.4 if plate["kind"] == "phase" else 1.0)
        x0, y0 = plate["x"] + seam / 2, plate["y"] + seam / 2
        x1, y1 = plate["x"] + plate["w"] - seam / 2, plate["y"] + plate["h"] - seam / 2
        top = rng.uniform(0, .0035)
        if plate["kind"] == "phase":
            top += .009
        if plate["kind"] == "tile":
            top -= .002
        obj = box(f"Plate {index}", x0, y0, x1, y1, -depth, top, ceramic, spec["bevel"])
        if plate["kind"] == "phase":
            cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            obj.data.transform(__import__("mathutils").Matrix.Translation((-cx, -cy, 0)))
            obj.location = (cx + rng.uniform(-.01, .01), cy + rng.uniform(-.01, .01), 0)
            obj.rotation_euler = (math.radians(rng.uniform(-.45, .45)), math.radians(rng.uniform(-.45, .45)), math.radians(rng.uniform(-.7, .7)))
            obj["plate_id"] = rng.uniform(.92, 1.0)
        else:
            obj["plate_id"] = rng.uniform(.08, .86)
        objects.append(obj)
        if plate.get("inset"):
            m = min(plate["w"], plate["h"]) * .22
            cut = box(f"Inset cutter {index}", x0 + m, y0 + m, x1 - m, y1 - m, top - .004, top + .05, ceramic, 0)
            mod = obj.modifiers.new("Inset", "BOOLEAN")
            mod.operation = "DIFFERENCE"
            mod.object = cut
            mod.solver = "EXACT"
            obj.modifiers.move(len(obj.modifiers) - 1, 0)
            cut.hide_render = True
            cut.hide_viewport = True
        if plate["kind"] != "phase" and rng.random() < spec["fasteners"] and plate["w"] > .45 and plate["h"] > .4:
            inset = .07
            for (fx, fy) in [(x0 + inset, y0 + inset), (x1 - inset, y1 - inset)] if rng.random() < .5 else \
                    [(x0 + inset, y1 - inset), (x1 - inset, y0 + inset)]:
                bolt = cylinder(f"Fastener {index}", fx, fy, top - .004, top + .0025, .011, alloy)
                bolt["plate_id"] = rng.uniform(0, .04)
                objects.append(bolt)
    # Periodic copies so every seam and bevel wraps across the tile boundary.
    for obj in list(objects):
        for dx in (-P, 0, P):
            for dy in (-P, 0, P):
                if dx == 0 and dy == 0:
                    continue
                copy = obj.copy()
                copy.location = (obj.location.x + dx, obj.location.y + dy, obj.location.z)
                bpy.context.scene.collection.objects.link(copy)
                for mod in copy.modifiers:
                    if mod.type == "BOOLEAN" and mod.object is not None:
                        cutter = mod.object.copy()
                        cutter.location = (mod.object.location.x + dx, mod.object.location.y + dy, mod.object.location.z)
                        bpy.context.scene.collection.objects.link(cutter)
                        mod.object = cutter
    return depth


def data_material(kind, depth):
    m = bpy.data.materials.new("Data " + kind)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    links.new(emit.outputs[0], out.inputs[0])
    geo = nodes.new("ShaderNodeNewGeometry")
    if kind == "normal":
        mul = nodes.new("ShaderNodeVectorMath"); mul.operation = "MULTIPLY_ADD"
        mul.inputs[1].default_value = (.5, .5, .5); mul.inputs[2].default_value = (.5, .5, .5)
        links.new(geo.outputs["Normal"], mul.inputs[0])
        links.new(mul.outputs[0], emit.inputs["Color"])
    elif kind == "data":
        plate = nodes.new("ShaderNodeAttribute"); plate.attribute_type = "OBJECT"; plate.attribute_name = "plate_id"
        ao = nodes.new("ShaderNodeAmbientOcclusion"); ao.samples = 24; ao.inputs["Distance"].default_value = .05
        bevel = nodes.new("ShaderNodeBevel"); bevel.samples = 8; bevel.inputs["Radius"].default_value = .005
        dot = nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
        links.new(bevel.outputs[0], dot.inputs[0]); links.new(geo.outputs["Normal"], dot.inputs[1])
        edge = nodes.new("ShaderNodeMath"); edge.operation = "MULTIPLY_ADD"
        edge.inputs[1].default_value = -6.0; edge.inputs[2].default_value = 6.0
        links.new(dot.outputs["Value"], edge.inputs[0])
        clamp = nodes.new("ShaderNodeClamp")
        links.new(edge.outputs[0], clamp.inputs[0])
        combine = nodes.new("ShaderNodeCombineColor")
        links.new(plate.outputs["Fac"], combine.inputs[0])
        links.new(ao.outputs["AO"], combine.inputs[1])
        links.new(clamp.outputs[0], combine.inputs[2])
        links.new(combine.outputs[0], emit.inputs["Color"])
    else:  # height
        sep = nodes.new("ShaderNodeSeparateXYZ")
        links.new(geo.outputs["Position"], sep.inputs[0])
        remap = nodes.new("ShaderNodeMapRange")
        remap.inputs["From Min"].default_value = -depth
        remap.inputs["From Max"].default_value = .012
        links.new(sep.outputs["Z"], remap.inputs["Value"])
        links.new(remap.outputs[0], emit.inputs["Color"])
    return m


def setup_render(resolution):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    try:
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for device in prefs.devices:
            device.use = True
        scene.cycles.device = "GPU"
    except Exception:
        scene.cycles.device = "CPU"
    scene.render.resolution_x = scene.render.resolution_y = resolution
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_depth = "16"
    scene.cycles.use_denoising = False
    cam_data = bpy.data.cameras.new("Atlas")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = P
    cam = bpy.data.objects.new("Atlas", cam_data)
    cam.location = (P / 2, P / 2, 3)
    scene.collection.objects.link(cam)
    scene.camera = cam
    world = bpy.data.worlds.new("Black")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0
    scene.world = world


def render_pass(kind, path, depth, samples):
    scene = bpy.context.scene
    scene.view_settings.view_transform = "Raw"
    scene.display_settings.display_device = "sRGB"
    scene.cycles.samples = samples
    scene.view_layers[0].material_override = data_material(kind, depth)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    scene.view_layers[0].material_override = None


def compose(prefix):
    def load(path):
        img = bpy.data.images.load(str(path))
        img.colorspace_settings.name = "Non-Color"
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return img, px.reshape(h, w, 4)
    data_img, data = load(prefix.with_name(prefix.name + "-data-rgb.png"))
    _, height = load(prefix.with_name(prefix.name + "-height.png"))
    data[..., 3] = height[..., 0]
    out = bpy.data.images.new(prefix.name + "-data", data.shape[1], data.shape[0], alpha=True, float_buffer=False)
    out.colorspace_settings.name = "Non-Color"
    out.alpha_mode = "STRAIGHT"
    out.pixels.foreach_set(data.ravel())
    out.filepath_raw = str(prefix.with_name(prefix.name + "-data.png"))
    out.file_format = "PNG"
    out.save()
    for extra in ("-data-rgb.png", "-height.png"):
        prefix.with_name(prefix.name + extra).unlink()


def preview(path, depth):
    scene = bpy.context.scene
    scene.view_settings.view_transform = "AgX"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .25
    sun = bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN"))
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(58), 0, math.radians(35))
    scene.collection.objects.link(sun)
    cam = scene.camera
    cam.data.type = "PERSP"
    cam.data.lens = 35
    cam.location = (P * .5, -.6, 1.35)
    cam.rotation_euler = (math.radians(62), 0, 0)
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.image_settings.color_depth = "8"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    args = arguments()
    spec = VARIANTS[args.variant]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rng = random.Random(spec["seed"])
    depth = build(spec, rng)
    setup_render(args.resolution)
    OUT.mkdir(parents=True, exist_ok=True)
    prefix = OUT / f"plates-{args.variant}"
    render_pass("normal", prefix.with_name(prefix.name + "-normal.png"), depth, 4)
    render_pass("data", prefix.with_name(prefix.name + "-data-rgb.png"), depth, 48)
    render_pass("height", prefix.with_name(prefix.name + "-height.png"), depth, 4)
    compose(prefix)
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(Path(args.blend).resolve()))
    if args.preview:
        Path(args.preview).mkdir(parents=True, exist_ok=True)
        preview(Path(args.preview) / f"plates-{args.variant}.png", depth)
    print("PLATE ATLAS", args.variant, "->", prefix)


main()
