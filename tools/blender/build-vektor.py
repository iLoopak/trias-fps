"""Build the original Vektor armor. Run with Blender --background --python."""
import math
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

PROJECT = Path(__file__).resolve().parents[2] / "unity-vfx-preview"
bpy.ops.wm.read_factory_settings(use_empty=True)
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
for key, (color, metal, roughness) in MATERIALS.items():
    material = bpy.data.materials.new(key)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = roughness
    if key == "Signal":
        shader.inputs["Emission Color"].default_value = (*color, 1)
        shader.inputs["Emission Strength"].default_value = .38
    paints[key] = material

parts = {}
group = None

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

group = "Helmet"
band("Sealed monocoque",(0,0,-.013),[
    (-.132,.044,.047,-.018),(-.106,.084,.085,.003),(-.066,.118,.115,.008),
    (.013,.124,.116,0),(.086,.112,.099,-.016),(.119,.083,.073,-.025),
    (.132,.039,.037,-.027)],"Ceramic",20,.73)
band("Collar gasket",(0,-.124,-.023),[
    (-.044,.047,.046,0),(-.019,.063,.050,.003),(.019,.061,.052,0)],"Fabric",16)
plate("Recessed face",[
    (-.112,-.013,.071),(-.080,-.065,.104),(0,-.078,.123),(.083,-.058,.107),
    (.114,-.004,.074),(.105,.058,.081),(.062,.074,.102),(0,.074,.125),(-.074,.070,.108),
    (-.110,.047,.082)],0,.018,"Graphite",.006)
# Compound visor planes recede at the temples. The brow masks the upper edge.
for side in (-1,1):
    lens=[(.001,-.003,.133),(.062,-.002,.126),(.104,.016,.099),
          (.105,.041,.096),(.063,.050,.128),(.003,.051,.135)]
    lit=[(.013,.035,.137),(.060,.035,.131),(.091,.030,.111),
         (.094,.039,.109),(.061,.043,.132),(.013,.043,.137)]
    face=lambda pts:[(side*x,y,z) for x,y,z in (pts if side==1 else list(reversed(pts)))]
    plate("Curved optical lens",face(lens),0,.013,"Visor")
    plate("Polarized sightline",face(lit),0,.004,"Signal",.001)
    plate("Ceramic eyebrow",face([(.004,.052,.134),(.066,.052,.129),(.105,.041,.101),
        (.116,.060,.076),(.082,.083,.095),(.015,.083,.123)]),0,.051,"Ceramic",.004)
    plate("Chin facing",face([(.012,-.090,.112),(.071,-.077,.109),(.099,-.045,.098),
        (.081,-.035,.112),(.060,-.056,.124),(.013,-.064,.135)]),0,.034,"Secondary")
    panel("Temples",side*.106,-.021,.096,.029,.074,.074,"Alloy",side*.18)
    fastener("Temple service bolt",side*.112,-.014,.098,.007)
panel("Center respirator",0,-.086,.143,.090,.037,.036,"Graphite",bevel=.006)
vents("Respirator slots",0,-.096,.144,3,.009,.047)
panel("Crown identification",-.047,.089,.092,.032,.048,.015,"Faction",.18)
panel("Service tab",.078,-.050,.115,.018,.026,.008,"Amber",-.23,.0016)

group = "Torso"
band("Pressure garment",(0,0,0),[
    (-.274,.101,.084,-.014),(-.225,.133,.107,0),(-.15,.135,.108,0),(-.056,.161,.117,.002),
    (.030,.188,.114,-.003),(.105,.196,.103,-.022),(.155,.164,.088,-.013),
    (.188,.089,.062,-.014)],"Fabric",16)
for side in (-1,1):
    face=lambda pts:[(side*x,y,z) for x,y,z in (pts if side==1 else list(reversed(pts)))]
    plate("Sternum ceramic",face([(.014,-.079,.149),(.104,-.091,.139),(.173,-.027,.109),
        (.183,.091,.089),(.150,.132,.093),(.063,.113,.132),(.018,.087,.153)]),0,.038,"Ceramic",.010)
    plate("Separated lower ceramic",face([(.028,-.176,.126),(.097,-.171,.116),(.131,-.139,.093),
        (.146,-.076,.103),(.109,-.096,.131),(.032,-.084,.146)]),0,.033,"Secondary")
    panel("Retention bracket",side*.148,.065,.132,.034,.084,.020,"Alloy",side*.19)
    fastener("Retention cap",side*.151,.092,.135,.0065)
    panel("Fascia faction insert",side*.102,-.017,.154,.111,.051,.011,"Faction",side*.34)
    panel("Transition pin",side*.063,-.104,.149,.061,.013,.008,"Signal",side*.17,.0014)
    for i in range(3):
        panel("Rib armor lamella",side*(.139-i*.013),-.133-i*.042,.090,.056,.029,.067,"Graphite",side*.33)
panel("Sternum key slot",0,.031,.151,.024,.115,.023,"Alloy",bevel=.004)
panel("Abdominal team stripe",0,-.180,.135,.238,.052,.025,"Faction",bevel=.005)
panel("Harness release",0,-.192,.150,.049,.048,.023,"Amber",bevel=.005)
panel("Dorsal equipment bay",0,.009,-.057,.288,.313,.138,"Graphite",bevel=.016)
for side in (-1,1):
    panel("Reactor mounting frame",side*.095,.025,-.075,.082,.258,.137,"Alloy",side*.13,.009)
    panel("Phase ballast ceramic",side*.102,.036,-.209,.067,.202,.055,"Ceramic",side*.13,.009)
    panel("Transponder stripe",side*.101,-.008,-.260,.064,.089,.012,"Faction",side*.13)
panel("Spine resonance cell",0,.023,-.209,.038,.158,.062,"Graphite",bevel=.008)
panel("Rear maintenance latch",0,.008,-.268,.021,.09,.009,"Signal",bevel=.002)
panel("Asymmetric telemetry pack",-.156,-.051,-.063,.078,.164,.123,"Graphite",-.12,.010)
panel("Telemetry ceramic door",-.182,-.049,-.179,.053,.137,.025,"Secondary",-.12,.006)

group = "Pelvis"
band("Pelvis support",(0,-.018,0),[
    (-.123,.067,.064,0),(-.095,.119,.085,0),(-.033,.145,.096,-.01),
    (.012,.166,.096,-.01),(.052,.13,.087,0),(.108,.113,.088,0),(.132,.093,.076,-.008)],"Fabric",16)
panel("Belt front",0,.022,.11,.296,.071,.032,"Graphite",bevel=.005)
panel("Belt clasp",0,.024,.137,.061,.044,.022,"Alloy",bevel=.005)
panel("Belt sign",0,.024,.141,.032,.017,.007,"Amber")
for side in (-1,1):
    panel("Floating hip guard",side*.124,-.019,.083,.078,.097,.097,"Ceramic",side*.24,.008)
    panel("Hip stripe",side*.135,-.015,.084,.025,.048,.009,"Faction",side*.2)
    panel("Hip carrier",side*.123,-.008,-.083,.076,.084,.048,"Graphite",bevel=.007)

for side, name in ((-1,"Left"),(1,"Right")):
    group=name+"Upper"
    band("Upper arm pressure sleeve",(0,0,0),[
        (-.145,.048,.048,0),(-.085,.062,.060,0),(.025,.054,.052,0),
        (.119,.043,.045,0),(.145,.039,.042,0)],"Fabric")
    plate("Split upper arm ceramic",[
        (-.047,-.074,.068),(.031,-.084,.064),(.060,-.042,.05),(.059,.061,.052),
        (.021,.086,.07),(-.048,.043,.067)],0,.025,"Ceramic",.006)
    panel("Upper mount",0,-.074,.076,.062,.032,.02,"Alloy",.12)
    panel("Bicep inset",0,.027,.077,.064,.044,.011,"Faction",.24)

    group=name+"Shoulder"
    band("Pauldron actuator housing",(0,0,0),[
        (-.065,.054,.066,0),(-.047,.090,.095,0),(.015,.096,.103,0),
        (.049,.071,.079,-.014),(.065,.046,.054,-.02)],"Graphite",16)
    band("Curved shoulder shell",(0,.020,-.006),[
        (-.04,.101,.103,.015),(.012,.097,.095,.005),(.044,.075,.082,0),
        (.060,.035,.045,-.011)],"Ceramic",16,.65)
    panel("Wide faction field",0,-.023,.120,.175,.085,.077,"Faction",side*.24,.009)
    panel("Shoulder mount",0,-.054,-.038,.133,.039,.049,"Alloy",side*.32)
    panel("Shoulder rear identifier",0,-.024,-.059,.132,.068,.036,"Faction",side*.27)
    fastener("Pauldron socket",side*.060,.007,.111,.008)

    group=name+"Forearm"
    band("Arm textile and rear tie",(0,0,0),[
        (-.141,.053,.046,0),(-.096,.064,.060,0),(-.013,.055,.052,.004),
        (.073,.051,.052,.004),(.135,.038,.037,0)],"Fabric")
    plate("Tapered gauntlet",[
        (-.046,-.104,.061),(.035,-.106,.063),(.067,-.057,.050),(.048,.080,.058),
        (.018,.105,.061),(-.039,.089,.063),(-.058,-.068,.060)],0,.031,"Ceramic",.005)
    panel("Reinforced ulnar ridge",0,-.007,.071,.040,.126,.019,"Graphite",side*.14)
    panel("Forearm team direction",0,-.019,.076,.038,.051,.012,"Faction",side*.34)
    for y in (-.11,.102):
        band("Floating wrist or elbow seal",(0,y,0),[
            (-.016,.054,.049,0),(-.010,.061,.052,.004),(.012,.058,.053,0),
            (.020,.05,.043,0)],"Graphite")
    fastener("Forearm service pin",side*.025,-.065,.073,.006)

    group=name+"Hand"
    band("Pressure glove",(0,0,.026),[
        (-.04,.023,.045,-.013),(-.025,.043,.067,0),(.01,.048,.059,.006),
        (.029,.035,.039,0)],"Fabric",12)
    for i in range(4):
        panel("Knuckle segments",(i-1.5)*.021,.017,.078,.015,.029,.027,"Graphite",bevel=.002)
    panel("Glove backing",0,.024,.047,.069,.023,.070,"Ceramic",bevel=.004)
    panel("Wrist lock",0,.015,-.034,.073,.039,.044,"Alloy",bevel=.004)

    group=name+"Thigh"
    band("Thigh undersuit",(0,0,0),[
        (-.175,.057,.058,0),(-.118,.077,.073,.004),(-.022,.076,.076,.003),
        (.090,.065,.062,0),(.178,.052,.053,0)],"Fabric",14)
    plate("Femoral ceramic plate",[
        (-.06,-.115,.078),(.035,-.133,.075),(.068,-.076,.064),(.065,.082,.065),
        (.020,.143,.061),(-.049,.099,.075),(-.068,-.064,.069)],0,.041,"Ceramic",.007)
    panel("Thigh hinge",0,-.084,.09,.087,.041,.018,"Graphite",side*.21)
    panel("Thigh faction patch",side*.025,-.031,.089,.050,.071,.011,"Faction",side*.25)
    fastener("Thigh plate pin",side*.036,.066,.079,.0065)

    group=name+"Shin"
    band("Calf pressure layer",(0,0,-.006),[
        (-.184,.056,.048,0),(-.139,.066,.065,-.010),(-.068,.076,.075,-.015),
        (.026,.058,.058,0),(.181,.044,.052,0)],"Fabric",14)
    plate("Tibial shell",[
        (-.052,-.085,.065),(.042,-.104,.065),(.071,-.059,.052),(.056,.114,.065),
        (.027,.155,.069),(-.041,.139,.066),(-.053,.065,.065)],0,.033,"Ceramic",.007)
    panel("Raised shin rib",side*.021,.022,.078,.041,.178,.017,"Secondary",side*.17,.003)
    panel("Shin inherited color",side*.012,.036,.090,.049,.089,.008,"Faction",side*.25)
    panel("Knee shock mount",0,.157,.076,.111,.090,.066,"Graphite",bevel=.009)
    panel("Knee cap",0,.160,.113,.078,.061,.04,"Alloy",bevel=.008)
    fastener("Knee bolt",0,.161,.119,.009)

    group=name+"Boot"
    band("Tread body",(0,0,.029),[
        (-.060,.047,.107,.025),(-.047,.069,.123,.021),(-.022,.071,.126,.018),
        (.036,.066,.115,.014),(.075,.052,.081,-.021),
        (.12,.047,.056,-.042)],"Graphite",16,.68)
    band("Ceramic toe",(0,0,.071),[
        (-.030,.063,.078,.015),(.007,.062,.080,.011),(.040,.050,.07,.002),
        (.052,.028,.052,-.009)],"Ceramic",16,.66)
    panel("Heel shock insert",0,.026,-.053,.103,.075,.048,"Alloy",bevel=.008)
    panel("Boot team panel",0,.040,.014,.078,.033,.059,"Faction")
    for i in range(4):
        band("Recessed rubber tread",(0,-.053,.034+i*.043),[
            (-.014,.056,.013,0),(.010,.068,.018,0)],"Fabric",8,.64)

# Keep editable individual surfaces in the .blend. The FBX copy is consolidated per live rig mount.
display = {
    "Helmet": (0,1.58,-.022), "Torso": (0,1.235,0), "Pelvis": (0,.908,0),
}
for side,name in ((-1,"Left"),(1,"Right")):
    display.update({name+"Upper": (side*.238,1.162,-.008),name+"Shoulder": (side*.258,1.381,0),
        name+"Forearm": (side*.281,.893,.018),name+"Hand":(side*.285,.723,.020),
        name+"Thigh": (side*.104,.72,0),name+"Shin": (side*.123,.357,0),
        name+"Boot": (side*.129,.067,.02)})

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
scene.render.resolution_x=980
scene.render.resolution_y=1180
scene.render.resolution_percentage=100

def studio_item(name,data,location,aim):
    obj=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(obj)
    obj.location=location
    obj.rotation_euler=(Vector(aim)-obj.location).to_track_quat("-Z","Y").to_euler()
    return obj

scene.camera=studio_item("Vektor preview",bpy.data.cameras.new("Portrait"),(2.6,-4.7,2.13),(0,0,.86))
scene.camera.data.type="ORTHO"
scene.camera.data.ortho_scale=2.1
for name,loc,power,color,size in (
    ("Warm key",(-2.2,-4.0,4.4),335,(1,.83,.65),3.7),
    ("Cool fill",(2.1,-2.2,2.1),140,(.46,.65,1),2.6),
    ("Rim",(.4,2.2,3.8),440,(.47,.72,1),2.3)):
    light=bpy.data.lights.new(name,"AREA")
    light.energy,light.color,light.size=power,color,size
    studio_item(name,light,loc,(0,0,.96))
select_only(source_objects[0])
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/"Art/Vektor.blend"))

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
bpy.ops.export_scene.fbx(filepath=str(PROJECT/"Assets/Resources/TRIAS/Vektor/VektorSuit.fbx"),
    use_selection=True, bake_anim=False, axis_forward="Z", axis_up="Y",
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, mesh_smooth_type="OFF", object_types={"MESH"})
print("VEKTOR EXPORT",len(export),"mounts",sum(len(o.data.polygons) for o in export),"polygons")
