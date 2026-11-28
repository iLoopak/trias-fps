"""Model Bašta, Přízrak and Poštolka for the shared pilot skeleton."""
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band, fastener, panel, plate, vents


def face(name, outline, front, depth, finish, bevel=.006, side=1):
    points=[(side*x,y,z+front) for x,y,z in outline]
    plate(name,points if side==1 else list(reversed(points)),0,depth,finish,bevel)


def helmet(role):
    art.group="Helmet"
    if role=="Bulwark":
        # Breacher: a domed EOD-style shell, one wide recessed visor under a raked ceramic brow and a
        # faceted graphite chin guard. Mass comes from the dome and jaw, not from stacked boxes.
        band("Breacher dome",(0,0,-.016),[
             (-.125,.084,.070,.010),(-.090,.124,.104,.004),(-.020,.140,.122,.006),(.045,.136,.118,-.004),
             (.092,.112,.096,-.020),(.124,.072,.064,-.030),(.136,.034,.034,-.032)],"Ceramic",24,.74)
        face("Raked ceramic brow",[(-.128,.030,.096),(-.120,.072,.092),(-.050,.088,.116),(.050,.088,.116),
             (.120,.072,.092),(.128,.030,.096),(.060,.040,.128),(-.060,.040,.128)][::-1],0,.040,"Ceramic",.010)
        face("Wide recessed visor",[(-.112,-.006,.104),(-.104,.032,.110),(.104,.032,.110),(.112,-.006,.104),
             (.050,-.020,.126),(-.050,-.020,.126)][::-1],0,.022,"Visor",.006)
        # One offset rangefinder pod on the right temple instead of a row of "robot eyes".
        band("Rangefinder pod",(.118,.030,.040),[(-.030,.020,.022,0),(-.010,.026,.028,0),(.030,.024,.026,.012)],"Graphite",14,.8)
        panel("Rangefinder lens",.140,.030,.078,.018,.016,.010,"Signal",bevel=.002)
        face("Faceted chin guard",[(-.118,-.030,.088),(-.060,-.034,.124),(.060,-.034,.124),(.118,-.030,.088),
             (.100,-.098,.072),(.040,-.122,.104),(-.040,-.122,.104),(-.100,-.098,.072)][::-1],0,.050,"Graphite",.012)
        vents("Respirator slots",0,-.086,.150,4,.010,.040)
        for side in (-1,1):
            face("Jaw hinge cheek",[(.106,-.020,.040),(.132,-.008,.010),(.130,-.080,.000),(.100,-.100,.040)][::-1],0,.028,"Alloy",.006,side)
            fastener("Jaw lock",side*.128,-.050,.036,.008)
        panel("Crown faction stripe",0,.132,.020,.046,.016,.180,"Faction",bevel=.006)
    elif role=="Wraith":
        # Infiltrator cowl (chosen over a split mask and a lens cluster in tools/blender/explore-wraith.py):
        # a fabric hood frames a three-facet fired-ceramic keel blade with one off-centre vertical optic.
        band("Cowl skull",(0,0,-.030),[
            (-.118,.070,.082,.018),(-.060,.100,.112,.004),(.020,.106,.120,-.008),
            (.076,.094,.106,-.022),(.112,.064,.074,-.036),(.128,.030,.040,-.044)],"Graphite",24,.78)
        band("Fabric hood",(0,0,-.050),[
            (-.150,.092,.070,-.034),(-.090,.120,.110,-.008),(-.010,.126,.128,-.006),
            (.062,.118,.124,-.020),(.112,.088,.098,-.036),(.144,.040,.060,-.052)],"Fabric",24,.72)
        plate("Hood tail",[(-.072,-.020,None),(.072,-.020,None),(.046,-.215,None),(-.046,-.215,None)],-.132,.030,"Fabric",.010)
        face("Blade centre facet",[(-.026,-.124,.140),(.026,-.124,.140),(.030,.060,.136),(.000,.092,.128),(-.030,.060,.136)][::-1],
             0,.030,"Ceramic",.006)
        for side in (-1,1):
            face("Blade keel facet",[(.026,-.124,.136),(.062,-.096,.104),(.074,-.010,.100),(.062,.060,.098),(.030,.060,.134)][::-1],
                 0,.030,"Ceramic",.006,side)
        face("Blade chin facet",[(-.028,-.126,.138),(.028,-.126,.138),(.018,-.154,.100),(-.018,-.154,.100)][::-1],0,.026,"Ceramic",.006)
        face("Vertical optic",[(.032,-.030,.142),(.052,-.034,.126),(.054,.046,.122),(.034,.050,.139)][::-1],0,.010,"Visor",.003)
        face("Optic core",[(.038,-.010,.145),(.046,-.012,.139),(.047,.036,.136),(.039,.038,.142)][::-1],0,.003,"Signal",.001)
        face("Respirator ridge",[(-.020,-.092,.150),(.020,-.092,.150),(.016,-.114,.148),(-.016,-.114,.148)][::-1],0,.010,"Graphite",.003)
        panel("Faction tab",-.086,.030,.074,.020,.050,.030,"Faction",-.2)
    else:
        band("Flight helmet",(0,0,-.018),[
            (-.110,.064,.067,.017),(-.054,.100,.102,.008),(.021,.119,.122,.003),
            (.070,.112,.105,-.017),(.104,.069,.056,-.042),(.116,.040,.040,-.059)],"Ceramic",20,.63)
        for side in (-1,1):
            face("Split swept optic",[(.004,-.028,.138),(.064,-.012,.118),(.118,.035,.055),
                (.110,.066,.059),(.057,.053,.124),(.003,.026,.149)],0,.025,"Visor",.006,side)
            face("Flight optic edge",[(.013,.018,.150),(.062,.041,.127),(.104,.054,.073),
                (.102,.065,.071),(.062,.053,.130),(.013,.032,.151)],0,.007,"Signal",.002,side)
            panel("Flight telemetry plate",side*.101,.057,.033,.045,.087,.102,"Faction",side*.39)
        face("Pointed intake",[(0,-.115,.089),(.055,-.067,.103),(.043,-.021,.143),
             (0,.021,.163),(-.043,-.021,.143),(-.055,-.067,.103)],0,.043,"Secondary",.006)
        vents("Intake",0,-.064,.139,3,.011,.029)
    band("Neck pressure seal",(0,-.128,-.021),[(-.052,.044,.043,0),(.01,.055,.057,0),(.032,.059,.055,0)],"Fabric",14)


def torso(role, scale):
    art.group="Torso"
    band("Pressure garment",(0,0,0),[
        (-.272,.104*scale,.087,0),(-.167,.143*scale,.105,.007),(-.061,.158*scale,.122,0),
        (.082,.190*scale,.121,-.018),(.153,.160*scale,.103,-.026),(.188,.082,.062,-.022)],"Fabric",18)
    if role=="Bulwark":
        for side in (-1,1):
            face("Heavy stacked ceramic baffles",[
                (.022,-.079,.163),(.165,-.094,.146),(.224,-.028,.099),(.229,.101,.091),
                (.179,.131,.119),(.031,.125,.169)],0,.063,"Ceramic",.014,side)
            panel("Pressure front color bar",side*.150,-.007,.161,.122,.058,.022,"Faction",side*.14,.009)
            panel("Ballistic lower segment",side*.079,-.152,.142,.132,.093,.069,"Secondary",side*.11,.008)
            panel("Rigid dorsal cell",side*.138,.009,-.071,.129,.333,.167,"Graphite",bevel=.015)
            panel("Dorsal ceramic buffer",side*.144,.054,-.215,.096,.226,.083,"Ceramic",bevel=.012)
            panel("Dorsal faction block",side*.145,.085,-.285,.089,.061,.024,"Faction",bevel=.008)
            vents("Heat sink slots",side*.144,-.055,-.290,4,.013,.046)
        panel("Pressure coupler",0,.015,.174,.059,.206,.044,"Alloy",bevel=.009)
        panel("Rated load socket",0,-.179,.152,.11,.053,.029,"Amber",bevel=.007)
    elif role=="Wraith":
        face("Single canted ceramic chest mask",[
            (-.137,.105,.109),(-.062,-.096,.145),(.109,-.061,.151),(.133,.030,.110),(.045,.127,.140)],0,.044,"Ceramic",.011)
        panel("Diagonal armature harness",-.093,.005,.158,.041,.245,.023,"Alloy",-.58,.005)
        panel("Stabilizer field inset",.057,-.045,.159,.119,.055,.016,"Faction",.38,.006)
        panel("Obsidian rib guard",-.062,-.148,.123,.123,.098,.056,"Graphite",.27)
        panel("Low module back",.065,-.010,-.061,.143,.204,.129,"Graphite",-.42,.012)
        panel("Offset ceramic latch",.090,.029,-.183,.082,.128,.044,"Secondary",-.42,.008)
        panel("Offset unit marking",.099,.074,-.218,.059,.044,.025,"Faction",-.4)
        panel("Diagonal rear guard",-.069,-.080,-.130,.047,.199,.048,"Ceramic",.65,.007)
    else:
        for side in (-1,1):
            face("Chevron flight plate",[(.020,-.112,.144),(.085,-.078,.151),(.161,.051,.118),
                (.166,.130,.083),(.117,.136,.096),(.052,.073,.135),(.019,.033,.153)],0,.043,"Ceramic",.009,side)
            panel("Flight color wedge",side*.092,-.041,.149,.078,.062,.015,"Faction",side*.54)
            panel("Shoulder harness",side*.139,.060,.103,.044,.118,.053,"Alloy",side*.30)
            face("Dorsal swept vane",[(.073,-.139,-.143),(.133,-.098,-.178),(.221,.167,-.237),
                (.177,.188,-.262),(.111,.126,-.237),(.090,.017,-.176)],0,.076,"Ceramic",.010,side)
            panel("Vane faction mask",side*.178,.109,-.259,.053,.112,.025,"Faction",side*.48,.006)
            panel("Vane backplate",side*.134,.006,-.164,.056,.223,.118,"Graphite",side*.37,.009)
        panel("Flight pack actuator",0,.001,-.059,.172,.252,.129,"Graphite",bevel=.017)
        panel("Vertical core interface",0,.003,-.187,.046,.183,.044,"Alloy",bevel=.007)
        panel("Aperture marker",0,.032,-.220,.019,.102,.008,"Signal",bevel=.002)
    panel("Lower faction band",0,-.196,.134,.218*scale,.041,.031,"Faction",bevel=.005)
    # Gorget: a padded collar and a ceramic collar plate, higher at the back, so the helmet sits on
    # the shoulders instead of floating on a thin neck (portrait review exposed the gap).
    band("Padded gorget",(0,0,-.012),[(.150,.112*scale,.090,0),(.205,.098*scale,.084,-.004),
        (.262,.074,.068,-.010)],"Fabric",18)
    face("Collar plate",[(-.060,.170,.020),(.060,.170,.020),(.098,.205,-.040),(.090,.262,-.085),
        (-.090,.262,-.085),(-.098,.205,-.040)],0,.020,"Graphite" if role=="Wraith" else "Ceramic",.006)
    for side in (-1,1):
        face("Collar side plate",[(.070,.168,.040),(.112,.178,-.010),(.104,.232,-.040),(.074,.226,.020)],0,.018,
             "Graphite",.005,side)
    # Abdominal armour closes the gap to the belt; the rig's longer adult spine exposed it.
    band("Abdominal flex layer",(0,-.30,0),[(-.075,.118*scale,.092,.006),(-.02,.128*scale,.098,.008),
        (.045,.118*scale,.094,.004)],"Fabric",16)
    for i,y in enumerate((-.262,-.318)):
        panel("Abdominal segment",0,y,.092-i*.004,(.168-i*.018)*scale,.050,.044,"Graphite",bevel=.010)


def limbs(role, scale):
    art.group="Pelvis"
    band("Floating waist set",(0,0,0),[
        (-.130,.054,.066,0),(-.076,.125*scale,.089,0),(-.008,.154*scale,.103,-.011),
        (.053,.145*scale,.093,-.012),(.131,.091,.075,-.01)],"Fabric",16)
    panel("Waist load-bearing belt",0,.041,.116,.273*scale,.070,.047,"Graphite",bevel=.009)
    panel("Coupled clasp",0,.041,.145,.061,.046,.024,"Alloy",bevel=.006)
    for side,name in ((-1,"Left"),(1,"Right")):
        panel("Hip guard",side*.132*scale,-.012,.095,.073*scale,.115,.101,
              "Secondary" if role=="Wraith" else "Ceramic",side*.27,.008)

    for side,name in ((-1,"Left"),(1,"Right")):
        art.group=name+"Shoulder"
        size=1.27 if role=="Bulwark" else .70 if role=="Wraith" else .83
        if role=="Wraith" and side==-1: size=.91
        band("Shoulder joint housing",(0,0,0),[
            (-.064,.046*size,.055,0),(-.029,.073*size,.083,0),
            (.027,.085*size,.081,-.010),(.065,.055*size,.056,-.033)],"Graphite",16)
        if role=="Bulwark":
            panel("Wide pauldron shell",0,.035,.126,.233,.136,.218,"Ceramic",side*.15,.016)
            panel("Lower armor seam",0,-.036,.127,.230,.061,.116,"Faction",side*.14,.012)
            panel("Pauldron underside lugs",0,-.053,.087,.175,.038,.125,"Alloy",side*.08,.006)
            vents("Shoulder negative slots",0,.026,.128,3,.016,.072)
        elif role=="Wraith":
            panel("One-sided shade shoulder",0,.031,.083,.165*size,.106,.133,"Graphite",side*.53,.009)
            panel("Phase shoulder face",0,.032,.095,.113*size,.064,.036,"Faction",side*.62)
            panel("Ceramic break",side*.032,.049,.097,.063,.079,.031,"Ceramic",side*.55)
        else:
            face("Swept shoulder ceramic",[(-.073,-.035,.069),(.024,-.058,.083),(.085,.027,.035),
                (.056,.094,.005),(-.044,.086,.073),(-.082,.032,.089)],0,.106,"Ceramic",.009,side)
            panel("Swept team tab",0,-.015,.084,.114,.057,.018,"Faction",side*.52)
        panel("Back team stamp",0,-.035,-.071,.102*size,.042,.022,"Faction",side*.28)

        art.group=name+"Upper"
        band("Bicep undersuit",(0,0,0),[
            (-.147,.036*scale,.040,0),(-.097,.052*scale,.050,0),(.032,.057*scale,.058,0),
            (.143,.046*scale,.047,0)],"Fabric")
        panel("Floating bicep guard",0,.026,.065,.096*scale,.144,.045,
              "Graphite" if role=="Wraith" else "Ceramic",side*.19,.007)
        panel("Bicep team strip",0,.027,.069,.064*scale,.034,.010,"Faction",side*.24)

        art.group=name+"Forearm"
        band("Forearm impact sleeve",(0,0,0),[
            (-.137,.038*scale,.042,0),(-.080,.048*scale,.050,.003),(.045,.069*scale,.063,0),
            (.135,.049*scale,.051,0)],"Fabric")
        face("Forearm outer armor",[(-.059*scale,-.078,.064),(.023*scale,-.114,.062),
             (.061*scale,-.037,.069),(.056*scale,.101,.068),(.015*scale,.115,.078),
             (-.057*scale,.079,.080)],0,.049,"Ceramic" if role!="Wraith" else "Graphite",.008)
        panel("Gauntlet team indicator",0,.026,.088,.058*scale,.066,.014,"Faction",side*.35)
        fastener("Gauntlet set screw",side*.021,-.084,.074,.0068)
        if role=="Bulwark":
            panel("Exoframe piston shroud",side*.055,.006,-.010,.061,.199,.071,"Alloy",side*.09,.007)

        art.group=name+"Hand"
        band("Pressure glove",(0,0,.026),[
            (-.040,.026,.049,-.015),(-.020,.046*scale,.066,0),(.014,.047*scale,.059,.005),
            (.031,.034,.042,0)],"Fabric",12)
        panel("Glove metacarpal shield",0,.025,.060,.076,.032,.061,"Graphite",bevel=.006)
        for index in range(4):
            panel("Finger armor",(index-1.5)*.021,.017,.084,.015,.021,.027,"Alloy",bevel=.002)

        art.group=name+"Thigh"
        band("Leg pressure garment",(0,0,0),[
            (-.173,.049*scale,.052,0),(-.12,.070*scale,.067,0),(.044,.075*scale,.075,.002),
            (.171,.048*scale,.063,0)],"Fabric",16)
        panel("Long thigh facet",0,.039,.086,.132*scale,.225,.070,
              "Ceramic" if role=="Bulwark" else "Secondary",side*.25,.010)
        panel("Femoral badge",0,.041,.092,.073*scale,.076,.019,"Faction",side*.45)
        fastener("Femoral axle",side*.027,.111,.095,.008)

        art.group=name+"Shin"
        band("Calf pressure wrap",(0,0,-.013),[
            (-.178,.043*scale,.044,0),(-.119,.053*scale,.059,-.012),
            (.019,.067*scale,.069,-.024),(.173,.053*scale,.057,-.002)],"Graphite",16)
        panel("Carinated shin shell",0,-.038,.077,.139*scale,.241,.077,
              "Graphite" if role=="Wraith" else "Ceramic",side*.21,.012)
        panel("Shin tracking field",side*.018,-.015,.085,.050*scale,.109,.018,"Faction",side*.26,.006)
        panel("Ceramic patella slider",0,.148,.098,.099*scale,.077,.073,"Secondary",bevel=.010)
        if role=="Kestrel":
            panel("Flight brake side blade",side*.049,-.044,-.035,.052,.203,.093,"Alloy",side*.36,.008)

        art.group=name+"Boot"
        band("Boot tread support",(0,0,.024),[
             (-.063,.052*scale,.111,.035),(-.043,.069*scale,.126,.032),(.018,.067*scale,.115,.012),
             (.059,.052*scale,.081,-.022),(.116,.044*scale,.053,-.035)],"Graphite",18,.66)
        band("Scleral toe overlay",(0,0,.126),[
             (-.035,.059*scale,.052,.002),(.012,.061*scale,.056,.003),
             (.041,.043*scale,.049,.001),(.053,.028*scale,.026,-.003)],"Ceramic",14,.68)
        panel("Weighted heel color",0,.021,-.066,.091*scale,.057,.032,"Faction",bevel=.007)
        panel("Tread hook",0,-.037,.185,.080*scale,.035,.026,"Alloy",bevel=.004)


def build(role, label, scale):
    art.reset()
    helmet(role)
    torso(role,scale)
    limbs(role,scale)
    show={"Helmet":(0,1.58,-.020),"Torso":(0,1.238,0),"Pelvis":(0,.907,0)}
    for side,name in ((-1,"Left"),(1,"Right")):
        show.update({name+"Upper": (side*.238*scale,1.153,-.008),
            name+"Shoulder":(side*.263*scale,1.375,0),name+"Forearm":(side*.274*scale,.899,.018),
            name+"Hand":(side*.283*scale,.724,.025),name+"Thigh":(side*.10*scale,.724,0),
            name+"Shin":(side*.119*scale,.349,0),name+"Boot":(side*.12*scale,.067,.023)})
    art.finish(label,"PilotModels/"+role+"Suit.fbx",show,camera_at=(2.65,-4.7,2.1),camera_width=2.18)


for role,label,width in (("Bulwark","Basta",1.24),("Wraith","Prizrak",.83),("Kestrel","Postolka",.92)):
    build(role,label,width)
