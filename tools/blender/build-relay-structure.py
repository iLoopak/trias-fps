"""Author continuous Relay load-bearing structures and export their Unity mounts."""
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import trias_mesh as art

art.reset()


def loft(name, sections, surface):
    """Join equal cross-sections into one closed body with real recesses and chamfers."""
    art.group = name
    sides = len(sections[0])
    points = [point for section in sections for point in section]
    faces = [(tuple(reversed(range(sides))), 'Graphite')]
    for span in range(len(sections)-1):
        for edge in range(sides):
            a = span * sides + edge
            b = span * sides + (edge+1) % sides
            faces.append(((a, b, b+sides, a+sides), surface(span, edge)))
    end = (len(sections)-1)*sides
    faces.append((tuple(range(end,end+sides)), 'Graphite'))
    art.surface(name, points, [face for face, _ in faces], 'Graphite')
    body = art.parts[name][0]
    names = ['Graphite', 'Ceramic', 'Secondary', 'Alloy', 'Amber']
    for material in names[1:]:
        body.data.materials.append(art.paints[material])
    for polygon, (_, material) in zip(body.data.polygons, faces):
        polygon.material_index = names.index(material)

    art.select_only(body)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    bevel = body.modifiers.new('CNC edge radius', 'BEVEL')
    bevel.width = .004
    bevel.segments = 3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for polygon in body.data.polygons:
        polygon.use_smooth = True
    normals = body.modifiers.new('Area-weighted normals', 'WEIGHTED_NORMAL')
    normals.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=normals.name)


# The dipped sides form a service chase within the girder. The deck and lower chords
# are part of the same surface. Full cross-section is held across the traversable span.
side = [(.34, -.5), (.455, -.405), (.48, -.235), (.436, -.152), (.436, -.027),
        (.495, .049), (.50, .33), (.44, .50), (-.44, .50),
        (-.50, .33), (-.495, .049), (-.436, -.027), (-.436, -.152),
        (-.48, -.235), (-.455, -.405), (-.34, -.5)]
bridge_stations = [(-.50, .91), (-.468, 1), (-.304, 1), (-.294, .986), (-.282, 1),
                   (.147, 1), (.159, .988), (.172, 1), (.453, 1), (.50, .91)]
loft('ContinuousGantry', [[(x, y, z*width) for z, y in side] for x, width in bridge_stations],
     lambda span, edge: ('Alloy' if span in (2,3,5,6) and edge in (6,7,8)
                        else 'Ceramic' if edge in (6,7,8)
                        else 'Amber' if span in (4,7) and edge in (5,9)
                        else 'Secondary' if edge in (5,9) else 'Graphite'))

# A steel root and vertical side chase flow into a broad tapered crown. Stepped
# stations change the actual surface, so their shadows follow the shape of the casting.
outline = [(-.33,-.5), (.27,-.5), (.5,-.26), (.5,.02), (.438,.09), (.438,.26),
           (.5,.32), (.37,.48), (.17,.5), (-.33,.5), (-.5,.31), (-.5,-.28)]
pylon_stations = [(-.5,.91,.91,0), (-.428,1,1,0), (-.295,.96,.97,-.005),
                  (-.265,.93,.94,-.008), (-.214,.948,.975,-.008),
                  (.205,.945,.96,-.023), (.241,1,1,-.025), (.394,.968,.95,-.03),
                  (.5,.865,.87,-.037)]
loft('ContinuousPylon', [[(x*width+offset,y,z*depth) for x,z in outline]
                          for y,width,depth,offset in pylon_stations],
     lambda span, edge: ('Graphite' if edge in (3,4,5) or span < 3
                        else 'Amber' if span == 5 and edge in (0,1,9)
                        else 'Alloy' if span == 4 and edge in (1,6,10)
                        else 'Secondary' if edge in (2,6,10)
                        else 'Ceramic'))

# Pressure bulkhead. The scalloped middle courses are modeled into both
# faces and turn the corner with the chamfer. No overlays are needed.
bulkhead_profile = [(.438,-.50), (.493,-.447), (.497,-.318), (.480,-.296),
                    (.441,-.274), (.441,-.148), (.494,-.111), (.497,.336), (.476,.389),
                    (.374,.50), (-.374,.50), (-.476,.389), (-.497,.336), (-.494,-.111),
                    (-.441,-.148), (-.441,-.274), (-.480,-.296), (-.497,-.318),
                    (-.493,-.447), (-.438,-.50)]
bulkhead_stations = [(-.50,.84,.88), (-.438,1,1), (-.307,1,1), (-.284,.91,.95),
                     (-.194,.91,.95), (-.170,1,1), (.322,1,1), (.36,.924,.97),
                     (.410,.924,.97), (.446,1,1), (.50,.81,.88)]


def bulkhead_finish(span, edge):
    if edge in (6,7,8,9,10,11,12):
        if span in (2,3,4,6,7,8):
            return 'Secondary'
        return 'Ceramic' if edge in (7,8,9,10,11) else 'Alloy'
    if span in (1,5) and edge in (5,13):
        return 'Amber'
    return 'Graphite'


loft('ContinuousBulkhead',
     [[(x,y*height,z*width) for z,y in bulkhead_profile] for x,width,height in bulkhead_stations],
     bulkhead_finish)

art.finish('RelayStructure','RelayStructure/RelayStructure.fbx',
           {'ContinuousGantry': (0, .93, 0), 'ContinuousPylon': (0, -.52, 0), 'ContinuousBulkhead': (0, .18, .78)},
           camera_at=(2,-3,2.0), camera_target=(0,0,.28), camera_width=3.4)
