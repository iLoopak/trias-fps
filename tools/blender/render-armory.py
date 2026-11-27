"""Render presentation and unlabelled silhouette plates from Arsenal.blend."""
import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion, Vector

KEYS = ("pulse", "scatter", "lance", "arc", "rift")
ANGLES = {
    "side": (1, 0, 0),
    "quarter": (1, .95, .65),
    "top": (0, 0, 1),
    "first-person": (-.58, 1, .44),
}


def render():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    folder = args.output.resolve()
    folder.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    camera = scene.camera
    scene.cycles.samples = 12
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    source = [item for item in scene.objects if item.type == "MESH" and item.parent]
    neutral = bpy.data.materials.new("Neutral proof")
    neutral.diffuse_color = (.32, .32, .32, 1)
    black = bpy.data.materials.new("Silhouette proof")
    black.diffuse_color = (0, 0, 0, 1)
    black.use_nodes = True
    nodes = black.node_tree.nodes
    output = nodes.get("Material Output")
    shader = nodes.new("ShaderNodeEmission")
    shader.inputs["Color"].default_value = (0,0,0,1)
    black.node_tree.links.new(shader.outputs[0],output.inputs["Surface"])

    def shot(name, width, height, transparent):
        scene.render.resolution_x, scene.render.resolution_y = width, height
        scene.render.film_transparent = transparent
        scene.render.filepath = str(folder / (name + ".png"))
        bpy.ops.render.render(write_still=True)

    shot("arsenal-lineup",1280,920,False)
    for index,key in enumerate(KEYS):
        group = [item for item in source if item.parent.name.startswith(key+"_")]
        for item in source:
            item.hide_render = item not in group
        center = Vector(((index-2)*.455, -.105, .02))
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.15
        for name, direction in ANGLES.items():
            camera.location = center + Vector(direction).normalized()*3.2
            rotation = (center-camera.location).to_track_quat("-Z","Y")
            if name == "top":
                rotation @= Quaternion((0, 0, 1), 1.57079632679)
            camera.rotation_euler = rotation.to_euler()
            scene.view_layers[0].material_override = black if name == "side" else neutral
            shot(f"{index+1:02}-{name}",420,300,True)
        scene.view_layers[0].material_override = None
        camera.location = center + Vector(ANGLES["quarter"]).normalized()*3.2
        camera.rotation_euler = (center-camera.location).to_track_quat("-Z","Y").to_euler()
        shot(key,900,650,True)

        if key in ("scatter", "rift"):
            explosion = {
                "Chamber": (0, -.18, .04), "Action": (-.14, 0, .13),
                "Payload": (.13, 0, -.13), "Vane1": (.13, -.15, .12),
                "Vane2": (-.13, -.15, .12), "Vane3": (0, -.14, -.13),
            }
            parents = {item.parent for item in group}
            original = {item: item.location.copy() for item in parents}
            for item in parents:
                item.location += Vector(explosion.get(item.name.split("_", 1)[1], (0, 0, 0)))
            camera.data.ortho_scale = 1.36
            shot(key + "-exploded", 900, 650, True)
            for item, location in original.items():
                item.location = location


if __name__ == "__main__":
    render()
