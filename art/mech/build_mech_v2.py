"""Modular hero mech, version 2: every part in the game has its own model.

Run headless:
  blender --background --factory-startup --python art/mech/build_mech_v2.py -- <out_dir> [--render] [--only <part_id,...>]

All variants share one skeleton (same bone names/positions as v1, which the
game animates procedurally). Each variant's geometry is joined per bone and
named  P_<partid>__<bone>  so the game can switch parts by toggling renderers.
Special markers:  P_<weapon>__Barrel  (muzzle) and  P_<booster>__Nozzle_N  (flames).

The mech faces -Y, Z up, meters (~8.5 m tall).
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.path.dirname(__file__), 'out'))
RENDER = '--render' in argv
ONLY = None
if '--only' in argv:
    ONLY = set(argv[argv.index('--only') + 1].split(','))
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ============================================================== materials
def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

def mat(name, color, metal=0.0, rough=0.5, emit=None, strength=0.0, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*srgb(color), 1)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    if coat:
        b.inputs['Coat Weight'].default_value = coat
    if emit:
        b.inputs['Emission Color'].default_value = (*srgb(emit), 1)
        b.inputs['Emission Strength'].default_value = strength
    return m

M = {
    'paint':  mat('Paint_Primary', '#3a4250', 0.55, 0.32, coat=0.4),
    'accent': mat('Paint_Accent', '#d98a1c', 0.4, 0.3, coat=0.5),
    'frame':  mat('Frame_Dark', '#1b1d22', 0.8, 0.45),
    'joint':  mat('Joint_Rubber', '#0e0f12', 0.0, 0.75),
    'steel':  mat('Steel_Bare', '#8a8f99', 1.0, 0.28),
    'glow':   mat('Emissive_Neon', '#00d8ff', 0.0, 0.3, emit='#00d8ff', strength=12),
    'thrust': mat('Emissive_Thruster', '#ff9a3c', 0.0, 0.3, emit='#ffb066', strength=10),
    'glass':  mat('Visor_Glass', '#05080c', 0.2, 0.05, coat=1.0),
    'decal':  mat('Decal_White', '#d8dade', 0.0, 0.5),
}
MAT_ORDER = list(M.keys())

# ============================================================== skeleton (identical to v1)
JOINTS = {
    'root':        ((0, 0, 0), (0, 0.6, 0), None),
    'pelvis':      ((0, 0, 4.55), (0, 0, 5.05), 'root'),
    'spine':       ((0, 0, 5.05), (0, 0, 5.9), 'pelvis'),
    'chest':       ((0, 0, 5.9), (0, 0, 6.85), 'spine'),
    'head':        ((0, -0.15, 6.95), (0, -0.15, 7.6), 'chest'),
    'booster':     ((0, 0.9, 6.1), (0, 1.5, 6.1), 'chest'),
    'shoulder.L':  ((0.85, 0, 6.45), (1.75, 0, 6.45), 'chest'),
    'upperarm.L':  ((1.85, 0, 6.3), (1.95, -0.05, 5.0), 'shoulder.L'),
    'forearm.L':   ((1.95, -0.05, 5.0), (1.95, -0.25, 3.75), 'upperarm.L'),
    'hand.L':      ((1.95, -0.25, 3.75), (1.95, -0.3, 3.3), 'forearm.L'),
    'pod.L':       ((1.1, 0.55, 7.0), (1.1, 1.0, 7.0), 'chest'),
    'thigh.L':     ((0.62, 0, 4.5), (0.68, -0.4, 2.75), 'pelvis'),
    'shin.L':      ((0.68, -0.4, 2.75), (0.68, 0.25, 0.65), 'thigh.L'),
    'foot.L':      ((0.68, 0.25, 0.65), (0.68, -0.85, 0.15), 'shin.L'),
}
for n in list(JOINTS):
    if n.endswith('.L'):
        h, t, p = JOINTS[n]
        JOINTS[n.replace('.L', '.R')] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]), p.replace('.L', '.R') if p else None)

arm_data = bpy.data.armatures.new('STRAY_Skeleton')
rig = bpy.data.objects.new('STRAY_Rig', arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for n, (h, t, p) in JOINTS.items():
    b = arm_data.edit_bones.new(n)
    b.head, b.tail = h, t
for n, (h, t, p) in JOINTS.items():
    if p:
        arm_data.edit_bones[n].parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT')

# ============================================================== modelling toolkit
CUR = {'prefix': None, 'objs': []}   # objects of the part being built: (obj, bone, keep_separate_name)

def begin(part_id):
    CUR['prefix'] = part_id
    CUR['objs'] = []

def _new(name, bm, loc, rot, material, bone, bevel, segs=2, angle=35, separate=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    for k in MAT_ORDER:
        obj.data.materials.append(M[k])
    idx = MAT_ORDER.index(material)
    for p in obj.data.polygons:
        if p.material_index == 0:
            p.material_index = idx
    if bevel > 0:
        md = obj.modifiers.new('Bevel', 'BEVEL')
        md.width = bevel
        md.segments = segs
        md.limit_method = 'ANGLE'
        md.angle_limit = math.radians(angle)
        md.harden_normals = True
        obj.modifiers.new('WN', 'WEIGHTED_NORMAL').keep_sharp = True
    CUR['objs'].append((obj, bone, separate))
    return obj

def _frustum_bm(bw, bd, tw, td, h, ox=0, oy=0):
    bm = bmesh.new()
    v = [bm.verts.new(p) for p in [
        (-bw / 2, -bd / 2, 0), (bw / 2, -bd / 2, 0), (bw / 2, bd / 2, 0), (-bw / 2, bd / 2, 0),
        (-tw / 2 + ox, -td / 2 + oy, h), (tw / 2 + ox, -td / 2 + oy, h), (tw / 2 + ox, td / 2 + oy, h), (-tw / 2 + ox, td / 2 + oy, h)]]
    for f in [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
        bm.faces.new([v[i] for i in f])
    bm.normal_update()
    return bm

def _panelize(bm, depth=0.025, inset=0.06, min_area=0.12):
    """Inset the larger faces and raise them: reads as armor panels with seams."""
    faces = [f for f in bm.faces if f.calc_area() > min_area]
    if not faces:
        return
    res = bmesh.ops.inset_individual(bm, faces=faces, thickness=inset, depth=0)
    for f in faces:
        n = f.normal.copy()
        for v in f.verts:
            v.co += n * depth
    for f in res.get('faces', []):
        f.material_index = MAT_ORDER.index('joint') + 0  # seam
    # mark seams with a unique flag via material index on the new faces (already set)

def plate(name, bottom, top, h, loc, material='paint', bone=None, top_off=(0, 0), rot=(0, 0, 0), bevel=0.04, panel=True, mirror=False, segs=2):
    """Tapered armor block. bottom/top = (width, depth); loc = base center."""
    bm = _frustum_bm(bottom[0], bottom[1], top[0], top[1], h, *top_off)
    if panel and material in ('paint', 'accent', 'frame') and min(bottom + top + (h,)) > 0.18:
        _panelize(bm)
    o = _new(name, bm, loc, rot, material, bone, bevel, segs)
    if mirror:
        _mirror(o)
    return o

def cyl(name, r, depth, loc, rot=(0, 0, 0), material='joint', bone=None, verts=16, r2=None, mirror=False, bevel=0.015):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    for f in bm.faces:
        f.smooth = len(f.verts) == 4
    o = _new(name, bm, loc, rot, material, bone, bevel, 1, 30)
    if mirror:
        _mirror(o)
    return o

def between(name, a, b, r, material='steel', bone=None, verts=12, r2=None, mirror=False):
    """Cylinder from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = d.to_track_quat('Z', 'Y').to_euler()
    return cyl(name, r, d.length, (a + b) / 2, rot, material, bone, verts, r2, mirror)

def piston(name, a, b, r, bone, mirror=False):
    a, b = Vector(a), Vector(b)
    m = a.lerp(b, 0.55)
    between(name + '_Sleeve', a, m, r, 'frame', bone, 12, mirror=mirror)
    between(name + '_Rod', m - (m - a) * 0.1, b, r * 0.55, 'steel', bone, 10, mirror=mirror)
    cyl(name + '_CapA', r * 1.25, r * 1.6, tuple(a), ((b - a).to_track_quat('Z', 'Y').to_euler()), 'joint', bone, 10, mirror=mirror)
    cyl(name + '_CapB', r * 1.1, r * 1.4, tuple(b), ((b - a).to_track_quat('Z', 'Y').to_euler()), 'joint', bone, 10, mirror=mirror)

def vents(name, loc, w, h, n, bone, facing='-Y', mirror=False, depth=0.06):
    """Recessed vent grille with angled slats. facing: '-Y' front, '+Y' back, '+X'/'-X' sides, '+Z' top."""
    rot = {'-Y': (0, 0, 0), '+Y': (0, 0, math.pi), '+X': (0, 0, math.pi / 2), '-X': (0, 0, -math.pi / 2), '+Z': (-math.pi / 2, 0, 0)}[facing]
    bm = _frustum_bm(w, depth, w, depth, h)
    o = _new(name + '_Frame', bm, loc, rot, 'joint', bone, 0.01, 1)
    if mirror:
        _mirror(o)
    for i in range(n):
        t = (i + 0.5) / n
        bm = _frustum_bm(w * 0.92, 0.025, w * 0.92, 0.025, h / n * 0.45)
        R = Euler(rot).to_matrix()
        off = R @ Vector((0, -depth * 0.55, h * t - h / n * 0.25))
        o = _new(f'{name}_Slat{i}', bm, tuple(Vector(loc) + off), (rot[0] + 0.35, rot[1], rot[2]), 'frame', bone, 0, 1)
        if mirror:
            _mirror(o)

def light(name, loc, size, bone, rot=(0, 0, 0), material='glow', mirror=False):
    bm = _frustum_bm(size[0], size[1], size[0], size[1], size[2])
    o = _new(name, bm, loc, rot, material, bone, 0.005, 1)
    if mirror:
        _mirror(o)
    return o

def bolts(name, start, end, n, bone, r=0.035, axis_rot=(math.pi / 2, 0, 0), mirror=False):
    s, e = Vector(start), Vector(end)
    for i in range(n):
        p = s.lerp(e, i / max(1, n - 1))
        cyl(f'{name}{i}', r, 0.03, tuple(p), axis_rot, 'steel', bone, 6, mirror=mirror, bevel=0)

def text(name, body, loc, size, bone, rot=(math.pi / 2, 0, 0), material='decal', mirror=False):
    """Extruded 3D text decal (unit numbers, maker marks)."""
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.size = size
    cu.extrude = 0.006
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    tobj = bpy.data.objects.new(name + '_tmp', cu)
    scene.collection.objects.link(tobj)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tobj.evaluated_get(dg))
    bpy.data.objects.remove(tobj)
    bm = bmesh.new()
    bm.from_mesh(me)
    for f in bm.faces:
        f.material_index = 0
    o = _new(name, bm, loc, rot, material, bone, 0, 1)
    if mirror:
        _mirror(o, flip_text=True)
    return o

def hazard(name, loc, w, h, bone, rot=(0, 0, 0), stripes=6):
    for i in range(stripes):
        bm = bmesh.new()
        x0 = -w / 2 + i * w / stripes
        sw = w / stripes
        v = [bm.verts.new(p) for p in [(x0, 0, 0), (x0 + sw * 0.5, 0, 0), (x0 + sw * 0.5 + h * 0.6, 0, h), (x0 + h * 0.6, 0, h)]]
        bm.faces.new(v)
        o = _new(f'{name}{i}', bm, loc, rot, 'accent', bone, 0, 1)

def _mirror(obj, flip_text=False):
    dup = obj.copy()
    dup.data = obj.data.copy()
    scene.collection.objects.link(dup)
    dup.name = obj.name.replace('.L', '.R') if '.L' in obj.name else obj.name + '_R'
    dup.location.x = -obj.location.x
    dup.rotation_euler = Euler((obj.rotation_euler.x, -obj.rotation_euler.y, -obj.rotation_euler.z))
    for v in dup.data.vertices:
        v.co.x = -v.co.x
    dup.data.flip_normals()
    if flip_text:
        for v in dup.data.vertices:
            v.co.x = -v.co.x
        dup.data.flip_normals()
    bone = next((b for o, b, s in CUR['objs'] if o == obj), None)
    CUR['objs'].append((dup, bone.replace('.L', '.R') if bone else None, None))
    return dup

def end():
    """Bake transforms, join per bone, weight to that bone, name P_<id>__<bone>."""
    pid = CUR['prefix']
    groups = {}
    for obj, bone, separate in CUR['objs']:
        if obj.name not in bpy.data.objects:
            continue
        key = (bone, separate)
        groups.setdefault(key, []).append(obj)
    bpy.context.view_layer.update()
    for (bone, separate), objs in groups.items():
        for o in objs:
            # apply modifiers so the joined mesh keeps bevels
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
            me.transform(o.matrix_world)
            o.modifiers.clear()
            old = o.data
            o.data = me
            o.matrix_world = Matrix.Identity(4)
            bpy.data.meshes.remove(old)
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        j = bpy.context.view_layer.objects.active
        j.name = f'P_{pid}__{separate or bone}'
        j.data.name = j.name
        # remove unused material slots
        used = {p.material_index for p in j.data.polygons}
        for i in reversed(range(len(j.data.materials))):
            if i not in used:
                j.active_material_index = i
                bpy.ops.object.material_slot_remove()
        vg = j.vertex_groups.new(name=bone)
        vg.add(range(len(j.data.vertices)), 1.0, 'REPLACE')
        j.parent = rig
        am = j.modifiers.new('Armature', 'ARMATURE')
        am.object = rig

    CUR['objs'] = []

def want(pid):
    return ONLY is None or pid in ONLY

# ============================================================== PARTS
X = 1  # handedness helper

# ---------------------------------------------------------------- shared frame (always on)
begin('frame')
cyl('Waist', 0.42, 0.55, (0, 0, 5.1), material='joint', bone='spine')
plate('Pelvis', (1.4, 1.0), (1.15, 0.9), 0.5, (0, 0.02, 4.45), 'frame', 'pelvis')
plate('SkirtF', (1.2, 0.22), (0.95, 0.28), 0.75, (0, -0.56, 4.0), 'paint', 'pelvis', top_off=(0, 0.08))
plate('SkirtS.L', (0.24, 0.9), (0.2, 0.75), 0.8, (0.82, 0.02, 3.95), 'paint', 'pelvis', top_off=(-0.08, 0), mirror=True)
plate('SkirtB', (1.1, 0.24), (0.95, 0.2), 0.7, (0, 0.55, 4.05), 'frame', 'pelvis', top_off=(0, -0.05))
cyl('Hip.L', 0.36, 0.5, (0.62, 0, 4.5), rot=(0, math.pi / 2, 0), bone='thigh.L', mirror=True)
cyl('ShoulderJoint.L', 0.32, 0.5, (1.3, 0, 6.42), rot=(0, math.pi / 2, 0), bone='shoulder.L', mirror=True)
cyl('Neck', 0.22, 0.35, (0, -0.1, 6.95), material='joint', bone='head')
end()

# ---------------------------------------------------------------- HEADS
if want('hd_pike'):
    begin('hd_pike')
    plate('Base', (0.72, 0.85), (0.6, 0.8), 0.28, (0, -0.15, 7.0), 'frame', 'head')
    plate('Shell', (0.82, 1.0), (0.46, 0.62), 0.42, (0, -0.2, 7.26), 'paint', 'head', top_off=(0, 0.14))
    plate('Brow', (0.7, 0.22), (0.62, 0.12), 0.14, (0, -0.66, 7.42), 'accent', 'head', top_off=(0, -0.04), panel=False)
    plate('Visor', (0.6, 0.06), (0.5, 0.06), 0.12, (0, -0.71, 7.3), 'glass', 'head', top_off=(0, 0.04), panel=False)
    light('Eye.L', (0.16, -0.745, 7.33), (0.1, 0.02, 0.05), 'head', mirror=True)
    plate('Antenna', (0.05, 0.05), (0.02, 0.02), 0.55, (0.3, 0.1, 7.6), 'steel', 'head', panel=False, bevel=0)
    plate('Cheek.L', (0.18, 0.5), (0.12, 0.4), 0.35, (0.4, -0.35, 7.05), 'frame', 'head', top_off=(-0.03, 0), mirror=True)
    end()

if want('hd_moth'):
    begin('hd_moth')
    plate('Core', (0.62, 0.9), (0.52, 0.75), 0.3, (0, -0.15, 7.0), 'frame', 'head')
    plate('Dome', (0.6, 0.85), (0.3, 0.45), 0.25, (0, -0.15, 7.3), 'paint', 'head', top_off=(0, 0.1))
    for s, n in ((1, 'L'), ):
        plate(f'Wing.{n}', (0.9, 0.38), (0.75, 0.1), 0.08, (0.62, 0.05, 7.42), 'paint', 'head', top_off=(0.25, 0.1), rot=(0, -0.25, 0), mirror=True)
        light(f'WingTip.{n}', (1.12, 0.12, 7.47), (0.08, 0.16, 0.03), 'head', mirror=True)
    cyl('Lens', 0.14, 0.12, (0, -0.62, 7.18), rot=(math.pi / 2, 0, 0), material='glass', bone='head', verts=20)
    cyl('LensRing', 0.18, 0.08, (0, -0.58, 7.18), rot=(math.pi / 2, 0, 0), material='steel', bone='head', verts=20)
    light('Iris', (0, -0.69, 7.18), (0.1, 0.01, 0.05), 'head')
    cyl('SubLens.L', 0.06, 0.08, (0.2, -0.55, 7.08), rot=(math.pi / 2, 0, 0), material='glow', bone='head', verts=10, mirror=True)
    end()

if want('hd_bastion'):
    begin('hd_bastion')
    plate('Base', (0.95, 1.05), (0.9, 1.0), 0.35, (0, -0.12, 6.98), 'frame', 'head')
    plate('Dome', (1.05, 1.15), (0.7, 0.8), 0.5, (0, -0.12, 7.3), 'paint', 'head', top_off=(0, 0.06))
    plate('Crest', (0.16, 0.9), (0.12, 0.6), 0.2, (0, -0.1, 7.78), 'accent', 'head', panel=False)
    plate('Slit', (0.75, 0.05), (0.75, 0.05), 0.07, (0, -0.71, 7.42), 'glow', 'head', panel=False, bevel=0.005)
    plate('Jaw', (0.8, 0.3), (0.85, 0.25), 0.28, (0, -0.6, 7.02), 'frame', 'head')
    vents('JawVent', (0, -0.76, 7.06), 0.55, 0.18, 4, 'head')
    end()

# ---------------------------------------------------------------- CORES
if want('cr_ferro'):
    begin('cr_ferro')
    plate('Abdomen', (1.05, 0.9), (1.75, 1.2), 0.75, (0, 0.05, 5.25), 'frame', 'spine')
    vents('AbVent', (0, -0.48, 5.35), 0.7, 0.35, 5, 'spine')
    plate('Chest', (1.85, 1.3), (2.45, 1.5), 0.95, (0, 0.05, 5.95), 'paint', 'chest')
    plate('Breast', (1.45, 0.25), (1.95, 0.32), 0.85, (0, -0.76, 6.0), 'paint', 'chest', top_off=(0, -0.18))
    plate('BreastStripe.L', (0.12, 0.06), (0.12, 0.06), 0.82, (0.42, -0.95, 6.02), 'accent', 'chest', top_off=(0, -0.2), panel=False, bevel=0.01, mirror=True)
    plate('Collar', (2.35, 1.4), (1.7, 1.1), 0.3, (0, 0.1, 6.9), 'frame', 'chest')
    plate('Back', (2.1, 0.3), (1.9, 0.4), 1.1, (0, 0.85, 5.6), 'frame', 'chest')
    light('Vent.L', (0.55, -0.9, 5.6), (0.45, 0.04, 0.05), 'chest', mirror=True)
    light('Vent2.L', (0.55, -0.9, 5.7), (0.45, 0.04, 0.05), 'chest', mirror=True)
    text('Number', '07', (0, -1.03, 6.45), 0.28, 'chest', rot=(math.pi / 2 + 0.21, 0, 0))
    bolts('Bolt', (-0.7, -0.88, 6.75), (0.7, -0.88, 6.75), 6, 'chest')
    end()

if want('cr_wasp'):
    begin('cr_wasp')
    plate('Abdomen', (0.85, 0.8), (1.3, 1.0), 0.8, (0, 0.05, 5.2), 'frame', 'spine')
    plate('Chest', (1.3, 1.0), (2.1, 1.25), 0.9, (0, 0.1, 5.95), 'paint', 'chest')
    plate('Keel', (0.5, 0.6), (0.9, 0.9), 0.95, (0, -0.55, 5.85), 'paint', 'chest', top_off=(0, -0.3))
    plate('KeelLight', (0.06, 0.06), (0.06, 0.06), 0.9, (0, -1.02, 5.92), 'glow', 'chest', top_off=(0, -0.32), panel=False, bevel=0)
    plate('Intake.L', (0.45, 0.7), (0.35, 0.55), 0.6, (0.82, -0.2, 6.0), 'frame', 'chest', top_off=(0.1, 0), mirror=True)
    vents('IntakeVent.L', (0.82, -0.56, 6.05), 0.32, 0.45, 4, 'chest', mirror=True)
    plate('Collar', (1.9, 1.1), (1.3, 0.85), 0.25, (0, 0.12, 6.85), 'frame', 'chest')
    plate('Spine', (0.4, 0.5), (0.3, 0.6), 1.2, (0, 0.7, 5.6), 'accent', 'chest', top_off=(0, 0.1))
    text('Number', '31', (0.55, -0.86, 6.45), 0.22, 'chest', rot=(math.pi / 2, 0, 0.35))
    end()

if want('cr_anvil'):
    begin('cr_anvil')
    plate('Abdomen', (1.3, 1.1), (2.2, 1.5), 0.8, (0, 0.05, 5.2), 'frame', 'spine')
    plate('Chest', (2.6, 1.7), (3.2, 2.0), 1.1, (0, 0.05, 5.95), 'paint', 'chest')
    plate('Plate', (2.4, 0.3), (2.6, 0.35), 1.0, (0, -1.0, 5.95), 'paint', 'chest', top_off=(0, -0.1))
    hazard('Hazard', (0, -1.2, 6.55), 2.0, 0.22, 'chest')
    plate('Collar', (3.0, 1.9), (2.4, 1.5), 0.4, (0, 0.05, 7.0), 'frame', 'chest')
    plate('Shield.L', (0.35, 1.6), (0.3, 1.4), 1.1, (1.45, 0.05, 5.9), 'accent', 'chest', mirror=True)
    vents('SideVent.L', (1.63, 0.05, 6.1), 0.9, 0.5, 5, 'chest', facing='+X', mirror=True)
    bolts('Bolt', (-1.0, -1.18, 6.9), (1.0, -1.18, 6.9), 8, 'chest')
    text('Number', '50', (0, -1.18, 6.2), 0.3, 'chest')
    end()

# ---------------------------------------------------------------- ARMS (both sides)
def arm_common(pid, thick, shoulder_scale, extra):
    begin(pid)
    s = shoulder_scale
    plate('Pauldron.L', (1.1 * s, 1.35 * s), (0.85 * s, 1.1 * s), 0.85 * s, (1.88, 0.05, 6.0), 'paint', 'upperarm.L', top_off=(-0.12, 0.05), mirror=True)
    plate('PauldronTrim.L', (1.0 * s, 1.2 * s), (0.6 * s, 0.85 * s), 0.25, (1.82, 0.07, 6.82), 'accent', 'upperarm.L', top_off=(-0.12, 0.05), mirror=True)
    plate('UpperArm.L', (thick, thick * 1.1), (thick * 1.15, thick * 1.2), 1.15, (1.95, -0.02, 4.92), 'frame', 'upperarm.L', mirror=True)
    cyl('Elbow.L', thick * 0.62, thick * 1.2, (1.95, -0.05, 5.0), rot=(0, math.pi / 2, 0), bone='forearm.L', mirror=True)
    plate('Forearm.L', (thick * 1.25, thick * 1.5), (thick * 1.4, thick * 1.65), 1.15, (1.95, -0.2, 3.75), 'paint', 'forearm.L', top_off=(0, 0.1), mirror=True)
    plate('Guard.L', (0.14, thick * 1.4), (0.14, thick * 1.7), 1.0, (1.95 + thick * 0.78, -0.22, 3.85), 'accent', 'forearm.L', top_off=(0, 0.1), mirror=True)
    piston('ElbowPiston.L', (1.95 - thick * 0.75, 0.15, 5.6), (1.95 - thick * 0.8, -0.05, 4.3), 0.06, 'forearm.L', mirror=True)
    plate('Hand.L', (0.42, 0.5), (0.5, 0.55), 0.5, (1.95, -0.3, 3.3), 'frame', 'hand.L', mirror=True)
    for f in range(3):
        plate(f'Finger{f}.L', (0.1, 0.12), (0.08, 0.1), 0.35, (1.85 + f * 0.11, -0.52, 3.05), 'joint', 'hand.L', mirror=True, panel=False)
    extra()
    end()

if want('ar_hand'):
    arm_common('ar_hand', 0.85, 1.0, lambda: light('ArmLight.L', (2.4, -0.42, 4.25), (0.04, 0.4, 0.05), 'forearm.L', mirror=True))
if want('ar_needle'):
    def needle_extra():
        between('Sensor.L', (2.35, 0.2, 6.5), (2.35, -0.6, 6.6), 0.05, 'steel', 'upperarm.L', mirror=True)
        light('SensorTip.L', (2.35, -0.62, 6.6), (0.06, 0.06, 0.06), 'upperarm.L', mirror=True)
    arm_common('ar_needle', 0.62, 0.85, needle_extra)
if want('ar_hauler'):
    def hauler_extra():
        plate('Bracer.L', (1.25, 1.3), (1.2, 1.25), 0.55, (1.95, -0.2, 3.9), 'frame', 'forearm.L', mirror=True)
        hazard('Haz.L', (1.95, -0.86, 4.15), 0.9, 0.16, 'forearm.L')
        vents('ArmVent.L', (2.6, -0.2, 4.4), 0.5, 0.4, 4, 'forearm.L', facing='+X', mirror=True)
    arm_common('ar_hauler', 1.1, 1.18, hauler_extra)

# ---------------------------------------------------------------- LEGS
def foot_standard(scale=1.0, mat_toe='paint'):
    s = scale
    plate('Foot.L', (0.9 * s, 1.8 * s), (0.7 * s, 1.2 * s), 0.42, (0.68, -0.25, 0.0), 'frame', 'foot.L', top_off=(0, 0.15), mirror=True)
    plate('Toe.L', (0.85 * s, 0.6 * s), (0.6 * s, 0.35 * s), 0.32, (0.68, -1.2 * s, 0.0), mat_toe, 'foot.L', top_off=(0, -0.05), mirror=True)
    plate('Heel.L', (0.5 * s, 0.45 * s), (0.35 * s, 0.3 * s), 0.36, (0.68, 0.8 * s, 0.0), 'paint', 'foot.L', top_off=(0, 0.1), mirror=True)
    cyl('Ankle.L', 0.28, 0.62, (0.68, 0.25, 0.62), rot=(0, math.pi / 2, 0), bone='foot.L', mirror=True)

if want('lg_strider'):
    begin('lg_strider')
    plate('Thigh.L', (0.62, 0.72), (0.78, 0.88), 1.6, (0.66, -0.2, 2.9), 'frame', 'thigh.L', top_off=(-0.04, 0.2), mirror=True)
    plate('ThighPlate.L', (0.74, 0.26), (0.84, 0.32), 1.4, (0.68, -0.6, 3.0), 'paint', 'thigh.L', top_off=(0, 0.25), mirror=True)
    piston('ThighPiston.L', (0.98, 0.25, 4.2), (0.98, -0.05, 3.0), 0.07, 'thigh.L', mirror=True)
    cyl('Knee.L', 0.36, 0.82, (0.68, -0.4, 2.75), rot=(0, math.pi / 2, 0), bone='shin.L', mirror=True)
    plate('KneeGuard.L', (0.8, 0.45), (0.7, 0.3), 0.8, (0.68, -0.75, 2.45), 'accent', 'shin.L', top_off=(0, 0.12), mirror=True)
    plate('Shin.L', (0.6, 0.75), (0.75, 1.0), 1.9, (0.68, 0.2, 0.75), 'paint', 'shin.L', top_off=(0, -0.55), mirror=True)
    plate('Calf.L', (0.5, 0.5), (0.62, 0.72), 1.5, (0.68, 0.55, 0.95), 'frame', 'shin.L', top_off=(0, -0.35), mirror=True)
    vents('CalfVent.L', (0.68, 0.88, 1.5), 0.42, 0.5, 4, 'shin.L', facing='+Y', mirror=True)
    light('ShinLight.L', (0.98, -0.25, 1.1), (0.05, 0.05, 1.1), 'shin.L', rot=(0.3, 0, 0), mirror=True)
    foot_standard()
    end()

if want('lg_hopper'):
    begin('lg_hopper')
    # reverse-joint look: thigh swept back, long shin forward, raised heel spur
    plate('Thigh.L', (0.55, 0.8), (0.7, 1.05), 1.45, (0.66, 0.05, 3.05), 'frame', 'thigh.L', top_off=(0, -0.1), mirror=True)
    plate('ThighArmor.L', (0.68, 0.3), (0.75, 0.35), 1.2, (0.68, -0.45, 3.2), 'paint', 'thigh.L', top_off=(0, 0.2), mirror=True)
    cyl('Knee.L', 0.32, 0.75, (0.68, -0.4, 2.75), rot=(0, math.pi / 2, 0), bone='shin.L', mirror=True)
    plate('Shin.L', (0.45, 0.55), (0.55, 0.7), 2.1, (0.68, 0.1, 0.6), 'paint', 'shin.L', top_off=(0, -0.55), mirror=True)
    plate('Spur.L', (0.3, 0.9), (0.2, 0.3), 0.5, (0.68, 0.75, 1.8), 'accent', 'shin.L', top_off=(0, 0.3), mirror=True)
    piston('ShinPiston.L', (0.98, 0.55, 2.4), (0.98, 0.45, 0.95), 0.07, 'shin.L', mirror=True)
    light('Stripe.L', (0.94, -0.18, 1.4), (0.04, 0.05, 0.9), 'shin.L', rot=(0.3, 0, 0), mirror=True)
    plate('Foot.L', (0.6, 1.6), (0.45, 1.0), 0.35, (0.68, -0.35, 0.0), 'frame', 'foot.L', top_off=(0, 0.1), mirror=True)
    plate('Claw1.L', (0.18, 0.6), (0.1, 0.2), 0.3, (0.88, -1.15, 0.0), 'steel', 'foot.L', top_off=(0, -0.2), mirror=True, panel=False)
    plate('Claw2.L', (0.18, 0.6), (0.1, 0.2), 0.3, (0.48, -1.15, 0.0), 'steel', 'foot.L', top_off=(0, -0.2), mirror=True, panel=False)
    plate('HeelClaw.L', (0.2, 0.5), (0.1, 0.15), 0.28, (0.68, 0.55, 0.0), 'steel', 'foot.L', top_off=(0, 0.2), mirror=True, panel=False)
    cyl('Ankle.L', 0.24, 0.55, (0.68, 0.25, 0.62), rot=(0, math.pi / 2, 0), bone='foot.L', mirror=True)
    end()

if want('lg_glide'):
    begin('lg_glide')
    plate('Thigh.L', (0.75, 0.85), (0.9, 1.0), 1.5, (0.66, -0.15, 3.0), 'frame', 'thigh.L', top_off=(0, 0.15), mirror=True)
    plate('ThighFin.L', (0.12, 1.1), (0.08, 0.6), 1.3, (1.05, -0.1, 3.1), 'accent', 'thigh.L', mirror=True, panel=False)
    cyl('Knee.L', 0.4, 0.85, (0.68, -0.4, 2.75), rot=(0, math.pi / 2, 0), bone='shin.L', mirror=True)
    plate('Shin.L', (1.0, 1.3), (0.8, 1.05), 1.6, (0.68, -0.05, 0.85), 'paint', 'shin.L', top_off=(0, -0.25), mirror=True)
    plate('Skirt.L', (1.25, 1.75), (1.1, 1.5), 0.55, (0.68, -0.1, 0.3), 'frame', 'foot.L', mirror=True)
    cyl('Pad.L', 0.5, 0.08, (0.68, -0.1, 0.25), material='glow', bone='foot.L', verts=24, mirror=True, bevel=0)
    vents('SkirtVent.L', (0.68, -0.98, 0.55), 0.8, 0.3, 4, 'foot.L', mirror=True)
    light('SkirtRim.L', (1.32, -0.1, 0.55), (0.04, 1.3, 0.05), 'foot.L', mirror=True)
    end()

if want('lg_bulwark'):
    begin('lg_bulwark')
    plate('Thigh.L', (0.95, 1.0), (1.15, 1.2), 1.6, (0.7, -0.15, 2.9), 'frame', 'thigh.L', top_off=(0, 0.15), mirror=True)
    plate('ThighArmor.L', (1.1, 0.35), (1.2, 0.4), 1.5, (0.72, -0.7, 2.95), 'paint', 'thigh.L', top_off=(0, 0.2), mirror=True)
    piston('ThighPiston.L', (1.3, 0.3, 4.1), (1.3, 0.0, 2.9), 0.1, 'thigh.L', mirror=True)
    cyl('Knee.L', 0.48, 1.1, (0.7, -0.4, 2.75), rot=(0, math.pi / 2, 0), bone='shin.L', mirror=True)
    plate('KneeGuard.L', (1.1, 0.55), (0.95, 0.4), 0.9, (0.7, -0.85, 2.4), 'accent', 'shin.L', top_off=(0, 0.12), mirror=True)
    plate('Shin.L', (1.1, 1.2), (1.2, 1.35), 1.95, (0.7, 0.1, 0.65), 'paint', 'shin.L', top_off=(0, -0.45), mirror=True)
    hazard('Haz.L', (0.7, -0.62, 1.2), 0.9, 0.16, 'shin.L')
    vents('ShinVent.L', (1.31, 0.1, 1.4), 0.7, 0.6, 5, 'shin.L', facing='+X', mirror=True)
    foot_standard(1.3)
    end()

# ---------------------------------------------------------------- BOOSTERS
def nozzle(i, x, y, z, r, pid):
    cyl(f'Nozzle{i}', r, 0.5, (x, y, z), material='steel', bone='booster', r2=r * 1.25)
    CUR['objs'][-1] = (CUR['objs'][-1][0], 'booster', f'Nozzle_{i}')
    cyl(f'Burner{i}', r * 0.75, 0.06, (x, y, z - 0.24), material='thrust', bone='booster', verts=14, bevel=0)

if want('bs_flare'):
    begin('bs_flare')
    plate('Pack', (1.6, 0.6), (1.5, 0.55), 1.2, (0, 1.15, 5.5), 'frame', 'booster')
    for i, x in enumerate((0.55, -0.55)):
        plate(f'Pod{i}', (0.55, 0.7), (0.45, 0.6), 1.5, (x, 1.35, 5.3), 'paint', 'booster', top_off=(0, -0.05))
        light(f'PodStripe{i}', (x, 1.02, 5.4), (0.08, 0.04, 1.2), 'booster', material='accent')
        nozzle(i, x, 1.4, 5.1, 0.24, 'bs_flare')
    end()

if want('bs_comet'):
    begin('bs_comet')
    plate('Pack', (1.9, 0.7), (1.7, 0.6), 1.3, (0, 1.2, 5.45), 'frame', 'booster')
    for i, x in enumerate((0.7, 0, -0.7)):
        plate(f'Pod{i}', (0.5, 0.75), (0.4, 0.7), 1.9, (x, 1.45, 5.0), 'paint', 'booster', top_off=(0, 0.05))
        nozzle(i, x, 1.5, 4.8, 0.22, 'bs_comet')
    for x in (1.0, -1.0):
        plate(f'Fin{x}', (0.08, 0.9), (0.06, 0.4), 1.0, (x, 1.6, 6.3), 'accent', 'booster', top_off=(0, 0.3), panel=False)
    light('CometStripe', (0, 1.84, 5.8), (1.6, 0.04, 0.06), 'booster')
    end()

if want('bs_tern'):
    begin('bs_tern')
    plate('Pack', (1.2, 0.5), (1.1, 0.45), 1.0, (0, 1.1, 5.6), 'frame', 'booster')
    for i, x in enumerate((0.95, -0.95)):
        plate(f'Wing{i}', (0.6, 0.3), (0.9, 0.12), 1.4, (x * 0.75, 1.2, 5.7), 'paint', 'booster', top_off=(x * 0.5, 0.1), rot=(0, -0.3 if x > 0 else 0.3, 0))
        nozzle(i, x * 0.6, 1.25, 5.45, 0.18, 'bs_tern')
    end()

# ---------------------------------------------------------------- WEAPONS (right hand)
def barrel_marker(pid, start, end_, r):
    between('Barrel', start, end_, r, 'steel', 'hand.R')
    CUR['objs'][-1] = (CUR['objs'][-1][0], 'hand.R', 'Barrel')

if want('wp_hornet'):
    begin('wp_hornet')
    plate('Receiver', (0.34, 2.4), (0.3, 2.2), 0.45, (-1.95, -1.05, 3.2), 'frame', 'hand.R')
    plate('Shroud', (0.3, 1.2), (0.26, 1.1), 0.3, (-1.95, -2.6, 3.32), 'paint', 'hand.R')
    barrel_marker('wp_hornet', (-1.95, -3.1, 3.47), (-1.95, -4.1, 3.47), 0.07)
    plate('Mag', (0.22, 0.45), (0.22, 0.38), 0.6, (-1.95, -0.7, 2.62), 'paint', 'hand.R', top_off=(0, 0.05))
    plate('Sight', (0.14, 0.5), (0.12, 0.42), 0.18, (-1.95, -1.2, 3.65), 'frame', 'hand.R', panel=False)
    light('SightLens', (-1.95, -1.46, 3.74), (0.1, 0.02, 0.08), 'hand.R')
    light('Ammo', (-1.79, -1.2, 3.35), (0.02, 0.8, 0.05), 'hand.R')
    end()

if want('wp_gale'):
    begin('wp_gale')
    plate('Drum', (0.7, 0.9), (0.7, 0.9), 0.7, (-1.95, -0.7, 2.75), 'frame', 'hand.R')
    plate('Body', (0.5, 1.4), (0.45, 1.3), 0.5, (-1.95, -1.3, 3.15), 'paint', 'hand.R')
    for k in range(6):
        a = k / 6 * math.pi * 2
        between(f'Barrel{k}', (-1.95 + math.cos(a) * 0.12, -2.0, 3.4 + math.sin(a) * 0.12), (-1.95 + math.cos(a) * 0.12, -3.3, 3.4 + math.sin(a) * 0.12), 0.045, 'steel', 'hand.R', 8)
    cyl('Clamp', 0.22, 0.12, (-1.95, -2.9, 3.4), rot=(math.pi / 2, 0, 0), material='frame', bone='hand.R')
    barrel_marker('wp_gale', (-1.95, -3.2, 3.4), (-1.95, -3.35, 3.4), 0.05)
    hazard('Haz', (-1.95, -1.98, 3.3), 0.4, 0.12, 'hand.R')
    end()

if want('wp_maul'):
    begin('wp_maul')
    plate('Body', (0.55, 2.0), (0.5, 1.8), 0.55, (-1.95, -1.1, 3.1), 'frame', 'hand.R')
    between('Bore', (-1.95, -2.0, 3.38), (-1.95, -3.1, 3.38), 0.2, 'frame', 'hand.R', 16)
    barrel_marker('wp_maul', (-1.95, -3.05, 3.38), (-1.95, -3.2, 3.38), 0.22)
    plate('Pump', (0.4, 0.6), (0.4, 0.55), 0.25, (-1.95, -1.9, 2.95), 'paint', 'hand.R')
    plate('Shells', (0.12, 0.7), (0.12, 0.7), 0.2, (-1.65, -1.0, 3.2), 'accent', 'hand.R', panel=False)
    end()

if want('wp_lance'):
    begin('wp_lance')
    plate('Capacitor', (0.55, 1.4), (0.5, 1.3), 0.6, (-1.95, -0.8, 3.0), 'paint', 'hand.R')
    for k in range(3):
        light(f'Coil{k}', (-1.95, -1.2 - k * 0.45, 3.65), (0.5, 0.08, 0.06), 'hand.R')
    plate('Rail.L', (0.08, 3.5), (0.08, 3.5), 0.28, (-1.8, -3.0, 3.2), 'steel', 'hand.R', panel=False)
    plate('Rail.R', (0.08, 3.5), (0.08, 3.5), 0.28, (-2.1, -3.0, 3.2), 'steel', 'hand.R', panel=False)
    plate('RailGlow', (0.14, 3.3), (0.14, 3.3), 0.05, (-1.95, -3.0, 3.3), 'glow', 'hand.R', panel=False, bevel=0)
    barrel_marker('wp_lance', (-1.95, -4.6, 3.34), (-1.95, -4.8, 3.34), 0.06)
    end()

# ---------------------------------------------------------------- SHOULDERS (left)
if want('sh_swarm'):
    begin('sh_swarm')
    plate('Box', (0.95, 1.4), (0.9, 1.3), 0.78, (1.15, 0.55, 6.95), 'paint', 'pod.L')
    plate('Face', (0.82, 0.06), (0.82, 0.06), 0.62, (1.15, -0.17, 7.0), 'frame', 'pod.L', panel=False, bevel=0.005)
    for ix in range(3):
        for iz in range(2):
            cyl(f'Cell{ix}{iz}', 0.1, 0.1, (0.92 + ix * 0.23, -0.22, 7.15 + iz * 0.28), rot=(math.pi / 2, 0, 0), material='thrust', bone='pod.L', verts=10, bevel=0)
    plate('Mount', (0.4, 0.6), (0.3, 0.5), 0.3, (1.15, 0.55, 6.7), 'frame', 'pod.L')
    text('Mark', 'SWARM', (1.62, 0.55, 7.32), 0.13, 'pod.L', rot=(math.pi / 2, 0, math.pi / 2))
    end()

if want('sh_mines'):
    begin('sh_mines')
    cyl('Drum', 0.5, 1.2, (1.15, 0.65, 7.25), rot=(math.pi / 2, 0, 0), material='paint', bone='pod.L', verts=20)
    cyl('DrumCap', 0.42, 1.3, (1.15, 0.65, 7.25), rot=(math.pi / 2, 0, 0), material='frame', bone='pod.L', verts=20)
    light('Chute', (1.15, 1.32, 7.25), (0.5, 0.04, 0.3), 'pod.L', material='thrust')
    hazard('Haz', (1.15, 0.0, 7.6), 0.6, 0.12, 'pod.L')
    plate('Mount', (0.4, 0.6), (0.3, 0.5), 0.3, (1.15, 0.6, 6.7), 'frame', 'pod.L')
    end()

if want('sh_spear'):
    begin('sh_spear')
    plate('Tube', (0.55, 2.6), (0.5, 2.5), 0.55, (1.15, 0.3, 7.0), 'paint', 'pod.L')
    cyl('Muzzle', 0.24, 0.12, (1.15, -0.97, 7.27), rot=(math.pi / 2, 0, 0), material='thrust', bone='pod.L', verts=14, bevel=0)
    plate('Fin', (0.06, 0.8), (0.04, 0.4), 0.5, (1.15, 1.2, 7.5), 'accent', 'pod.L', panel=False)
    plate('Mount', (0.4, 0.6), (0.3, 0.5), 0.3, (1.15, 0.55, 6.7), 'frame', 'pod.L')
    end()

# ============================================================== pose actions, export
def pose_action(name, frames):
    act = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = act
    for f, pose in frames:
        for bn, (rx, ry, rz) in pose.items():
            pb = rig.pose.bones[bn]
            pb.rotation_mode = 'XYZ'
            pb.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
            pb.keyframe_insert('rotation_euler', frame=f)
    return act

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'stray_v2.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'stray_v2.fbx'), use_selection=True, apply_unit_scale=True,
                         use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                         object_types={'ARMATURE', 'MESH'}, armature_nodetype='NULL', mesh_smooth_type='FACE')

parts = sorted({o.name.split('__')[0] for o in bpy.data.objects if o.name.startswith('P_')})
print('PARTS', parts)

# ============================================================== beauty renders of a few loadouts
if RENDER:
    world = bpy.data.worlds.new('Night')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.004, 0.006, 0.012, 1)
    bpy.ops.mesh.primitive_plane_add(size=80)
    floor = bpy.context.active_object
    floor.data.materials.append(mat('Wet', '#0b0c10', 0.3, 0.12, coat=1.0))

    def L(name, kind, loc, energy, color, size=1.0):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if kind == 'AREA':
            ld.size = size
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 4)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    L('Key', 'AREA', (-7, -9, 9), 2600, (1.0, 0.86, 0.72), 4)
    L('Rim', 'AREA', (8, 7, 6), 3200, (0.2, 0.75, 1.0), 3)
    L('Rim2', 'AREA', (-8, 8, 4), 900, (1.0, 0.25, 0.6), 3)
    L('Fill', 'AREA', (6, -10, 5), 900, (0.8, 0.85, 1.0), 6)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = scene.render.resolution_y = 1100
    scene.view_settings.view_transform = 'AgX'

    loadouts = {
        'standard': ['frame', 'hd_pike', 'cr_ferro', 'ar_hand', 'lg_strider', 'bs_flare', 'wp_hornet', 'sh_swarm'],
        'racer': ['frame', 'hd_moth', 'cr_wasp', 'ar_needle', 'lg_glide', 'bs_comet', 'wp_lance', 'sh_spear'],
        'heavy': ['frame', 'hd_bastion', 'cr_anvil', 'ar_hauler', 'lg_bulwark', 'bs_tern', 'wp_maul', 'sh_mines'],
        'hopper': ['frame', 'hd_pike', 'cr_wasp', 'ar_hand', 'lg_hopper', 'bs_flare', 'wp_gale', 'sh_swarm'],
    }
    for lname, ids in loadouts.items():
        for o in bpy.data.objects:
            if o.name.startswith('P_'):
                pid = o.name[2:].split('__')[0]
                o.hide_render = pid not in ids
        for shot, (pos, tgt) in {'34': ((-9.5, -13.5, 5.2), (0, 0, 4.1)), 'back': ((10.5, 12, 6.0), (0, 0, 4.4))}.items():
            cam.location = pos
            cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = os.path.join(OUT, f'v2_{lname}_{shot}.png')
            bpy.ops.render.render(write_still=True)

print('BUILD_OK')
