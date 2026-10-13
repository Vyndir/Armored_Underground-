"""Export the default-loadout v3 mech to glTF binary for the Godot build.

    blender --background art/mech/out/v3/stray_v3.blend --python art/mech/v3/export_glb.py -- <out.glb> [part ids...]

Keeps the shared frame plus one part per slot (the default loadout unless part ids
are given), applies modifiers, and writes a skinned GLB. Material names (MB_<part>,
MC_<class>) are kept so the game can assign its own mech surface shader.
"""
import bpy
import sys

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = argv[0] if argv else 'kestrel.glb'
keep = set(argv[1:]) or {'hd_pike', 'cr_ferro', 'ar_hand', 'lg_strider', 'bs_flare', 'wp_hornet', 'sh_swarm'}
keep.add('frame')

removed = 0
for ob in list(bpy.data.objects):
    n = ob.name
    if n.startswith('P_') and '__' in n:
        pid = n[2:n.index('__')]
        if pid not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
            removed += 1
    elif ob.type in ('CAMERA', 'LIGHT'):
        bpy.data.objects.remove(ob, do_unlink=True)
print(f'[export] removed {removed} unused part objects, kept {sorted(keep)}')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(
    filepath=out,
    export_format='GLB',
    use_selection=False,
    export_apply=True,
    export_skins=True,
    export_animations=False,
    export_materials='EXPORT',
    export_image_format='NONE',
    export_yup=True,
)
print('[export] wrote', out)
