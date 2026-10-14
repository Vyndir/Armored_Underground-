class_name BuildSite
extends RefCounted
## Terrain (near ground with collision, coarse far land on both banks), bay water,
## the yard and the north quay yard, the quay with its parapet, the slipway and
## the north ramp onto the training shelf, and lamp posts.

static func _terrain_mat() -> Material:
	return Mats.shader("Terrain", "res://shaders/terrain.gdshader", {
		"noise_a": Tex.noise(0.01, 31), "noise_b": Tex.noise(0.05, 32),
		"rock_nrm": Tex.noise(0.08, 33, 512, true, 8.0), "water_level": Site.WATER_Y,
	})

## Height-field mesh over a grid; `height` is a Callable(x, z) -> float.
static func _grid(root: Node3D, name: String, x0: float, z0: float, nx: int, nz: int, step: float, height: Callable) -> PackedFloat32Array:
	var h := PackedFloat32Array()
	h.resize((nx + 1) * (nz + 1))
	for j in nz + 1:
		for i in nx + 1:
			h[j * (nx + 1) + i] = height.call(x0 + i * step, z0 + j * step)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for j in nz + 1:
		for i in nx + 1:
			var x := x0 + i * step
			var z := z0 + j * step
			var hl: float = h[j * (nx + 1) + maxi(i - 1, 0)]
			var hr: float = h[j * (nx + 1) + mini(i + 1, nx)]
			var hd: float = h[maxi(j - 1, 0) * (nx + 1) + i]
			var hu: float = h[mini(j + 1, nz) * (nx + 1) + i]
			st.set_normal(Vector3(hl - hr, 2.0 * step, hd - hu).normalized())
			st.set_uv(Vector2(x, z) / 8.0)
			st.add_vertex(Vector3(x, h[j * (nx + 1) + i], z))
	for j in nz:
		for i in nx:
			var a := j * (nx + 1) + i
			var b := a + 1
			var c := a + nx + 1
			var d := c + 1
			# clockwise seen from above (+Y) for Godot front faces
			st.add_index(a); st.add_index(b); st.add_index(c)
			st.add_index(b); st.add_index(d); st.add_index(c)
	st.generate_tangents()
	st.set_material(_terrain_mat())
	var mi := MeshInstance3D.new()
	mi.name = name
	mi.mesh = st.commit()
	root.add_child(mi)
	return h

static func terrain(root: Node3D) -> void:
	var x0 := -1000.0
	var z0 := -1000.0
	var step := 6.0
	var n := 230
	var h := _grid(root, "Terrain", x0, z0, n, n, step, func(x: float, z: float) -> float: return Site.ground(x, z))
	# coarse land north to the bridge and beyond (scenery, no collision); it
	# overlaps under the near grid by one coarse row to hide the seam
	_grid(root, "TerrainNorth", -1000.0, -2600.0, 69, 81, 20.0, func(x: float, z: float) -> float: return Site.ground(x, z) - (0.3 if z > -1010.0 else 0.0))
	# the far (Downtown) bank
	_grid(root, "TerrainEast", 1000.0, -3400.0, 65, 120, 40.0, func(x: float, z: float) -> float: return Site.east_ground(x, z))
	(root.get_node("TerrainEast") as MeshInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	(root.get_node("TerrainNorth") as MeshInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	# collision from the same heights over the playable part of the map
	var faces := PackedVector3Array()
	for j in n:
		for i in n:
			var x := x0 + i * step
			var z := z0 + j * step
			if x < -320.0 or x > 260.0 or z < -560.0 or z > 200.0:
				continue
			var pa := Vector3(x, h[j * (n + 1) + i], z)
			var pb := Vector3(x + step, h[j * (n + 1) + i + 1], z)
			var pc := Vector3(x, h[(j + 1) * (n + 1) + i], z + step)
			var pd := Vector3(x + step, h[(j + 1) * (n + 1) + i + 1], z + step)
			faces.append_array([pa, pb, pc, pb, pd, pc])
	var body := StaticBody3D.new()
	body.name = "TerrainCol"
	var cs := CollisionShape3D.new()
	var shape := ConcavePolygonShape3D.new()
	shape.backface_collision = true
	shape.set_faces(faces)
	cs.shape = shape
	body.add_child(cs)
	root.add_child(body)

static func water(root: Node3D) -> void:
	var mat := Mats.shader("Water", "res://shaders/water.gdshader", {
		"wave_a": Tex.noise(0.02, 41, 512, true, 3.0), "wave_b": Tex.noise(0.05, 42, 512, true, 2.0),
		"foam_noise": Tex.noise(0.04, 43),
	})
	# fine grid near the site for the swell, coarse planes beyond
	var near := PlaneMesh.new()
	near.size = Vector2(900, 900)
	near.subdivide_width = 180
	near.subdivide_depth = 180
	var mi := MeshInstance3D.new()
	mi.name = "WaterNear"
	mi.mesh = near
	mi.material_override = mat
	mi.position = Vector3(150, Site.WATER_Y, -150)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	root.add_child(mi)
	for k in 8:
		var far := PlaneMesh.new()
		far.size = Vector2(900, 900)
		var a := TAU * k / 8.0
		var ring := MeshInstance3D.new()
		ring.name = "WaterFar%d" % k
		ring.mesh = far
		ring.material_override = mat
		ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		root.add_child(ring)
		ring.position = Vector3(150 + roundf(cos(a)) * 900, Site.WATER_Y, -150 + roundf(sin(a)) * 900)
	var big := PlaneMesh.new()
	big.size = Vector2(12000, 12000)
	var outer := MeshInstance3D.new()
	outer.name = "WaterHorizon"
	outer.mesh = big
	outer.material_override = mat
	outer.position = Vector3(150, Site.WATER_Y - 0.05, -150)
	outer.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	root.add_child(outer)
	# the training shelf floor: exactly where the visible sea bed is
	var m := Site.SHELF_MARGIN
	Geo.box_collider(root, Vector3((Site.SHELF_X0 + Site.SHELF_X1 + m) * 0.5, Site.SHELF_FLOOR - 1.0, (Site.SHELF_Z0 + Site.SHELF_Z1) * 0.5),
		Vector3(Site.SHELF_X1 + m - Site.SHELF_X0, 2.0, Site.SHELF_Z1 - Site.SHELF_Z0 + m * 2.0))

# ------------------------------------------------------------------ yard, quay, slipway and ramp
static func apron(root: Node3D, markings: Texture2D) -> void:
	var conc := Mats.concrete("Apron", Color(0.5, 0.49, 0.46), 0.6)
	conc.set_shader_parameter("shore_x", Site.APRON_X1)
	conc.set_shader_parameter("markings", markings)
	conc.set_shader_parameter("markings_rect", Vector4(Site.APRON_X0, Site.APRON_Z0, Site.APRON_X1 - Site.APRON_X0, Site.APRON_Z1 - Site.APRON_Z0))
	conc.set_shader_parameter("use_markings", 1.0)
	var north := Mats.concrete("NorthYard", Color(0.47, 0.46, 0.44), 0.55)
	north.set_shader_parameter("shore_x", Site.APRON_X1)
	var wall := Mats.concrete("QuayWall", Color(0.55, 0.54, 0.5), 0.0)
	var g := Geo.new()
	var y := 0.0
	var base := -9.0
	# paving in strips so the slipway and ramp corridors can be cut out of the quay edge:
	# [z0, z1, seaward x, west x, material]
	var strips := [[Site.SLICE_Z, Site.APRON_Z0, Site.APRON_X1, Site.NYARD_X0, north],
		[Site.APRON_Z0, Site.RAMP_Z0, Site.APRON_X1, Site.APRON_X0, conc], [Site.RAMP_Z0, Site.RAMP_Z1, Site.RAMP_X0, Site.APRON_X0, conc],
		[Site.RAMP_Z1, Site.SLIP_Z0, Site.APRON_X1, Site.APRON_X0, conc], [Site.SLIP_Z0, Site.SLIP_Z1, Site.SLIP_X0, Site.APRON_X0, conc],
		[Site.SLIP_Z1, Site.APRON_Z1, Site.APRON_X1 - 12.0, Site.APRON_X0, conc]]
	for s in strips:
		var z0: float = s[0]
		var z1: float = s[1]
		var xe: float = s[2]
		var xw: float = s[3]
		var mat: Material = s[4]
		var steps := int((z1 - z0) / 12.0) + 1
		for k in steps:
			var za := lerpf(z0, z1, float(k) / steps)
			var zb := lerpf(z0, z1, float(k + 1) / steps)
			var x := xw
			while x < xe - 0.01:
				var xb := minf(x + 18.0, xe)
				g.quad(mat, Vector3(x, y, za), Vector3(xb, y, za), Vector3(xb, y, zb), Vector3(x, y, zb), Vector3.UP, 8.0)
				x = xb
		# seaward face of the quay / apron edge
		g.quad(wall, Vector3(xe, base, z0), Vector3(xe, base, z1), Vector3(xe, y, z1), Vector3(xe, y, z0), Vector3.RIGHT, 6.0)
		Geo.box_collider(root, Vector3((xw + xe) * 0.5, y - 4.0, (z0 + z1) * 0.5), Vector3(xe - xw, 8.0, z1 - z0))
	# west edge of the north yard and the south edge of the yard
	g.quad(wall, Vector3(Site.NYARD_X0, base, Site.SLICE_Z), Vector3(Site.NYARD_X0, base, Site.APRON_Z0), Vector3(Site.NYARD_X0, y, Site.APRON_Z0), Vector3(Site.NYARD_X0, y, Site.SLICE_Z), Vector3.LEFT, 6.0)
	g.quad(wall, Vector3(Site.APRON_X0, base, Site.APRON_Z1), Vector3(Site.APRON_X1 - 12.0, base, Site.APRON_Z1), Vector3(Site.APRON_X1 - 12.0, y, Site.APRON_Z1), Vector3(Site.APRON_X0, y, Site.APRON_Z1), Vector3.BACK, 6.0)
	g.commit(root, "Apron", true, false)
	_ramp(root, "Slipway", Site.SLIP_X0, Site.SLIP_X1, Site.SLIP_Z0, Site.SLIP_Z1, Site.APRON_X1, Site.APRON_X1 - 12.0)
	_ramp(root, "NorthRamp", Site.RAMP_X0, Site.RAMP_X1, Site.RAMP_Z0, Site.RAMP_Z1, Site.APRON_X1, Site.APRON_X1)

## A grooved concrete ramp from yard level down onto the shelf floor, with kerbs,
## side walls where it cuts through the quay, and a sloped collider.
static func _ramp(root: Node3D, name: String, xa0: float, xa1: float, z0: float, z1: float, wall_end0: float, wall_end1: float) -> void:
	var slip := Mats.concrete("Slipway", Color(0.58, 0.56, 0.52), 0.8, 1.6)
	var wall := Mats.concrete("QuayWall", Color(0.55, 0.54, 0.5), 0.0)
	var yf := func(x: float) -> float: return lerpf(0.0, Site.SHELF_FLOOR, clampf((x - xa0) / (xa1 - xa0), 0.0, 1.0))
	var g := Geo.new()
	var segs := 14
	for k in segs:
		var xa := lerpf(xa0, xa1, float(k) / segs)
		var xb := lerpf(xa0, xa1, float(k + 1) / segs)
		var ya: float = yf.call(xa)
		var yb: float = yf.call(xb)
		g.quad(slip, Vector3(xa, ya, z0), Vector3(xb, yb, z0), Vector3(xb, yb, z1), Vector3(xa, ya, z1), Vector3.UP, 6.0)
	# side walls through the quay
	for zz: float in [z0, z1]:
		var n := Vector3(0, 0, 1) if zz == z0 else Vector3(0, 0, -1)
		var xend := wall_end0 if zz == z0 else wall_end1
		for k in 10:
			var xa := lerpf(xa0, xend, float(k) / 10)
			var xb := lerpf(xa0, xend, float(k + 1) / 10)
			g.quad(wall, Vector3(xa, yf.call(xa) - 0.2, zz), Vector3(xb, yf.call(xb) - 0.2, zz), Vector3(xb, 0, zz), Vector3(xa, 0, zz), -n, 6.0)
	# kerbs along both sides
	for zz: float in [z0 - 0.6, z1 + 0.6]:
		for k in segs:
			var xa := lerpf(xa0, xa1, float(k) / segs)
			var xb := lerpf(xa0, xa1, float(k + 1) / segs)
			var dirv := Vector3(xb - xa, yf.call(xb) - yf.call(xa), 0)
			var c := Vector3((xa + xb) * 0.5, (yf.call(xa) + yf.call(xb)) * 0.5 + 0.35, zz)
			g.box(wall, c, Vector3(dirv.length() + 0.05, 1.2, 1.2), Basis(dirv.normalized(), Vector3.UP.rotated(Vector3.BACK, atan2(dirv.y, dirv.x)), Vector3.BACK))
	g.commit(root, name, true, false)
	var dir := Vector3(xa1 - xa0, Site.SHELF_FLOOR, 0)
	var mid := Vector3((xa0 + xa1) * 0.5, Site.SHELF_FLOOR * 0.5 - 0.5, (z0 + z1) * 0.5)
	var basis := Basis(dir.normalized(), Vector3(0, 1, 0).rotated(Vector3.BACK, atan2(dir.y, dir.x)), Vector3.BACK)
	Geo.box_collider(root, mid, Vector3(dir.length() + 1.0, 1.0, z1 - z0), basis)

## The quay edge: capping blocks, bollards, fenders, ladders and lamp posts. North
## of the training shelf the blocks run unbroken as a parapet along deep water.
static func quay(root: Node3D) -> void:
	var blk := Mats.concrete("QuayBlocks", Color(0.66, 0.64, 0.6), 0.1)
	var g := Geo.new()
	var x := Site.APRON_X1
	var z := Site.SLICE_Z + 1.0
	var k := 0
	while z < Site.SLIP_Z0 - 3.0:
		if z > Site.RAMP_Z0 - 2.0 and z < Site.RAMP_Z1 + 1.0:
			z = Site.RAMP_Z1 + 1.0
			continue
		var parapet := z < Site.SHELF_Z0
		var tall := parapet or k % 3 == 0
		var w := 5.0 if tall else 3.2
		g.box(blk, Vector3(x - 1.4, (1.6 if tall else 0.8), z + w * 0.5), Vector3(2.8, 3.2 if tall else 1.6, w - 0.3))
		if tall and (k % 3 == 0 or not parapet):
			g.box(Mats.yellow(), Vector3(x - 1.4, 3.25, z + w * 0.5), Vector3(2.9, 0.12, w - 0.2))
		if not tall:
			g.cylinder(Mats.dark(), Vector3(x - 3.6, 0, z + w * 0.5), Vector3(x - 3.6, 0.9, z + w * 0.5), 0.35, 12)
			g.cylinder(Mats.dark(), Vector3(x - 3.6, 0.9, z + w * 0.5), Vector3(x - 3.6, 1.05, z + w * 0.5), 0.5, 12)
		if k % 2 == 0:
			g.box(Mats.rubber(), Vector3(x + 0.35, -1.8, z + w * 0.5), Vector3(0.7, 2.6, 1.2))
		z += w
		k += 1
		if tall:
			Geo.box_collider(root, Vector3(x - 1.4, 1.6, z - w * 0.5), Vector3(2.8, 3.2, w - 0.3))
	# ladders down to the water
	for lz: float in [-400.0, -300.0, -200.0, -120.0, -10.0]:
		for rung in 10:
			var yy := -0.2 - rung * 0.4
			g.beam(Mats.steel(), Vector3(x + 0.25, yy, lz - 0.35), Vector3(x + 0.25, yy, lz + 0.35), 0.06)
		for s: float in [-0.4, 0.4]:
			g.beam(Mats.steel(), Vector3(x + 0.25, 1.0, lz + s), Vector3(x + 0.25, -4.2, lz + s), 0.08)
	g.commit(root, "Quay", true, false)
	# lamp posts along the quay strip and the south edge of the yard
	var lamps := Geo.new()
	var pts := []
	# on top of the parapet along deep water; at the inland edge of the strip by the
	# shelf, so nothing narrows the strip into a trap for a 3.7 m-wide frame
	for lz in range(int(Site.SLICE_Z) + 14, int(Site.SLIP_Z0) - 6, 28):
		if lz > Site.RAMP_Z0 - 3 and lz < Site.RAMP_Z1 + 3:
			continue
		pts.append(Vector3(x - 1.4, 3.2, lz) if lz < Site.SHELF_Z0 else Vector3(Site.STRIP_X0 + 0.6, 0, lz))
	for lx in range(-60, 40, 30):
		pts.append(Vector3(lx, 0, Site.APRON_Z1 - 3))
	for p in pts:
		lamp_post(lamps, p)
		var l := OmniLight3D.new()
		l.position = p + Vector3(0, 10.4, 0)
		l.light_color = Color(1.0, 0.85, 0.65)
		l.light_energy = 0.0   # switched on at dusk
		l.omni_range = 22
		l.add_to_group("night_lights")
		root.add_child(l)
		Geo.box_collider(root, p + Vector3(0, 5.5, 0), Vector3(0.6, 11, 0.6))
	lamps.commit(root, "LampPosts", true, false)

static func lamp_post(g: Geo, p: Vector3) -> void:
	g.cylinder(Mats.dark(), p, p + Vector3(0, 0.7, 0), 0.35, 10)
	g.cylinder(Mats.dark(), p + Vector3(0, 0.7, 0), p + Vector3(0, 10.5, 0), 0.16, 10, 0.12)
	g.box(Mats.dark(), p + Vector3(0, 10.7, 0), Vector3(1.1, 0.35, 0.5))
	g.box(Mats.lamp(), p + Vector3(0, 10.5, 0), Vector3(0.9, 0.08, 0.38))
	g.box(Mats.dark(), p + Vector3(0, 9.0, 0.45), Vector3(0.08, 0.08, 0.9))
