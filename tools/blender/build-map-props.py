"""Author map cover, trees, a river bridge and station fixtures."""
import math
import random
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band, panel

art.MATERIALS.update({
    "DryStone": ((.37,.35,.30),0,.87), "DarkStone": ((.22,.24,.21),0,.83),
    "Moss": ((.26,.34,.17),0,.90), "Bark": ((.19,.12,.065),0,.88),
    "Leaf": ((.17,.29,.135),0,.85), "YoungLeaf": ((.30,.42,.18),0,.8),
    "Wood": ((.43,.30,.15),0,.78), "Heat": ((.80,.22,.036),0,.48),
})
art.reset()


def shingle(name, seed, width=1.0, outcrop=False):
    rng=random.Random(seed)
    art.group=name
    sides=13
    rings=[(-.47,.76),(-.32,1),(-.10,.88),(.035,.87),(.21,.76),(.34,.62),(.47,.31)]
    samples=[(rng.uniform(.74,1.18),rng.uniform(-.12,.12)) for _ in range(sides)]
    verts=[]
    for j,(y,r) in enumerate(rings):
        for i,(lump,offset) in enumerate(samples):
            angle=math.tau*i/sides+.13+j*.044
            seam=math.sin(i*2.7+j*.65)*.061
            verts.append((math.sin(angle)*r*lump*.49*width+j*.018-.043,
                         y+offset*r+seam,math.cos(angle)*r*lump*.52-j*.018))
    faces=[tuple(reversed(range(sides)))]
    for j in range(len(rings)-1):
        for i in range(sides):
            a=j*sides+i
            b=j*sides+(i+1)%sides
            if (i+j)%3:
                faces.append((a,b,b+sides,a+sides))
            else:
                faces.extend(((a,b,a+sides),(b,b+sides,a+sides)))
    faces.append(tuple((len(rings)-1)*sides+i for i in range(sides)))
    art.surface("Fractured layers",verts,faces,"DarkStone",smooth=False)
    mesh=art.parts[name][-1].data
    mesh.materials.append(art.paints["DryStone"])
    mesh.materials.append(art.paints["Moss" if not outcrop else "Alloy"])
    for polygon in mesh.polygons:
        phase=polygon.center.z
        polygon.material_index=2 if phase>.27 and polygon.index%4!=0 else int(.3+rng.random()>.64)

for name,seed,width in (("Granite",379,.89),("Shelves",41,1.26),("SplitRock",44,.99)):
    shingle(name,seed,width)
shingle("Ore",805,1.14,True)

art.group="Spruce"
band("Branching spruce trunk",(0,0,0),[(-.5,.072,.062,0),(-.2,.051,.056,0),(.21,.033,.031,0),(.49,.009,.009,0)],"Bark",10)
rng=random.Random(301)
for tier in range(7):
    y=-.257+tier*.10
    radius=.45-tier*.049
    branches=7 if tier<4 else 6
    for i in range(branches):
        angle=i*math.tau/branches+tier*.412+rng.uniform(-.16,.16)
        reach=radius*rng.uniform(.78,1.08)
        dx,dz=math.sin(angle),math.cos(angle)
        left=(dz,-dx)
        branch=[(0,y,-.002),(dx*reach,y-.15*radius,dz*reach),
                (dx*reach*.56+left[0]*radius*.35,y-.03,dz*reach*.56+left[1]*radius*.35),
                (dx*reach*.29,y+.13-tier*.009,dz*reach*.29),
                (dx*reach*.62-left[0]*radius*.38,y-.018,dz*reach*.62-left[1]*radius*.38)]
        faces=[(0,2,3),(2,1,3),(1,4,3),(4,0,3),(0,4,1,2)]
        art.surface("Splayed needle branch",branch,faces,"Leaf" if (tier+i)%3 else "YoungLeaf",smooth=False)

art.group="Bridge"
for row in range(22):
    z=(row-10.5)*.24
    panel("Weathered plank",0,.0,z,2.77,.092,.218,"Wood" if row%4 else "Bark",bevel=.014)
for side in (-1,1):
    panel("Edge binding",side*1.22,-.052,2.64,.16,.13,5.28,"Bark",bevel=.019)

art.group="Airlock"
band("Pressure bulkhead",(0,0,0),[(-.48,.46,.48,0),(-.36,.5,.5,0),(.28,.473,.489,0),(.46,.382,.469,0)],"Graphite",10,.47)
panel("Habitat casing",-.08,.113,.491,.688,.552,.111,"Ceramic",-.15,.032)
panel("Interlock status",.284,.051,.5,.171,.218,.14,"Alloy",.12,.025)
panel("Safety seam",-.28,-.19,.504,.372,.042,.019,"Amber",-.07)
for side in (-1,1):
    panel("Side return",side*.362,-.21,.443,.201,.198,.17,"Secondary",side*.14,.028)

art.group="Kiln"
band("Refractory chamber",(0,0,0),[(-.50,.49,.5,0),(-.31,.50,.5,0),(.22,.385,.386,0),(.38,.435,.409,0),(.48,.374,.363,0)],"Graphite",12,.71)
for i in range(3):
    y=.17-i*.16
    band("Refractory baffle",(0,y,0),[(-.08,.40,.41,0),(-.043,.467,.47,0),(.039,.451,.455,0),(.074,.41,.42,0)],"Secondary",12,.77)
    panel("Furnace seam",0,y+.045,.459,.51,.027,.014,"Heat",bevel=.002)
panel("Ceramic heat shield",0,-.294,.456,.68,.217,.132,"Ceramic",bevel=.026)

art.group="Slag"
band("Cooling bed assembly",(0,0,0),[(-.48,.49,.47,0),(-.22,.46,.50,0),(.28,.438,.46,0),(.47,.372,.446,0)],"Alloy",10,.58)
for i in range(5):
    panel("Vent slot",(i-2)*.143,.101,.461,.076,.274,.086,"Graphite",.20,.012)
panel("Temperature lockout",.034,-.142,.497,.419,.068,.017,"Heat",-.16,.008)

show={}
for i,slot in enumerate(art.parts):
    show[slot]=((i%5-2)*1.48, .0, (i//5-.5)*3.25)
art.finish("MapProps","MapProps/MapProps.fbx",show,camera_at=(4,-6,9),camera_target=(0,0,0),
    camera_width=9.6,resolution=(1260,920))
