"""v3 mech build: model all parts, optionally bake textures, render and export.

blender --background --factory-startup --python art/mech/v3/build.py -- <out> [--render] [--bake] [--export]
"""
import bpy, os, sys
sys.path.insert(0, os.path.dirname(__file__))
for m in ('toolkit', 'common', 'parts_default', 'parts_extra', 'render', 'bake'):
    sys.modules.pop(m, None)
import common, render
from parts_default import DEFAULT
try:
    from parts_extra import EXTRA
except ImportError:
    EXTRA = []

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.path.dirname(__file__), '..', 'out', 'v3'))
os.makedirs(OUT, exist_ok=True)

rig = common.reset()
for fn in DEFAULT + EXTRA:
    fn(rig)

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
print('TRIS_TOTAL', tris)

if '--bake' in argv:
    import bake
    bake.bake_all(OUT)

if '--export' in argv:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'stray_v3.fbx'), use_selection=True, apply_unit_scale=True,
                             use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                             object_types={'ARMATURE', 'MESH'}, armature_nodetype='NULL', mesh_smooth_type='FACE',
                             use_tspace=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'stray_v3.blend'))

if '--render' in argv:
    cam = render.setup('CYCLES' if '--cycles' in argv else 'EEVEE')
    loadouts = {'standard': ['frame', 'hd_pike', 'cr_ferro', 'ar_hand', 'lg_strider', 'bs_flare', 'wp_hornet', 'sh_swarm']}
    if EXTRA:
        loadouts.update({
            'racer': ['frame', 'hd_moth', 'cr_wasp', 'ar_needle', 'lg_glide', 'bs_comet', 'wp_lance', 'sh_spear'],
            'heavy': ['frame', 'hd_bastion', 'cr_anvil', 'ar_hauler', 'lg_bulwark', 'bs_tern', 'wp_maul', 'sh_mines'],
            'hopper': ['frame', 'hd_pike', 'cr_wasp', 'ar_hand', 'lg_hopper', 'bs_flare', 'wp_gale', 'sh_swarm'],
        })
    only = [a.split('=')[1] for a in argv if a.startswith('--loadout=')]
    for name, ids in loadouts.items():
        if only and name not in only:
            continue
        render.show_only(ids)
        render.shoot(cam, OUT, name, ('34', 'back', 'close', 'legs') if name == 'standard' else ('34', 'back'))
print('BUILD_OK')
