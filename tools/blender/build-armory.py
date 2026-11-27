"""Model the five TRIAS weapons and their moving assemblies in Blender."""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import bore

# These locations match the production grip, animated service points, and muzzle effects.
SPECS={
    "pulse":   (.51,(0,.026,.412),(-.075,.07,.12),(-.034,-.115,.080)),
    "scatter": (.48,(0,.026,.384),(-.075,.07,.12),(-.093,.040,.11)),
    "lance":   (.64,(0,.026,.544),(-.075,.07,.19),(-.108,.091,.16)),
    "arc":     (.41,(0,.026,.314),(-.075,-.018,.12),(-.107,-.045,.09)),
    "rift":    (.54,(0,.026,.444),(-.075,.07,.12),(-.107,.032,.13)),
}
MOTIFS={
    "pulse": (.04,.63,.78), "scatter": (.94,.37,.09), "lance": (.36,.61,.90),
    "arc": (.22,.73,.60), "rift": (.71,.29,.80),
}
PIVOTS={}


def part(key, name):
    art.group=key+"_"+name
    info=SPECS[key]
    PIVOTS[art.group]={"Receiver":(0,0,0),"Action":info[2],"Payload":info[3]}.get(name,info[1])


def block(name, x, y, profile, finish, bevel=.003):
    """Machine an eight-sided receiver along the bore axis."""
    verts=[]
    for z,w,h,lift in profile:
        verts.extend((x+xx*w, y+lift+yy*h,z) for xx,yy in
           ((-.66,-1),(.66,-1),(1,-.60),(1,.57),(.64,1),(-.64,1),(-1,.60),(-1,-.57)))
    faces=[tuple(reversed(range(8)))]
    for ring in range(len(profile)-1):
        for i in range(8):
            a=ring*8+i
            b=ring*8+(i+1)%8
            faces.append((a,b,b+8,a+8))
    faces.append(tuple((len(profile)-1)*8+i for i in range(8)))
    art.surface(name,verts,faces,finish,bevel)


def rail(name, x, y, z0, z1, w, h, finish, rake=0):
    inset=min(.016,(z1-z0)*.25)
    block(name,x,y,[(z0,w*.70,h*.74,0),(z0+inset,w,h,rake*.15),
        (z1-inset,w,h,rake*.79),(z1,w*.62,h*.61,rake)],finish,min(.0025,inset*.55))


def tube(name,x,y,z0,z1,r,wall,finish,sides=18):
    inset=min(.011,(z1-z0)*.22)
    profile=[(z0,r*.88),(z0+inset,r),(z1-inset,r),(z1,r*.90),
             (z1,r*wall),(z1-inset,r*wall*.86),(z0+inset,r*wall*.86),(z0,r*wall)]
    bore(name,(x,y,0),profile,finish,sides)


def segment(name, at, radius, depth, width, angle, sweep, finish):
    x,y,z=at
    sides=math.ceil(sweep/8)
    verts=[]
    for zz,rr in ((-depth*.5,radius), (depth*.5,radius*.98),
                  (depth*.5,radius-width),(-depth*.5,radius-width)):
        for i in range(sides+1):
            t=math.radians(angle+sweep*i/sides)
            verts.append((x+math.cos(t)*rr,y+math.sin(t)*rr,z+zz))
    stride=sides+1
    faces=[]
    for ring in range(4):
        partner=(ring+1)%4
        for i in range(sides):
            faces.append((ring*stride+i,ring*stride+i+1,partner*stride+i+1,partner*stride+i))
    faces+= [(0,stride,2*stride,3*stride),(sides,3*stride+sides,2*stride+sides,stride+sides)]
    art.surface(name,verts,faces,finish,.003)


def slab(name, outline, x, half_width, finish, chamfer=.006, bevel=.0025):
    """Extrude a designed side profile across the weapon. `outline` is a list of (z forward, y up)
    points; both side faces are inset by `chamfer` so the top and nose read as machined facets."""
    pts=[(y,z) for z,y in outline]
    area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(pts,pts[1:]+pts[:1]))
    if area<0:
        pts.reverse()
    n=len(pts)

    def inset(c):
        out=[]
        for i in range(n):
            p0,p1,p2=pts[i-1],pts[i],pts[(i+1)%n]
            normals=[]
            for a,b in ((p0,p1),(p1,p2)):
                dy,dz=b[0]-a[0],b[1]-a[1]
                length=math.hypot(dy,dz) or 1
                normals.append((-dz/length,dy/length))
            my,mz=normals[0][0]+normals[1][0],normals[0][1]+normals[1][1]
            scale=c/max(.35,1+normals[0][0]*normals[1][0]+normals[0][1]*normals[1][1])
            out.append((p1[0]+my*scale,p1[1]+mz*scale))
        return out

    rings=[(x-half_width,inset(chamfer)),(x-half_width+chamfer,pts),
           (x+half_width-chamfer,pts),(x+half_width,inset(chamfer))]
    verts=[(xx,y,z) for xx,ring in rings for y,z in ring]
    faces=[tuple(reversed(range(n)))]
    for r in range(3):
        for i in range(n):
            a=r*n+i
            b=r*n+(i+1)%n
            faces.append((a,b,b+n,a+n))
    faces.append(tuple(3*n+i for i in range(n)))
    art.surface(name,verts,faces,finish,bevel)


def grip(key, rear=-.302, stock=True):
    block("Trigger hand grip",.023,-.082,[(-.135,.038,.074,-.028),(-.116,.046,.09,-.017),
        (-.036,.044,.094,-.006),(-.018,.039,.071,0)],"Fabric",.006)
    for i in range(4):
        rail("Grip traction rib",.055,-.113-i*.012,-.108,-.028,.009,.005,"Graphite")
    for x in (-.046,.050):
        rail("Trigger guard branch",x,-.087,-.049,.056,.008,.009,"Alloy",-.018)
    rail("Forward guard return",.002,-.081,.043,.075,.050,.007,"Graphite",.022)
    if stock:
        rail("Recoil shoe",0,.015,rear,-.144,.079,.049,"Graphite",.018)
        rail("Rear strike plate",0,.009,rear-.025,rear+.014,.064,.043,"Fabric")


def channels(x, y, a, b, count, finish="Alloy"):
    step=(b-a)/count
    for i in range(count):
        rail("Machined ribs",x,y,a+i*step,a+(i+.66)*step,.009,.013,finish,.004)


def pulse():
    """Pulse / Metronome: a low wedge carbine. One continuous graphite chassis carries a long ivory
    ceramic spine split by two exact phase gaps into three beats; the gaps show the cyan contacts
    from the shooter's eye, so the rhythm reads on the top surface, not as rings on the muzzle.
    A short squared alloy brake ends a bare barrel. Read: dependable, measured, machined."""
    k="pulse"
    part(k,"Receiver")
    grip(k,-.236,stock=False)
    # Graphite chassis: raked heel, flat belly, magwell cut, a long forend that shrouds the barrel.
    slab("Graphite carbine chassis",[(-.236,.000),(-.222,-.038),(-.150,-.048),(-.060,-.048),
        (-.050,-.032),(.010,-.032),(.020,-.046),(.250,-.040),(.330,-.026),(.352,-.004),
        (.350,.020),(-.200,.026),(-.232,.022)],0,.034,"Graphite",.008,.003)
    # Three heavy ceramic beats along the spine: rear block, long middle, raked nose.
    beats=(((-.226,.024),(-.226,.054),(-.202,.074),(-.098,.077),(-.094,.024)),
           ((-.074,.024),(-.074,.080),(.096,.080),(.096,.024)),
           ((.116,.024),(.116,.078),(.236,.072),(.318,.040),(.330,.024)))
    for i,outline in enumerate(beats):
        slab("Ceramic spine beat",list(outline),0,.043 - i*.003,"Ceramic",.011,.0035)
    # Recessed contacts glow inside the two gaps; a graphite floor keeps them from reading as tape.
    for z in (-.084,.106):
        slab("Phase gap floor",[(z-.010,.024),(z-.010,.040),(z+.010,.040),(z+.010,.024)],0,.030,"Graphite",.002,.001)
        slab("Phase gap contact",[(z-.0035,.038),(z-.0035,.066),(z+.0035,.066),(z+.0035,.038)],0,.026,"PulseGlow",.001,0)
        for side in (-1,1):
            rail("Gap clamp",side*.040,.050,z-.007,z+.007,.005,.012,"Alloy")
    # Low rear sight blade and a recessed front post: a reliable sight line, no scope.
    slab("Rear sight blade",[(-.170,.074),(-.166,.092),(-.138,.094),(-.128,.076)],0,.011,"Graphite",.002,.0015)
    slab("Front sight post",[(.262,.068),(.264,.084),(.274,.084),(.280,.066)],0,.005,"Alloy",.0015,.001)
    # Side ribs on the forend: four evenly spaced cooling cuts (beat spacing) on both sides.
    for side in (-1,1):
        for i in range(4):
            z=.120+i*.050
            rail("Chassis cooling rib",side*.034,-.014,z,z+.026,.003,.014,"Alloy")
    rail("Cassette latch",-.033,-.040,-.110,-.090,.004,.008,"Amber")
    part(k,"Chamber")
    tube("Bare alloy barrel",0,0,-.090,.050,.0115,.55,"Alloy",18)
    tube("Barrel bearing",0,0,-.066,-.050,.019,.60,"Graphite",18)
    # Squared brake: two flat ported cheeks around the bore.
    slab("Brake block",[(.040,-.016),(.040,.016),(.094,.014),(.100,.008),(.100,-.010),(.094,-.016)],0,.017,"Alloy",.003,.002)
    for z in (.054,.070,.086):
        rail("Brake port",0,.0,z,z+.007,.019,.008,"Graphite")
    tube("Muzzle crown",0,0,.098,.104,.0125,.55,"Graphite",18)
    part(k,"Action")
    slab("Charging slide",[(-.050,-.010),(-.050,.010),(.036,.010),(.044,.000),(.036,-.010)],0,.009,"Alloy",.002,.0015)
    rail("Charging knob",-.017,.002,-.006,.020,.008,.010,"Graphite")
    part(k,"Payload")
    slab("Slim energy cassette",[(-.040,.010),(.034,.010),(.040,-.050),(-.028,-.056)],.034,.017,"Graphite",.003,.002)
    rail("Cassette status",.052,-.018,-.016,.022,.002,.020,"PulseGlow")


def scatter():
    """Scatter / Breaker: brutal and heavy. A tall, short, squared receiver carries over-under
    barrels behind a wrap-around ceramic breach shield split once by a phase gap. Seen from the eye
    it is a wall of ivory with two black bores; nothing about it is slim."""
    k="scatter"
    part(k,"Receiver")
    grip(k,-.205,stock=False)
    slab("Squared graphite breach",[(-.205,-.012),(-.196,-.058),(-.060,-.066),(-.052,-.036),
        (.020,-.036),(.030,-.078),(.222,-.078),(.236,-.050),(.236,.060),(.214,.074),(-.150,.074),
        (-.205,.040)],0,.056,"Graphite",.010,.0035)
    slab("Butt pad",[(-.236,-.020),(-.236,.034),(-.205,.044),(-.205,-.030)],0,.050,"Fabric",.006,.003)
    # Wrap-around shield: a heavy roof in two pieces and full-height cheeks.
    slab("Breach shield roof",[(-.160,.070),(-.150,.104),(.040,.112),(.040,.070)],0,.066,"Ceramic",.014,.004)
    slab("Breach shield brow",[(.058,.070),(.058,.114),(.228,.104),(.252,.070)],0,.068,"Ceramic",.014,.004)
    slab("Shield phase contact",[(.044,.072),(.044,.100),(.054,.100),(.054,.072)],0,.044,"ScatterGlow",.001,0)
    for side in (-1,1):
        slab("Shield cheek",[(.060,-.060),(.060,.068),(.226,.066),(.246,.020),(.240,-.066)],side*.060,.009,"Ceramic",.004,.002)
        for i in range(3):
            rail("Cheek bolt",side*.071,.020-i*.034,.214,.226,.006,.008,"Alloy")
        rail("Hazard latch band",side*.058,-.040,-.040,.000,.006,.012,"Amber")
    rail("Top carry rib",0,.118,-.120,.010,.012,.010,"Graphite")
    part(k,"Chamber")
    for y in (.036,-.036):
        tube("Over-under barrel",0,y,-.230,.090,.031,.72,"Alloy",24)
        tube("Heavy muzzle sleeve",0,y,.044,.098,.040,.70,"Graphite",24)
        tube("Bore flare",0,y,.084,.096,.025,.82,"ScatterGlow",20)
    slab("Barrel web",[(-.200,-.012),(-.200,.012),(.092,.012),(.092,-.012)],0,.016,"Graphite",.004,.002)
    slab("Muzzle yoke",[(.046,-.080),(.046,.080),(.094,.080),(.098,.060),(.098,-.060),(.094,-.080)],0,.012,"Graphite",.003,.002)
    part(k,"Action")
    block("Break-action latch and hinge",0,0,[(-.057,.039,.017,0),(-.037,.044,.026,0),
        (.044,.047,.021,.004),(.067,.029,.015,.008)],"Alloy",.005)
    rail("Thermal handle",-.040,.014,-.030,.051,.014,.011,"Secondary")
    part(k,"Payload")
    for y in (0,.036):
        tube("Twin high-pressure cartridges",0,y,-.040,.038,.019,.26,"Secondary",18)
        tube("Rim of cartridge",0,y,-.044,-.028,.023,.41,"Alloy",18)
        tube("Signal seal",0,y,.022,.040,.017,.24,"ScatterGlow",16)


def lance():
    k="lance"
    part(k,"Receiver")
    grip(k,-.39,stock=False)
    # Needle chassis: long, low, knife-like taper toward the rails.
    slab("Needle chassis",[(-.150,.024),(-.140,-.040),(-.050,-.044),(-.040,-.030),(.020,-.030),
        (.030,-.040),(.200,-.030),(.290,-.010),(.300,.006),(.240,.020),(-.120,.034)],0,.028,"Graphite",.007,.003)
    # Skeletal stock: a raked top bar, a lower strut and a narrow heel; open between them.
    slab("Stock top bar",[(-.420,.020),(-.410,.046),(-.140,.034),(-.140,.012)],0,.016,"Graphite",.004,.002)
    slab("Stock lower strut",[(-.400,-.042),(-.386,-.026),(-.150,-.012),(-.150,-.032)],0,.012,"Alloy",.003,.002)
    slab("Stock heel",[(-.432,-.050),(-.432,.048),(-.404,.050),(-.396,-.050)],0,.024,"Fabric",.005,.002)
    for side in (-1,1):
        rail("Needle guide",side*.045,.026,.148,.513,.011,.013,"Alloy",.008)
        rail("Splayed aft rail",side*.059,-.014,.055,.269,.012,.020,"Alloy",.012)
        rail("Exposed ceramic isolator",side*.044,.043,.267,.335,.014,.025,"Ceramic",-.01)
        rail("Floating front electrode",side*.042,.038,.395,.473,.010,.011,"LanceGlow")
    slab("Ceramic cheek blade",[(-.390,.048),(-.380,.066),(-.200,.058),(-.180,.040)],0,.020,"Ceramic",.006,.002)
    rail("Cantilevered sensor mount",0,.060,-.123,.067,.028,.030,"Alloy")
    tube("Long precision optic",0,.122,-.126,.200,.033,.72,"Graphite",24)
    tube("Forward optic lens",0,.122,.184,.201,.026,.25,"Visor",20)
    segment("Rear aperture",(0,.122,-.128),.029,.009,.006,0,320,"LanceGlow")
    part(k,"Chamber")
    for z in (-.024,.027,.077):
        tube("Disconnected focus annulus",0,.006,z,z+.016,.034,.62,"Alloy",18)
    for side in (-1,1):
        rail("Baffle guide",side*.034,.005,-.037,.091,.007,.012,"Graphite")
        rail("Front witness block",side*.038,.023,.062,.092,.008,.014,"Ceramic")
    part(k,"Action")
    rail("Long bolt carriage",0,.008,-.096,.084,.025,.023,"Alloy",.003)
    rail("Offset lock",-.025,-.009,-.061,.039,.022,.014,"Graphite")
    part(k,"Payload")
    tube("Phase needle cartridge",0,0,-.081,.072,.020,.37,"Alloy",16)
    tube("Copper isolator",0,0,-.060,-.035,.024,.36,"Secondary",16)
    tube("Optical release collar",0,0,.052,.075,.023,.31,"LanceGlow",16)


def arc():
    k="arc"
    part(k,"Receiver")
    grip(k,-.200,stock=False)
    slab("Offset transformer chassis",[(-.200,-.010),(-.190,-.060),(-.060,-.066),(-.050,-.036),
        (.020,-.036),(.030,-.070),(.150,-.064),(.176,-.030),(.170,.040),(.120,.070),(-.160,.060),
        (-.200,.034)],-.030,.050,"Graphite",.010,.0035)
    for x,y,z in ((-.058,.069,-.138),(-.044,-.038,-.109),(.059,.009,-.071)):
        tube("Stacked transformer winding",x,y,z,z+.135,.041,.65,"Alloy",16)
        for off in (.016,.057,.094):
            segment("Insulated winding crown",(x,y,z+off),.047,.014,.009,30,250,"Secondary")
    # Jagged ceramic back: a discharge-scarred ridge, deliberately unequal teeth.
    slab("Jagged ceramic back",[(-.180,.050),(-.172,.090),(-.140,.128),(-.118,.098),(-.090,.142),
        (-.062,.104),(-.030,.120),(-.004,.088),(.050,.098),(.090,.060),(.090,.050)],-.064,.022,"Ceramic",.007,.0025)
    slab("Arc scar contact",[(-.122,.096),(-.094,.136),(-.088,.128),(-.114,.092)],-.064,.024,"ArcGlow",.001,0)
    rail("Outboard bus",.106,-.035,-.106,.198,.018,.019,"Alloy",.011)
    rail("Isolated lower cell handle",-.061,-.076,.023,.195,.030,.017,"Fabric")
    channels(.110,-.018,.005,.158,4,"ArcGlow")
    part(k,"Chamber")
    for x,y,z0,z1 in ((-.061,.084,-.127,.053),(.087,-.020,-.134,.099)):
        rail("Cantilever discharge prong",x,y,z0,z1,.016,.019,"Alloy",.007)
        tube("Open electrode collector",x,y,z1-.035,z1+.002,.024,.50,"ArcGlow",14)
        segment("Porcelain neck isolator",(x,y,z0+.033),.044,.043,.018,10,223,"Ceramic")
    rail("Lower grounding fork",.054,-.057,-.101,.061,.011,.009,"Graphite")
    part(k,"Action")
    block("Side-loading sled",0,0,[(-.063,.030,.029,0),(-.042,.037,.032,0),
        (.044,.036,.028,.007),(.062,.025,.020,.015)],"Alloy",.005)
    rail("Sled handle",-.033,.024,-.045,.045,.009,.014,"Fabric")
    part(k,"Payload")
    block("Parallel charge block",0,0,[(-.048,.035,.033,0),(-.033,.042,.047,0),
        (.035,.034,.042,0),(.059,.030,.030,0)],"Graphite",.006)
    for x in (-.026,0,.026):
        rail("Electrode contact strip",x,.039,-.031,.044,.006,.006,"ArcGlow")


def rift():
    k="rift"
    part(k,"Receiver")
    grip(k,-.230,stock=False)
    # The receiver is torn: the rear half holds the grip, the front half floats 4 cm ahead,
    # lifted and rotated off-axis. Nothing bridges the tear except the field and three shards.
    slab("Rear torn receiver",[(-.236,.000),(-.226,-.050),(-.060,-.058),(-.050,-.034),(.010,-.034),
        (.010,.040),(-.020,.068),(-.024,.050),(-.044,.078),(-.200,.070),(-.236,.044)],0,.058,"Graphite",.010,.0035)
    slab("Rear ceramic shell",[(-.214,.066),(-.204,.092),(-.054,.098),(-.040,.080),(-.060,.066)],0,.066,"Ceramic",.012,.004)
    slab("Front torn receiver",[(.052,-.030),(.058,.052),(.076,.070),(.090,.046),(.110,.078),
        (.200,.064),(.214,.010),(.190,-.040)],.006,.052,"Graphite",.010,.0035)
    slab("Front ceramic shell",[(.068,.070),(.074,.098),(.196,.090),(.208,.066)],.006,.060,"Ceramic",.012,.004)
    for i,(z,y,size) in enumerate(((.024,.060,.012),(.034,.012,.009),(.020,-.024,.010))):
        slab("Suspended shard",[(z-size,y-size*.6),(z-size*.2,y+size),(z+size,y+size*.4),(z+size*.4,y-size)],
             (-.018,.022,-.004)[i],.010,"Ceramic",.003,.0015)
    slab("Tear field",[(.014,-.020),(.014,.050),(.048,.064),(.048,-.024)],0,.006,"RiftGlow",.001,0)
    for x,y,z,rake in ((-.091,.073,.227,.049),(.102,-.019,.246,-.027),(-.040,-.089,.247,-.032)):
        rail("Triangulated containment arm",x,y,.039,z,.024,.026,"Graphite",rake)
        rail("Alloy terminal saddle",x,y+rake,z-.063,z+.021,.028,.019,"Alloy")
    part(k,"Chamber")
    for i,(z,radius,angle,sweep) in enumerate(((-.088,.147,17,88),(-.045,.158,154,82),(.032,.119,269,82)),1):
        part(k,"Vane"+str(i))
        segment("Suspended ceramic containment vane",(0,.019,z),radius,.079,.033,angle,sweep,"Ceramic")
        segment("Alloy field return",(0,.019,z-.027),radius-.014,.019,.020,angle+4,sweep-8,"Alloy")
        segment("Interior phase pickup",(0,.019,z+.029),radius-.025,.009,.009,angle+7,sweep-16,"RiftGlow")
    part(k,"Chamber")
    # A dark faceted void, not a glowing gem: the field shows only as a thin inner seed.
    block("Suspended faceted core",0,.019,[(-.111,.012,.015,-.004),(-.061,.053,.054,0),
        (-.011,.052,.046,.009),(.053,.014,.019,.005)],"Visor",.003)
    block("Core seed",0,.019,[(-.070,.004,.004,0),(-.040,.012,.030,0),(-.020,.012,.026,.004),(.010,.004,.004,0)],"RiftGlow",.001)
    part(k,"Action")
    block("Rotating containment gate",0,0,[(-.064,.043,.024,0),(-.031,.054,.038,0),
        (.059,.046,.033,0),(.073,.037,.022,0)],"Alloy",.006)
    rail("Gate release tongue",-.038,.025,-.043,.061,.022,.016,"Secondary")
    part(k,"Payload")
    # Alloy ampoule with a single narrow field window; the glow must not read as a toy sphere.
    tube("Sealed reactor ampoule",0,.005,-.05,.05,.033,.17,"Alloy",20)
    tube("Ampoule field window",0,.005,-.006,.006,.035,.80,"RiftGlow",20)
    for z in (-.042,.027):
        tube("Ampoule isolator cap",0,.005,z,z+.024,.043,.65,"Graphite",18)


for name,color in MOTIFS.items():
    art.MATERIALS[name.capitalize()+"Glow"]=(color,.04,.32)
art.reset()
for model in (pulse,scatter,lance,arc,rift):
    model()

# Preview layout translates whole weapons while keeping each assembly at its gameplay pivot.
display={}
for name,pivot in PIVOTS.items():
    index=list(SPECS).index(name.split("_")[0])
    layout_x=(index-2)*.455
    display[name]=(layout_x+pivot[0],pivot[1],pivot[2])
art.finish("Arsenal","Armory/Arsenal.fbx",display,camera_at=(1.74,-2.36,3.98),
    camera_target=(0,-.13,0),camera_width=2.95,resolution=(1440,1060))
