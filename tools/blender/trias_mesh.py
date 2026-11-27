"""Reusable modeling and export helpers for authored TRIAS art."""
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

PROJECT = Path(__file__).resolve().parents[2] / "unity-vfx-preview"
MATERIALS = {
    # Game-facing slot names. Unity substitutes the current world's shared finishes.
    "Ceramic": ((.75, .65, .49), .025, .41),
    "Secondary": ((.43, .32, .22), .04, .50),
    "Graphite": ((.029, .040, .052), .12, .49),
    "Fabric": ((.015, .020, .026), .0, .74),
    "Alloy": ((.31, .36, .40), .63, .34),
    "Visor": ((.022, .059, .082), .42, .20),
    "Faction": ((.022, .47, .61), .19, .40),
    "Signal": ((.05, .72, .89), .04, .24),
    "Amber": ((.73, .24, .049), .08, .44),
}
paints = {}
parts = {}
group = None

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    paints.clear()
    parts.clear()
    for key, (color, metal, roughness) in MATERIALS.items():
        material = bpy.data.materials.new(key)
        material.diffuse_color = (*color, 1)
        material.use_nodes = True
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*color, 1)
        shader.inputs["Metallic"].default_value = metal
        shader.inputs["Roughness"].default_value = roughness
        if key == "Signal" or key.endswith("Glow"):
            shader.inputs["Emission Color"].default_value = (*color, 1)
            shader.inputs["Emission Strength"].default_value = .38
        paints[key] = material

def coordinates(xyz):
    x, y, z = xyz
    return (x, -z, y)

def select_only(*objects):
    bpy.ops.object.select_all(action="DESELECT")
    for item in objects:
        item.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

def surface(name, verts, faces, finish, bevel=0, smooth=False):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([coordinates(p) for p in verts], [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name="Finish UV")
    for polygon in mesh.polygons:
        n = polygon.normal
        axis = max(range(3), key=lambda i: abs(n[i]))
        u, v = tuple(i for i in range(3) if i != axis)
        for corner in polygon.loop_indices:
            p = mesh.vertices[mesh.loops[corner].vertex_index].co
            uv.data[corner].uv = (p[u]*6 + .5, p[v]*6 + .5)
        polygon.use_smooth = smooth
    item = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(item)
    mesh.materials.append(paints[finish])
    parts.setdefault(group, []).append(item)
    if bevel:
        select_only(item)
        edge = item.modifiers.new("Machined radius", "BEVEL")
        edge.width = bevel
        edge.segments = 2
        bpy.ops.object.modifier_apply(modifier=edge.name)
        for face in mesh.polygons:
            face.use_smooth = True
        normals = item.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
        normals.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normals.name)

def band(name, at, profile, finish, sides=12, corner=.80):
    """Loft axial sections (height, half width, half depth, forward offset)."""
    x, y, z = at
    verts = []
    for h, width, depth, offset in profile:
        for i in range(sides):
            a = i*math.tau/sides
            c, s = math.sin(a), math.cos(a)
            verts.append((x+math.copysign(abs(c)**corner,c)*width,y+h,
                          z+offset+math.copysign(abs(s)**corner,s)*depth))
    faces = [tuple(reversed(range(sides)))]
    for j in range(len(profile)-1):
        for i in range(sides):
            a, b = j*sides+i, j*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple((len(profile)-1)*sides+i for i in range(sides)))
    surface(name,verts,faces,finish,smooth=True)

def plate(name, outline, front, depth, finish, bevel=.004):
    """Extrude a shaped ceramic face or stamp along the forward axis."""
    n=len(outline)
    verts=[(x,y,(z if z is not None else front)-d) for d in (0,depth) for x,y,z in outline]
    faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]
    faces += [(i,i+n,(i+1)%n+n,(i+1)%n) for i in range(n)]
    surface(name,verts,faces,finish,bevel)

def panel(name, x, y, z, width, height, depth, finish, rake=0, bevel=.003):
    w,h=width/2,height/2
    pts=[(-w,-h*.66),(-w*.74,-h),(w*.66,-h),(w,-h*.63),
         (w,h*.64),(w*.65,h),(-w*.64,h),(-w,h*.66)]
    plate(name,[(x+dx+rake*dy,y+dy,None) for dx,dy in pts],z,depth,finish,bevel)

def fastener(name,x,y,z, radius, finish="Alloy"):
    plate(name,[(x+math.cos(i*math.tau/6)*radius,y+math.sin(i*math.tau/6)*radius,None)
                for i in range(6)],z,.004,finish,.0008)

def vents(name,x,y,z,count,spacing,width,finish="Fabric"):
    for i in range(count):
        panel(name,x,y+i*spacing,z,width,.006,.003,finish,bevel=.001)

def bore(name, at, profile, finish, sides=18):
    """Sweep a closed radial cross-section (axial distance, radius)."""
    x, y, z = at
    verts = [(x+math.cos(i*math.tau/sides)*r,y+math.sin(i*math.tau/sides)*r,z+depth)
             for depth,r in profile for i in range(sides)]
    faces = []
    for section in range(len(profile)):
        next_section = (section+1)%len(profile)
        for i in range(sides):
            j = (i+1)%sides
            faces.append((section*sides+i,section*sides+j,next_section*sides+j,next_section*sides+i))
    surface(name,verts,faces,finish,smooth=True)

def finish(filename, resource, display, camera_at=(2.6,-4.7,2.13),
           camera_target=(0,0,.86), camera_width=2.1, resolution=(980,1180)):
    source_objects = []
    for slot, members in parts.items():
        pivot=bpy.data.objects.new(slot,None)
        bpy.context.collection.objects.link(pivot)
        pivot.location=coordinates(display[slot])
        # Part fronts and vertical axes match the Unity mounts in the neutral preview.
        for member in members:
            member.parent=pivot
            source_objects.append(member)

    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1.0
    scene.world=bpy.data.worlds.new("Neutral studio")
    scene.world.use_nodes=True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value=(.165,.212,.29,1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value=.31
    scene.render.engine="CYCLES"
    scene.cycles.samples=20
    scene.cycles.use_denoising=True
    scene.render.resolution_x=resolution[0]
    scene.render.resolution_y=resolution[1]
    scene.render.resolution_percentage=100

    def studio_item(name,data,location,aim):
        obj=bpy.data.objects.new(name,data)
        bpy.context.collection.objects.link(obj)
        obj.location=location
        obj.rotation_euler=(Vector(aim)-obj.location).to_track_quat("-Z","Y").to_euler()
        return obj

    scene.camera=studio_item("Preview",bpy.data.cameras.new("Studio"),camera_at,camera_target)
    scene.camera.data.type="ORTHO"
    scene.camera.data.ortho_scale=camera_width
    for name,loc,power,color,size in (
        ("Warm key",(-2.2,-4.0,4.4),335,(1,.83,.65),3.7),
        ("Cool fill",(2.1,-2.2,2.1),140,(.46,.65,1),2.6),
        ("Rim",(.4,2.2,3.8),440,(.47,.72,1),2.3)):
        light=bpy.data.lights.new(name,"AREA")
        light.energy,light.color,light.size=power,color,size
        studio_item(name,light,loc,(0,0,.96))
    select_only(source_objects[0])
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/"Art"/(filename+".blend")))

    export=[]
    for slot, members in parts.items():
        copies=[]
        for src in members:
            duplicate=src.copy()
            duplicate.data=src.data.copy()
            # Unity's FBX conversion puts Blender's front on -Z without this correction.
            duplicate.data.transform(Matrix.Rotation(math.pi,4,"Z"))
            duplicate.parent=None
            duplicate.location=(0,0,0)
            bpy.context.collection.objects.link(duplicate)
            copies.append(duplicate)
        select_only(*copies)
        bpy.ops.object.join()
        item=copies[0]
        item.name=slot+"_Mount"
        item.data.name=slot+"_Mesh"
        # Join may retain duplicate indices for the same finish. Collapse before export.
        slots=list(item.data.materials)
        unique=list(dict.fromkeys(slots))
        indices=[unique.index(slots[p.material_index]) for p in item.data.polygons]
        item.data.materials.clear()
        for material in unique:
            item.data.materials.append(material)
        for polygon,index in zip(item.data.polygons,indices):
            polygon.material_index=index
        export.append(item)
    select_only(*export)
    bpy.ops.export_scene.fbx(filepath=str(PROJECT/"Assets/Resources/TRIAS"/resource),
        use_selection=True, bake_anim=False, axis_forward="Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, mesh_smooth_type="OFF", object_types={"MESH"})
    print(filename.upper(), "EXPORT",len(export),"mounts",sum(len(o.data.polygons) for o in export),"polygons")
