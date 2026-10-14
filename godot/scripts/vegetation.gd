class_name Vegetation
extends RefCounted
## Trees and scatter for the waterfront: four tree types with near and far
## meshes, placed by setting (coastal pines by the water, mixed groves on the
## plain, conifers up the hills, a sparse far layer on the Downtown bank), and a
## chunked LOD scatter used for trees and rocks.
##
## Every tree mesh is a single surface with vertex colours (bark and foliage), so
## a chunk of one type costs one draw call per pass; per-instance colour gives
## each tree its own shade. Only near chunks around the playable area cast
## shadows; far versions use big chunks and never cast.

const NEAR_END := 420.0          # near mesh fades out, far mesh fades in
const FAR_END := 3200.0
const NEAR_CHUNK := 200.0
const FAR_CHUNK := 520.0
const SHADOW_CENTER := Vector2(-60.0, -150.0)
const SHADOW_RADIUS := 520.0

static var _mat: StandardMaterial3D
static var _rng := RandomNumberGenerator.new()

static func material() -> StandardMaterial3D:
	if _mat:
		return _mat
	_mat = StandardMaterial3D.new()
	_mat.resource_name = "Vegetation"
	_mat.vertex_color_use_as_albedo = true
	_mat.vertex_color_is_srgb = true
	_mat.roughness = 0.92
	_mat.backlight_enabled = true
	_mat.backlight = Color(0.16, 0.2, 0.1)
	_mat.normal_enabled = true
	_mat.normal_texture = Tex.noise(0.25, 52, 256, true, 5.0)
	_mat.uv1_triplanar = true
	_mat.uv1_scale = Vector3(0.7, 0.7, 0.7)
	return _mat

# ------------------------------------------------------------------ mesh building
const BARK := Color(0.28, 0.21, 0.16)

static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, col: Color) -> void:
	# callers pass (a, b, c) counter-clockwise seen from outside; Godot's front
	# faces are clockwise, so emit (a, c, b) with the outward normal
	var n := (b - a).cross(c - a).normalized()
	st.set_color(col)
	st.set_normal(n)
	st.add_vertex(a)
	st.add_vertex(c)
	st.add_vertex(b)

## Jagged cone tier: a skirt of `sides` facets drooping from r at y0 to a point at y1.
static func _cone(st: SurfaceTool, base: Vector3, h: float, r: float, sides: int, col: Color, rng: RandomNumberGenerator, droop := 0.25) -> void:
	var tip := base + Vector3(rng.randf_range(-0.1, 0.1) * r, h, rng.randf_range(-0.1, 0.1) * r)
	var ring := []
	for k in sides:
		var a := TAU * k / sides + rng.randf_range(-0.2, 0.2)
		var rr := r * rng.randf_range(0.78, 1.15)
		ring.append(base + Vector3(cos(a) * rr, -droop * rr * rng.randf_range(0.6, 1.2), sin(a) * rr))
	var under := base + Vector3(0, h * 0.12, 0)
	for k in sides:
		var p: Vector3 = ring[k]
		var q: Vector3 = ring[(k + 1) % sides]
		var shade := col * rng.randf_range(0.88, 1.08)
		shade.a = 1.0
		_tri(st, p, tip, q, shade)
		_tri(st, q, under, p, col * 0.55)

## Low-poly blob for broad crowns and pine heads.
static func _blob(st: SurfaceTool, c: Vector3, rad: Vector3, col: Color, rng: RandomNumberGenerator, seg := 6, rings := 4) -> void:
	var pts := []
	for j in rings + 1:
		var v := PI * j / rings
		var row := []
		for k in seg:
			var u := TAU * k / seg + (0.5 if j % 2 == 1 else 0.0) * TAU / seg
			var jit := 1.0 + rng.randf_range(-0.16, 0.16) if j > 0 and j < rings else 1.0
			row.append(c + Vector3(cos(u) * sin(v) * rad.x * jit, cos(v) * rad.y * (0.9 if j == rings else 1.0), sin(u) * sin(v) * rad.z * jit))
		pts.append(row)
	for j in rings:
		for k in seg:
			var a: Vector3 = pts[j][k]
			var b: Vector3 = pts[j][(k + 1) % seg]
			var cc: Vector3 = pts[j + 1][k]
			var d: Vector3 = pts[j + 1][(k + 1) % seg]
			var lit := col * lerpf(1.08, 0.62, float(j) / rings) * rng.randf_range(0.92, 1.06)
			lit.a = 1.0
			if j > 0:
				_tri(st, a, b, cc, lit)
			if j < rings - 1:
				_tri(st, b, d, cc, lit)

static func _trunk(st: SurfaceTool, a: Vector3, b: Vector3, r0: float, r1: float, sides := 5) -> void:
	var d := (b - a).normalized()
	var x := d.cross(Vector3.FORWARD if absf(d.z) < 0.9 else Vector3.RIGHT).normalized()
	var y := d.cross(x)
	for k in sides:
		var a0 := TAU * k / sides
		var a1 := TAU * (k + 1) / sides
		var p0 := a + (x * cos(a0) + y * sin(a0)) * r0
		var p1 := a + (x * cos(a1) + y * sin(a1)) * r0
		var q0 := b + (x * cos(a0) + y * sin(a0)) * r1
		var q1 := b + (x * cos(a1) + y * sin(a1)) * r1
		_tri(st, p0, p1, q0, BARK)
		_tri(st, p1, q1, q0, BARK)

static func _commit(st: SurfaceTool) -> ArrayMesh:
	st.generate_tangents()
	return st.commit()

static func _begin() -> SurfaceTool:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_uv(Vector2.ZERO)
	return st

## Tall narrow spruce, 16-24 m, dense drooping tiers.
static func spruce(lod: int) -> ArrayMesh:
	_rng.seed = 101
	var st := _begin()
	var g := Color(0.16, 0.26, 0.15)
	if lod == 0:
		_trunk(st, Vector3.ZERO, Vector3(0, 6, 0), 0.38, 0.22)
		for k in 7:
			var y := 3.0 + k * 2.55
			_cone(st, Vector3(0, y, 0), 4.6 - k * 0.25, 3.4 * (1.0 - k * 0.12), 8, g * (0.9 + k * 0.03), _rng, 0.32)
	else:
		_trunk(st, Vector3.ZERO, Vector3(0, 4, 0), 0.4, 0.3, 4)
		_cone(st, Vector3(0, 3.0, 0), 11.0, 3.4, 6, g, _rng, 0.1)
		_cone(st, Vector3(0, 10.0, 0), 10.0, 2.4, 6, g * 1.06, _rng, 0.1)
	return _commit(st)

## Fuller fir, 12-18 m, wider and softer.
static func fir(lod: int) -> ArrayMesh:
	_rng.seed = 202
	var st := _begin()
	var g := Color(0.2, 0.3, 0.17)
	if lod == 0:
		_trunk(st, Vector3.ZERO, Vector3(0, 4, 0), 0.42, 0.26)
		for k in 5:
			var y := 2.4 + k * 2.6
			_cone(st, Vector3(0, y, 0), 5.2 - k * 0.3, 4.4 * (1.0 - k * 0.16), 9, g * (0.92 + k * 0.03), _rng, 0.3)
	else:
		_trunk(st, Vector3.ZERO, Vector3(0, 3, 0), 0.45, 0.3, 4)
		_cone(st, Vector3(0, 2.2, 0), 14.5, 4.4, 6, g, _rng, 0.08)
	return _commit(st)

## Crooked coastal pine with flat-topped cloud heads, 9-14 m, for the rocky shore.
static func coastal_pine(lod: int) -> ArrayMesh:
	_rng.seed = 303
	var st := _begin()
	var g := Color(0.24, 0.33, 0.16)
	if lod == 0:
		_trunk(st, Vector3.ZERO, Vector3(0.6, 5.0, 0.2), 0.42, 0.3)
		_trunk(st, Vector3(0.6, 5.0, 0.2), Vector3(-0.4, 9.0, 0.6), 0.3, 0.18)
		_trunk(st, Vector3(0.5, 6.0, 0.2), Vector3(2.4, 8.0, -0.5), 0.18, 0.1, 4)
		_blob(st, Vector3(-0.4, 10.0, 0.6), Vector3(3.6, 1.5, 3.2), g, _rng)
		_blob(st, Vector3(2.6, 8.4, -0.6), Vector3(2.4, 1.1, 2.2), g * 0.95, _rng)
		_blob(st, Vector3(-2.0, 8.0, 1.6), Vector3(2.0, 1.0, 2.0), g * 0.9, _rng)
	else:
		_trunk(st, Vector3.ZERO, Vector3(0.2, 8.0, 0.3), 0.4, 0.25, 4)
		_blob(st, Vector3(0.0, 9.4, 0.3), Vector3(4.2, 1.8, 3.8), g, _rng, 5, 3)
	return _commit(st)

## Round broadleaf, 8-12 m, brighter green, for the plain and the city.
static func broadleaf(lod: int) -> ArrayMesh:
	_rng.seed = 404
	var st := _begin()
	var g := Color(0.3, 0.4, 0.17)
	if lod == 0:
		_trunk(st, Vector3.ZERO, Vector3(0, 4.0, 0), 0.4, 0.28)
		_blob(st, Vector3(0, 7.0, 0), Vector3(3.8, 3.2, 3.8), g, _rng, 7, 4)
		_blob(st, Vector3(2.0, 6.0, 1.0), Vector3(2.6, 2.2, 2.6), g * 0.95, _rng)
		_blob(st, Vector3(-1.8, 6.4, -1.2), Vector3(2.4, 2.0, 2.4), g * 1.04, _rng)
	else:
		_trunk(st, Vector3.ZERO, Vector3(0, 3.6, 0), 0.4, 0.3, 4)
		_blob(st, Vector3(0, 6.8, 0), Vector3(4.4, 3.4, 4.4), g, _rng, 5, 3)
	return _commit(st)

# ------------------------------------------------------------------ chunked scatter
## Spread instances over spatial chunks. Each item: [Transform3D, Color].
## Near chunks (NEAR_CHUNK) draw the lod0 mesh up to NEAR_END; far chunks
## (FAR_CHUNK) draw the lod1 mesh from NEAR_END to FAR_END. Near chunks within
## SHADOW_RADIUS of the playable area cast shadows; nothing else does.
static func chunked(root: Node3D, name: String, lod0: Mesh, lod1: Mesh, items: Array, mat: Material, far_only := false, near_chunk := NEAR_CHUNK, far_chunk := FAR_CHUNK) -> void:
	var near := {}
	var far := {}
	for it in items:
		var t: Transform3D = it[0]
		var kn := Vector2i(floori(t.origin.x / near_chunk), floori(t.origin.z / near_chunk))
		var kf := Vector2i(floori(t.origin.x / far_chunk), floori(t.origin.z / far_chunk))
		if not far_only:
			if not near.has(kn): near[kn] = []
			near[kn].append(it)
		if not far.has(kf): far[kf] = []
		far[kf].append(it)
	var holder := Node3D.new()
	holder.name = name
	root.add_child(holder)
	for k: Vector2i in near:
		var c := (Vector2(k) + Vector2(0.5, 0.5)) * near_chunk
		var mmi := _mm(holder, lod0, near[k], mat)
		mmi.visibility_range_end = NEAR_END
		mmi.visibility_range_end_margin = 40.0
		mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if c.distance_to(SHADOW_CENTER) < SHADOW_RADIUS else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	for k: Vector2i in far:
		var mmi := _mm(holder, lod1, far[k], mat)
		mmi.visibility_range_begin = 0.0 if far_only else NEAR_END
		mmi.visibility_range_begin_margin = 0.0 if far_only else 40.0
		mmi.visibility_range_end = FAR_END
		mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

static func _mm(holder: Node3D, mesh: Mesh, items: Array, mat: Material) -> MultiMeshInstance3D:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = mesh
	mm.instance_count = items.size()
	for i in items.size():
		mm.set_instance_transform(i, items[i][0])
		mm.set_instance_color(i, items[i][1])
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	if mat:
		mmi.material_override = mat
	holder.add_child(mmi)
	return mmi

# ------------------------------------------------------------------ the forest
static func _tint(base: float, warm: float) -> Color:
	return Color(base + warm, base + warm * 0.4, base - warm * 0.5)

## Trees on both banks. Kept off paving, roads, water, the steepest bluff faces
## and the yard's open field; grouped into groves by noise.
static func forest(root: Node3D) -> void:
	_rng.seed = 11
	var kinds := {"spruce": [], "fir": [], "pine": [], "broad": []}
	var far := {"spruce": [], "broad": []}
	for k in 16000:
		var x := _rng.randf_range(-1000.0, 380.0)
		var z := _rng.randf_range(-2600.0, 380.0)
		if not Site.on_land(x, z) or Site.in_paved(x, z, 14.0):
			continue
		if absf(x - _road_x(z)) < 14.0 and z < Site.SLICE_Z:
			continue
		var h := Site.ground(x, z)
		if h < Site.WATER_Y + 1.4 or Site.slope(x, z) > 0.95:
			continue
		var grove := 0.5 + 0.5 * Site.n2(x, z, 0.011, 77)
		var d := Site.coast_x(z) - x
		var hill := d - Site.lowland_w(z)
		var dens := grove * (0.55 if hill < 0.0 else 1.0) * (0.35 if z < -1000.0 else 1.0)
		if _rng.randf() > dens:
			continue
		var r := _rng.randf()
		var kind := "pine"
		if d < 55.0:
			kind = "pine" if r < 0.7 else "fir"
		elif hill < 0.0:
			kind = "broad" if r < 0.4 else ("pine" if r < 0.65 else "fir")
		else:
			kind = "spruce" if r < 0.5 else ("fir" if r < 0.9 else "pine")
		var s := _rng.randf_range(0.75, 1.3)
		var lean := Basis(Vector3(_rng.randf_range(-1, 1), 0, _rng.randf_range(-1, 1)).normalized(), _rng.randf_range(0.0, 0.08 if kind != "pine" else 0.16))
		var b := lean * Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s, s * _rng.randf_range(0.85, 1.2), s))
		var col := _tint(_rng.randf_range(0.82, 1.12), _rng.randf_range(-0.06, 0.08))
		kinds[kind].append([Transform3D(b, Vector3(x, h - 0.4, z)), col])
	# the far bank: parks among the city and forest on its hills (far meshes only)
	for k in 5000:
		var x := _rng.randf_range(1300.0, 3500.0)
		var z := _rng.randf_range(-3300.0, 1300.0)
		var d := x - Site.east_coast_x(z)
		if d < 30.0:
			continue
		var downtown := x < 2800.0 and z < -1800.0 and z > -2700.0
		if downtown and _rng.randf() > 0.12:
			continue
		if _rng.randf() > 0.25 + 0.75 * smoothstep(300.0, 1200.0, d):
			continue
		var s := _rng.randf_range(0.9, 1.5)
		var t := Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s, s, s)), Vector3(x, Site.east_ground(x, z) - 0.4, z))
		far["broad" if d < 700.0 and _rng.randf() < 0.6 else "spruce"].append([t, _tint(_rng.randf_range(0.85, 1.1), _rng.randf_range(-0.04, 0.06))])
	var m := material()
	chunked(root, "TreesSpruce", spruce(0), spruce(1), kinds["spruce"], m)
	chunked(root, "TreesFir", fir(0), fir(1), kinds["fir"], m)
	chunked(root, "TreesPine", coastal_pine(0), coastal_pine(1), kinds["pine"], m)
	chunked(root, "TreesBroadleaf", broadleaf(0), broadleaf(1), kinds["broad"], m)
	chunked(root, "TreesFarSpruce", spruce(0), spruce(1), far["spruce"], m, true)
	chunked(root, "TreesFarBroadleaf", broadleaf(0), broadleaf(1), far["broad"], m, true)
	print("[vegetation] spruce=%d fir=%d pine=%d broadleaf=%d far=%d" % [kinds["spruce"].size(), kinds["fir"].size(), kinds["pine"].size(), kinds["broad"].size(), far["spruce"].size() + far["broad"].size()])

## Where the waterfront road runs (matches BuildDistrict.road_to_bridge).
static func _road_x(z: float) -> float:
	if z > -700.0:
		return -40.0
	if z > -950.0:
		return lerpf(-40.0, -80.0, (-700.0 - z) / 250.0)
	if z > -1200.0:
		return lerpf(-80.0, -140.0, (-950.0 - z) / 250.0)
	return -140.0
