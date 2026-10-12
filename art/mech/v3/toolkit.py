"""Hard-surface modelling toolkit for the v3 mech (Blender, bmesh based).

Every generated object records:
  - the bone it is rigidly skinned to
  - a per-face "mclass" attribute (paint / accent / frame / joint / steel / decal /
    glow / thrust / glass) used later for texture baking and material assignment.

Coordinates: mech faces -Y, Z up, meters.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler, Quaternion

MCLASS = ['paint', 'accent', 'frame', 'joint', 'steel', 'decal', 'glow', 'thrust', 'glass', 'rubber']
MC = {n: i for i, n in enumerate(MCLASS)}

STATE = {'part': None, 'objs': [], 'cutters': []}   # objs: list of (obj, bone, marker)
RNG = random.Random(7)
import math as _m


def _bm():
    bm = bmesh.new()
    bm.faces.layers.int.new('mclass')
    return bm


def begin(part_id, seed=None):
    STATE['part'] = part_id
    STATE['objs'] = []
    STATE['cutters'] = []
    RNG.seed(seed if seed is not None else hash(part_id) & 0xffff)


def _link(name, bm, mclass, bone, bevel=0.0, bevel_segs=2, angle=32, marker=None, smooth=False, harden=True, per_face_class=None):
    """bm -> object. per_face_class: optional dict face.index -> class name (overrides mclass)."""
    lay = bm.faces.layers.int.get('mclass') or bm.faces.layers.int.new('mclass')
    for f in bm.faces:
        if f[lay] == 0 and mclass != 'paint':
            f[lay] = MC[mclass]
        f.smooth = smooth
    if per_face_class:
        for f in bm.faces:
            if f.index in per_face_class:
                f[lay] = MC[per_face_class[f.index]]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    if bevel > 0:
        md = obj.modifiers.new('Bevel', 'BEVEL')
        md.width = bevel
        md.segments = bevel_segs
        md.limit_method = 'ANGLE'
        md.angle_limit = math.radians(angle)
        md.harden_normals = harden
        md.miter_outer = 'MITER_ARC'
    if harden:
        wn = obj.modifiers.new('WN', 'WEIGHTED_NORMAL')
        wn.keep_sharp = True
    STATE['objs'].append((obj, bone, marker))
    if AUTO_DETAIL['on'] and mclass == 'paint' and bevel > 0:
        _auto_detail(obj, bone)
    return obj


AUTO_DETAIL = {'on': True, 'chance': 0.28, 'max': 3}
BOOL_SOLVER = ['MANIFOLD']


def _auto_detail(obj, bone):
    """Sprinkle hard-surface detail on big armour faces: light channels, slot rows, recessed hatches."""
    me = obj.data
    faces = [p for p in me.polygons if p.area > 0.07]
    RNG.shuffle(faces)
    done = 0
    for p in faces:
        if done >= AUTO_DETAIL['max'] or RNG.random() > AUTO_DETAIL['chance']:
            continue
        # face frame: longest edge direction as 'up'
        vs = [me.vertices[i].co for i in p.vertices]
        best, up = 0, Vector((0, 0, 1))
        for a, b in zip(vs, vs[1:] + vs[:1]):
            if (b - a).length > best:
                best, up = (b - a).length, (b - a).normalized()
        span = min(best, math.sqrt(p.area))
        n = p.normal.copy()
        c = p.center.copy()
        kind = RNG.random()
        if kind < 0.1:
            recess(obj, c + up * span * RNG.uniform(-0.15, 0.15), tuple(n), 0.05, span * 0.55, 0.028, up=tuple(up), light=True, bone=bone)
        elif kind < 0.3:
            recess(obj, c + up * span * RNG.uniform(-0.15, 0.15), tuple(n), 0.05, span * 0.55, 0.028, up=tuple(up))
        elif kind < 0.7:
            side = n.cross(up).normalized()
            k = RNG.randint(3, 5)
            a = c - side * span * 0.25 + up * span * 0.25
            slots(obj, a, a + side * span * 0.4, k, tuple(n), 0.035, span * 0.18, 0.025, up=tuple(up))
        else:
            recess(obj, c, tuple(n), span * 0.32, span * 0.22, 0.022, up=tuple(up))
        done += 1


def _set_class(bm, faces, mclass):
    lay = bm.faces.layers.int.get('mclass') or bm.faces.layers.int.new('mclass')
    for f in faces:
        f[lay] = MC[mclass]


# ------------------------------------------------------------------ 2D profiles
def rect(w, h):
    return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]


def chamfer(w, h, c, c2=None):
    """Rectangle with chamfered corners (c: bottom corners, c2: top corners)."""
    c2 = c if c2 is None else c2
    x, y = w / 2, h / 2
    return [(-x + c, -y), (x - c, -y), (x, -y + c), (x, y - c2), (x - c2, y), (-x + c2, y), (-x, y - c2), (-x, -y + c)]


def ridge(w, front, back=0.12, ridge_w=0.35, shoulder=0.62, edge=0.12):
    """Armor cross-section: flat back, faceted front rising to a central ridge.
    u = side, v = outward (front). Gives shells a sculpted, light-catching crest."""
    hw = w / 2
    return [(-hw * 0.82, -back), (hw * 0.82, -back), (hw, edge * front), (hw * 0.82, shoulder * front),
            (hw * ridge_w, front * 0.97), (0, front), (-hw * ridge_w, front * 0.97), (-hw * 0.82, shoulder * front), (-hw, edge * front)]


def poly(*pts):
    return list(pts)


def circle(r, n=16, sx=1.0, sy=1.0):
    return [(math.cos(a) * r * sx, math.sin(a) * r * sy) for a in (i / n * math.pi * 2 for i in range(n))]


# ------------------------------------------------------------------ frames
def frame_from(normal, up=Vector((0, 0, 1))):
    """Orthonormal (x, y, n) frame for a section plane with given normal."""
    n = Vector(normal).normalized()
    up = Vector(up)
    if abs(n.dot(up)) > 0.95:
        up = Vector((0, 1, 0)) if abs(n.dot(Vector((0, 1, 0)))) < 0.95 else Vector((1, 0, 0))
    x = up.cross(n).normalized()
    y = n.cross(x).normalized()
    return x, y, n


# ------------------------------------------------------------------ primitives
def prism(name, profile, depth, loc, axis='-Y', mclass='paint', bone=None, bevel=0.035, taper=1.0, back_offset=(0, 0),
          rot=(0, 0, 0), panel=0.0, panel_depth=0.02, up=(0, 0, 1), marker=None, front_class=None, bevel_segs=2, smooth=False):
    """Extrude a 2D profile into a slab. The profile lies in a plane facing `axis`;
    the front face is at loc + axis*depth/2, the back at loc - axis*depth/2 scaled by
    `taper` and shifted by back_offset (gives armor its wedge thickness).
    panel>0 insets the front face and raises it (armor panel with seam)."""
    axes = {'-Y': (0, -1, 0), '+Y': (0, 1, 0), '+X': (1, 0, 0), '-X': (-1, 0, 0), '+Z': (0, 0, 1), '-Z': (0, 0, -1)}
    n = Vector(axes[axis]) if isinstance(axis, str) else Vector(axis).normalized()
    x, y, n = frame_from(n, Vector(up))
    R = Euler(rot).to_matrix()
    x, y, n = R @ x, R @ y, R @ n
    bm = _bm()
    c = Vector(loc)
    front = [bm.verts.new(c + n * depth / 2 + x * u + y * v) for u, v in profile]
    back = [bm.verts.new(c - n * depth / 2 + x * (u * taper + back_offset[0]) + y * (v * taper + back_offset[1])) for u, v in profile]
    ff = bm.faces.new(front)
    bf = bm.faces.new(list(reversed(back)))
    k = len(profile)
    sides = []
    for i in range(k):
        j = (i + 1) % k
        sides.append(bm.faces.new([front[j], front[i], back[i], back[j]]))
    bm.normal_update()
    # make sure normals point outward
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if front_class:
        _set_class(bm, [ff], front_class)
    if panel > 0:
        res = bmesh.ops.inset_individual(bm, faces=[ff], thickness=panel, depth=0)
        nrm = ff.normal.copy()
        for v in ff.verts:
            v.co += nrm * panel_depth
        _set_class(bm, [f for f in res['faces'] if f.is_valid and f != ff], 'joint')
    return _link(name, bm, mclass, bone, bevel, bevel_segs, marker=marker, smooth=smooth)


def loft(name, sections, mclass='frame', bone=None, cap=True, bevel=0.02, smooth=False, marker=None, cap_class=None):
    """Bridge a list of sections. Each section: (center, normal, profile2d[, up]).
    All profiles need the same number of points."""
    bm = _bm()
    rings = []
    for sec in sections:
        c, nrm, prof = Vector(sec[0]), Vector(sec[1]), sec[2]
        up = Vector(sec[3]) if len(sec) > 3 else Vector((0, 0, 1))
        x, y, n = frame_from(nrm, up)
        rings.append([bm.verts.new(c + x * u + y * v) for u, v in prof])
    k = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for i in range(k):
            j = (i + 1) % k
            bm.faces.new([a[i], a[j], b[j], b[i]])
    caps = []
    if cap:
        caps.append(bm.faces.new(list(reversed(rings[0]))))
        caps.append(bm.faces.new(rings[-1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if cap_class:
        _set_class(bm, caps, cap_class)
    return _link(name, bm, mclass, bone, bevel, 2, smooth=smooth, marker=marker)


def cyl(name, a, b, r, mclass='joint', bone=None, n=16, r2=None, bevel=0.012, marker=None, smooth=True, cap_class=None):
    a, b = Vector(a), Vector(b)
    d = (b - a)
    prof = circle(1.0, n)
    r2 = r if r2 is None else r2
    return loft(name, [(a, d, [(u * r, v * r) for u, v in prof]), (b, d, [(u * r2, v * r2) for u, v in prof])],
                mclass, bone, True, bevel, smooth, marker, cap_class)


def ring_stack(name, center, axis, radii, widths, mclass='joint', bone=None, n=20):
    """Stacked discs along an axis: joint caps / actuator housings."""
    c, ax = Vector(center), Vector(axis).normalized()
    total = sum(widths)
    p = c - ax * total / 2
    objs = []
    classes = [mclass, 'frame', 'steel']
    for i, (r, w) in enumerate(zip(radii, widths)):
        objs.append(cyl(f'{name}_{i}', p, p + ax * w, r, classes[i % len(classes)] if i else mclass, bone, n))
        p = p + ax * w
    return objs


def piston(name, a, b, r, bone, rings=2):
    a, b = Vector(a), Vector(b)
    m = a.lerp(b, 0.52)
    cyl(name + '_Sleeve', a, m, r, 'frame', bone, 14)
    for i in range(rings):
        t = 0.15 + 0.6 * i / max(1, rings - 1)
        p = a.lerp(m, t)
        cyl(f'{name}_Ring{i}', p - (m - a).normalized() * 0.03, p + (m - a).normalized() * 0.03, r * 1.18, 'steel', bone, 14)
    cyl(name + '_Rod', m - (m - a) * 0.15, b, r * 0.55, 'steel', bone, 12)
    d = (b - a).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 0.1:
        side = d.cross(Vector((1, 0, 0)))
    side.normalize()
    for p, nm in ((a, 'A'), (b, 'B')):
        cyl(f'{name}_Eye{nm}', p - side * r * 1.1, p + side * r * 1.1, r * 1.2, 'joint', bone, 12)


def cable(name, points, r, bone, mclass='rubber', res=8):
    """Bezier-ish cable through points (curve with bevel, converted to mesh)."""
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = r
    cu.bevel_resolution = 2
    cu.resolution_u = res
    sp = cu.splines.new('BEZIER')
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = Vector(p)
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    tmp = bpy.data.objects.new(name + '_tmp', cu)
    bpy.context.scene.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    return _link(name, bm, mclass, bone, 0, smooth=True, harden=False)


def bolts(name, start, end, n, bone, r=0.03, normal=(0, -1, 0)):
    s, e = Vector(start), Vector(end)
    nrm = Vector(normal).normalized()
    for i in range(n):
        p = s.lerp(e, i / max(1, n - 1))
        cyl(f'{name}{i}', p - nrm * 0.01, p + nrm * 0.025, r, 'steel', bone, 6, bevel=0, smooth=False)


def vents(name, center, w, h, n, bone, normal=(0, -1, 0), up=(0, 0, 1), depth=0.07, slat_class='frame'):
    """Recessed grille: housing + angled slats."""
    c = Vector(center)
    x, y, nn = frame_from(Vector(normal), Vector(up))
    prism(name + '_Housing', rect(w, h), depth, c, tuple(nn), 'joint', bone, bevel=0.01, up=tuple(up))
    for i in range(n):
        t = (i + 0.5) / n - 0.5
        p = c + y * (t * h * 0.9) + nn * (depth * 0.15)
        prism(f'{name}_Slat{i}', rect(w * 0.9, h / n * 0.42), 0.03, p, tuple((nn + y * 0.7).normalized()), slat_class, bone, bevel=0.004, up=tuple(up))


def light(name, center, w, h, bone, normal=(0, -1, 0), up=(0, 0, 1), depth=0.03, mclass='glow'):
    return prism(name, rect(w, h), depth, center, tuple(Vector(normal).normalized()), mclass, bone, bevel=0.004, up=up)


def text(name, body, center, size, bone, normal=(0, -1, 0), up=(0, 0, 1), mclass='decal', depth=0.006):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.size = size
    cu.extrude = depth
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    tmp = bpy.data.objects.new(name + '_tmp', cu)
    bpy.context.scene.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    x, y, n = frame_from(Vector(normal), Vector(up))
    M = Matrix((x, y, n)).transposed().to_4x4()
    M.translation = Vector(center)
    me.transform(M)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    o = _link(name, bm, mclass, bone, 0, harden=False)
    o['text_c'] = list(Vector(center))
    o['text_u'] = list(x)
    return o


def stripes(name, center, w, h, n, bone, normal=(0, -1, 0), up=(0, 0, 1), mclass='accent', slant=0.6):
    """Hazard stripes as thin raised parallelograms."""
    c = Vector(center)
    x, y, nn = frame_from(Vector(normal), Vector(up))
    sw = w / n
    for i in range(n):
        u0 = -w / 2 + i * sw
        prof = [(u0, -h / 2), (u0 + sw * 0.5, -h / 2), (u0 + sw * 0.5 + h * slant, h / 2), (u0 + h * slant, h / 2)]
        prism(f'{name}{i}', prof, 0.008, c, tuple(nn), mclass, bone, bevel=0, up=up)


def greebles(name, center, u_axis, v_axis, size_u, size_v, count, bone, normal, h=(0.03, 0.09), mclass='frame'):
    """Scatter small boxes on a surface patch."""
    c, U, V, N = Vector(center), Vector(u_axis).normalized(), Vector(v_axis).normalized(), Vector(normal).normalized()
    for i in range(count):
        p = c + U * (RNG.random() - 0.5) * size_u + V * (RNG.random() - 0.5) * size_v
        w, d = RNG.uniform(0.06, 0.22), RNG.uniform(0.05, 0.18)
        hh = RNG.uniform(*h)
        prism(f'{name}{i}', rect(w, d), hh, p + N * hh / 2, tuple(N), RNG.choice([mclass, 'frame', 'steel', 'joint']), bone, bevel=0.006, up=tuple(V))


# ------------------------------------------------------------------ boolean detailing
def _cutter(center, normal, up, w, h, depth, profile=None):
    """Hidden boolean cutter box/prism centred on a surface point (half above, half below)."""
    x, y, n = frame_from(Vector(normal), Vector(up))
    c = Vector(center)
    prof = profile or rect(w, h)
    bm = _bm()
    lay = bm.faces.layers.int.get('mclass')
    top = [bm.verts.new(c + n * depth + x * u + y * v) for u, v in prof]
    bot = [bm.verts.new(c - n * depth + x * u + y * v) for u, v in prof]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    k = len(prof)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new([top[j], top[i], bot[i], bot[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        f[lay] = MC['joint']
    me = bpy.data.meshes.new('Cutter')
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new('Cutter', me)
    bpy.context.scene.collection.objects.link(o)
    o.hide_render = True
    o.hide_viewport = True
    STATE['cutters'].append(o)
    return o


def _add_bool(target, cutter):
    md = target.modifiers.new('Cut', 'BOOLEAN')
    md.operation = 'DIFFERENCE'
    md.solver = BOOL_SOLVER[0]
    md.object = cutter
    # booleans must run before bevel / weighted normals
    for m in target.modifiers:
        if m.type == 'BEVEL':
            m.miter_outer = 'MITER_SHARP'
            m.use_clamp_overlap = True
    idx = len(target.modifiers) - 1
    first_non_bool = next((i for i, m in enumerate(target.modifiers) if m.type != 'BOOLEAN'), idx)
    target.modifiers.move(idx, first_non_bool)


def recess(target, center, normal, w, h, depth=0.035, up=(0, 0, 1), light=False, bone=None, profile=None, inset_class='joint'):
    """Cut a shallow pocket into an armour piece; optionally lay a glowing strip in it."""
    _add_bool(target, _cutter(center, normal, up, w, h, depth, profile))
    if light:
        n = Vector(normal).normalized()
        prism(target.name + '_RecessLight', rect(w * 0.8, h * 0.5), 0.02, Vector(center) - n * (depth * 0.7), tuple(n), 'glow', bone, bevel=0, up=up)


def slots(target, start, end, n, normal, w, h, depth=0.03, up=(0, 0, 1)):
    """A row of small vent/lightening slots cut into a surface."""
    s, e = Vector(start), Vector(end)
    for i in range(n):
        _add_bool(target, _cutter(s.lerp(e, i / max(1, n - 1)), normal, up, w, h, depth))


# ------------------------------------------------------------------ mirroring and finishing
def mirror_last(count, from_side='.L'):
    """Mirror the last `count` objects across X (bone .L -> .R)."""
    if count <= 0:
        return
    new = []
    for obj, bone, marker in STATE['objs'][-count:]:
        dup = obj.copy()
        dup.data = obj.data.copy()
        bpy.context.scene.collection.objects.link(dup)
        dup.name = obj.name.replace('.L', '.R') if '.L' in obj.name else obj.name + '_R'
        for v in dup.data.vertices:
            v.co.x = -v.co.x
        dup.data.flip_normals()
        for md in dup.modifiers:
            if md.type == 'BOOLEAN' and md.object:
                cm = md.object.copy()
                cm.data = md.object.data.copy()
                bpy.context.scene.collection.objects.link(cm)
                for v in cm.data.vertices:
                    v.co.x = -v.co.x
                cm.data.flip_normals()
                STATE['cutters'].append(cm)
                md.object = cm
        if 'text_c' in obj:
            # keep text readable: reflect again along its own reading axis
            c = Vector(obj['text_c']); c.x = -c.x
            u = Vector(obj['text_u']); u.x = -u.x
            for v in dup.data.vertices:
                v.co = v.co - 2 * (v.co - c).dot(u) * u
            dup.data.flip_normals()
        new.append((dup, bone.replace('.L', '.R') if bone else None, marker))
    STATE['objs'].extend(new)


class Mirror:
    """with Mirror(): ... builds left-side geometry; on exit mirrors everything created inside."""
    def __enter__(self):
        self.start = len(STATE['objs'])
    def __exit__(self, *a):
        mirror_last(len(STATE['objs']) - self.start)


def finish(rig):
    """Apply modifiers, join per (bone, marker), weight to bone, name P_<part>__<bone|marker>.
    Material slots follow MCLASS; each face's slot = its mclass."""
    pid = STATE['part']
    groups = {}
    for obj, bone, marker in STATE['objs']:
        groups.setdefault((bone, marker), []).append(obj)
    dg = bpy.context.evaluated_depsgraph_get()
    result = []
    for (bone, marker), objs in groups.items():
        meshes = []
        for o in objs:
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
            meshes.append(me)
        bm = bmesh.new()
        for me in meshes:
            bm.from_mesh(me)
        for o in objs:
            bpy.data.objects.remove(o)
        for me in meshes:
            bpy.data.meshes.remove(me)
        name = f'P_{pid}__{marker or bone}'
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        # merged meshes can carry partial UV layers (from text/curves) that crash unwrapping
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        # material slot per class
        attr = me.attributes.get('mclass')
        for cname in MCLASS:
            obj.data.materials.append(bpy.data.materials.get('MC_' + cname))
        if attr:
            vals = [0] * len(me.polygons)
            attr.data.foreach_get('value', vals)
            me.polygons.foreach_set('material_index', vals)
        vg = obj.vertex_groups.new(name=bone)
        vg.add(range(len(me.vertices)), 1.0, 'REPLACE')
        obj.parent = rig
        am = obj.modifiers.new('Armature', 'ARMATURE')
        am.object = rig
        obj['part'] = pid
        obj['bone'] = bone
        result.append(obj)
    for c in STATE['cutters']:
        if c.name in bpy.data.objects:
            bpy.data.objects.remove(c)
    STATE['cutters'] = []
    STATE['objs'] = []
    return result
