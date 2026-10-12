"""Texture baking for v3 mech parts (Cycles).

Per part: shared UV atlas -> procedural weathering shader -> baked maps:
  <pid>_albedo.png  sRGB   painted areas are neutral (tinted in-engine), with grime/wear
  <pid>_mask.png    linear R = primary paint, G = accent paint, B = edge wear, A = 1
  <pid>_mra.png     linear R = metallic, G = roughness, B = ambient occlusion
  <pid>_normal.png  linear tangent-space normal (micro surface: orange peel, scratches, dents)
Then each part gets a preview material MB_<pid> using those maps (emissive / glass faces
keep their own materials).
"""
import bpy, os, math
from toolkit import MCLASS, MC
from common import srgb

# class -> (base color, metallic, roughness, primary, accent)
LOOK = {
    'paint':  ('#c4c7cc', 0.25, 0.36, 1, 0),
    'accent': ('#d4d6da', 0.2, 0.34, 0, 1),
    'frame':  ('#2a2c31', 0.85, 0.46, 0, 0),
    'joint':  ('#141519', 0.55, 0.58, 0, 0),
    'steel':  ('#a7acb4', 1.0, 0.24, 0, 0),
    'decal':  ('#e6e7ea', 0.0, 0.48, 0, 0),
    'glow':   ('#202020', 0.0, 0.5, 0, 0),
    'thrust': ('#202020', 0.0, 0.5, 0, 0),
    'glass':  ('#050608', 0.3, 0.05, 0, 0),
    'rubber': ('#0d0d0e', 0.0, 0.86, 0, 0),
}
EMISSIVE = {'glow', 'thrust', 'glass'}
BIG_PARTS = ('cr_', 'lg_', 'ar_', 'frame')

DEFAULT_PAINT = srgb('#5d6674')
DEFAULT_ACCENT = srgb('#d98a1c')


def setup_cycles():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        for dev_type in ('OPTIX', 'CUDA'):
            try:
                prefs.compute_device_type = dev_type
                prefs.get_devices()
                if any(d.type == dev_type for d in prefs.devices):
                    break
            except TypeError:
                continue
        for d in prefs.devices:
            d.use = True
        scene.cycles.device = 'GPU'
    except Exception as e:
        print('GPU setup failed, using CPU', e)
    scene.cycles.samples = 32
    scene.render.bake.margin = 6
    scene.render.bake.use_clear = True


def _n(nodes, t, loc, **kw):
    n = nodes.new(t)
    n.location = loc
    for k, v in kw.items():
        setattr(n, k, v)
    return n


def _mix(nodes, links, dtype, fac, a, b, loc, blend='MIX'):
    """ShaderNodeMix with explicit socket indices (names are ambiguous across data types).
    fac/a/b may be sockets or constants."""
    n = nodes.new('ShaderNodeMix')
    n.data_type = dtype
    n.blend_type = blend
    n.location = loc
    ia, ib, io = (2, 3, 0) if dtype == 'FLOAT' else (6, 7, 2)
    for idx, v in ((0, fac), (ia, a), (ib, b)):
        if hasattr(v, 'is_linked') or hasattr(v, 'links'):
            links.new(v, n.inputs[idx])
        else:
            n.inputs[idx].default_value = v
    return n.outputs[io]


def build_bake_material():
    """One material that evaluates every class + weathering, with switchable outputs."""
    m = bpy.data.materials.new('BAKE')
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = _n(nodes, 'ShaderNodeOutputMaterial', (1600, 0))
    emit = _n(nodes, 'ShaderNodeEmission', (1400, 0))
    links.new(emit.outputs[0], out.inputs['Surface'])

    attr = _n(nodes, 'ShaderNodeAttribute', (-1400, 0), attribute_name='mcls')
    cls = attr.outputs['Fac']

    def weight(k, y):
        sub = _n(nodes, 'ShaderNodeMath', (-1200, y), operation='SUBTRACT')
        links.new(cls, sub.inputs[0]); sub.inputs[1].default_value = k
        ab = _n(nodes, 'ShaderNodeMath', (-1050, y), operation='ABSOLUTE')
        links.new(sub.outputs[0], ab.inputs[0])
        lt = _n(nodes, 'ShaderNodeMath', (-900, y), operation='LESS_THAN')
        links.new(ab.outputs[0], lt.inputs[0]); lt.inputs[1].default_value = 0.5
        return lt.outputs[0]

    def accumulate(values, y0, is_color):
        """Sum_k w_k * value_k."""
        acc = None
        for i, cname in enumerate(MCLASS):
            w = weight(MC[cname], y0 - i * 60)
            if is_color:
                rgb = _n(nodes, 'ShaderNodeRGB', (-900, y0 - i * 60 - 30))
                rgb.outputs[0].default_value = (*values[cname], 1)
                mul = _n(nodes, 'ShaderNodeVectorMath', (-700, y0 - i * 60), operation='SCALE')
                links.new(rgb.outputs[0], mul.inputs[0]); links.new(w, mul.inputs['Scale'])
                if acc is None:
                    acc = mul.outputs[0]
                else:
                    add = _n(nodes, 'ShaderNodeVectorMath', (-550, y0 - i * 60), operation='ADD')
                    links.new(acc, add.inputs[0]); links.new(mul.outputs[0], add.inputs[1])
                    acc = add.outputs[0]
            else:
                mul = _n(nodes, 'ShaderNodeMath', (-700, y0 - i * 60), operation='MULTIPLY')
                links.new(w, mul.inputs[0]); mul.inputs[1].default_value = values[cname]
                if acc is None:
                    acc = mul.outputs[0]
                else:
                    add = _n(nodes, 'ShaderNodeMath', (-550, y0 - i * 60), operation='ADD')
                    links.new(acc, add.inputs[0]); links.new(mul.outputs[0], add.inputs[1])
                    acc = add.outputs[0]
        return acc

    base = accumulate({k: srgb(v[0]) for k, v in LOOK.items()}, 2000, True)
    metal = accumulate({k: v[1] for k, v in LOOK.items()}, 1200, False)
    rough = accumulate({k: v[2] for k, v in LOOK.items()}, 400, False)
    prim = accumulate({k: v[3] for k, v in LOOK.items()}, -400, False)
    acc_ = accumulate({k: v[4] for k, v in LOOK.items()}, -1200, False)
    painted = _n(nodes, 'ShaderNodeMath', (-300, -800), operation='ADD')
    links.new(prim, painted.inputs[0]); links.new(acc_, painted.inputs[1])

    # ---- geometric masks
    geo = _n(nodes, 'ShaderNodeNewGeometry', (-1400, -2000))
    bevel = _n(nodes, 'ShaderNodeBevel', (-1200, -2100), samples=12)
    bevel.inputs['Radius'].default_value = 0.025
    dot = _n(nodes, 'ShaderNodeVectorMath', (-1000, -2000), operation='DOT_PRODUCT')
    links.new(bevel.outputs[0], dot.inputs[0]); links.new(geo.outputs['Normal'], dot.inputs[1])
    edge = _n(nodes, 'ShaderNodeMapRange', (-800, -2000))
    links.new(dot.outputs['Value'], edge.inputs['Value'])
    edge.inputs['From Min'].default_value = 0.985
    edge.inputs['From Max'].default_value = 0.88
    ao = _n(nodes, 'ShaderNodeAmbientOcclusion', (-1200, -2400), samples=24)
    ao.inputs['Distance'].default_value = 0.35

    tc = _n(nodes, 'ShaderNodeTexCoord', (-1600, -2700))
    # breakup noise for wear
    noise = _n(nodes, 'ShaderNodeTexNoise', (-1200, -2700))
    noise.inputs['Scale'].default_value = 9.0
    noise.inputs['Detail'].default_value = 8.0
    noise.inputs['Roughness'].default_value = 0.65
    links.new(tc.outputs['Object'], noise.inputs['Vector'])
    wearm = _n(nodes, 'ShaderNodeMath', (-800, -2600), operation='MULTIPLY')
    links.new(edge.outputs[0], wearm.inputs[0]); links.new(noise.outputs['Fac'], wearm.inputs[1])
    wear = _n(nodes, 'ShaderNodeMapRange', (-600, -2600))
    links.new(wearm.outputs[0], wear.inputs['Value'])
    wear.inputs['From Min'].default_value = 0.32
    wear.inputs['From Max'].default_value = 0.42
    # scratches: stretched noise on paint
    scr_map = _n(nodes, 'ShaderNodeMapping', (-1400, -3000))
    scr_map.inputs['Scale'].default_value = (1.0, 1.0, 22.0)
    scr_map.inputs['Rotation'].default_value = (0.3, 0.6, 0.1)
    links.new(tc.outputs['Object'], scr_map.inputs['Vector'])
    scr = _n(nodes, 'ShaderNodeTexNoise', (-1200, -3000))
    scr.inputs['Scale'].default_value = 4.0
    scr.inputs['Detail'].default_value = 2.0
    links.new(scr_map.outputs[0], scr.inputs['Vector'])
    scr_m = _n(nodes, 'ShaderNodeMapRange', (-1000, -3000))
    links.new(scr.outputs['Fac'], scr_m.inputs['Value'])
    scr_m.inputs['From Min'].default_value = 0.70
    scr_m.inputs['From Max'].default_value = 0.72
    scr_w = _n(nodes, 'ShaderNodeMath', (-800, -3000), operation='MULTIPLY')
    links.new(scr_m.outputs[0], scr_w.inputs[0]); scr_w.inputs[1].default_value = 0.45
    wear_tot = _n(nodes, 'ShaderNodeMath', (-450, -2700), operation='MAXIMUM')
    links.new(wear.outputs[0], wear_tot.inputs[0]); links.new(scr_w.outputs[0], wear_tot.inputs[1])
    wear_p = _n(nodes, 'ShaderNodeMath', (-300, -2700), operation='MULTIPLY')   # wear only where painted
    links.new(wear_tot.outputs[0], wear_p.inputs[0]); links.new(painted.outputs[0], wear_p.inputs[1])
    # grime: occlusion + low-on-the-body dirt + noise
    grime_noise = _n(nodes, 'ShaderNodeTexNoise', (-1200, -3300))
    grime_noise.inputs['Scale'].default_value = 3.0
    grime_noise.inputs['Detail'].default_value = 6.0
    links.new(tc.outputs['Object'], grime_noise.inputs['Vector'])
    inv_ao = _n(nodes, 'ShaderNodeMath', (-1000, -2400), operation='SUBTRACT')
    inv_ao.inputs[0].default_value = 1.0; links.new(ao.outputs['AO'], inv_ao.inputs[1])
    sep = _n(nodes, 'ShaderNodeSeparateXYZ', (-1200, -3550))
    links.new(geo.outputs['Position'], sep.inputs[0])
    low = _n(nodes, 'ShaderNodeMapRange', (-1000, -3550))
    links.new(sep.outputs['Z'], low.inputs['Value'])
    low.inputs['From Min'].default_value = 2.0
    low.inputs['From Max'].default_value = 0.0
    g1 = _n(nodes, 'ShaderNodeMath', (-800, -3300), operation='MULTIPLY_ADD')
    links.new(inv_ao.outputs[0], g1.inputs[0]); g1.inputs[1].default_value = 1.3; links.new(low.outputs[0], g1.inputs[2])
    grime = _n(nodes, 'ShaderNodeMath', (-600, -3300), operation='MULTIPLY')
    links.new(g1.outputs[0], grime.inputs[0]); links.new(grime_noise.outputs['Fac'], grime.inputs[1])
    grime_c = _n(nodes, 'ShaderNodeMath', (-450, -3300), operation='MINIMUM', use_clamp=True)
    links.new(grime.outputs[0], grime_c.inputs[0]); grime_c.inputs[1].default_value = 0.85

    # ---- panel lines: triplanar brick pattern -> thin grooves on painted armor
    sep_p = _n(nodes, 'ShaderNodeSeparateXYZ', (-1500, -4500))
    links.new(tc.outputs['Object'], sep_p.inputs[0])
    sep_n = _n(nodes, 'ShaderNodeSeparateXYZ', (-1500, -4800))
    links.new(geo.outputs['Normal'], sep_n.inputs[0])
    lines_acc = None
    weights = []
    for k, (a_, b_) in enumerate((('Y', 'Z'), ('X', 'Z'), ('X', 'Y'))):
        comb = _n(nodes, 'ShaderNodeCombineXYZ', (-1300, -4500 - k * 200))
        links.new(sep_p.outputs[a_], comb.inputs[0]); links.new(sep_p.outputs[b_], comb.inputs[1])
        br = _n(nodes, 'ShaderNodeTexBrick', (-1100, -4500 - k * 200), offset=0.3, squash=1.0)
        br.inputs['Scale'].default_value = 1.0
        br.inputs['Mortar Size'].default_value = 0.0035
        br.inputs['Mortar Smooth'].default_value = 0.0
        br.inputs['Brick Width'].default_value = 1.35 - k * 0.12
        br.inputs['Row Height'].default_value = 0.78 + k * 0.07
        links.new(comb.outputs[0], br.inputs['Vector'])
        ax = 'XYZ'[k]
        w_abs = _n(nodes, 'ShaderNodeMath', (-1100, -4800 - k * 60), operation='ABSOLUTE')
        links.new(sep_n.outputs[ax], w_abs.inputs[0])
        w_pow = _n(nodes, 'ShaderNodeMath', (-950, -4800 - k * 60), operation='POWER')
        links.new(w_abs.outputs[0], w_pow.inputs[0]); w_pow.inputs[1].default_value = 6.0
        weights.append(w_pow.outputs[0])
        term = _n(nodes, 'ShaderNodeMath', (-800, -4500 - k * 200), operation='MULTIPLY')
        links.new(br.outputs['Fac'], term.inputs[0]); links.new(w_pow.outputs[0], term.inputs[1])
        if lines_acc is None:
            lines_acc = term.outputs[0]
        else:
            add = _n(nodes, 'ShaderNodeMath', (-650, -4500 - k * 200), operation='ADD')
            links.new(lines_acc, add.inputs[0]); links.new(term.outputs[0], add.inputs[1])
            lines_acc = add.outputs[0]
    wsum = _n(nodes, 'ShaderNodeMath', (-800, -5000), operation='ADD')
    links.new(weights[0], wsum.inputs[0]); links.new(weights[1], wsum.inputs[1])
    wsum2 = _n(nodes, 'ShaderNodeMath', (-650, -5000), operation='ADD')
    links.new(wsum.outputs[0], wsum2.inputs[0]); links.new(weights[2], wsum2.inputs[1])
    lines_n = _n(nodes, 'ShaderNodeMath', (-500, -4700), operation='DIVIDE', use_clamp=True)
    links.new(lines_acc, lines_n.inputs[0]); links.new(wsum2.outputs[0], lines_n.inputs[1])
    # keep lines off bevelled edges and only on painted armor
    not_edge = _n(nodes, 'ShaderNodeMath', (-350, -4700), operation='SUBTRACT', use_clamp=True)
    not_edge.inputs[0].default_value = 1.0; links.new(edge.outputs[0], not_edge.inputs[1])
    pl1 = _n(nodes, 'ShaderNodeMath', (-200, -4700), operation='MULTIPLY')
    links.new(lines_n.outputs[0], pl1.inputs[0]); links.new(not_edge.outputs[0], pl1.inputs[1])
    panel_lines = _n(nodes, 'ShaderNodeMath', (-50, -4700), operation='MULTIPLY', use_clamp=True)
    links.new(pl1.outputs[0], panel_lines.inputs[0]); links.new(painted.outputs[0], panel_lines.inputs[1])

    # ---- final albedo: base, revealed metal where worn, darkened by grime
    worn_col = _n(nodes, 'ShaderNodeRGB', (-300, 1800))
    worn_col.outputs[0].default_value = (*srgb('#8e939b'), 1)
    mix_w = _mix(nodes, links, 'RGBA', wear_p.outputs[0], base, worn_col.outputs[0], (0, 1900))
    dirt_col = _n(nodes, 'ShaderNodeRGB', (-300, 1600))
    dirt_col.outputs[0].default_value = (*srgb('#1b1712'), 1)
    grime_f = _n(nodes, 'ShaderNodeMath', (-150, 1600), operation='MULTIPLY')
    links.new(grime_c.outputs[0], grime_f.inputs[0]); grime_f.inputs[1].default_value = 0.7
    mix_g = _mix(nodes, links, 'RGBA', grime_f.outputs[0], mix_w, dirt_col.outputs[0], (200, 1900), 'MULTIPLY')
    # edge highlight on painted edges (lighter paint where light catches)
    edge_l = _n(nodes, 'ShaderNodeMath', (200, 1650), operation='MULTIPLY')
    links.new(edge.outputs[0], edge_l.inputs[0]); edge_l.inputs[1].default_value = 0.12
    albedo0 = _mix(nodes, links, 'RGBA', edge_l.outputs[0], mix_g, (1, 1, 1, 1), (400, 1900), 'ADD')
    line_dark = _n(nodes, 'ShaderNodeMath', (400, 1700), operation='MULTIPLY')
    links.new(panel_lines.outputs[0], line_dark.inputs[0]); line_dark.inputs[1].default_value = 0.5
    albedo = _mix(nodes, links, 'RGBA', line_dark.outputs[0], albedo0, (0.03, 0.03, 0.035, 1), (600, 1900))

    # metallic / roughness with wear and grime
    metal_f = _mix(nodes, links, 'FLOAT', wear_p.outputs[0], metal, 1.0, (0, 1200))
    rough_w = _mix(nodes, links, 'FLOAT', wear_p.outputs[0], rough, 0.32, (0, 400))
    rough_g = _n(nodes, 'ShaderNodeMath', (200, 400), operation='MULTIPLY_ADD', use_clamp=True)
    links.new(grime_c.outputs[0], rough_g.inputs[0]); rough_g.inputs[1].default_value = 0.35; links.new(rough_w, rough_g.inputs[2])
    rough_n = _n(nodes, 'ShaderNodeMath', (350, 400), operation='MULTIPLY_ADD', use_clamp=True)
    links.new(noise.outputs['Fac'], rough_n.inputs[0]); rough_n.inputs[1].default_value = 0.12; links.new(rough_g.outputs[0], rough_n.inputs[2])

    # paint masks minus wear
    inv_w = _n(nodes, 'ShaderNodeMath', (0, -400), operation='SUBTRACT', use_clamp=True)
    inv_w.inputs[0].default_value = 1.0; links.new(wear_p.outputs[0], inv_w.inputs[1])
    prim_f = _n(nodes, 'ShaderNodeMath', (200, -400), operation='MULTIPLY')
    links.new(prim, prim_f.inputs[0]); links.new(inv_w.outputs[0], prim_f.inputs[1])
    acc_f = _n(nodes, 'ShaderNodeMath', (200, -550), operation='MULTIPLY')
    links.new(acc_, acc_f.inputs[0]); links.new(inv_w.outputs[0], acc_f.inputs[1])

    comb_mask = _n(nodes, 'ShaderNodeCombineXYZ', (500, -450))
    links.new(prim_f.outputs[0], comb_mask.inputs[0]); links.new(acc_f.outputs[0], comb_mask.inputs[1]); links.new(wear_p.outputs[0], comb_mask.inputs[2])
    comb_mra = _n(nodes, 'ShaderNodeCombineXYZ', (500, 800))
    ao_l = _n(nodes, 'ShaderNodeMath', (350, 700), operation='MULTIPLY_ADD', use_clamp=True)
    links.new(panel_lines.outputs[0], ao_l.inputs[0]); ao_l.inputs[1].default_value = -0.6; links.new(ao.outputs['AO'], ao_l.inputs[2])
    links.new(metal_f, comb_mra.inputs[0]); links.new(rough_n.outputs[0], comb_mra.inputs[1]); links.new(ao_l.outputs[0], comb_mra.inputs[2])

    # micro normal: orange-peel noise + scratches + dents, fed to a BSDF for the NORMAL bake
    micro = _n(nodes, 'ShaderNodeTexNoise', (-1200, -3900))
    micro.inputs['Scale'].default_value = 120.0
    micro.inputs['Detail'].default_value = 2.0
    links.new(tc.outputs['Object'], micro.inputs['Vector'])
    dents = _n(nodes, 'ShaderNodeTexVoronoi', (-1200, -4150))
    dents.inputs['Scale'].default_value = 7.0
    links.new(tc.outputs['Object'], dents.inputs['Vector'])
    h1 = _n(nodes, 'ShaderNodeMath', (-900, -3950), operation='MULTIPLY_ADD')
    links.new(micro.outputs['Fac'], h1.inputs[0]); h1.inputs[1].default_value = 0.15; links.new(scr_m.outputs[0], h1.inputs[2])
    h2 = _n(nodes, 'ShaderNodeMath', (-700, -4000), operation='MULTIPLY_ADD')
    links.new(dents.outputs['Distance'], h2.inputs[0]); h2.inputs[1].default_value = 0.25; links.new(h1.outputs[0], h2.inputs[2])
    h3 = _n(nodes, 'ShaderNodeMath', (-600, -4100), operation='MULTIPLY_ADD')
    links.new(panel_lines.outputs[0], h3.inputs[0]); h3.inputs[1].default_value = -1.6; links.new(h2.outputs[0], h3.inputs[2])
    bump = _n(nodes, 'ShaderNodeBump', (-500, -4000))
    bump.inputs['Strength'].default_value = 0.18
    bump.inputs['Distance'].default_value = 0.004
    links.new(h3.outputs[0], bump.inputs['Height'])
    bsdf = _n(nodes, 'ShaderNodeBsdfPrincipled', (1200, -400))
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    img_node = _n(nodes, 'ShaderNodeTexImage', (1400, 400))
    m['outs'] = {}
    return m, {'albedo': albedo, 'mask': comb_mask.outputs[0], 'mra': comb_mra.outputs[0]}, emit, out, bsdf, img_node


def _new_image(name, size, non_color, alpha=False):
    img = bpy.data.images.get(name) or bpy.data.images.new(name, size, size, alpha=alpha, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    return img


def bake_part(pid, objs, out_dir, mat, outs, emit, out, bsdf, img_node):
    tex_dir = os.path.join(out_dir, 'textures')
    os.makedirs(tex_dir, exist_ok=True)
    size = 2048 if pid.startswith(BIG_PARTS) else 1024

    # class attribute as float for Cycles
    for o in objs:
        me = o.data
        src = me.attributes.get('mclass')
        vals = [0] * len(me.polygons)
        if src:
            src.data.foreach_get('value', vals)
        dst = me.attributes.get('mcls') or me.attributes.new('mcls', 'FLOAT', 'FACE')
        dst.data.foreach_set('value', [float(v) for v in vals])

    # shared UV atlas
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
        if not o.data.uv_layers:
            o.data.uv_layers.new(name='UVMap')
        uvl = o.data.uv_layers[0]
        o.data.uv_layers.active = uvl
        uvl.active_render = True
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
    try:
        bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
    except TypeError:
        bpy.ops.uv.pack_islands(margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')

    # temporarily give every face the bake material
    saved = {}
    for o in objs:
        saved[o.name] = ([s.material for s in o.material_slots], [p.material_index for p in o.data.polygons])
        o.data.materials.clear()
        o.data.materials.append(mat)
        for p in o.data.polygons:
            p.material_index = 0

    nt = mat.node_tree
    nt.nodes.active = img_node
    img_node.select = True
    scene = bpy.context.scene
    results = {}
    for key, non_color in (('albedo', False), ('mask', True), ('mra', True)):
        img = _new_image(f'{pid}_{key}', size, non_color)
        img_node.image = img
        nt.links.new(outs[key], emit.inputs['Color'])
        nt.links.new(emit.outputs[0], out.inputs['Surface'])
        scene.cycles.samples = 48 if key == 'mra' else 16
        bpy.ops.object.bake(type='EMIT', margin=6, use_clear=True)
        img.filepath_raw = os.path.join(tex_dir, f'{pid}_{key}.png')
        img.file_format = 'PNG'
        img.save()
        results[key] = img
    img = _new_image(f'{pid}_normal', size, True)
    img_node.image = img
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])
    scene.cycles.samples = 8
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', margin=6, use_clear=True)
    img.filepath_raw = os.path.join(tex_dir, f'{pid}_normal.png')
    img.file_format = 'PNG'
    img.save()
    results['normal'] = img

    # final materials: MB_<pid> for body faces, emissive classes keep their own
    mb = preview_material(pid, results)
    keep = {MC[c]: bpy.data.materials['MC_' + c] for c in EMISSIVE}
    for o in objs:
        mats, idxs = saved[o.name]
        o.data.materials.clear()
        o.data.materials.append(mb)
        slot_of = {0: 0}
        for cidx, m in keep.items():
            o.data.materials.append(m)
            slot_of[cidx] = len(o.data.materials) - 1
        cls = [0] * len(o.data.polygons)
        o.data.attributes['mclass'].data.foreach_get('value', cls)
        o.data.polygons.foreach_set('material_index', [slot_of.get(c, 0) for c in cls])
    return results


def preview_material(pid, maps):
    """Blender-side material equivalent of the in-game shader (default paint colors)."""
    m = bpy.data.materials.new('MB_' + pid)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    b = nodes['Principled BSDF']
    def tex(img, loc, non_color):
        n = nodes.new('ShaderNodeTexImage'); n.location = loc; n.image = img
        return n
    ta, tm, tr, tn = tex(maps['albedo'], (-900, 300), False), tex(maps['mask'], (-900, 0), True), tex(maps['mra'], (-900, -300), True), tex(maps['normal'], (-900, -600), True)
    sm = nodes.new('ShaderNodeSeparateColor'); sm.location = (-600, 0); links.new(tm.outputs[0], sm.inputs[0])
    sr = nodes.new('ShaderNodeSeparateColor'); sr.location = (-600, -300); links.new(tr.outputs[0], sr.inputs[0])
    p1 = _mix(nodes, links, 'RGBA', sm.outputs[0], ta.outputs[0], (*[min(1.0, c * 1.8) for c in DEFAULT_PAINT], 1), (-400, 300), 'MULTIPLY')
    p2 = _mix(nodes, links, 'RGBA', sm.outputs[1], p1, (*[min(1.0, c * 1.7) for c in DEFAULT_ACCENT], 1), (-200, 300), 'MULTIPLY')
    aom = _mix(nodes, links, 'RGBA', 1.0, p2, sr.outputs[2], (0, 300), 'MULTIPLY')
    links.new(aom, b.inputs['Base Color'])
    links.new(sr.outputs[0], b.inputs['Metallic'])
    links.new(sr.outputs[1], b.inputs['Roughness'])
    nm = nodes.new('ShaderNodeNormalMap'); nm.location = (-300, -600)
    links.new(tn.outputs[0], nm.inputs['Color']); links.new(nm.outputs[0], b.inputs['Normal'])
    return m


def bake_all(out_dir, only=None):
    setup_cycles()
    mat, outs, emit, out, bsdf, img_node = build_bake_material()
    parts = {}
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.startswith('P_'):
            parts.setdefault(o.name[2:].split('__')[0], []).append(o)
    for pid, objs in sorted(parts.items()):
        if only and pid not in only:
            continue
        print('BAKING', pid, len(objs), flush=True)
        bake_part(pid, objs, out_dir, mat, outs, emit, out, bsdf, img_node)
    # restore an engine suitable for lookdev
    return parts
