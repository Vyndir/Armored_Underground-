class_name BuildNature
extends RefCounted
## Granite boulders along the shore, pine forest on the hills and headland, and
## the far side of the bay: mountains, the Central Bridge, Downtown, islets,
## a lighthouse and boats.

static var _rng := RandomNumberGenerator.new()

## Faceted granite boulder: a subdivided sphere pushed around by noise and squashed.
static func rock_mesh(seed: int, segs := 14, rings := 8) -> ArrayMesh:
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	sphere.radial_segments = segs
	sphere.rings = rings
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
	var far_meshes := []
	for k in 4:
		meshes.append(rock_mesh(100 + k))
		far_meshes.append(rock_mesh(100 + k, 7, 4))
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
		if not Site.on_land(x, z) or Site.in_paved(x, z, 30.0):
			continue
		var s := _rng.randf_range(2.0, 7.0)
		per[_rng.randi() % meshes.size()].append(Transform3D(Basis(Vector3.UP, _rng.randf() * TAU).scaled(Vector3(s, s * 0.7, s)), Vector3(x, Site.ground(x, z) - s * 0.3, z)))
	for k in meshes.size():
		var items := []
		for xf in per[k]:
			items.append([xf, Color(1, 1, 1)])
		Vegetation.chunked(root, "Rocks%d" % k, meshes[k], far_meshes[k], items, mat, false, 420.0, 1100.0)
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
	Vegetation.forest(root)

# ------------------------------------------------------------------ across the bay
static func far_side(root: Node3D) -> void:
	_rng.seed = 21
	# mountain layers: radial height fields with ridged noise, forest green on the
	# slopes grading to rock at the crests; the far range is bluer with distance
	_mountain_layer(root, "MountainsNear", 2500.0, 3700.0, 260.0, Color(0.13, 0.22, 0.11), Color(0.3, 0.34, 0.28), 7)
	_mountain_layer(root, "MountainsFar", 4100.0, 6400.0, 680.0, Color(0.17, 0.25, 0.2), Color(0.36, 0.4, 0.42), 13)
	bridge(root)
	City.build(root)
	islets(root)

static func _ridge(a: float, r: float, seed: int) -> float:
	var n := 0.0
	var amp := 0.6
	var f := 1.0
	for o in 4:
		n += (1.0 - absf(Site.n2(a * 700.0 * f, r * 0.6 * f, 0.004, seed + o))) * amp
		amp *= 0.5
		f *= 2.1
	return n

static func _mountain_layer(root: Node3D, name: String, r0: float, r1: float, hmax: float, low: Color, high: Color, seed: int) -> void:
	var segs := 200
	var rows := 9
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var grid := []
	for j in rows + 1:
		var row := []
		var r := lerpf(r0, r1, float(j) / rows)
		for k in segs + 1:
			var a := lerpf(-0.18 * PI, 1.12 * PI, float(k) / segs)
			# profile across the ring: rises from the shore side, peaks, then falls behind
			var t := float(j) / rows
			var prof := sin(PI * clampf(t * 1.15, 0.0, 1.0))
			var h := hmax * prof * (0.25 + 0.75 * pow(_ridge(a, r, seed), 1.6)) if j > 0 else 0.0
			row.append(Vector3(cos(a) * r + 400.0, Site.WATER_Y - 4.0 + h, -sin(a) * r - 500.0))
		grid.append(row)
	for j in rows:
		for k in segs:
			var p00: Vector3 = grid[j][k]
			var p01: Vector3 = grid[j][k + 1]
			var p10: Vector3 = grid[j + 1][k]
			var p11: Vector3 = grid[j + 1][k + 1]
			for tri in [[p00, p10, p01], [p01, p10, p11]]:
				var a: Vector3 = tri[0]
				var b: Vector3 = tri[1]
				var c: Vector3 = tri[2]
				var n := (b - a).cross(c - a).normalized()
				if n.y < 0.0:
					n = -n
					var tmp := b
					b = c
					c = tmp
				for v: Vector3 in [a, c, b]:
					var hy := clampf((v.y - Site.WATER_Y) / hmax, 0.0, 1.0)
					var col := low.lerp(high, smoothstep(0.35, 0.85, hy + (1.0 - n.y) * 0.4))
					st.set_color(col)
					st.set_normal(n)
					st.add_vertex(v)
	var m := StandardMaterial3D.new()
	m.vertex_color_use_as_albedo = true
	m.vertex_color_is_srgb = true
	m.roughness = 0.95
	var mi := MeshInstance3D.new()
	mi.name = name
	mi.mesh = st.commit()
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	root.add_child(mi)

## The Central Bridge: a monumental suspension bridge across the bay (same
## position and span as before). Stone-coloured towers on wide piers with portal
## frames, a deck with stiffening-truss edges, two main cables sweeping from
## anchorages on both banks over the towers, suspenders, side-span piers, and
## deck lamps and tower beacons for night. Far scenery: no shadows, no collision.
const BRIDGE_Z := -1700.0
const BRIDGE_DECK := 47.0
const BRIDGE_TOWERS := [370.0, 1130.0]
const BRIDGE_ANCHORS := [-150.0, 1680.0]

static func bridge(root: Node3D) -> void:
	var stone := Mats.lit("BridgeStone", Color(0.78, 0.74, 0.66), 0.85)
	var steel := Mats.lit("BridgeSteel", Color(0.46, 0.47, 0.5), 0.55, 0.5)
	var dark := Mats.lit("BridgeUnder", Color(0.22, 0.23, 0.25), 0.8, 0.3)
	var lamp := Mats.glow("BridgeLamp", Color(1.0, 0.85, 0.6), 5.0)
	var beacon := Mats.glow("BridgeBeacon", Color(1.0, 0.2, 0.15), 8.0)
	var g := Geo.new()
	var z := BRIDGE_Z
	var x0: float = BRIDGE_ANCHORS[0]
	var x1: float = BRIDGE_ANCHORS[1]
	var deck := BRIDGE_DECK
	var top := 175.0
	# deck: box girder, darker underside, truss chords and diagonals along both edges
	g.box(steel, Vector3((x0 + x1) * 0.5, deck - 2.0, z), Vector3(x1 - x0, 4.0, 30.0), Basis(), 20.0, [], false)
	g.box(dark, Vector3((x0 + x1) * 0.5, deck - 4.3, z), Vector3(x1 - x0, 0.6, 26.0), Basis(), 20.0, [], false)
	for s: float in [-1.0, 1.0]:
		var ez := z + s * 15.5
		g.beam(steel, Vector3(x0, deck + 5.0, ez), Vector3(x1, deck + 5.0, ez), 0.8)
		var x := x0
		var k := 0
		while x < x1:
			var xn := minf(x + 15.0, x1)
			g.beam(steel, Vector3(x, deck, ez), Vector3(xn, deck + 5.0, ez) if k % 2 == 0 else Vector3(x, deck + 5.0, ez), 0.45)
			g.beam(steel, Vector3(x, deck + 5.0, ez), Vector3(xn, deck, ez) if k % 2 == 1 else Vector3(xn, deck + 5.0, ez), 0.45)
			if k % 2 == 0:
				g.box(lamp, Vector3(x, deck + 6.2, ez), Vector3(0.6, 0.6, 0.6), Basis(), 1.0, [], false)
			x = xn
			k += 1
	# towers: two tapering legs, three portal frames, caps with beacons, wide piers
	for tx: float in BRIDGE_TOWERS:
		g.box(stone, Vector3(tx, Site.WATER_Y + 6.0, z), Vector3(34.0, 16.0, 62.0), Basis(), 10.0, [], false)
		for s: float in [-1.0, 1.0]:
			var lz := z + s * 17.0
			var steps := 6
			for k in steps:
				var y0 := Site.WATER_Y + 14.0 + (top - 14.0) * k / steps
				var y1 := Site.WATER_Y + 14.0 + (top - 14.0) * (k + 1) / steps
				var w := lerpf(14.0, 8.5, float(k) / steps)
				g.box(stone, Vector3(tx, (y0 + y1) * 0.5, lz - s * float(k) * 0.4), Vector3(w, y1 - y0 + 0.2, w * 0.8), Basis(), 10.0, [], false)
			g.box(stone, Vector3(tx, top + 4.0, lz - s * 2.4), Vector3(10.0, 8.0, 8.0), Basis(), 10.0, [], false)
			g.box(beacon, Vector3(tx, top + 8.6, lz - s * 2.4), Vector3(1.4, 1.2, 1.4), Basis(), 1.0, [], false)
		for py: float in [deck - 9.0, 105.0, 150.0]:
			g.box(stone, Vector3(tx, py, z), Vector3(9.0, 9.0, 34.0), Basis(), 10.0, [], false)
			g.box(dark, Vector3(tx + 4.6, py - 1.5, z), Vector3(0.3, 4.0, 20.0), Basis(), 10.0, [], false)
	# anchorages: massive concrete blocks on each bank
	for ax: float in BRIDGE_ANCHORS:
		var gy := Site.ground(ax, z) if ax < 900.0 else Site.east_ground(ax, z)
		g.box(stone, Vector3(ax, gy + 10.0, z), Vector3(40.0, 26.0, 52.0), Basis(), 10.0, [], false)
	# side-span piers over the land on both banks
	for px: float in [x0 + 70.0, x0 + 190.0, x1 - 70.0, x1 - 190.0]:
		var gy := maxf(Site.ground(px, z) if px < 900.0 else Site.east_ground(px, z), Site.WATER_Y)
		g.box(stone, Vector3(px, (gy + deck - 4.0) * 0.5, z), Vector3(7.0, deck - 4.0 - gy, 24.0), Basis(), 10.0, [], false)
	# main cables and suspenders
	var t0: float = BRIDGE_TOWERS[0]
	var t1: float = BRIDGE_TOWERS[1]
	for s: float in [-1.0, 1.0]:
		var cz := z + s * 14.5
		var pts := []
		var n := 96
		for k in n + 1:
			var x := lerpf(x0, x1, float(k) / n)
			var y := 0.0
			if x < t0:
				var u := (x - x0) / (t0 - x0)
				y = lerpf(deck + 2.0, top + 2.0, u * u * 0.4 + u * 0.6)
			elif x > t1:
				var u := (x1 - x) / (x1 - t1)
				y = lerpf(deck + 2.0, top + 2.0, u * u * 0.4 + u * 0.6)
			else:
				var t := (x - t0) / (t1 - t0) * 2.0 - 1.0
				y = deck + 7.0 + (top + 2.0 - deck - 7.0) * t * t
			pts.append(Vector3(x, y, cz))
		for k in pts.size() - 1:
			g.beam(steel, pts[k], pts[k + 1], 1.3, -1.0, false)
			var p: Vector3 = pts[k]
			if p.y > deck + 6.0:
				g.beam(steel, p, Vector3(p.x, deck + 5.0, p.z), 0.3, -1.0, false)
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
