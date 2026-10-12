"""Build the hero mech ("VX-7 STRAY") procedurally in Blender.

Run headless:
  blender --background --factory-startup --python art/mech/build_mech.py -- <out_dir> [--render]

Produces:
  <out_dir>/stray.blend   editable source
  <out_dir>/stray.glb     skinned mesh + skeleton (Godot / web)
  <out_dir>/stray.fbx     skinned mesh + skeleton (Unreal / Unity)
  <out_dir>/stray_*.png   beauty renders (with --render)

Every armor part is its own mesh object, rigidly skinned to one bone, so parts
can be swapped at runtime by replacing meshes that share the skeleton.
The mech faces -Y (exports as +Z forward / glTF convention), Z up, meters.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.path.dirname(__file__), 'out'))
RENDER = '--render' in argv
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- scene reset
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

# ---------------------------------------------------------------- materials
def mat(name, color, metal=0.0, rough=0.5, emit=None, strength=0.0, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    if coat:
        b.inputs['Coat Weight'].default_value = coat
        b.inputs['Coat Roughness'].default_value = 0.15
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = strength
    return m

def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

M = {
    'paint':  mat('Paint_Primary', srgb('#3a4250'), 0.55, 0.32, coat=0.4),
    'accent': mat('Paint_Accent', srgb('#d98a1c'), 0.4, 0.3, coat=0.5),
    'frame':  mat('Frame_Dark', srgb('#1b1d22'), 0.8, 0.45),
    'joint':  mat('Joint_Rubber', srgb('#0e0f12'), 0.0, 0.75),
    'steel':  mat('Steel_Bare', srgb('#8a8f99'), 1.0, 0.28),
    'glow':   mat('Emissive_Neon', srgb('#00d8ff'), 0.0, 0.3, emit=srgb('#00d8ff'), strength=18),
    'thrust': mat('Emissive_Thruster', srgb('#ff9a3c'), 0.0, 0.3, emit=srgb('#ffb066'), strength=12),
    'glass':  mat('Visor_Glass', srgb('#05080c'), 0.2, 0.05, coat=1.0),
}

# ---------------------------------------------------------------- mesh helpers
PARTS = []  # (object, bone)

def _finish(obj, bevel=0.035, segs=2):
    if bevel > 0:
        md = obj.modifiers.new('Bevel', 'BEVEL')
        md.width = bevel
        md.segments = segs
        md.limit_method = 'ANGLE'
        md.angle_limit = math.radians(35)
        md.harden_normals = True
        obj.modifiers.new('WeightedNormal', 'WEIGHTED_NORMAL').keep_sharp = True
    return obj

def _panel(me, material):
    """Inset large faces and push them in: reads as panel seams once bevelled.
    Recessed seams get the dark frame material (slot 1); a few get a neon strip (slot 2)."""
    bm = bmesh.new()
    bm.from_mesh(me)
    faces = [f for f in bm.faces if f.calc_area() > 0.18]
    res = bmesh.ops.inset_individual(bm, faces=faces, thickness=0.07, depth=0.0)
    for f in faces:
        # inner face goes outward a touch so the plate reads as a raised panel
        n = f.normal.copy()
        for v in f.verts:
            v.co += n * 0.03
    seam_faces = res.get('faces', [])
    for f in seam_faces:
        f.material_index = 1
    bm.to_mesh(me)
    bm.free()

def frustum(name, bottom, top, h, loc=(0, 0, 0), top_off=(0, 0), material='paint', bone=None,
            bevel=0.035, rot=(0, 0, 0), mirror=False):
    """Box whose top rectangle can differ in size and shift: the basic armor plate."""
    bw, bd = bottom
    tw, td = top
    ox, oy = top_off
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    v = [bm.verts.new(p) for p in [
        (-bw / 2, -bd / 2, 0), (bw / 2, -bd / 2, 0), (bw / 2, bd / 2, 0), (-bw / 2, bd / 2, 0),
        (-tw / 2 + ox, -td / 2 + oy, h), (tw / 2 + ox, -td / 2 + oy, h), (tw / 2 + ox, td / 2 + oy, h), (-tw / 2 + ox, td / 2 + oy, h)]]
    for f in [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
        bm.faces.new([v[i] for i in f])
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    if material in ('paint', 'accent', 'frame') and min(bw, bd, tw, td, h) > 0.2:
        _panel(me, material)
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    obj.data.materials.append(M[material])
    obj.data.materials.append(M['joint'])  # slot 1: recessed panel seams
    _finish(obj, bevel)
    PARTS.append((obj, bone))
    if mirror:
        _mirror(obj, bone)
    return obj

def cyl(name, r, depth, loc=(0, 0, 0), rot=(0, 0, 0), material='joint', bone=None, verts=16, r2=None, mirror=False, bevel=0.02):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    obj.data.materials.append(M[material])
    for p in obj.data.polygons:
        p.use_smooth = True
    _finish(obj, bevel, 1)
    PARTS.append((obj, bone))
    if mirror:
        _mirror(obj, bone)
    return obj

def _mirror(obj, bone):
    """Duplicate across X. Bone names ending in .L become .R."""
    dup = obj.copy()
    dup.data = obj.data.copy()
    # flip geometry in X (in local space we mirror via scale, then apply)
    bpy.context.collection.objects.link(dup)
    dup.name = obj.name.replace('.L', '.R') if '.L' in obj.name else obj.name + '.R'
    dup.location.x = -obj.location.x
    dup.rotation_euler = Euler((obj.rotation_euler.x, -obj.rotation_euler.y, -obj.rotation_euler.z))
    me = dup.data
    for vtx in me.vertices:
        vtx.co.x = -vtx.co.x
    me.flip_normals()
    PARTS.append((dup, bone.replace('.L', '.R') if bone else None))
    return dup

# ---------------------------------------------------------------- skeleton
JOINTS = {
    # name: (head, tail, parent)
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
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for n, (h, t, p) in JOINTS.items():
    b = arm_data.edit_bones.new(n)
    b.head, b.tail = h, t
for n, (h, t, p) in JOINTS.items():
    if p:
        arm_data.edit_bones[n].parent = arm_data.edit_bones[p]
        arm_data.edit_bones[n].use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- the mech
# Proportions take cues from lean, tall assembly mechs: narrow waist, broad
# shoulders, forward-canted knees, heavy feet, small sensor head.

# -- Pelvis / waist
frustum('Pelvis_Core', (1.5, 1.0), (1.2, 0.9), 0.55, (0, 0, 4.45), material='frame', bone='pelvis')
frustum('Pelvis_Skirt.F', (1.3, 0.2), (1.0, 0.25), 0.7, (0, -0.55, 4.05), top_off=(0, 0.08), bone='pelvis')
frustum('Pelvis_Skirt.L', (0.25, 0.9), (0.2, 0.8), 0.75, (0.82, 0, 4.0), top_off=(-0.08, 0), bone='pelvis', mirror=True)
cyl('Waist_Joint', 0.42, 0.5, (0, 0, 5.1), material='joint', bone='spine')

# -- Core: layered chest with a forward-raked breastplate
frustum('Core_Lower', (1.1, 0.95), (1.9, 1.3), 0.75, (0, 0.05, 5.25), material='frame', bone='spine')
frustum('Core_Chest', (1.9, 1.35), (2.5, 1.5), 0.95, (0, 0.05, 5.95), bone='chest')
frustum('Core_Breast', (1.5, 0.25), (2.0, 0.35), 0.85, (0, -0.78, 6.0), top_off=(0, -0.18), bone='chest', material='paint')
frustum('Core_BreastStripe', (0.12, 0.05), (0.12, 0.05), 0.8, (0.45, -0.98, 6.02), top_off=(0, -0.2), material='accent', bone='chest', bevel=0.01, mirror=True)
frustum('Core_Collar', (2.4, 1.4), (1.7, 1.1), 0.3, (0, 0.1, 6.9), material='frame', bone='chest')
frustum('Core_Vent.L', (0.5, 0.06), (0.5, 0.06), 0.3, (0.55, -0.92, 5.55), material='glow', bone='chest', bevel=0.005, mirror=True)
frustum('Core_Back', (2.2, 0.3), (2.0, 0.4), 1.1, (0, 0.85, 5.6), material='frame', bone='chest')

# -- Head: low sensor wedge with a visor slit and a fin
frustum('Head_Base', (0.75, 0.9), (0.6, 0.85), 0.35, (0, -0.15, 6.95), material='frame', bone='head')
frustum('Head_Shell', (0.8, 0.95), (0.5, 0.6), 0.4, (0, -0.2, 7.25), top_off=(0, 0.12), bone='head')
frustum('Head_Visor', (0.62, 0.06), (0.52, 0.06), 0.13, (0, -0.68, 7.32), top_off=(0, 0.05), material='glow', bone='head', bevel=0.01)
frustum('Head_Fin', (0.06, 0.7), (0.04, 0.35), 0.45, (0, 0.05, 7.6), top_off=(0, 0.2), material='accent', bone='head', bevel=0.01)
cyl('Head_Eye.L', 0.07, 0.08, (0.2, -0.66, 7.15), rot=(math.pi / 2, 0, 0), material='glow', bone='head', verts=10, mirror=True)

# -- Booster pack: twin vertical thrusters on the back
frustum('Booster_Frame', (1.6, 0.6), (1.5, 0.55), 1.2, (0, 1.15, 5.5), material='frame', bone='booster')
for side in (1,):
    frustum('Booster_Pod.L', (0.55, 0.7), (0.45, 0.6), 1.5, (0.55 * side, 1.35, 5.3), top_off=(0, -0.05), bone='booster', mirror=True)
    cyl('Booster_Nozzle.L', 0.24, 0.45, (0.55 * side, 1.4, 5.1), material='steel', bone='booster', r2=0.3, mirror=True)
    cyl('Booster_Flame.L', 0.17, 0.05, (0.55 * side, 1.4, 4.86), material='thrust', bone='booster', verts=12, mirror=True, bevel=0)
    frustum('Booster_Stripe.L', (0.08, 0.05), (0.08, 0.05), 1.3, (0.55 * side, 1.02, 5.4), material='accent', bone='booster', bevel=0.005, mirror=True)

# -- Shoulders + arms
frustum('Shoulder_Joint.L', (0.5, 0.5), (0.5, 0.5), 0.5, (1.25, 0, 6.2), material='joint', bone='shoulder.L', mirror=True)
frustum('Shoulder_Pauldron.L', (1.1, 1.35), (0.85, 1.1), 0.85, (1.85, 0.05, 6.0), top_off=(-0.1, 0.05), bone='upperarm.L', mirror=True)
frustum('Shoulder_Cap.L', (0.95, 1.15), (0.55, 0.8), 0.3, (1.78, 0.08, 6.85), top_off=(-0.1, 0.05), material='accent', bone='upperarm.L', mirror=True)
frustum('UpperArm.L', (0.45, 0.5), (0.55, 0.6), 1.1, (1.95, -0.02, 4.95), material='frame', bone='upperarm.L', mirror=True)
cyl('Elbow.L', 0.3, 0.6, (1.95, -0.05, 5.0), rot=(0, math.pi / 2, 0), bone='forearm.L', mirror=True)
frustum('Forearm.L', (0.6, 0.75), (0.7, 0.85), 1.15, (1.95, -0.2, 3.75), top_off=(0, 0.1), bone='forearm.L', mirror=True)
frustum('Forearm_Guard.L', (0.15, 0.7), (0.15, 0.9), 1.0, (2.32, -0.22, 3.85), top_off=(0, 0.1), material='accent', bone='forearm.L', mirror=True)
frustum('Hand.L', (0.42, 0.5), (0.5, 0.55), 0.5, (1.95, -0.3, 3.3), material='frame', bone='hand.L', mirror=True)

# -- Right-hand rifle (held forward)
rifle_bone = 'hand.R'
frustum('Rifle_Body', (0.32, 2.6), (0.28, 2.4), 0.42, (-1.95, -1.0, 3.25), material='frame', bone=rifle_bone)
frustum('Rifle_Barrel', (0.14, 1.8), (0.14, 1.8), 0.14, (-1.95, -3.0, 3.42), material='steel', bone=rifle_bone, bevel=0.01)
frustum('Rifle_Mag', (0.22, 0.45), (0.22, 0.4), 0.6, (-1.95, -0.65, 2.7), top_off=(0, 0.05), material='paint', bone=rifle_bone)
frustum('Rifle_Sight', (0.12, 0.5), (0.12, 0.45), 0.16, (-1.95, -1.3, 3.66), material='glow', bone=rifle_bone, bevel=0.01)

# -- Left shoulder missile pod
frustum('Pod_Box', (0.95, 1.4), (0.9, 1.3), 0.75, (1.15, 0.55, 6.95), material='paint', bone='pod.L')
frustum('Pod_Face', (0.8, 0.05), (0.8, 0.05), 0.6, (1.15, -0.17, 7.0), material='frame', bone='pod.L', bevel=0.005)
for ix in range(3):
    for iz in range(2):
        cyl(f'Pod_Cell_{ix}{iz}', 0.09, 0.08, (0.92 + ix * 0.23, -0.2, 7.15 + iz * 0.28), rot=(math.pi / 2, 0, 0), material='thrust', bone='pod.L', verts=8, bevel=0)

# -- Legs: forward-canted knee, armored shin, heavy foot with toe and heel claws
cyl('Hip_Joint.L', 0.38, 0.5, (0.62, 0, 4.5), rot=(0, math.pi / 2, 0), bone='thigh.L', mirror=True)
frustum('Thigh.L', (0.65, 0.75), (0.8, 0.9), 1.6, (0.66, -0.2, 2.9), top_off=(-0.04, 0.2), material='frame', bone='thigh.L', mirror=True)
frustum('Thigh_Plate.L', (0.75, 0.25), (0.85, 0.3), 1.4, (0.68, -0.58, 3.0), top_off=(0, 0.25), bone='thigh.L', mirror=True)
cyl('Knee.L', 0.36, 0.8, (0.68, -0.4, 2.75), rot=(0, math.pi / 2, 0), bone='shin.L', mirror=True)
frustum('Knee_Guard.L', (0.8, 0.45), (0.7, 0.3), 0.8, (0.68, -0.75, 2.45), top_off=(0, 0.12), material='accent', bone='shin.L', mirror=True)
frustum('Shin.L', (0.6, 0.75), (0.75, 1.0), 1.9, (0.68, 0.2, 0.75), top_off=(0, -0.55), bone='shin.L', mirror=True)
frustum('Shin_Calf.L', (0.5, 0.5), (0.6, 0.7), 1.5, (0.68, 0.55, 0.95), top_off=(0, -0.35), material='frame', bone='shin.L', mirror=True)
frustum('Shin_Light.L', (0.06, 0.05), (0.06, 0.05), 1.2, (0.98, -0.25, 1.1), top_off=(0, -0.4), material='glow', bone='shin.L', bevel=0.005, mirror=True)
cyl('Ankle.L', 0.28, 0.6, (0.68, 0.25, 0.62), rot=(0, math.pi / 2, 0), bone='foot.L', mirror=True)
frustum('Foot.L', (0.9, 1.8), (0.7, 1.2), 0.42, (0.68, -0.25, 0.0), top_off=(0, 0.15), material='frame', bone='foot.L', mirror=True)
frustum('Foot_Toe.L', (0.85, 0.55), (0.6, 0.35), 0.3, (0.68, -1.2, 0.0), top_off=(0, -0.05), bone='foot.L', mirror=True)
frustum('Foot_Heel.L', (0.5, 0.45), (0.35, 0.3), 0.35, (0.68, 0.8, 0.0), top_off=(0, 0.1), bone='foot.L', mirror=True)

# ---------------------------------------------------------------- skin to rig
bpy.context.view_layer.update()
for obj, bone in PARTS:
    if bone is None:
        continue
    # bake object transform into the mesh so every part is in rig space
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    vg = obj.vertex_groups.new(name=bone)
    vg.add(range(len(obj.data.vertices)), 1.0, 'REPLACE')
    obj.parent = rig
    am = obj.modifiers.new('Armature', 'ARMATURE')
    am.object = rig
    # keep the armature modifier first so bevels are computed in rest pose
    obj.modifiers.move(len(obj.modifiers) - 1, 0)

# ---------------------------------------------------------------- idle + boost poses as actions
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
    for pb in rig.pose.bones:
        pb.rotation_euler = (0, 0, 0)
    return act

# A grounded, slightly crouched "ready" stance used for renders and as the idle pose.
READY = {
    'spine': (4, 0, 0), 'chest': (3, 0, 0),
}
idle = pose_action('Idle', [(1, READY), (40, {**READY, 'spine': (8, 0, 0), 'chest': (5, 0, 0)}), (80, READY)])
rig.animation_data.action = idle
scene.frame_set(1)

# ---------------------------------------------------------------- save + export
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'stray.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, 'stray.glb'), export_format='GLB', export_apply=True,
                          export_animations=True, export_skins=True, use_selection=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'stray.fbx'), use_selection=True, apply_unit_scale=True,
                         use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=True,
                         object_types={'ARMATURE', 'MESH'}, armature_nodetype='NULL')

# ---------------------------------------------------------------- beauty render
if RENDER:
    world = bpy.data.worlds.new('Night')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.004, 0.006, 0.012, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0

    # wet asphalt floor
    bpy.ops.mesh.primitive_plane_add(size=80)
    floor = bpy.context.active_object
    fm = mat('Wet_Asphalt', srgb('#0b0c10'), 0.3, 0.12, coat=1.0)
    floor.data.materials.append(fm)

    def light(name, kind, loc, energy, color, size=1.0, rot=None):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if kind == 'AREA':
            ld.size = size
        if kind == 'SPOT':
            ld.spot_size = math.radians(55)
            ld.spot_blend = 0.6
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        if rot:
            lo.rotation_euler = rot
        else:
            d = Vector((0, 0, 4)) - Vector(loc)
            lo.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        return lo
    light('Key', 'AREA', (-7, -9, 9), 2500, (1.0, 0.85, 0.7), 4)
    light('RimCyan', 'AREA', (8, 7, 6), 3500, (0.2, 0.75, 1.0), 3)
    light('RimMagenta', 'AREA', (-8, 8, 4), 900, (1.0, 0.25, 0.6), 3)
    light('Fill', 'AREA', (6, -10, 5), 900, (0.8, 0.85, 1.0), 6)
    light('Under', 'POINT', (0, -4, 0.6), 300, (1.0, 0.6, 0.25))

    cam_d = bpy.data.cameras.new('Cam')
    cam_d.lens = 50
    cam = bpy.data.objects.new('Cam', cam_d)
    scene.collection.objects.link(cam)
    scene.camera = cam

    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
    ee = scene.eevee
    for attr, val in (('use_raytracing', True), ('use_shadows', True), ('taa_render_samples', 64)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 1280
    scene.view_settings.view_transform = 'AgX'
    try:
        scene.view_settings.look = 'AgX - Punchy'
    except TypeError:
        pass

    shots = {'front34': ((-9.5, -13.5, 5.2), (0, 0, 4.1)), 'back34': ((10.5, 12, 6.0), (0, 0, 4.4)), 'low': ((-3.5, -9.5, 1.4), (0, 0, 4.8)), 'side': ((-16, 0, 4.2), (0, 0, 4.0))}
    for nm, (pos, tgt) in shots.items():
        cam.location = pos
        cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(OUT, f'stray_{nm}.png')
        bpy.ops.render.render(write_still=True)

print('BUILD_OK', OUT, len(PARTS), 'parts')
