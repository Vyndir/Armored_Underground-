"""Shared setup for the v3 mech: scene reset, class materials, skeleton, limb frames."""
import bpy, math
from mathutils import Vector

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


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


# Preview look per class (the bake pass replaces these with textures)
CLASS_LOOK = {
    'paint':  ('#3d4553', 0.55, 0.35, None),
    'accent': ('#d98a1c', 0.4, 0.32, None),
    'frame':  ('#202227', 0.85, 0.42, None),
    'joint':  ('#121316', 0.6, 0.55, None),
    'steel':  ('#9aa0aa', 1.0, 0.22, None),
    'decal':  ('#d8dade', 0.0, 0.5, None),
    'glow':   ('#00d8ff', 0.0, 0.3, '#00d8ff'),
    'thrust': ('#ff9a3c', 0.0, 0.3, '#ffb066'),
    'glass':  ('#05080c', 0.3, 0.05, None),
    'rubber': ('#0b0b0c', 0.0, 0.8, None),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for cname, (col, metal, rough, emit) in CLASS_LOOK.items():
        m = bpy.data.materials.new('MC_' + cname)
        m.use_nodes = True
        b = m.node_tree.nodes['Principled BSDF']
        b.inputs['Base Color'].default_value = (*srgb(col), 1)
        b.inputs['Metallic'].default_value = metal
        b.inputs['Roughness'].default_value = rough
        if emit:
            b.inputs['Emission Color'].default_value = (*srgb(emit), 1)
            b.inputs['Emission Strength'].default_value = 10
    return build_rig()


def build_rig():
    scene = bpy.context.scene
    arm = bpy.data.armatures.new('STRAY_Skeleton')
    rig = bpy.data.objects.new('STRAY_Rig', arm)
    scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for n, (h, t, p) in JOINTS.items():
        b = arm.edit_bones.new(n)
        b.head, b.tail = h, t
    for n, (h, t, p) in JOINTS.items():
        if p:
            arm.edit_bones[n].parent = arm.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


class Limb:
    """Frame along a bone: p(t, side, front) with side = +X (outward on the left), front = toward -Y."""
    def __init__(self, bone):
        h, t, _ = JOINTS[bone]
        self.a, self.b = Vector(h), Vector(t)
        self.d = (self.b - self.a).normalized()
        self.s = Vector((1, 0, 0))
        f = self.d.cross(self.s).normalized()     # perpendicular to limb in the YZ plane
        if f.y > 0:
            f = -f
        self.f = f
        self.u = -self.d
        self.len = (self.b - self.a).length

    def p(self, t, side=0.0, front=0.0):
        return self.a.lerp(self.b, t) + self.s * side + self.f * front
