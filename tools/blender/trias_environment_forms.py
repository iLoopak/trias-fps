"""Form recipes for the TRIAS environment library. Coordinates are Unity metres."""
import math
from random import Random

from mathutils import Matrix, Vector

import trias_mesh as mesh


def ring(w, d, x=0, z=0, cut=.15):
    """Clockwise plan viewed from above, with independent corner shoulders."""
    a, b = w / 2, d / 2
    c, e = min(cut, a * .55), min(cut, b * .55)
    return [(x-a+c,z-b), (x+a-c,z-b), (x+a,z-b+e), (x+a,z+b-e),
            (x+a-c,z+b), (x-a+c,z+b), (x-a,z+b-e), (x-a,z-b+e)]


def loft(name, sections, finish, bevel=0):
    """Sections are (height, plan). Per-vertex heights allow inclined strata."""
    n = len(sections[0][1])
    vertices = [(p[0], y + (p[2] if len(p) > 2 else 0), p[1])
                for y, plan in sections for p in plan]
    faces = [tuple(range(n))]
    for j in range(len(sections)-1):
        for i in range(n):
            a, b = j*n+i, j*n+(i+1)%n
            faces.append((b, a, a+n, b+n))
    faces.append(tuple(reversed(range(len(vertices)-n, len(vertices)))))
    mesh.surface(name, vertices, faces, finish, bevel, segments=1)


def block(name, x, y, z, w, h, d, finish, cut=.10, bevel=0, taper=1, lean=0):
    loft(name, [(y,ring(w,d,x,z,cut)), (y+h,ring(w*taper,d*taper,x+lean,z,cut))],finish,bevel)


def plates(name, points, z, depth, finish, bevel=0):
    """Same profile on both faces, rotated 180 degrees for the reverse face."""
    mesh.plate(name,[(x,y,None) for x,y in points],z,depth,finish,bevel,segments=1)
    mesh.plate(name,[(x,y,None) for x,y in points],z,depth,finish,bevel,segments=1)
    mesh.parts[mesh.group][-1].data.transform(Matrix.Rotation(math.pi,4,"Z"))


def profile(x0,x1,y0,y1,tilt=.12):
    dx = min(.18, (x1-x0)*.24)
    dy = min(.17, (y1-y0)*.26)
    return [(x0,y0),(x1-dx,y0),(x1,y0+dy),(x1+tilt,y1-dy),
            (x1+tilt-dx,y1),(x0+tilt+dx,y1),(x0+tilt,y1-dy)]


def wall(style, detail):
    w,h,d = 3.2,3.0,.62
    # A continuous pressure hull stays readable as solid across the seams.
    block("Continuous pressure hull",0,0,0,w,h,d*.90,"Graphite",.035,.007 if detail else 0)
    shell = "Refractory" if style == "Reinforced" else "Ceramic"
    if style == "Recessed":
        plates("Service throat",profile(-1.36,1.30,.53,2.54),.29,.032,"Alloy",.008 if detail else 0)
    if style == "Core":
        for i in range(5):
            plates("Compression vane",profile(-.68+i*.26,-.53+i*.26,.48,2.56,-.27),.298,.053,"Alloy")
    if style == "Angled":
        plates("Raked stress panel", profile(-1.57,.79,.25,2.74,.61),.31,.048,shell,.017 if detail else 0)
        plates("Counterbrace",profile(.90,1.42,.09,1.72,-.44),.307,.09,"Secondary")
    elif style == "Phase":
        plates("Lower floating shoulder", [(-1.56,.36),(.81,.36),(.47,1.20),(-1.32,1.49)],.31,.040,shell,.014 if detail else 0)
        plates("Suspended crown", [(-.93,1.75),(.34,1.59),(1.54,2.02),(1.53,2.83),(-.48,2.83)],.31,.037,shell,.015 if detail else 0)
        for x in (-.65,.42):
            plates("Paired phase guide",profile(x,x+.22,1.27,1.92,.17),.294,.10,"Alloy")
    else:
        split = {"Standard":.47,"Reinforced":.22,"Recessed":.94,"Core":.79,"End":.64}.get(style,.48)
        plates("Left armor shell",profile(-1.55,-split,.34,2.75,.12),.31,.073,shell,.016 if detail else 0)
        plates("Right armor shell",profile(split,1.54,.41,2.71,-.13),.31,.076,
               "Secondary" if style == "End" else shell,.014 if detail else 0)
        if style in ("Standard","Reinforced","End"):
            plates("Mechanical splice",profile(-.44,.38,.45,2.46,.11),.298,.055,"Alloy" if style == "Reinforced" else "Secondary")
    block("Wear sill",0,.05,0,3.16,.19,.61,"Secondary",.055,.009 if detail else 0)
    block("Panel head datum",0,2.84,0,3.18,.13,.61,"Graphite",.035)
    if detail:
        plates("Registry plaque",profile(-1.34,-.81,.49,.70,.015),.313,.006,"Graphite")
        for i in range(3):
            plates("Registry tally",profile(-1.30+i*.11,-1.23+i*.11,.525,.63,.01),.316,.003,"Amber")


def beam(name,a,b,width,depth,finish,sides=8):
    a,b=Vector(a),Vector(b)
    axis=(b-a).normalized()
    right=Vector((0,0,1)).cross(axis)
    if right.length < .1:
        right=Vector((1,0,0)).cross(axis)
    right.normalize()
    other=axis.cross(right)
    vertices=[tuple(p + right*(math.cos(i*math.tau/sides)*r) +
                    other*(math.sin(i*math.tau/sides)*q))
              for p,r,q in ((a,width/2,depth/2),(b,width*.39,depth*.39)) for i in range(sides)]
    faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]
    for i in range(sides):
        j=(i+1)%sides
        faces.append((i,j,j+sides,i+sides))
    mesh.surface(name,vertices,faces,finish)


def support(style, detail):
    heavy=style=="Heavy"
    block("Shoe",0,0,0,1.58,.22,1.38,"Graphite",.18,.013 if detail else 0)
    if style == "Bracket":
        beam("Wall diagonal",(-.57,.46,0),(.71,2.48,0),.55,.77,"Alloy")
        beam("Triangulated hanger",(.52,2.55,0),(.54,1.0,0),.48,.93,"Graphite")
    else:
        lean=.19 if style in ("Angled","Phase") else 0
        loft("Tapered graphite load path",[(.16,ring(1.48,1.33)),(.43,ring(1.43,1.32)),
             (2.55,ring(1.18,1.12,lean)),(3.095,ring(1.48,1.30))],"Graphite",.03 if detail else 0)
        shell="Refractory" if heavy else "Alloy" if style=="Mechanical" else "Ceramic"
        for x in (-.49,.47):
            if heavy:
                # The lower refractory block seats against a separate inclined load shoe.
                outline = ([(-.74,.28),(.62,.28),(.64,.76),(.11,1.16),(-.72,1.03)]
                           if x<0 else [(-.69,1.28),(.12,1.41),(.64,.98),(.64,2.47),(.37,2.87),(-.48,2.87)])
                plates("Refractory thrust block" if x<0 else "Inclined pressure shoe",
                       outline,.69,.13,shell if x<0 else "Secondary",.018 if detail else 0)
            else:
                plates("Compression cheeks",profile(x-.29,x+.27,.31,1.51 if style=="Phase" else 2.92,lean),
                       .69,.13,shell,.018 if detail else 0)
            if style=="Phase":
                plates("Aligned compression shoe",profile(x-.18,x+.18,1.29,1.93,lean),.64,.13,"Alloy",.009 if detail else 0)
                plates("Suspended ceramic shoulder",profile(x-.37,x+.23,1.77,2.97,lean),.69,.17,shell,.018 if detail else 0)
        if heavy or style=="Mechanical":
            # The player approaches the short ends as often as the plated broad face.
            # A furnace jaw carries load low; a survey bracket reaches up to its datum.
            sections = (
                [(.24,.17,.87),(.48,.22,1.03),(1.26,.16,.82),(1.64,.08,.46)]
                if heavy else
                [(.36,.08,.35),(.61,.13,.50),(1.92,.15,.66),(2.58,.10,.45)]
            )
            for side in (-1,1):
                courses=[]
                for y,width,depth in sections:
                    inset=.69-.085*(y/3)
                    courses.append((y,ring(width,depth,side*inset,.08*side,cut=.052)))
                loft("Furnace thrust jaw" if heavy else "Survey datum bracket",
                     courses,shell,.012 if detail else 0)
    block("Bearing cap",0,3.02,0,1.57,.18,1.38,"Secondary",.10,.009 if detail else 0)


def cover(style, detail):
    h = {"Low":1.12,"Full":2.35,"Reinforced":1.52,"Equipment":1.48,"Ceramic":1.44,"Phase":1.36,"Weathered":1.34}[style]
    w,d = 2.45,1.12
    shell={"Reinforced":"Refractory","Weathered":"Secondary"}.get(style,"Ceramic")
    # Keep the backing almost at the fitted collision extents, even at the lip.
    loft("Solid cover backing",[(0,ring(w,d*.965,cut=.055)),(h*.21,ring(w,d*.965,cut=.05)),
         (h*.73,ring(w,d*.97,cut=.045)),(h-.042,ring(w,d*.96,cut=.065))],"Graphite",.009 if detail else 0)
    block("Rail cap",0,h-.065,0,w-.10,.065,d,"Alloy",.09,.009 if detail else 0)
    if style in ("Phase","Weathered"):
        plates("Lower shell", [(-1.18,.11),(.63,.08),(.30,h*.49),(-.85,h*.64)], d/2,.055,shell,.012 if detail else 0)
        plates("Separated shoulder", [(-.19,h*.67),(.49,h*.51),(1.17,h*.72),(1.08,h*.92),(.12,h*.91)], d/2,.075,shell,.012 if detail else 0)
        plates("Phase keeper",profile(.22,.62,h*.26,h*.80,.15),d/2-.014,.045,"Alloy")
    elif style == "Equipment":
        for i in range(4):
            plates("Cooling fin",profile(-.71+i*.29,-.62+i*.29,h*.31,h*.68,.09),d/2,.075,"Alloy")
        for x in (-1.11,.65):
            plates("Cooler end plate",profile(x,x+.43,h*.08,h*.91,.06),d/2,.09,shell,.018 if detail else 0)
    else:
        plates("Ceramic shear surface",profile(-1.14,.47,h*.20,h*.93,.24),d/2,.068,shell,.016 if detail else 0)
        plates("Armor toe",profile(.57,1.16,h*.09,h*.74,-.13),d/2-.009,.075,"Secondary")
    if detail:
        for x in (-.95,.89):
            plates("Recess index",profile(x,x+.16,h*.15,h*.32,.035),d/2+.004,.004,"Amber")


def container(style, detail):
    w,h,d=1.86,1.08,1.26
    block("Pressure vessel",0,.11,0,w*.97,h*.83,d*.91,"Graphite",.20,.022 if detail else 0)
    for x in (-.70,.70):
        block("Load bearing corner cradle",x,0,0,.46,.32,d,"Graphite",.13,.016 if detail else 0)
        block("Stacking shoe",x,.86,0,.43,.22,d,"Alloy",.10,.016 if detail else 0)
    shell="Refractory" if style=="Thermal" else "Ceramic"
    if style=="Thermal":
        for i in range(5):
            x=-.48+i*.23
            plates("Heat rejection rib",profile(x,x+.13,.19,.88,.035),d/2,.16,shell,.007 if detail else 0)
    else:
        plates("Fluted lid armor",profile(-.89,.83,.47,.88,.027),d/2,.14,shell,.019 if detail else 0)
        plates("Lower scuff shell",profile(-.82,.81,.11,.44,.03),d/2-.018,.11,"Secondary")
    for x in (-.57,.57):
        plates("Lock socket",profile(x-.19,x+.19,.37,.63,.025),d/2+.014,.032,"Graphite")
        plates("Latch lever",profile(x-.045,x+.055,.40,.65,.025),d/2+.026,.018,"Alloy")
    plates("Load class",profile(-.12,.17,.22,.33,.02),d/2+.008,.008,"Amber")


def doorway(style, detail):
    w,h = {"Door":(2.7,2.85),"Reinforced":(3.0,3.0),"Maintenance":(2.0,2.5),
           "Machinery":(4.4,3.8),"Phase":(3.25,3.2),"Window":(2.8,1.9),"Corner":(2.5,2.9)}[style]
    if style=="Corner":
        block("Continuous transverse pressure hull",-.1,0,0,2.30,h,.56,"Graphite",.055)
        block("Continuous return pressure hull",.98,0,-.95,.60,h,1.68,"Graphite",.075)
        plates("Corner shear panel",profile(-1.20,.74,.28,2.66,.20),.316,.08,
               "Ceramic",.016 if detail else 0)
        block("Ceramic return flange",1.13,.24,-.96,.33,2.35,1.36,"Ceramic",.095,.014 if detail else 0)
        block("Graphite corner shoe",1.02,.025,-.24,.68,.195,.92,"Alloy",.08)
        return
    low=.65 if style=="Window" else 0
    side=.30 if style!="Machinery" else .48
    shell="Refractory" if style=="Reinforced" else "Ceramic"
    for sign in (-1,1):
        x=sign*(w/2-side/2)
        block("Frame jamb",x,low,0,side,h-low-.205,.59,"Graphite",.075,.012 if detail else 0)
        bottom=h*.64 if style=="Phase" else low+.12
        block("Ceramic jamb armor",x,bottom,0,side*.88,h-bottom-.21,.64,shell,.065,.016 if detail else 0,.80,-sign*.04)
        if style=="Phase":
            block("Stabilizer shoe",x,low+.09,0,side*.86,h*.46-low,.64,"Secondary",.067)
        elif detail:
            block("Jamb inspection register",x,low+.78,0,side*.53,.094,.657,"Amber",.015)
    block("Overhead bearing",0,h-.25,0,w,.25,.60,"Graphite",.06,.012 if detail else 0)
    block("Bifurcated lintel",-.12,h-.18,0,w*.76,.16,.66,shell,.08,.014 if detail else 0)
    if style=="Window":
        block("Window sill",0,low,0,w,.15,.65,"Secondary",.09)
        block("Window split",.44,low,0,.11,h-low,.30,"Alloy",.035)


def deck(style, detail):
    w,h,d={"Walkway":(2.8,.40,4.0),"Bridge":(5.6,.72,8.0),"Suspended":(3.35,.57,4.2),
           "Edge":(3.20,.42,1.0),"Exposed":(3.25,.78,4.0),"Tread":(2.8,.21,.56),
           "Ramp":(2.8,.6,3.2),"Floor":(3.2,.045,2.8)}[style]
    if style=="Ramp":
        plan=ring(w,d,cut=.065)
        loft("Continuous ramp plate",[(0,plan),(.085,[(x,z,(z/d+.5)*(h-.085)) for x,z in plan])],"Alloy")
        for side in (-1,1):
            beam("Ceramic ramp curb",(side*w*.46,.08,-d*.47),(side*w*.46,h,d*.48),.17,.16,"Ceramic")
        return
    if style=="Floor":
        block("Deck maintenance bed",0,0,0,w,.023,d,"Graphite",.038)
        for x,span,y in ((-.81,1.52,-.04),(.78,1.59,.08)):
            block("Sealed floor tile",x,.020,y,span,.025,d-.24,"Alloy",.19)
        for x in (-1.35,1.20):
            block("Route bar",x,.020,0,.085,.025,d*.74,"Secondary",.025)
        return
    slab=h*.4
    block("Continuous traction deck",0,h-slab,0,w,slab,d,"Alloy",.035,.007 if detail else 0)
    if style=="Tread":
        block("Ceramic nosing",0,h*.36,-d*.37,w*.98,h*.39,d*.25,"Secondary",.027)
        block("Step riser",0,0,0,w,h*.66,d*.94,"Graphite",.021)
        return
    for side in (-1,1):
        x=side*w*.41
        # The deck, cassette and rail overlap vertically. Give each its own side planes.
        block("Compression rail",x,0,0,w*.153,h*.70,d*.98,"Graphite",.060,.013 if detail else 0)
        if style!="Exposed":
            for i in (-1,1):
                block("Separated edge cassette",x,h*.22,i*d*.249,w*.17,h*.63,d*.46,
                      "Secondary" if style=="Edge" else "Ceramic",.12,.015 if detail else 0)
        for i in (-1,1):
            beam("Diagonal web",(x,0,i*d*.40),(side*w*.16,h*.69,i*d*.16),w*.11,h*.31,"Graphite")
    if detail:
        for z in (-d*.34,d*.34):
            block("Access lock",0,h-.012,z,w*.28,.012,.09,"Graphite",.025)


def rail(detail):
    w,h,d=3.0,.87,.20
    block("Continuous handrail",0,.75,0,w,.12,d,"Alloy",.03,.007 if detail else 0)
    block("Toe restraint",0,0,0,w,.12,d*.91,"Graphite",.03)
    for x in (-1.08,0,1.08):
        beam("Swept brace",(x-.18,.055,0),(x+.11,.78,0),.20,.19,"Graphite")
        block("Ceramic grip insert",x,.768,0,.59,.09,d,"Ceramic",.035,.004 if detail else 0)
    # Thin dark web makes the existing solid gameplay rail legible between braces.
    block("Opaque restraint web",0,.105,0,w,.65,.032,"Graphite",.012)


def machine(style, detail):
    block("Grouted service skid",0,0,0,2.27,.20,1.37,"Graphite",.19,.018 if detail else 0)
    if style in ("Stabilizer","Suspended"):
        for side in (-1,1):
            x=side*(.94 if style=="Suspended" else .83)
            beam("Fork with separated bearing",(x,.2,0),(x+side*.21,1.96,0),.43,.88,"Graphite")
            block("Ceramic pole shoe",x,.31,0,.42,.68,1.08,"Ceramic",.12,.019 if detail else 0)
            block("Detached ceramic return",x+side*.07,1.26,0,.39,.66,1.05,"Ceramic",.12,.018 if detail else 0,.78,side*.09)
        loft("Central induction spine",[(.35,ring(.30,.48)),(.77,ring(.52,.61)),(1.40,ring(.40,.49)),(1.77,ring(.19,.22))],"Alloy",.015 if detail else 0)
        for y in (.70,1.32):
            block("Concentric phase guide",0,y,0,.79,.105,.76,"Graphite",.15,.016 if detail else 0)
            block("Energy return terminal",0,y+.016,.38,.36,.065,.12,"Signal",.026)
        if style=="Suspended":
            beam("Cantilevered transfer tongue",(-1.0,1.97,0),(.81,2.27,0),.39,.66,"Alloy")
    else:
        shell = "Refractory" if style in ("Processor","Cooling","Extraction") else "Ceramic"
        tilt = .25 if style in ("Extraction","Control") else -.12
        block("Recessed machine frame",0,.16,0,1.83,1.28,1.12,"Graphite",.16,.02 if detail else 0,.83,tilt)
        for sign in (-1,1):
            x=sign*.74
            block("Removable heat shield",x,.25,0,.58,1.06,1.31,shell,.14,.015 if detail else 0,.82,tilt)
        if style in ("Conduit","Distribution","Extraction"):
            for i in range(3):
                x=-.38+i*.36
                beam("Feed trunk",(x,.34,.61),(x+tilt,1.57,.61),.24,.23,"Alloy",10)
                block("Insulated collar",x+tilt*.61,.94,.61,.31,.16,.32,"Secondary",.075)
        elif style in ("Cooling","Processor"):
            for i in range(6 if detail else 3):
                y=.31+i*(.145 if detail else .28)
                if style=="Processor":
                    y += .025*i
                block("Cooling vane",0,y,.59,.93,.055,.20,"Alloy",.04)
        else:
            block("Maintenance console bed",0,.91,.20,1.13,.28,1.12,"Alloy",.13,.016 if detail else 0,.84)
            block("Inspection terminal",-.12,1.184,.40,.68,.054,.35,"Secondary",.07)
        cap_width=1.48 if style in ("Control","Conduit") else 1.73
        block("Raised service cap",tilt,1.39,0,cap_width,.19,1.24,shell,.15,.018 if detail else 0)
        if style=="Extraction":
            beam("Inclined extraction feed",(-.23,1.46,.49),(.32,1.94,.64),.44,.36,"Alloy",10)
        elif style=="Processor":
            block("Ceramic charge chute",0,1.54,.45,.68,.35,.64,shell,.12,.012 if detail else 0,.78,.10)
        elif style=="Cooling":
            block("Double vane crown",0,1.54,0,1.15,.19,.68,"Alloy",.151,.012 if detail else 0)
        elif style=="Distribution":
            block("Service isolator bus",-.07,.66,.66,1.23,.12,.27,"Alloy",.075)
        elif style=="Conduit":
            beam("Elbow return",(.42,.39,.63),(.96,.83,.61),.30,.29,"Alloy",10)
        elif style=="Control":
            block("Control panel surround",-.13,1.20,.59,.74,.16,.43,shell,.10)

    if detail:
        block("Disconnect switch",.64,.49,.68,.19,.10,.12,"Amber",.022)


def weather(item, seed, size, detail):
    """Break the lofted strata with deterministic fractal weathering. LOD0 is subdivided once so the
    erosion reads as chipped rock; LOD1 displaces the same base vertices, keeping the silhouette."""
    import bmesh
    from mathutils import noise
    bm=bmesh.new()
    bm.from_mesh(item.data)
    if detail:
        bmesh.ops.subdivide_edges(bm,edges=bm.edges[:],cuts=1,use_grid_fill=True)
    bm.normal_update()
    offset=Vector((seed*1.7,seed*.31,seed*.73))
    for vertex in bm.verts:
        p=vertex.co*1.45+offset
        erosion=noise.fractal(p,.85,2.1,4)
        chips=noise.noise(vertex.co*4.6+offset)
        vertex.co+=vertex.normal*(erosion*.075+chips*.028)*size
        if vertex.co.z<0:
            vertex.co.z=0
    # Displaced quads are no longer planar: split them so every face keeps its own projection plane.
    bmesh.ops.triangulate(bm,faces=bm.faces[:])
    bm.to_mesh(item.data)
    bm.free()
    item.data.update()


def geology(style, detail):
    w,h,d,seed = {
        "Shelf":(3.9,2.4,2.3,18),"Split":(3.45,2.05,2.65,32),"Erased":(3,1.63,2.42,49),
        "Outcrop":(5.4,2.8,3.8,52),"Pebbles":(1.05,.37,.77,23),"Cliff":(5.4,3.9,2.55,86),
        "Seam":(3.1,1.95,2.65,28),"Phase":(3.25,2.52,2.9,61)
    }[style]
    rng=Random(seed)
    plan=[(x*(.89+rng.random()*.15),z*(.86+rng.random()*.19)) for x,z in ring(w,d,cut=min(w,d)*.24)]
    slope={"Shelf":.16,"Split":-.17,"Erased":.06,"Outcrop":.27,"Pebbles":.045,
           "Cliff":.15,"Seam":-.22,"Phase":.20}[style]
    erosion=[rng.uniform(-1,1) for _ in plan]

    def bedding(y,scale,shift,depth_shift):
        layer=y/h
        return [(x*scale+shift+disturbance*.10*w*layer,
                 z*scale+depth_shift+disturbance*.055*d*math.sin(layer*2.3),
                 (x*slope+z*.052+disturbance*h*.077)*min(1,layer*6))
                for (x,z),disturbance in zip(plan,erosion)]

    levels=(
        (.0, .82, -.04,0),(.13,.96,0,0),(.27,.96,.04,-.03),(.31,1,.04,-.03),
        (.52,.84,.12,.02),(.565,.87,.10,.01),(.69,.76,.16,.03),(.74,.78,.16,.03),
        (.87,.70,.27,.12),(1,.58,.28,.13)
    )
    if style=="Split":
        levels=tuple((a,b*(1-a*.17),c-a*.75,dz+a*.42) for a,b,c,dz in levels)
    if style=="Outcrop":
        levels=tuple((a,b*(1-a*.28),c-a*.95,dz+a*.28) for a,b,c,dz in levels)
    if style=="Erased":
        levels=tuple((a, .87+b*.13-a*.16, c+a*.38,dz-a*.24) for a,b,c,dz in levels)
    if style=="Cliff":
        levels=tuple((a, .86+b*.14, c-a*.32,dz) for a,b,c,dz in levels)
    if style=="Pebbles":
        slope=.048
        levels=tuple((a,b,c*.25,dz*.25) for a,b,c,dz in levels)
    if style=="Phase":
        levels=levels[:6]
    sections=[]
    for layer,scale,shift,depth_shift in levels:
        points=bedding(layer*h,scale,shift,depth_shift)
        if style in ("Outcrop","Shelf","Split","Erased"):
            # A single diagonal shear truncates successive beds on the upstream face.
            # The foot retains the original footprint while the crown recedes to one side.
            cut=shift+w*(.52-.55*layer)
            points=[(x-.88*max(0,x-cut-.23*z),z,rise) for x,z,rise in points]
        sections.append((layer*h,points))
    # One closed stratigraphic core. Different beds are assigned on the same connected surface.
    loft("Inclined sediment with directional fracture",sections,"DryStone")
    item=mesh.parts[mesh.group][-1]
    weather(item,seed,min(w,d),detail)
    rock=item.data
    for finish in ("DarkStone","Moss","OchreStone"):
        rock.materials.append(mesh.paints[finish])
    # Finishes follow geology, not polygon rows (rows read as camouflage stripes in review):
    # moss only where water and soil settle on upward faces, dark stone on undercuts and the wet
    # foot, one thin iron-stained bed, weathered dry stone everywhere else.
    top=max(v.co.z for v in rock.vertices) or 1
    band=.36+(seed%5)*.03
    # Decide finishes on neighbourhood-smoothed normals: per-triangle normals after weathering
    # alternate from face to face and made moss read as diamond camouflage on the river rocks.
    around={}
    for polygon in rock.polygons:
        for v in polygon.vertices:
            around.setdefault(v,[]).append(polygon.index)
    smooth=[p.normal.copy()*p.area for p in rock.polygons]
    for _ in range(3):
        vertex_sum={v:sum((smooth[f] for f in faces),Vector()) for v,faces in around.items()}
        smooth=[sum((vertex_sum[v] for v in p.vertices),Vector()) for p in rock.polygons]
    for polygon,soft in zip(rock.polygons,smooth):
        n,c=(soft.normalized() if soft.length>0 else polygon.normal),polygon.center
        rel=c.z/top
        if n.z>.6 and rel>.3 and style not in ("Seam","Phase","Cliff"):
            polygon.material_index=2
        elif n.z<-.15 or (n.z<.3 and rel<.16):
            polygon.material_index=1
        elif abs(rel-band)<.03 and n.z<.5:
            polygon.material_index=3
        else:
            polygon.material_index=0
    if style=="Phase":
        loft("Suspended bedding fragment",[(h*.71,bedding(h*.71,.65,.31,.09)),
             (h*.83,bedding(h*.83,.69,.38,.14)),(h,bedding(h,.52,.52,.18))],"DryStone")
        beam("Exposed geological keeper",(-w*.26,h*.42,0),(w*.24,h*.80,0),.18,.23,"OchreStone",6)
    if detail and style in ("Outcrop","Seam","Split","Pebbles"):
        for i in (-1,1):
            x=i*w*.35
            rock_h=h*(.26 if i>0 else .44)
            loft("Detached erosion shoulder",[(0,ring(w*.35,d*.56,x,-d*.13,cut=d*.10)),
                 (rock_h*.72,ring(w*.31,d*.46,x+.07,-d*.17,cut=d*.09)),
                 (rock_h,ring(w*.21,d*.33,x-.04,-d*.15,cut=d*.085))],"DarkStone" if i>0 else "OchreStone")


def crown(name, at, width, depth, height, rng, detail, juvenile=False):
    x,y,z=at
    # Oblong fans with a folded crest and a raised windward lip.
    n=9 if detail else 6
    plan=[]
    for i in range(n):
        angle=i*math.tau/n
        radius=.77+rng.random()*.23
        plan.append((math.cos(angle)*radius,math.sin(angle)*radius))
    sections=[]
    for yoff,mag,shift in ((-.37,.57,-.06),(-.08,1,0),(.20,.80,.14),(.48,.36,.19)):
        sections.append((y+yoff*height,[(x+(px*mag+shift)*width*.5,z+pz*depth*.5*mag,
                                         px*.055*height+pz*.10*height) for px,pz in plan]))
    loft(name,sections,"YoungLeaf" if juvenile else "Leaf")
    leaf=mesh.parts[mesh.group][-1].data
    leaf.materials.append(mesh.paints["LeafShade"])
    leaf.materials.append(mesh.paints["YoungLeaf"])
    for polygon in leaf.polygons:
        if polygon.index<=n:
            polygon.material_index=1
        elif polygon.index>=n*2 and polygon.index%3==0:
            polygon.material_index=2


def tree(style, detail):
    height, spread, seed = {"Sentinel":(6.6,4.7,43),"Ridge":(4.5,3.75,28),
                            "Sapling":(2.6,2.05,47),"Windward":(4.75,4.3,83)}[style]
    rng=Random(seed)
    r=height*.142
    # A legible, almost vertical rootstock owns the silhouette at eye level.
    trunk=[(0,ring(r*1.35,r*1.23,cut=r*.18)),(height*.12,ring(r,r*.91,cut=r*.17)),
           (height*.47,ring(r*.69,r*.68,.05,.035,cut=r*.12)),
           (height*.73,ring(r*.46,r*.51,.20,.13,cut=r*.07)),
           (height*.93,ring(r*.20,r*.23,.49,.21,cut=r*.036))]
    loft("Bent regional trunk",trunk,"Bark")
    patterns = {
        "Sentinel": ((.21,.81,.33),(2.77,.66,.36),(4.38,.78,.38),(1.72,.89,.21),(-.7,.95,.17)),
        "Ridge": ((.12,.73,.41),(2.77,.79,.35),(4.38,.72,.39),(1.72,.92,.23),(-.7,.88,.19)),
        "Sapling": ((.21,.74,.24),(2.87,.67,.33),(4.38,.81,.25),(1.72,.92,.21)),
        "Windward": ((.16,.70,.40),(3.16,.61,.30),(5.55,.78,.41),(.89,.87,.34),(-.8,.91,.07)),
    }
    for i,(angle,level,reach) in enumerate(patterns[style]):
        angle += rng.uniform(-.24,.24)
        length=spread*reach
        ex=math.cos(angle)*length + level*.21
        ez=math.sin(angle)*length
        y=height*level
        beam("Load branch",(.10,y-height*.24,.06),(ex,y,ez),r*.60,r*.49,"Bark",7 if detail else 5)
        crown("Tiered wind-swept crown",(ex,y,ez),spread*(.59 if i==1 else .52),spread*.34,height*.16,
              Random(seed+i),detail,i%3==1)
        if detail:
            dx,dz=math.cos(angle-.68)*spread*.15,math.sin(angle-.68)*spread*.15
            beam("Forked twig",(ex*.65,y-height*.058,ez*.64),(ex+dx,y+height*.065,ez+dz),r*.19,r*.18,"Bark",5)
            crown("Outer leaf pad",(ex+dx,y+height*.073,ez+dz),spread*.26,spread*.23,height*.084,
                  Random(seed+i+56),True,i%2==0)
    if detail:
        for i in range(3):
            a=i*math.tau/3 + .3
            beam("Surface root",(0,height*.1,0),(math.cos(a)*r*1.1,.015,math.sin(a)*r*.90),
                 r*.51,r*.35,"Bark",6)


def undergrowth(style, detail):
    rng=Random({"Bramble":91,"Sedge":31,"Fern":81,"Bloom":71}[style])
    if style=="Bramble":
        for i in range(5 if detail else 3):
            angle=i*2.399
            x,z=.27*math.cos(angle),.27*math.sin(angle)
            beam("Woody shoot",(0,0,0),(x,.32,z),.045,.04,"Bark",5)
            crown("Bramble crown",(x,.28+i*.025,z),.57,.48,.28,rng,detail,i%2==0)
        return
    n=12 if detail else 6
    scale=.55 if style=="Sedge" else .8
    for i in range(n):
        angle=i*2.399
        x,z=math.cos(angle),math.sin(angle)
        reach=rng.uniform(.16,.47)*scale
        tip=rng.uniform(.22,.53)*scale
        width=rng.uniform(.066,.118)*scale
        pts=[(x*.046,0,z*.046),
             (x*reach*.51-z*width,.68*tip,z*reach*.51+x*width),
             (x*reach,tip,z*reach),
             (x*reach*.51+z*width,.69*tip,z*reach*.51-x*width),
             (x*reach*.61,.74*tip,z*reach*.61),
             (x*reach*.54,.64*tip,z*reach*.54)]
        faces=[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(0,5,1),(1,5,2),(2,5,3),(3,5,0)]
        # Orient the closed blade based on the sign of its tetrahedral volume.
        volume=sum(Vector(pts[a]).dot(Vector(pts[b]).cross(Vector(pts[c]))) for a,b,c in faces)
        if volume<0:
            faces=[tuple(reversed(face)) for face in faces]
        mesh.surface("Folded blade",pts,faces,"Leaf" if i%3 else "YoungLeaf")
        if style=="Bloom" and i%3==0:
            beam("Seed stem",(x*.04,0,z*.04),(x*reach*.73,tip*1.38,z*reach*.73),.023,.024,"Leaf",5)
            block("Dried seed head",x*reach*.73,tip*1.35,z*reach*.73,.047,.044,.049,"OchreStone",.015)


def ruin(detail):
    geology("Shelf",detail)
    # Inclined fragments are socketed into the exposed beds with a visible gap at the shear line.
    beam("Buried graphite keeper",(-1.69,.28,.81),(.55,2.03,.79),.41,.63,"Graphite")
    beam("Displaced ceramic deflector",(-1.86,.46,1.03),(-.60,1.56,.97),.68,.50,"Ceramic")
    beam("Offset ceramic shoulder",(-.34,1.90,.78),(.91,2.53,.36),.74,.61,"Ceramic")
    block("Buried survey terminal",-.45,.30,1.16,.65,.38,.53,"Alloy",.12)
    if detail:
        for x,z in ((-1.42,.51),(.46,-.84),(1.26,-.33)):
            crown("Roots and ground cover", (x,.42,z),.58,.43,.36,Random(int(x*10)),True,True)
            foliage=mesh.parts[mesh.group][-1].data
            foliage.materials.clear()
            foliage.materials.append(mesh.paints["Moss"])
            for polygon in foliage.polygons:
                polygon.material_index=0
