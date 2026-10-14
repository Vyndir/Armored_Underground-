class_name BuildNature
extends RefCounted
## Granite boulders along the shore, pine forest on the hills and headland, and
## the far side of the bay: mountains, the Central Bridge, Downtown, islets,
## a lighthouse and boats.

static var _rng := RandomNumberGenerator.new()

## Faceted granite boulder: a subdivided sphere pushed around by noise and squashed.
static func rock_mesh(seed: int) -> ArrayMesh:
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	sphere.radial_segments = 14
	sphere.rings = 8
	var arr := sphere.get_mesh_arrays()
	var verts: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	var fn := FastNoiseLite.new()
	fn.seed = seed
	fn.frequency = 1.1
	fn.fractal_octaves = 3
	for i in verts.size():
		var v := verts[i]
		var d := 1.0 + fn.get_noise_3dv(v * 1.3) * 0.45
		v *= d
		v.y = v.y * 0.62 if v.y > 0.0 else v.y * 0.35
		# planar cleavage faces
		if v.x > 0.55: v.x = 0.55 + (v.x - 0.55) * 0.3
		verts[i] = v
	arr[Mesh.ARRAY_VERTEX] = verts
	var st := SurfaceTool.new()
	st.create_from_arrays(arr)
	st.deindex()
	st.generate_normals()
	st.generate_tangents()
	return st.commit()

static func rocks(root: Node3D) -> void:
	_rng.seed = 7
	var mat := Mats.shader("Granite", "res://shaders/terrain.gdshader", {
		"noise_a": Tex.noise(0.01, 31), "noise_b": Tex.noise(0.05, 32),
		"rock_nrm": Tex.noise(0.08, 33, 512, true, 8.0), "water_level": Site.WATER_Y,
		"grass": Color(0.5, 0.48, 0.45), "litter": Color(0.42, 0.4, 0.37),
	})
	var meshes := []
	for k in 8:
		meshes.append(rock_mesh(100 + k))
	var per := []
	for k in meshes.size():
		per.append([])
	# along the shoreline (skip the quay), denser on the headland and the foreground
	for z in range(-560, 300, 3):
		if z > Site.APRON_Z0 - 6 and z < Site.APRON_Z1 + 2:
			continue
		var cx := Site.coast_x(z)
		for k in 3:
			var x := cx + _rng.randf_range(-14.0, 8.0)
			var s := _rng.randf_range(1.5, 6.5) * (1.4 if _rng.randf() < 0.15 else 1.0)
			var y := Site.ground(x, z) - s * 0.25
			per[_rng.randi() % meshes.size()].append(Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s * _rng.randf_range(0.8, 1.5), s, s * _rng.randf_range(0.8, 1.3))), Vector3(x, y, z)))
	# talus and fallen blocks scattered down the coastal bluffs
	for z in range(-560, 300, 4):
		if z > Site.APRON_Z0 - 10 and z < Site.APRON_Z1 + 6:
			continue
		var cx2 := Site.coast_x(z)
		for k in 2:
			var x := cx2 - _rng.randf_range(6.0, 95.0)
			var zz := z + _rng.randf_range(-2.0, 2.0)
			if Site.slope(x, zz) < 0.55:
				continue
			var s := _rng.randf_range(2.0, 7.5)
			per[_rng.randi() % meshes.size()].append(Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s * _rng.randf_range(0.9, 1.6), s * 0.75, s)), Vector3(x, Site.ground(x, zz) - s * 0.3, zz)))
	# the rocky foreground of the concept, below the apron's south edge
	for k in 120:
		var x := _rng.randf_range(-120.0, 70.0)
		var z := _rng.randf_range(Site.APRON_Z1 + 1.0, Site.APRON_Z1 + 45.0)
		var s := _rng.randf_range(1.2, 4.5)
		var y := maxf(Site.ground(x, z), Site.WATER_Y - 2.0) - s * 0.2
		per[_rng.randi() % meshes.size()].append(Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s * 1.3, s * 0.8, s)), Vector3(x, y, z)))
	# outcrops on the hills
	for k in 160:
		var x := _rng.randf_range(-600.0, 300.0)
		var z := _rng.randf_range(-560.0, 260.0)
		if not Site.on_land(x, z) or Site.in_apron(x, z, 30.0):
			continue
		var s := _rng.randf_range(2.0, 7.0)
		per[_rng.randi() % meshes.size()].append(Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s, s * 0.7, s)), Vector3(x, Site.ground(x, z) - s * 0.3, z)))
	for k in meshes.size():
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = meshes[k]
		mm.instance_count = per[k].size()
		for i in per[k].size():
			mm.set_instance_transform(i, per[k][i])
		var mmi := MultiMeshInstance3D.new()
		mmi.name = "Rocks%d" % k
		mmi.multimesh = mm
		mmi.material_override = mat
		root.add_child(mmi)
		# solid boulders near the playable shore
		for xf in per[k]:
			var t: Transform3D = xf
			if t.origin.x > -220 and t.origin.x < 220 and t.origin.z > -260 and t.origin.z < 140:
				var s := t.basis.get_scale()
				Geo.box_collider(root, t.origin + Vector3(0, s.y * 0.2, 0), Vector3(s.x * 1.3, s.y * 1.0, s.z * 1.3), Basis(Vector3.UP, t.basis.get_euler().y))

static func pine_mesh() -> ArrayMesh:
	var bark := Mats.lit("Bark", Color(0.24, 0.17, 0.12), 0.95)
	var needles := Mats.lit("PineNeedles", Color(0.13, 0.22, 0.12), 0.9, 0.0, Tex.noise(0.2, 51, 256, true, 6.0), 0.8, Vector3(3, 3, 3))
	var g := Geo.new()
	g.cylinder(bark, Vector3.ZERO, Vector3(0, 5, 0), 0.35, 7, 0.2, false, false)
	var tiers := [[1.8, 7.0, 3.2], [4.0, 9.5, 2.6], [6.2, 11.8, 2.0], [8.4, 13.6, 1.4], [10.6, 15.4, 0.8]]
	for t in tiers:
		g.cylinder(needles, Vector3(0, t[0], 0), Vector3(0, t[1], 0), t[2], 9, 0.05, true, false)
	var mi := g.commit(Node3D.new(), "tmp", true, false)
	var m: ArrayMesh = mi.mesh
	mi.get_parent().free()
	return m

static func trees(root: Node3D) -> void:
	_rng.seed = 11
	var mesh := pine_mesh()
	var xfs := []
	for k in 9000:
		var x := _rng.randf_range(-950.0, 380.0)
		var z := _rng.randf_range(-950.0, 380.0)
		if not Site.on_land(x, z) or Site.in_apron(x, z, 14.0):
			continue
		var h := Site.ground(x, z)
		if h < Site.WATER_Y + 1.6 or Site.slope(x, z) > 0.95:   # leave the bluff faces bare
			continue
		var dens := 0.5 + 0.5 * Site.n2(x, z, 0.012, 77)
		if _rng.randf() > dens:
			continue
		var s := _rng.randf_range(0.7, 1.45)
		xfs.append(Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s, s * _rng.randf_range(0.85, 1.25), s)), Vector3(x, h - 0.3, z)))
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = xfs.size()
	for i in xfs.size():
		mm.set_instance_transform(i, xfs[i])
	var mmi := MultiMeshInstance3D.new()
	mmi.name = "Pines"
	mmi.multimesh = mm
	mmi.visibility_range_end = 2600
	root.add_child(mmi)

# ------------------------------------------------------------------ across the bay
static func far_side(root: Node3D) -> void:
	_rng.seed = 21
	var haze := Mats.lit("FarMountain", Color(0.33, 0.4, 0.42), 0.95)
	var forest := Mats.lit("FarForest", Color(0.2, 0.27, 0.2), 0.95)
	# mountain ring: a jagged skirt of ridges from north-west round to east
	var g := Geo.new()
	var segs := 140
	for k in segs:
		var a0 := lerpf(-0.15 * PI, 1.1 * PI, float(k) / segs)
		var a1 := lerpf(-0.15 * PI, 1.1 * PI, float(k + 1) / segs)
		for ring in [[2600.0, 3600.0, 380.0, forest], [4200.0, 6200.0, 1100.0, haze]]:
			var r0: float = ring[0]
			var r1: float = ring[1]
			var hm: float = ring[2]
			var m: Material = ring[3]
			var p0 := Vector3(cos(a0) * r0 + 400, Site.WATER_Y, -sin(a0) * r0 - 500)
			var p1 := Vector3(cos(a1) * r0 + 400, Site.WATER_Y, -sin(a1) * r0 - 500)
			var h0 := hm * (0.35 + 0.65 * absf(Site.n2(a0 * 900, r0, 0.004, 5)) + 0.25 * Site.n2(a0 * 3000, r0, 0.02, 6))
			var h1 := hm * (0.35 + 0.65 * absf(Site.n2(a1 * 900, r0, 0.004, 5)) + 0.25 * Site.n2(a1 * 3000, r0, 0.02, 6))
			var q0 := Vector3(cos(a0) * r1 + 400, Site.WATER_Y + h0, -sin(a0) * r1 - 500)
			var q1 := Vector3(cos(a1) * r1 + 400, Site.WATER_Y + h1, -sin(a1) * r1 - 500)
			var inward := Vector3(-cos(a0), 0.4, sin(a0))
			g.quad(m, p0, p1, q1, q0, inward, 200.0, Vector2.ZERO, false)
	var mi := g.commit(root, "Mountains", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	bridge(root)
	skyline(root)
	islets(root)

static func bridge(root: Node3D) -> void:
	var stone := Mats.lit("BridgeStone", Color(0.82, 0.78, 0.7), 0.85)
	var steel := Mats.lit("BridgeSteel", Color(0.55, 0.52, 0.48), 0.6, 0.4)
	var g := Geo.new()
	var z := -1700.0
	var x0 := -200.0
	var x1 := 1700.0
	var deck := 45.0
	g.box(steel, Vector3((x0 + x1) * 0.5, deck, z), Vector3(x1 - x0, 4.0, 30.0), Basis(), 20.0, [], false)
	var towers: Array[float] = [x0 + (x1 - x0) * 0.3, x0 + (x1 - x0) * 0.7]
	for tx in towers:
		for s: float in [-1.0, 1.0]:
			g.box(stone, Vector3(tx, (Site.WATER_Y + 150.0) * 0.5, z + s * 18.0), Vector3(12, 152, 10), Basis(), 10.0, [], false)
		for by: float in [60.0, 100.0, 140.0]:
			g.box(stone, Vector3(tx, by, z), Vector3(10, 7, 46), Basis(), 10.0, [], false)
		g.cylinder(stone, Vector3(tx, 150, z), Vector3(tx, 168, z), 5.0, 4, 0.2, true, false)
	# cables: catenary main span plus side spans
	for s: float in [-1.0, 1.0]:
		var pts := []
		for k in 61:
			var x := lerpf(x0, x1, k / 60.0)
			var y := 0.0
			if x < towers[0]:
				y = lerpf(deck + 6, 148, (x - x0) / (towers[0] - x0))
			elif x > towers[1]:
				y = lerpf(148, deck + 6, (x - towers[1]) / (x1 - towers[1]))
			else:
				var t: float = (x - towers[0]) / (towers[1] - towers[0]) * 2.0 - 1.0
				y = deck + 8 + (148 - deck - 8) * t * t
			pts.append(Vector3(x, y, z + s * 14.0))
		for k in pts.size() - 1:
			g.beam(steel, pts[k], pts[k + 1], 1.2, -1.0, false)
			if k % 2 == 0:
				g.beam(steel, pts[k], Vector3(pts[k].x, deck, pts[k].z), 0.35, -1.0, false)
	# arches under the approaches
	for k in 8:
		var ax := lerpf(x0 - 300, x0, k / 8.0)
		g.box(stone, Vector3(ax, (deck + Site.WATER_Y) * 0.5, z), Vector3(8, deck - Site.WATER_Y, 24), Basis(), 10.0, [], false)
	var mi := g.commit(root, "CentralBridge", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

static func skyline(root: Node3D) -> void:
	_rng.seed = 33
	var glass := Mats.lit("FarGlass", Color(0.35, 0.45, 0.55), 0.15, 0.6)
	var stone := Mats.lit("FarStone", Color(0.78, 0.75, 0.7), 0.8)
	var g := Geo.new()
	for k in 140:
		var x := _rng.randf_range(1300.0, 2700.0)
		var z := _rng.randf_range(-2600.0, -1850.0)
		var core := 1.0 - clampf(Vector2(x - 2000, z + 2150).length() / 700.0, 0.0, 1.0)
		var h := lerpf(20.0, 260.0, pow(core, 1.4)) * _rng.randf_range(0.5, 1.2)
		var w := _rng.randf_range(20.0, 45.0)
		var m := glass if h > 80 and _rng.randf() < 0.7 else stone
		g.box(m, Vector3(x, h * 0.5 + Site.WATER_Y, z), Vector3(w, h, w * _rng.randf_range(0.7, 1.3)), Basis(), 30.0, [], false)
		if h > 180:
			g.cylinder(Mats.steel(), Vector3(x, h + Site.WATER_Y, z), Vector3(x, h + 40 + Site.WATER_Y, z), 1.5, 6, 0.2, true, false)
	# the domed capitol by the water
	g.box(stone, Vector3(1550, 15, -1900), Vector3(110, 30, 60), Basis(), 20.0, [], false)
	g.cylinder(stone, Vector3(1550, 30, -1900), Vector3(1550, 46, -1900), 20, 24, -1.0, true, false)
	var dome := SphereMesh.new()
	dome.radius = 21
	dome.height = 42
	dome.is_hemisphere = true
	g.mesh(Mats.lit("FarDome", Color(0.35, 0.52, 0.62), 0.35, 0.6), dome, Transform3D(Basis(), Vector3(1550, 46, -1900)))
	var mi := g.commit(root, "Downtown", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

static func islets(root: Node3D) -> void:
	var rock := Mats.get_m("Granite")
	var trees_m := pine_mesh()
	var g := Geo.new()
	for c: Vector3 in [Vector3(420, 0, -620), Vector3(700, 0, -1100), Vector3(950, 0, -900), Vector3(240, 0, -980)]:
		var big := rock_mesh(int(c.x))
		var s := 24.0 if c.x == 420 else 16.0
		g.mesh(rock, big, Transform3D(Basis().scaled(Vector3(s * 1.6, s * 0.8, s * 1.2)), c + Vector3(0, Site.WATER_Y - 2, 0)))
		for k in 6:
			var o := Vector3(randf_range(-s, s), 0, randf_range(-s * 0.6, s * 0.6))
			g.mesh(Mats.get_m("PineNeedles"), trees_m, Transform3D(Basis().scaled(Vector3.ONE * 0.9), c + o + Vector3(0, Site.WATER_Y + s * 0.35, 0)))
	g.commit(root, "Islets", true, false)
	# lighthouse on the nearest islet
	var lg := Geo.new()
	var b := Vector3(420, Site.WATER_Y + 16, -620)
	lg.cylinder(Mats.white(), b, b + Vector3(0, 20, 0), 3.0, 16, 2.2, true, false)
	lg.cylinder(Mats.lit("LighthouseRed", Color(0.6, 0.1, 0.08), 0.5), b + Vector3(0, 20, 0), b + Vector3(0, 21, 0), 3.0, 16, -1.0, true, false)
	lg.cylinder(Mats.lamp(), b + Vector3(0, 21, 0), b + Vector3(0, 23.5, 0), 1.8, 12, -1.0, true, false)
	lg.cylinder(Mats.lit("LighthouseRed", Color(0.6, 0.1, 0.08), 0.5), b + Vector3(0, 23.5, 0), b + Vector3(0, 26, 0), 2.4, 12, 0.2, true, false)
	lg.commit(root, "Lighthouse", true, false)

static func boats(root: Node3D) -> Array:
	var out := []
	var hull := Mats.white()
	for spec in [[Vector3(520, 0, -380), 25.0, 30.0], [Vector3(300, 0, -760), 120.0, 18.0], [Vector3(780, 0, -520), -40.0, 40.0], [Vector3(180, 0, -330), 60.0, 9.0]]:
		var g := Geo.new()
		var len: float = spec[2]
		var w := len * 0.28
		var outline := PackedVector2Array([Vector2(-w / 2, -len / 2), Vector2(w / 2, -len / 2), Vector2(w / 2, len * 0.25), Vector2(0, len / 2), Vector2(-w / 2, len * 0.25)])
		g.prism(hull, outline, -1.0, 1.5, Mats.lit("Deck", Color(0.5, 0.45, 0.38), 0.7), 4.0, false)
		g.box(Mats.white(), Vector3(0, 3.0, -len * 0.1), Vector3(w * 0.7, 3.0, len * 0.35), Basis(), 4.0, [], false)
		g.box(Mats.glass(), Vector3(0, 3.4, -len * 0.1), Vector3(w * 0.72, 1.2, len * 0.3), Basis(), 4.0, [], false)
		if len < 12:
			g.beam(Mats.white(), Vector3(0, 1.5, 0), Vector3(0, 12, 0), 0.2)
			g.tri(Mats.white(), Vector3(0, 2.5, 0.3), Vector3(0, 11.5, 0.3), Vector3(0, 2.5, 4.5), Vector3.RIGHT, Vector2.ZERO, Vector2.ZERO, Vector2.ZERO, false)
			g.tri(Mats.white(), Vector3(0, 2.5, 0.3), Vector3(0, 11.5, 0.3), Vector3(0, 2.5, 4.5), Vector3.LEFT, Vector2.ZERO, Vector2.ZERO, Vector2.ZERO, false)
		var holder := Node3D.new()
		holder.name = "Boat"
		root.add_child(holder)
		g.commit(holder, "Hull", true, false)
		holder.position = spec[0] + Vector3(0, Site.WATER_Y, 0)
		holder.rotation.y = deg_to_rad(spec[1])
		out.append(holder)
	return out
