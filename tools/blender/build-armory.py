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


def grip(key, rear=-.302):
    block("Trigger hand grip",.023,-.082,[(-.135,.038,.074,-.028),(-.116,.046,.09,-.017),
        (-.036,.044,.094,-.006),(-.018,.039,.071,0)],"Fabric",.006)
    for i in range(4):
        rail("Grip traction rib",.055,-.113-i*.012,-.108,-.028,.009,.005,"Graphite")
    for x in (-.046,.050):
        rail("Trigger guard branch",x,-.087,-.049,.056,.008,.009,"Alloy",-.018)
    rail("Forward guard return",.002,-.081,.043,.075,.050,.007,"Graphite",.022)
    rail("Recoil shoe",0,.015,rear,-.144,.079,.049,"Graphite",.018)
    rail("Rear strike plate",0,.009,rear-.025,rear+.014,.064,.043,"Fabric")


def channels(x, y, a, b, count, finish="Alloy"):
    step=(b-a)/count
    for i in range(count):
        rail("Machined ribs",x,y,a+i*step,a+(i+.66)*step,.009,.013,finish,.004)


def pulse():
    k="pulse"
    part(k,"Receiver")
    grip(k)
    block("Cast compression spine",0,-.013,[(-.255,.061,.048,.009),(-.198,.078,.073,.014),
        (.082,.070,.062,.007),(.266,.042,.034,.019)],"Graphite",.008)
    for side in (-1,1):
        block("Oval ceramic saddle",side*.065,.043,[(-.231,.018,.026,-.018),
            (-.176,.037,.057,-.002),(-.086,.042,.064,.003),(.065,.032,.052,.007),
            (.110,.019,.029,.013)],"Ceramic",.009)
        rail("Exposed compression strut",side*.061,.008,.075,.270,.015,.020,"Alloy",.008)
        rail("Induction contact",side*.077,.038,.102,.205,.008,.009,"PulseGlow")
    tube("Visible rear piston",0,.035,.117,.255,.035,.67,"Alloy",20)
    rail("Cassette lock",0,.100,-.155,-.061,.030,.009,"Amber")
    for x in (-.033,.033):
        rail("Sighting horns",x,.100,.108,.174,.008,.023,"Graphite")
    rail("Front witness",0,.108,.241,.272,.010,.009,"PulseGlow")

    part(k,"Chamber")
    tube("Hollow concentric cylinder",0,.011,-.078,.099,.063,.55,"Alloy",24)
    tube("Receiver port inset",0,.011,.077,.101,.039,.69,"Graphite",24)
    for angle in (18,198):
        segment("Suspended porcelain chamber", (0,.011,-.003),.102,.141,.029,angle,138,"Ceramic")
        segment("Chamber rear coupler", (0,.011,-.076),.088,.032,.027,angle+13,112,"Graphite")
        segment("Phase contacts", (0,.011,-.093),.086,.008,.013,angle+21,86,"PulseGlow")
    part(k,"Action")
    rail("T-shaped charging slide",0,0,-.069,.054,.032,.014,"Alloy")
    rail("Charging slide grip",-.034,.003,-.047,.051,.016,.018,"Graphite")
    rail("Charging witness",-.042,.017,-.034,.043,.004,.006,"PulseGlow")
    part(k,"Payload")
    block("Curved feed cassette",0,-.031,[(-.052,.035,.057,.015),
        (-.029,.038,.071,.003),(.036,.039,.066,-.012),(.059,.030,.046,-.016)],"Fabric",.005)
    for side in (-1,1):
        channels(side*.032,-.023,-.034,.044,4,"Graphite")
    rail("Magazine status runner",-.035,-.036,-.032,.036,.006,.008,"PulseGlow")
    rail("Magazine retaining shoe",0,-.079,-.055,.052,.044,.012,"Secondary")


def scatter():
    k="scatter"
    part(k,"Receiver")
    grip(k,-.263)
    block("Short pressure bridge",0,-.027,[(-.175,.041,.046,.017),(-.10,.085,.057,.003),
        (.125,.140,.038,0),(.268,.142,.028,.011)],"Graphite",.007)
    for side in (-1,1):
        x=side*.124
        rail("Dropped industrial support grip",side*.087,-.084,.033,.223,.060,.036,"Fabric")
        tube("Longitudinal hinge bearing",side*.155,-.031,.122,.263,.035,.40,"Alloy",16)
        rail("Latch between fork and receiver",side*.120,.045,-.012,.227,.023,.023,"Amber",-.019)
        for z in (.055,.098,.141):
            rail("Ribbed pressure frame",side*.144,-.056,z,z+.02,.029,.018,"Alloy")
    rail("Open central spine",0,.047,-.128,.246,.024,.024,"Graphite")
    part(k,"Chamber")
    for side in (-1,1):
        x=side*.097
        tube("Twin counterbored discharge",x,.016,-.153,.094,.084,.67,"Alloy",24)
        tube("Recessed muzzle sleeve",x,.016,.063,.095,.073,.80,"Graphite",24)
        segment("Massive pressure shell",(x,.016,-.006),.094,.147,.025,
                22 if side>0 else 96,236,"Ceramic")
        segment("Orange ceramic collar",(x,.016,-.093),.093,.029,.014,18,162,"Amber")
        segment("Separated pressure seal",(x,.016,-.143),.080,.015,.019,13,170,"ScatterGlow")
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
    grip(k,-.39)
    block("Low instrument bed",0,-.014,[(-.364,.039,.037,.019),(-.268,.065,.060,0),
        (.098,.053,.051,.003),(.223,.030,.023,.012)],"Graphite",.006)
    for side in (-1,1):
        rail("Needle guide",side*.045,.026,.148,.513,.011,.013,"Alloy",.008)
        rail("Splayed aft rail",side*.059,-.014,.055,.269,.012,.020,"Alloy",.012)
        rail("Exposed ceramic isolator",side*.044,.043,.267,.335,.014,.025,"Ceramic",-.01)
        rail("Floating front electrode",side*.042,.038,.395,.473,.010,.011,"LanceGlow")
    rail("Rear cheek pad",0,.051,-.381,-.170,.049,.016,"Secondary",.005)
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
    grip(k,-.250)
    block("Vertical transformer chassis",-.028,-.010,[(-.188,.044,.070,-.004),(-.148,.063,.105,.004),
        (.081,.059,.100,.016),(.163,.051,.055,.025)],"Graphite",.008)
    for x,y,z in ((-.058,.069,-.138),(-.044,-.038,-.109),(.059,.009,-.071)):
        tube("Stacked transformer winding",x,y,z,z+.135,.041,.65,"Alloy",16)
        for off in (.016,.057,.094):
            segment("Insulated winding crown",(x,y,z+off),.047,.014,.009,30,250,"Secondary")
    block("Tall canted ceramic back",-.065,.094,[(-.171,.032,.028,-.026),(-.119,.043,.052,.011),
        (.003,.040,.048,.009),(.079,.022,.028,-.006)],"Ceramic",.006)
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
    grip(k,-.291)
    block("Inclined containment saddle",0,-.011,[(-.256,.055,.047,.005),(-.179,.094,.063,.018),
        (.060,.080,.051,.010),(.178,.045,.028,-.016)],"Graphite",.008)
    rail("Ceramic yoke heel",.039,.061,-.217,.058,.067,.041,"Ceramic",.011)
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
    block("Suspended faceted core",0,.019,[(-.111,.012,.015,-.004),(-.061,.053,.054,0),
        (-.011,.052,.046,.009),(.053,.014,.019,.005)],"RiftGlow",.003)
    part(k,"Action")
    block("Rotating containment gate",0,0,[(-.064,.043,.024,0),(-.031,.054,.038,0),
        (.059,.046,.033,0),(.073,.037,.022,0)],"Alloy",.006)
    rail("Gate release tongue",-.038,.025,-.043,.061,.022,.016,"Secondary")
    part(k,"Payload")
    tube("Sealed reactor ampoule",0,.005,-.05,.05,.035,.17,"RiftGlow",20)
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
