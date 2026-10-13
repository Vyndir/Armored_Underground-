class_name BuildSite
extends RefCounted
## Terrain, bay water, the apron and quay, the slipway, buoy line, lamp posts,
## rocks, pines and the far side of the bay.

static func terrain(root: Node3D) -> void:
	var mat := Mats.shader("Terrain", "res://shaders/terrain.gdshader", {
		"noise_a": Tex.noise(0.01, 31), "noise_b": Tex.noise(0.05, 32),
		"rock_nrm": Tex.noise(0.08, 33, 512, true, 8.0), "water_level": Site.WATER_Y,
	})
	var x0 := -1000.0
	var z0 := -1000.0
	var step := 6.0
	var n := 230
	var h := []
	h.resize((n + 1) * (n + 1))
	for j in n + 1:
		for i in n + 1:
			h[j * (n + 1) + i] = Site.ground(x0 + i * step, z0 + j * step)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for j in n + 1:
		for i in n + 1:
			var x := x0 + i * step
			var z := z0 + j * step
			var hl: float = h[j * (n + 1) + maxi(i - 1, 0)]
			var hr: float = h[j * (n + 1) + mini(i + 1, n)]
			var hd: float = h[maxi(j - 1, 0) * (n + 1) + i]
			var hu: float = h[mini(j + 1, n) * (n + 1) + i]
			st.set_normal(Vector3(hl - hr, 2.0 * step, hd - hu).normalized())
			st.set_uv(Vector2(x, z) / 8.0)
			st.add_vertex(Vector3(x, h[j * (n + 1) + i], z))
	for j in n:
		for i in n:
			var a := j * (n + 1) + i
			var b := a + 1
			var c := a + n + 1
			var d := c + 1
			# clockwise seen from above (+Y) for Godot front faces
			st.add_index(a); st.add_index(b); st.add_index(c)
			st.add_index(b); st.add_index(d); st.add_index(c)
	st.generate_tangents()
	st.set_material(mat)
	var mi := MeshInstance3D.new()
	mi.name = "Terrain"
	mi.mesh = st.commit()
	root.add_child(mi)
	# collision from the same heights (trimesh over the playable part of the map)
	var faces := PackedVector3Array()
	for j in n:
		for i in n:
			var x := x0 + i * step
			var z := z0 + j * step
			if x < -500.0 or x > 420.0 or z < -560.0 or z > 300.0:
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
	# fine grid near the site for the swell, coarse plane beyond
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
	# shallow-water floor so mechs wade instead of sinking
	Geo.box_collider(root, Vector3(400, Site.WATER_Y - 3.6 - 2.0, -100), Vector3(2400, 4, 2400))
	# soft bounds: an invisible ring around the training area
	for k in 24:
		var a := TAU * k / 24.0
		var c := Vector3(-40 + cos(a) * 520.0, 60, -60 + sin(a) * 520.0)
		Geo.box_collider(root, c, Vector3(4, 200, 150), Basis(Vector3.UP, -a))

# ------------------------------------------------------------------ apron, quay and slipway
static func apron(root: Node3D, markings: Texture2D) -> void:
	var conc := Mats.concrete("Apron", Color(0.62, 0.6, 0.57), 0.45)
	conc.set_shader_parameter("markings", markings)
	conc.set_shader_parameter("markings_rect", Vector4(Site.APRON_X0, Site.APRON_Z0, Site.APRON_X1 - Site.APRON_X0, Site.APRON_Z1 - Site.APRON_Z0))
	conc.set_shader_parameter("use_markings", 1.0)
	var wall := Mats.concrete("QuayWall", Color(0.55, 0.54, 0.5), 0.0)
	var g := Geo.new()
	var y := 0.0
	var base := -9.0
	# apron top in strips so the slipway corridor can be cut out
	var strips := [[Site.APRON_Z0, Site.SLIP_Z0, Site.APRON_X1], [Site.SLIP_Z0, Site.SLIP_Z1, Site.SLIP_X0], [Site.SLIP_Z1, Site.APRON_Z1, Site.APRON_X1 - 12.0]]
	for s in strips:
		var z0: float = s[0]
		var z1: float = s[1]
		var xe: float = s[2]
		var steps := int((z1 - z0) / 12.0) + 1
		for k in steps:
			var za := lerpf(z0, z1, float(k) / steps)
			var zb := lerpf(z0, z1, float(k + 1) / steps)
			var xs := 18.0
			var x := Site.APRON_X0
			while x < xe - 0.01:
				var xb := minf(x + xs, xe)
				g.quad(conc, Vector3(x, y, za), Vector3(xb, y, za), Vector3(xb, y, zb), Vector3(x, y, zb), Vector3.UP, 8.0)
				x = xb
		# seaward face of the quay / apron edge
		g.quad(wall, Vector3(xe, base, z0), Vector3(xe, base, z1), Vector3(xe, y, z1), Vector3(xe, y, z0), Vector3.RIGHT, 6.0)
		Geo.box_collider(root, Vector3((Site.APRON_X0 + xe) * 0.5, y - 4.0, (z0 + z1) * 0.5), Vector3(xe - Site.APRON_X0, 8.0, z1 - z0))
	# slipway corridor side walls
	for zz: float in [Site.SLIP_Z0, Site.SLIP_Z1]:
		var n := Vector3(0, 0, 1) if zz == Site.SLIP_Z0 else Vector3(0, 0, -1)
		var steps2 := 10
		for k in steps2:
			var xa := lerpf(Site.SLIP_X0, Site.APRON_X1 if zz == Site.SLIP_Z0 else Site.APRON_X1 - 12.0, float(k) / steps2)
			var xb := lerpf(Site.SLIP_X0, Site.APRON_X1 if zz == Site.SLIP_Z0 else Site.APRON_X1 - 12.0, float(k + 1) / steps2)
			g.quad(wall, Vector3(xa, Site.slip_y(xa) - 0.2, zz), Vector3(xb, Site.slip_y(xb) - 0.2, zz), Vector3(xb, y, zz), Vector3(xa, y, zz), -n, 6.0)
	# south edge drops to the rocks
	g.quad(wall, Vector3(Site.APRON_X0, base, Site.APRON_Z1), Vector3(Site.APRON_X1 - 12.0, base, Site.APRON_Z1), Vector3(Site.APRON_X1 - 12.0, y, Site.APRON_Z1), Vector3(Site.APRON_X0, y, Site.APRON_Z1), Vector3.BACK, 6.0)
	g.commit(root, "Apron", true, false)

	# slipway: grooved ramp down into the water, with kerbs
	var slip := Mats.concrete("Slipway", Color(0.58, 0.56, 0.52), 0.8, 1.6)
	var gs := Geo.new()
	var segs := 16
	for k in segs:
		var xa := lerpf(Site.SLIP_X0, Site.SLIP_X1, float(k) / segs)
		var xb := lerpf(Site.SLIP_X0, Site.SLIP_X1, float(k + 1) / segs)
		gs.quad(slip, Vector3(xa, Site.slip_y(xa), Site.SLIP_Z0), Vector3(xb, Site.slip_y(xb), Site.SLIP_Z0), Vector3(xb, Site.slip_y(xb), Site.SLIP_Z1), Vector3(xa, Site.slip_y(xa), Site.SLIP_Z1), Vector3.UP, 6.0)
	for zz: float in [Site.SLIP_Z0 - 1.2, Site.SLIP_Z1]:
		for k in segs:
			var xa := lerpf(Site.SLIP_X0, Site.SLIP_X1, float(k) / segs)
			var xb := lerpf(Site.SLIP_X0, Site.SLIP_X1, float(k + 1) / segs)
			var c := Vector3((xa + xb) * 0.5, (Site.slip_y(xa) + Site.slip_y(xb)) * 0.5 + 0.4, zz + 0.6)
			var dirv := Vector3(xb - xa, Site.slip_y(xb) - Site.slip_y(xa), 0)
			gs.box(wall, c, Vector3(dirv.length() + 0.05, 1.4, 1.2), Basis(dirv.normalized(), Vector3.UP.rotated(Vector3.BACK, atan2(dirv.y, dirv.x)), Vector3.BACK))
	gs.commit(root, "Slipway", true, false)
	var mid := Vector3((Site.SLIP_X0 + Site.SLIP_X1) * 0.5, (0.0 + Site.SLIP_BOTTOM) * 0.5 - 0.5, (Site.SLIP_Z0 + Site.SLIP_Z1) * 0.5)
	var dir := Vector3(Site.SLIP_X1 - Site.SLIP_X0, Site.SLIP_BOTTOM, 0)
	var basis := Basis(dir.normalized(), Vector3(0, 1, 0).rotated(Vector3.BACK, atan2(dir.y, dir.x)), Vector3.BACK)
	Geo.box_collider(root, mid, Vector3(dir.length(), 1.0, Site.SLIP_Z1 - Site.SLIP_Z0), basis)

## The quay edge: interlocking concrete blocks, bollards, ladders, fenders and lamp posts.
static func quay(root: Node3D) -> void:
	var blk := Mats.concrete("QuayBlocks", Color(0.66, 0.64, 0.6), 0.1)
	var g := Geo.new()
	var z := Site.APRON_Z0 + 2.0
	var k := 0
	while z < Site.SLIP_Z0 - 3.0:
		# alternating capping blocks along the edge, like the concept's breakwater
		var tall := k % 3 == 0
		var w := 5.0 if tall else 3.2
		g.box(blk, Vector3(Site.APRON_X1 - 1.4, (1.6 if tall else 0.8), z + w * 0.5), Vector3(2.8, 3.2 if tall else 1.6, w - 0.3))
		if tall:
			g.box(Mats.yellow(), Vector3(Site.APRON_X1 - 1.4, 3.25, z + w * 0.5), Vector3(2.9, 0.12, w - 0.2))
		else:
			# bollard
			g.cylinder(Mats.dark(), Vector3(Site.APRON_X1 - 3.6, 0, z + w * 0.5), Vector3(Site.APRON_X1 - 3.6, 0.9, z + w * 0.5), 0.35, 12)
			g.cylinder(Mats.dark(), Vector3(Site.APRON_X1 - 3.6, 0.9, z + w * 0.5), Vector3(Site.APRON_X1 - 3.6, 1.05, z + w * 0.5), 0.5, 12)
		# rubber fenders on the face
		if k % 2 == 0:
			g.box(Mats.rubber(), Vector3(Site.APRON_X1 + 0.35, -1.8, z + w * 0.5), Vector3(0.7, 2.6, 1.2))
		z += w
		k += 1
		if tall:
			Geo.box_collider(root, Vector3(Site.APRON_X1 - 1.4, 1.6, z - w * 0.5), Vector3(2.8, 3.2, w - 0.3))
	# ladders down to the water
	for lz: float in [-150.0, -70.0, -10.0]:
		for rung in 10:
			var yy := -0.2 - rung * 0.4
			g.beam(Mats.steel(), Vector3(Site.APRON_X1 + 0.25, yy, lz - 0.35), Vector3(Site.APRON_X1 + 0.25, yy, lz + 0.35), 0.06)
		for s: float in [-0.4, 0.4]:
			g.beam(Mats.steel(), Vector3(Site.APRON_X1 + 0.25, 1.0, lz + s), Vector3(Site.APRON_X1 + 0.25, -4.2, lz + s), 0.08)
	g.commit(root, "Quay", true, false)
	# lamp posts along the quay and around the apron
	var lamps := Geo.new()
	var pts := []
	for lz in range(int(Site.APRON_Z0) + 12, int(Site.SLIP_Z0) - 6, 28):
		pts.append(Vector3(Site.APRON_X1 - 5.5, 0, lz))
	for lx in range(-60, 40, 30):
		pts.append(Vector3(lx, 0, Site.APRON_Z1 - 3))
	for p in pts:
		lamp_post(lamps, p)
		var l := OmniLight3D.new()
		l.position = p + Vector3(0, 10.4, 0)
		l.light_color = Color(1.0, 0.85, 0.65)
		l.light_energy = 0.0   # switched on at dusk by the time-of-day controller
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

## Orange floats on a sagging rope, fencing the training shallows.
static func buoys(root: Node3D) -> void:
	var g := Geo.new()
	var path := [Vector3(70, 0, 50), Vector3(110, 0, 70), Vector3(160, 0, 85), Vector3(215, 0, 92), Vector3(265, 0, 120)]
	var path2 := [Vector3(70, 0, 16), Vector3(125, 0, -15), Vector3(190, 0, -30), Vector3(260, 0, -36)]
	for line in [path, path2]:
		for i in line.size() - 1:
			var a: Vector3 = line[i]
			var b: Vector3 = line[i + 1]
			var nseg := int(a.distance_to(b) / 5.0)
			var prev := a + Vector3(0, Site.WATER_Y + 0.3, 0)
			for s in range(1, nseg + 1):
				var t := float(s) / nseg
				var p := a.lerp(b, t) + Vector3(0, Site.WATER_Y + 0.3 - sin(t * PI) * 0.15, 0)
				g.beam(Mats.dark(), prev, p, 0.05)
				var big := s % 3 == 0
				var r := 0.55 if big else 0.32
				g.cylinder(Mats.orange(), p - Vector3(0, r * 0.6, 0), p + Vector3(0, r * 0.6, 0), r, 12, r, true, false)
				prev = p
	g.commit(root, "BuoyLine", true, false)
