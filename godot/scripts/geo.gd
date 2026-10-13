class_name Geo
extends RefCounted
## Accumulates generated geometry per material and commits it as one mesh with a
## surface per material (plus an optional trimesh collider). Godot treats
## clockwise triangles as front faces, so every helper orients faces from an
## outward hint rather than trusting vertex order.

var _surf := {}        # Material -> {v: PackedVector3Array, n: PackedVector3Array, uv: PackedVector2Array}
var collide := PackedVector3Array()   # triangles for the collider (only from solid helpers)

func _s(m: Material) -> Dictionary:
	if not _surf.has(m):
		_surf[m] = {"v": PackedVector3Array(), "n": PackedVector3Array(), "uv": PackedVector2Array()}
	return _surf[m]

## One triangle; front side faces `hint`. Flat normal.
func tri(m: Material, a: Vector3, b: Vector3, c: Vector3, hint: Vector3, ua := Vector2.ZERO, ub := Vector2.ZERO, uc := Vector2.ZERO, solid := true) -> void:
	var n := (c - a).cross(b - a)
	if n.dot(hint) < 0.0:
		var t := b; b = c; c = t
		var tu := ub; ub = uc; uc = tu
		n = -n
	n = n.normalized()
	var s := _s(m)
	s.v.append(a); s.v.append(b); s.v.append(c)
	s.n.append(n); s.n.append(n); s.n.append(n)
	s.uv.append(ua); s.uv.append(ub); s.uv.append(uc)
	if solid:
		collide.append(a); collide.append(b); collide.append(c)

## Quad a-b-c-d (in order around the edge) facing `hint`, UVs from world size / tile.
func quad(m: Material, a: Vector3, b: Vector3, c: Vector3, d: Vector3, hint: Vector3, tile := 4.0, uv0 := Vector2.ZERO, solid := true) -> void:
	var w := a.distance_to(b) / tile
	var h := a.distance_to(d) / tile
	tri(m, a, b, c, hint, uv0, uv0 + Vector2(w, 0), uv0 + Vector2(w, h), solid)
	tri(m, a, c, d, hint, uv0, uv0 + Vector2(w, h), uv0 + Vector2(0, h), solid)

## Oriented box: centre, size, basis. Faces can be skipped with a mask of
## "+x","-x","+y","-y","+z","-z".
func box(m: Material, center: Vector3, size: Vector3, basis := Basis(), tile := 4.0, skip := [], solid := true) -> void:
	var h := size * 0.5
	var ax := basis.x * h.x
	var ay := basis.y * h.y
	var az := basis.z * h.z
	var faces := {
		"+x": [ax, ay, az], "-x": [-ax, ay, -az],
		"+y": [ay, az, ax], "-y": [-ay, az, -ax],
		"+z": [az, ay, -ax], "-z": [-az, ay, ax],
	}
	for k in faces:
		if k in skip:
			continue
		var f: Array = faces[k]
		var n: Vector3 = f[0]
		var u: Vector3 = f[2]
		var v: Vector3 = f[1]
		var c := center + n
		var uv0 := Vector2((c + basis.x * 0.0).dot(u.normalized()), c.y) / tile
		quad(m, c - u - v, c + u - v, c + u + v, c - u + v, n, tile, Vector2(uv0.x, (c - v).y / tile), solid)

## Axis-aligned box from min/max corners.
func aabb(m: Material, lo: Vector3, hi: Vector3, tile := 4.0, skip := [], solid := true) -> void:
	box(m, (lo + hi) * 0.5, hi - lo, Basis(), tile, skip, solid)

## Cylinder (or cone/frustum with r2) between a and b with smooth side normals.
func cylinder(m: Material, a: Vector3, b: Vector3, r: float, sides := 12, r2 := -1.0, caps := true, solid := true) -> void:
	if r2 < 0.0:
		r2 = r
	var axis := (b - a).normalized()
	var side := axis.cross(Vector3.RIGHT if absf(axis.y) > 0.9 else Vector3.UP).normalized()
	var up := side.cross(axis)
	var s := _s(m)
	var length := a.distance_to(b)
	for k in sides:
		var a0 := TAU * k / sides
		var a1 := TAU * (k + 1) / sides
		var o0 := side * cos(a0) + up * sin(a0)
		var o1 := side * cos(a1) + up * sin(a1)
		var p00 := a + o0 * r
		var p01 := a + o1 * r
		var p10 := b + o0 * r2
		var p11 := b + o1 * r2
		var u0 := float(k) / sides * TAU * r / 2.0
		var u1 := float(k + 1) / sides * TAU * r / 2.0
		# keep winding consistent with the outward direction
		var mid := (o0 + o1).normalized()
		var tri_list := [[p00, p10, p11, o0, o0, o1, Vector2(u0, 0), Vector2(u0, length / 2.0), Vector2(u1, length / 2.0)],
			[p00, p11, p01, o0, o1, o1, Vector2(u0, 0), Vector2(u1, length / 2.0), Vector2(u1, 0)]]
		for t in tri_list:
			var pa: Vector3 = t[0]
			var pb: Vector3 = t[1]
			var pc: Vector3 = t[2]
			var na: Vector3 = t[3]
			var nb: Vector3 = t[4]
			var nc: Vector3 = t[5]
			var ua: Vector2 = t[6]
			var ub: Vector2 = t[7]
			var uc: Vector2 = t[8]
			if (pc - pa).cross(pb - pa).dot(mid) < 0.0:
				var tp := pb; pb = pc; pc = tp
				var tn := nb; nb = nc; nc = tn
				var tu := ub; ub = uc; uc = tu
			s.v.append(pa); s.v.append(pb); s.v.append(pc)
			s.n.append(na); s.n.append(nb); s.n.append(nc)
			s.uv.append(ua); s.uv.append(ub); s.uv.append(uc)
			if solid:
				collide.append(pa); collide.append(pb); collide.append(pc)
		if caps:
			if r > 0.001:
				tri(m, a, p00, p01, -axis, Vector2(0.5, 0.5), Vector2(0.5 + cos(a0) * 0.5, 0.5 + sin(a0) * 0.5), Vector2(0.5 + cos(a1) * 0.5, 0.5 + sin(a1) * 0.5), solid)
			if r2 > 0.001:
				tri(m, b, p10, p11, axis, Vector2(0.5, 0.5), Vector2(0.5 + cos(a0) * 0.5, 0.5 + sin(a0) * 0.5), Vector2(0.5 + cos(a1) * 0.5, 0.5 + sin(a1) * 0.5), solid)

## Thin strut/rod between two points (box section), handy for railings and trusses.
func beam(m: Material, a: Vector3, b: Vector3, w: float, h := -1.0, solid := false) -> void:
	if h < 0.0:
		h = w
	var d := b - a
	var length := d.length()
	if length < 0.001:
		return
	var z := d / length
	var x := (Vector3.UP if absf(z.y) < 0.95 else Vector3.RIGHT).cross(z).normalized()
	var y := z.cross(x)
	box(m, (a + b) * 0.5, Vector3(w, h, length), Basis(x, y, z), 2.0, [], solid)

## Extruded polygon (XZ outline at y0..y1), side walls + caps (convex or simple star-shaped outlines).
func prism(m: Material, outline: PackedVector2Array, y0: float, y1: float, cap_m: Material = null, tile := 4.0, solid := true) -> void:
	var c := Vector2.ZERO
	for p in outline:
		c += p
	c /= outline.size()
	for i in outline.size():
		var p0 := outline[i]
		var p1 := outline[(i + 1) % outline.size()]
		var mid := (p0 + p1) * 0.5 - c
		quad(m, Vector3(p0.x, y0, p0.y), Vector3(p1.x, y0, p1.y), Vector3(p1.x, y1, p1.y), Vector3(p0.x, y1, p0.y), Vector3(mid.x, 0, mid.y), tile, Vector2.ZERO, solid)
		var cm := cap_m if cap_m else m
		tri(cm, Vector3(c.x, y1, c.y), Vector3(p0.x, y1, p0.y), Vector3(p1.x, y1, p1.y), Vector3.UP, c / tile, p0 / tile, p1 / tile, solid)

## Append an existing mesh (all surfaces) with a transform, into the given material.
func mesh(m: Material, src: Mesh, xf: Transform3D, solid := false) -> void:
	var s := _s(m)
	for si in src.get_surface_count():
		var arr := src.surface_get_arrays(si)
		var verts: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
		var nrms: PackedVector3Array = arr[Mesh.ARRAY_NORMAL]
		var uvs = arr[Mesh.ARRAY_TEX_UV]
		var idx = arr[Mesh.ARRAY_INDEX]
		var order: PackedInt32Array = idx if idx != null and idx.size() > 0 else PackedInt32Array(range(verts.size()))
		for i in order:
			var p := xf * verts[i]
			s.v.append(p)
			s.n.append((xf.basis * nrms[i]).normalized() if nrms.size() > i else Vector3.UP)
			s.uv.append(uvs[i] if uvs != null and uvs.size() > i else Vector2.ZERO)
			if solid:
				collide.append(p)

func is_empty() -> bool:
	return _surf.is_empty()

## Build the mesh (and optionally a static collider) under `parent`.
func commit(parent: Node3D, name: String, shadows := true, with_collision := false) -> MeshInstance3D:
	var am := ArrayMesh.new()
	for m in _surf:
		var s: Dictionary = _surf[m]
		if s.v.size() == 0:
			continue
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		for i in s.v.size():
			st.set_normal(s.n[i])
			st.set_uv(s.uv[i])
			st.add_vertex(s.v[i])
		st.generate_tangents()
		st.set_material(m)
		st.commit(am)
	var mi := MeshInstance3D.new()
	mi.name = name
	mi.mesh = am
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if shadows else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(mi)
	if with_collision and collide.size() >= 3:
		var body := StaticBody3D.new()
		body.name = name + "_Col"
		var cs := CollisionShape3D.new()
		var shape := ConcavePolygonShape3D.new()
		shape.set_faces(collide)
		cs.shape = shape
		body.add_child(cs)
		parent.add_child(body)
	_surf.clear()
	collide = PackedVector3Array()
	return mi

## Convenience: an invisible box collider.
static func box_collider(parent: Node3D, center: Vector3, size: Vector3, basis := Basis()) -> StaticBody3D:
	var body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	cs.shape = shape
	body.add_child(cs)
	body.transform = Transform3D(basis, center)
	parent.add_child(body)
	return body
