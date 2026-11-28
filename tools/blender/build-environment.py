"""Generate, validate and optionally render the original TRIAS environment kit."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import tempfile

import bpy
import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
import trias_mesh as mesh
import trias_environment_forms as form

FINISHES = {
    "Ceramic": ((.76,.66,.48),.045,.38), "Secondary": ((.44,.32,.21),.06,.50),
    "Graphite": ((.045,.061,.079),.26,.51), "Alloy": ((.31,.38,.42),.55,.37),
    "Amber": ((.78,.31,.069),.12,.54), "Signal": ((.11,.61,.65),.11,.26),
    "Refractory": ((.49,.29,.18),.08,.64), "DryStone": ((.31,.30,.23),0,.87),
    "DarkStone": ((.16,.19,.15),0,.92), "OchreStone": ((.34,.23,.14),0,.87),
    "Moss": ((.26,.33,.13),0,.94), "Bark": ((.18,.13,.075),0,.89),
    "Leaf": ((.11,.25,.13),0,.78), "LeafShade": ((.060,.14,.088),0,.92),
    "YoungLeaf": ((.24,.36,.15),0,.86),
}

# Stable mount identifiers and post-triangulation LOD0 budgets.
ARCHITECTURE = (
    ("Wall",form.wall,1800,"Standard Reinforced Angled Recessed Core Phase End"),
    ("Opening",form.doorway,1550,"Door Reinforced Maintenance Machinery Phase Window Corner"),
    ("Support",form.support,1900,"Tapered Angled Mechanical Phase Heavy Bracket"),
    ("Cover",form.cover,1800,"Low Full Reinforced Equipment Ceramic Phase Weathered"),
    ("Container",form.container,1900,"Pressure Thermal"),
    ("Deck",form.deck,1500,"Walkway Bridge Suspended Edge Exposed Tread Ramp Floor"),
    ("Rail",lambda _,detail:form.rail(detail),1100,"Swept"),
    ("Machine",form.machine,1800,"Stabilizer Distribution Processor Cooling Maintenance Control Suspended Conduit Extraction"),
)
NATURE = (
    ("Rock",form.geology,2600,"Shelf Split Erased Outcrop Pebbles Cliff Seam Phase"),
    ("Tree",form.tree,1750,"Sentinel Ridge Sapling Windward"),
    ("Plant",form.undergrowth,800,"Bramble Sedge Fern Bloom"),
    ("Site",lambda _,detail:form.ruin(detail),1450,"ReclaimedDeflector"),
)

def positive_source_volume(item):
    bm=bmesh.new()
    bm.from_mesh(item.data)
    volume=bm.calc_volume(signed=True)
    closed=all(edge.is_manifold and edge.is_contiguous for edge in bm.edges)
    bm.free()
    if not closed or volume<=1e-10:
        raise ValueError(f"{mesh.group}: {item.name} is open, inverted or has near-zero volume ({volume})")


def clean_mesh(item, name, budget):
    data=item.data
    if data.validate(clean_customdata=False):
        raise ValueError(f"{name}: Blender repaired invalid geometry")
    data.calc_loop_triangles()
    if not data.loop_triangles or len(data.loop_triangles)>budget or len(data.vertices)>budget*2:
        raise ValueError(f"{name}: {len(data.loop_triangles)} triangles outside budget {budget}")
    slots=list(data.materials)
    used=sorted({face.material_index for face in data.polygons})
    if any(n>=len(slots) or slots[n].name not in FINISHES for n in used) or len(used)>(7 if name.startswith("Site") else 5):
        raise ValueError(f"{name}: invalid material slots")
    indices=[used.index(face.material_index) for face in data.polygons]
    data.materials.clear()
    for index in used:
        data.materials.append(slots[index])
    for face,index in zip(data.polygons,indices):
        face.material_index=index

    # Meter-based dominant-plane UVs survive bevels and the FBX correction rotation.
    uv=data.uv_layers.active
    for polygon in data.polygons:
        axis=max(range(3),key=lambda i:abs(polygon.normal[i]))
        u,v=(i for i in range(3) if i!=axis)
        if abs(polygon.normal[axis])<.5:
            raise ValueError(f"{name}: invalid face normal")
        for loop_index in polygon.loop_indices:
            vertex=data.vertices[data.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv=(vertex[u]*.72+.5,vertex[v]*.72+.5)
    for tri in data.loop_triangles:
        a,b,c=(uv.data[i].uv for i in tri.loops)
        if not math.isfinite(tri.area) or tri.area<1e-9 or abs((b-a).cross(c-a))<1e-9:
            raise ValueError(f"{name}: degenerate face or UV in polygon {tri.polygon_index}: {tri.area:.9g}, UV {(b-a).cross(c-a):.9g}, {list(tri.vertices)}")

    coords=[v.co for v in data.vertices]
    if not all(math.isfinite(value) for p in coords for value in p):
        raise ValueError(f"{name}: non-finite vertex")
    # Distinct shells can meet at identical coordinates. Reject duplicates within each shell.
    bm=bmesh.new()
    bm.from_mesh(data)
    duplicates=bmesh.ops.find_doubles(bm,verts=bm.verts,dist=1e-7)["targetmap"]
    adjacent=any(a in {edge.other_vert(b) for edge in b.link_edges} for a,b in duplicates.items())
    bm.free()
    if adjacent:
        raise ValueError(f"{name}: duplicate connected vertices")
    low,high=([min(v[i] for v in coords) for i in range(3)],[max(v[i] for v in coords) for i in range(3)])
    if abs(low[2])>.002 or any(not .018<=b-a<=12 for a,b in zip(low,high)):
        raise ValueError(f"{name}: invalid metric extents or floor pivot {low}, {high}")
    if any(abs(value-expected)>1e-4 for value,expected in zip(item.matrix_world.translation,(0,0,0))):
        raise ValueError(f"{name}: unapplied translation")
    return {"mount":name,"boundsUnityXYZ":[[round(low[0],5), round(low[2],5), round(-high[1],5)],
                                         [round(high[0],5),round(high[2],5),round(-low[1],5)]],
            "triangles":len(data.loop_triangles),"vertices":len(data.vertices),
            "slots":[material.name for material in data.materials]}


def join(name, fn, variant, detail, budget):
    mesh.group=name
    fn(variant,detail)
    items=mesh.parts.pop(name)
    for item in items:
        positive_source_volume(item)
    mesh.select_only(*items)
    bpy.ops.object.join()
    item=bpy.context.object
    item.name=name+"_Mount"
    item.data.name=name+"_Mesh"
    base=min(vertex.co.z for vertex in item.data.vertices)
    if abs(base)>.001:
        item.data.transform(Matrix.Translation((0,0,-base)))
    stats=clean_mesh(item,name,budget)
    stats["authoredShells"]=len(items)
    return item,stats


def point(name,data,location,aim):
    obj=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(obj)
    obj.location=location
    obj.rotation_euler=(Vector(aim)-obj.location).to_track_quat("-Z","Y").to_euler()
    return obj


def studio():
    scene=bpy.context.scene
    scene.world=bpy.data.worlds.new("Slate studio")
    scene.world.use_nodes=True
    background=scene.world.node_tree.nodes["Background"]
    background.inputs[0].default_value=(.16,.21,.25,1)
    background.inputs[1].default_value=.31
    scene.render.engine="CYCLES"
    scene.cycles.samples=10
    scene.cycles.use_denoising=True
    scene.render.image_settings.file_format="PNG"
    scene.render.resolution_x=384
    scene.render.resolution_y=320
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.render.film_transparent=False
    scene.camera=point("Kit studio",bpy.data.cameras.new("Kit studio"),(4,-6,3),(0,0,1))
    scene.camera.data.type="ORTHO"
    for name,position,power,color,size in (
        ("Warm raking key",(-3,-4,7),1750,(1,.89,.73),4.4),
        ("Cool fill",(5,-4,4),900,(.61,.77,1),5.5),
        ("Neutral rim",(1,4,7),2200,(.82,.91,1),3.7)
    ):
        light=bpy.data.lights.new(name,"AREA")
        light.energy,light.color,light.size=power,color,size
        point(name,light,position,(0,0,1))
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.021))
    floor=bpy.context.object
    floor.name="Neutral stage"
    paint=bpy.data.materials.new("Slate stage")
    paint.diffuse_color=(.069,.086,.099,1)
    paint.use_nodes=True
    paint.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=paint.diffuse_color
    floor.data.materials.append(paint)
    return scene


def previews(items, folder, views, selected):
    if not folder:
        return
    folder.mkdir(parents=True,exist_ok=True)
    scene=studio()
    kinds={"quarter":(1.05,-1.7,.67),"front":(0,-2,0),"side":(2,0,0),
           "silhouette":(-1.5,-1.7,.19),"close":(.73,-1.8,.33)}
    black=bpy.data.materials.new("Silhouette")
    black.diffuse_color=(.002,.003,.004,1)
    black.use_nodes=True
    black.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=black.diffuse_color
    for item in items:
        item.hide_render=True
    for item in items:
        key=item.name.removesuffix("_Mount")
        if "_LOD" in key or (selected and key not in selected):
            continue
        item.hide_render=False
        corners=[Vector(corner) for corner in item.bound_box]
        center=sum(corners, Vector())/8
        dims=item.dimensions
        width=max(dims)
        for view in views:
            target=center.copy()
            if view=="close":
                target.z+=dims.z*.16
            scene.camera.location=target+Vector(kinds[view])*width*1.6
            scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat("-Z","Y").to_euler()
            scene.camera.data.ortho_scale=width*(.72 if view=="close" else 1.43)
            scene.view_layers[0].material_override=black if view=="silhouette" else None
            scene.render.filepath=str(folder/f"{key}--{view}.png")
            bpy.ops.render.render(write_still=True)
        item.hide_render=True


def save_editable(items, scene, path):
    collections={}
    for item in items:
        name=item.name.removesuffix("_Mount").removesuffix("_LOD1")
        if name not in collections:
            collection=bpy.data.collections.new(name)
            scene.collection.children.link(collection)
            collections[name]=collection
        for collection in list(item.users_collection):
            collection.objects.unlink(item)
        collections[name].objects.link(item)
        item.hide_set(item != items[0])
    mesh.select_only(items[0])
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    for item in items:
        item.hide_set(False)


def build(name, definitions, directory, args):
    mesh.MATERIALS=FINISHES
    mesh.reset()
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    items,records=[],[]
    for family,fn,budget,variants in definitions:
        for variant in variants.split():
            key=family+variant
            high,high_stats=join(key,fn,variant,True,budget)
            items.append(high)
            record={"id":key,"family":family,"collision":"none","lods":[high_stats]}
            # Floor and step modules are small repeating geometry with no useful silhouette reduction.
            if key not in ("DeckTread","DeckRamp","DeckFloor","DeckEdge"):
                low,low_stats=join(key+"_LOD1",fn,variant,False,budget)
                if low_stats["triangles"]<high_stats["triangles"]*.86:
                    items.append(low)
                    record["lods"].append(low_stats)
                else:
                    bpy.data.objects.remove(low,do_unlink=True)
            records.append(record)
            print(f"{key}: {high_stats['triangles']} triangles, {len(high_stats['slots'])} slots",flush=True)
    if args.save_blend:
        save_editable(items,scene,directory/f"{name}.blend")
    output=directory/f"{name}.fbx"
    rotation=Matrix.Rotation(math.pi,4,"Z")
    for item in items:
        item.data.transform(rotation)
    mesh.select_only(*items)
    bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True, bake_anim=False,
        axis_forward="Z",axis_up="Y",apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,mesh_smooth_type="OFF",use_triangles=True,object_types={"MESH"})
    if not output.read_bytes().startswith(b"Kaydara FBX Binary"):
        raise ValueError(f"{output}: invalid FBX header")
    # Independently reimport the emitted FBX so the contract is exercised on disk.
    previous=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(output),use_anim=False)
    imported={item.name.removesuffix(".001"):item for item in set(bpy.data.objects)-previous}
    if set(imported)!={item.name for item in items}:
        raise ValueError(f"{name}: exported mount names changed")
    for record in records:
        for lod in record["lods"]:
            item=imported[lod["mount"]+"_Mount"]
            if (len(item.data.polygons)!=lod["triangles"] or len(item.data.uv_layers)!=1
                    or [material.name.removesuffix(".001") for material in item.data.materials]!=lod["slots"]
                    or not item.data.has_custom_normals):
                raise ValueError(f"{item.name}: triangle, UV, normal or slot FBX round-trip failed")
            expected=next(src for src in items if src.name == lod["mount"]+"_Mount")
            for axis in range(3):
                source=[(expected.matrix_world @ v.co)[axis] for v in expected.data.vertices]
                result=[(item.matrix_world @ v.co)[axis] for v in item.data.vertices]
                if max(abs(min(source)-min(result)),abs(max(source)-max(result)))>1e-4:
                    raise ValueError(f"{item.name}: FBX axis {axis} bounds changed from "
                                     f"{min(source),max(source)} to {min(result),max(result)}")
    for item in imported.values():
        bpy.data.objects.remove(item,do_unlink=True)
    for item in items:
        item.data.transform(rotation.inverted())
    if args.preview:
        previews(items,args.preview,args.views.split(","),set(args.assets.split(",")) if args.assets else set())
    return records


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preview",type=Path,help="Directory for optional Blender renders")
    parser.add_argument("--assets",help="Comma-separated mount names for previews")
    parser.add_argument("--views",default="quarter",help="quarter,front,side,silhouette,close")
    parser.add_argument("--save-blend",action="store_true",help="Also emit optional editable .blend files")
    parser.add_argument("--output",type=Path,default=mesh.PROJECT/"Assets/Resources/TRIAS/Environment")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if set(args.views.split(",")) - {"quarter","front","side","silhouette","close"}:
        parser.error("unknown preview view")
    args.output=args.output.resolve()
    if args.preview:
        args.preview=args.preview.resolve()
    args.output.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="trias-environment-",dir=args.output.parent) as stage:
        directory=Path(stage)
        manifest={"schema":1,"blender":bpy.app.version_string,"units":"metres",
                  "source":"tools/blender/build-environment.py + trias_environment_forms.py + trias_mesh.py",
                  "provenance":"Original TRIAS procedural geometry and studio materials. No external inputs.",
                  "collections":{}}
        for name,definitions in (("Architecture",ARCHITECTURE),("Nature",NATURE)):
            records=build(name,definitions,directory,args)
            manifest["collections"][name]={"assets":records,
                "fbxSha256":hashlib.sha256((directory/f"{name}.fbx").read_bytes()).hexdigest()}
        (directory/"Manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
        for file in sorted(directory.iterdir()):
            file.replace(args.output/file.name)


if __name__=="__main__":
    main()
