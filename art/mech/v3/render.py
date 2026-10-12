"""Lookdev renders for the v3 mech (EEVEE, night studio lighting)."""
import bpy, math, os
from mathutils import Vector
from common import srgb


def setup(engine='EEVEE', res=1100):
    scene = bpy.context.scene
    world = bpy.data.worlds.new('Night')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.02, 0.024, 0.035, 1)
    bpy.ops.mesh.primitive_plane_add(size=60)
    floor = bpy.context.active_object
    fm = bpy.data.materials.new('Floor')
    fm.use_nodes = True
    b = fm.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*srgb('#0c0d10'), 1)
    b.inputs['Roughness'].default_value = 0.18
    b.inputs['Metallic'].default_value = 0.2
    floor.data.materials.append(fm)

    def L(name, loc, energy, color, size, target=(0, 0, 4.2)):
        ld = bpy.data.lights.new(name, 'AREA')
        ld.energy = energy
        ld.color = color
        ld.size = size
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    L('Key', (-6, -9, 10), 3000, (1.0, 0.88, 0.75), 5)
    L('Rim', (7, 7, 7), 2600, (0.55, 0.8, 1.0), 3)
    L('Rim2', (-8, 6, 3), 900, (0.9, 0.9, 1.0), 3)
    L('Fill', (7, -9, 3), 700, (0.8, 0.85, 1.0), 6)
    L('Top', (0, 0, 14), 800, (1, 1, 1), 8)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.lens = 55
    scene.collection.objects.link(cam)
    scene.camera = cam
    if engine == 'CYCLES':
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 64
        scene.cycles.use_denoising = True
    else:
        try:
            scene.render.engine = 'BLENDER_EEVEE'
        except TypeError:
            scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = scene.render.resolution_y = res
    scene.view_settings.view_transform = 'AgX'
    try:
        scene.view_settings.look = 'AgX - Punchy'
    except TypeError:
        pass
    return cam


SHOTS = {
    '34': ((-8.5, -12.0, 5.6), (0, 0, 4.2)),
    'back': ((9.5, 10.5, 6.2), (0, 0.3, 4.4)),
    'close': ((-3.2, -5.5, 6.8), (0.4, 0, 6.0)),
    'legs': ((-4.0, -6.0, 1.8), (0.4, 0, 1.8)),
}


def shoot(cam, out_dir, prefix, shots=('34', 'back', 'close')):
    scene = bpy.context.scene
    for s in shots:
        pos, tgt = SHOTS[s]
        cam.location = pos
        cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, f'{prefix}_{s}.png')
        bpy.ops.render.render(write_still=True)


def show_only(part_ids):
    for o in bpy.data.objects:
        if o.name.startswith('P_'):
            pid = o.name[2:].split('__')[0]
            o.hide_render = pid not in part_ids
