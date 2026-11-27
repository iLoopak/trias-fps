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
    PIVOTS[art.group]={"Receiver":(0,0,0),"Chamber":info[1],"Action":info[2],"Payload":info[3]}[name]


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
    block("Compact kinetic receiver",0,0,[(-.261,.062,.060,.019),(-.171,.087,.081,.013),
        (.118,.077,.073,.017),(.247,.065,.044,.022)],"Graphite",.007)
    for side in (-1,1):
        block("Ivory removable upper",side*.079,.052,[(-.216,.026,.047,-.019),
            (-.163,.041,.065,-.010),(.115,.035,.054,.012),(.220,.022,.031,.009)],"Ceramic",.006)
        rail("Floating insulation rib",side*.091,-.033,-.126,.188,.027,.018,"Secondary")
        rail("Recessed induction bus",side*.107,.009,.008,.190,.015,.014,"Graphite")
        rail("Induction slit",side*.117,.013,.022,.169,.007,.006,"PulseGlow")
        rails_z=-.12 if side<0 else -.10
        channels(side*.106,-.011,rails_z,rails_z+.093,4)
        rail("Half-floating current contact",side*.073,.026,.201,.275,.021,.022,"Alloy",.008)
    rail("Amber upper service latch",0,.094,-.204,-.096,.046,.017,"Amber",.003)
    rail("Upper rear optic spine",0,.112,-.099,.203,.032,.011,"Graphite",-.002)
    for x in (-.032,.032):
        rail("Protected aiming post",x,.128,.120,.198,.011,.036,"Graphite")
    rail("Rear sight lip",0,.161,.132,.178,.038,.006,"Alloy")
    rail("Separated front post",0,.107,.253,.284,.012,.015,"PulseGlow")

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
    block("Break-action breech frame",0,-.005,[(-.193,.070,.053,.025),(-.138,.102,.069,.004),
        (.119,.121,.064,.007),(.274,.121,.043,.021)],"Graphite",.008)
    rail("Wide heat-isolated pump grip",0,-.065,.070,.257,.107,.038,"Fabric")
    for side in (-1,1):
        for at in (.082,.117,.152,.187,.221):
            rail("Pump ribs",side*.080,-.073,at,at+.021,.021,.031,"Alloy")
        block("Recoil yoke ceramic",side*.092,.025,[(-.124,.047,.049,-.003),(-.088,.052,.059,.006),
            (.178,.055,.049,.004),(.236,.043,.037,0)],"Ceramic",.008)
        rail("Warning lock on each shoulder",side*.111,.078,-.027,.179,.023,.011,"Amber")
        tube("Hinge pin socket",side*.083,.028,.206,.28,.036,.40,"Alloy",16)
        rail("Local pressure check",side*.132,.009,.151,.208,.007,.015,"ScatterGlow")
    rail("Central firing notch",0,.099,.097,.192,.019,.016,"Graphite")
    part(k,"Chamber")
    for side in (-1,1):
        x=side*.073
        tube("Twin counterbored tube",x,.016,-.097,.094,.069,.64,"Alloy",24)
        tube("Black muzzle sleeve",x,.016,.067,.095,.070,.80,"Graphite",24)
        segment("Ceramic barrel shoulder",(x,.016,-.002),.079,.130,.019,13 if side>0 else 144,199,"Ceramic")
        segment("Safety index",(x,.016,-.035),.081,.096,.008,39,58,"Amber")
        segment("Chamber separator",(x,.016,-.102),.058,.012,.017,3,155,"ScatterGlow")
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
    grip(k,-.37)
    block("Long linear action",0,.019,[(-.331,.055,.045,-.012),(-.264,.080,.065,0),
        (.192,.074,.061,.022),(.309,.054,.039,.028)],"Graphite",.007)
    for side in (-1,1):
        rail("Long front ceramic rail",side*.071,.039,-.124,.467,.025,.035,"Ceramic",.015)
        rail("Visible inner guide",side*.055,.034,.238,.544,.015,.014,"Alloy")
        rail("Lance sensor path",side*.069,.056,.281,.477,.004,.010,"LanceGlow")
        channels(side*.084,-.014,-.123,.023,5)
    tube("Open isolation tube",0,.042,.225,.501,.030,.66,"Graphite",20)
    rail("Adjustable cheekpiece",0,.080,-.325,-.139,.053,.023,"Secondary",.005)
    rail("Precision sight channel",0,.127,-.099,.185,.052,.022,"Alloy")
    tube("Long rangefinder body",0,.158,-.093,.164,.036,.63,"Graphite",20)
    tube("Rangefinder lens",0,.158,.147,.165,.027,.27,"Visor",20)
    segment("Rear optic pupil",(0,.158,-.095),.030,.009,.006,0,340,"LanceGlow")
    part(k,"Chamber")
    tube("Reentrant muzzle break",0,.019,-.046,.093,.041,.61,"Graphite",20)
    for side in (-1,1):
        rail("Isolated baffle cheeks",side*.041,.019,-.031,.083,.012,.037,"Ceramic",-.013)
        for z in (-.025,.024,.064):
            rail("Baffle vent",side*.043,.022,z,z+.012,.004,.029,"Alloy")
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
    block("Compact pulse transformer",0,.009,[(-.199,.076,.057,-.005),(-.171,.095,.067,-.007),
        (.158,.090,.062,.004),(.213,.064,.044,.015)],"Graphite",.007)
    for side in (-1,1):
        z=.018*side
        rail("Asymmetric upper insulated shell",side*.079,.073,-.147+z,.134+z,.035,.038,"Ceramic",.014)
        rail("Service slab",side*.094,-.028,-.087+z,.142+z,.027,.030,"Secondary",.008)
        rail("Copper collector",side*.111,.011,-.084+z,.142+z,.013,.018,"Alloy")
        channels(side*.124,.020,-.061+z,.131+z,6,"ArcGlow")
    rail("Arc calibration socket",0,.107,.028,.133,.037,.016,"Amber")
    part(k,"Chamber")
    for side in (-1,1):
        x=side*.064
        z=.027*side
        tube("Cantilever electrode",x,.029,-.075+z,.072+z,.024,.39,"Alloy",18)
        tube("Stake plug",x,.029,.049+z,.070+z,.028,.43,"ArcGlow",18)
        segment("Broken ceramic jaw",(x,.029,.006+z),.048,.119,.018,35 if side>0 else 120,222,"Ceramic")
        for setback in (-.042,.009):
            segment("Dielectric rings",(x,.029,setback+z),.037,.016,.011,4,340,"Graphite")
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
    block("Heavy containment drive",0,0,[(-.262,.070,.057,.006),(-.197,.108,.073,.016),
        (.188,.112,.084,.025),(.270,.103,.059,.032)],"Graphite",.009)
    for side in (-1,1):
        rail("Layered reaction case",side*.099,.066,-.194,.229,.043,.051,"Ceramic",.026)
        rail("High temperature isolator",side*.117,-.027,-.123,.245,.031,.041,"Secondary")
        rail("Transverse retainer",side*.132,.010,-.125,.230,.010,.030,"Alloy")
        channels(side*.139,.023,-.078,.222,5,"Graphite")
        rail("Concentrated phase thread",side*.143,.028,-.017,.174,.006,.009,"RiftGlow")
    rail("Interlock block",0,.121,-.134,.179,.059,.025,"Graphite")
    rail("Armature interlock tab",0,.147,-.093,-.012,.035,.009,"Amber")
    part(k,"Chamber")
    tube("Rift containment inner bore",0,.012,-.096,.094,.062,.70,"Graphite",24)
    tube("Inner energy capsule",0,.012,-.042,.064,.042,.14,"RiftGlow",24)
    for angle in (27,147,267):
        segment("Outer shutters",(0,.012,-.001),.120,.182,.030,angle,98,"Ceramic")
        segment("Alloy baffle",(0,.012,.086),.106,.037,.024,angle+7,83,"Alloy")
        segment("Tracking seam",(0,.012,-.098),.103,.020,.013,angle+12,68,"RiftGlow")
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
