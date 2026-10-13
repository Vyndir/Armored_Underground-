class_name BuildProps
extends RefCounted
## Set dressing for the training hangar: the concrete door portal from the
## concept, container stacks, mobile light towers, flags and a windsock, a quay
## crane with a moored workboat, barrels, cones, barriers, signage, and inside
## the hall: hanging league banners, a leg on a stand and a welding station.

static var rng := RandomNumberGenerator.new()

static func build(root: Node3D, art: Dictionary) -> Dictionary:
	rng.seed = 41
	var out := {}
	portal(root)
	containers(root)
	out["light_towers"] = light_towers(root)
	out["flags"] = flags(root, art)
	quay_crane(root)
	out["workboat"] = workboat(root)
	clutter(root)
	signs(root, art)
	out["weld"] = interior_extras(root, art)
	return out

## Massive concrete frame around the hangar doors, with fins along the facade.
static func portal(root: Node3D) -> void:
	var g := Geo.new()
	var conc := Mats.concrete("PortalConcrete", Color(0.66, 0.64, 0.6), 0.0)
	var x := Site.HANGAR_X1
	var zc := (Site.DOOR_Z0 + Site.DOOR_Z1) * 0.5
	# side piers, stepped like the concept's heavy door frame
	for side: float in [-1.0, 1.0]:
		var z := zc + side * ((Site.DOOR_Z1 - Site.DOOR_Z0) * 0.5 + 4.6)
		g.box(conc, Vector3(x + 2.0, 15.5, z), Vector3(5.0, 31.0, 5.6))
		g.box(conc, Vector3(x + 4.9, 13.0, z), Vector3(1.0, 26.0, 4.0))
		g.box(Mats.hazard(), Vector3(x + 5.45, 1.6, z), Vector3(0.12, 3.2, 4.0))
		for k in 6:
			g.box(Mats.dark(), Vector3(x + 4.5, 4.0 + k * 4.2, z - side * 2.85), Vector3(0.6, 0.12, 0.3))
		Geo.box_collider(root, Vector3(x + 2.6, 15.5, z), Vector3(6.2, 31.0, 5.6))
	# lintel: deep beam over the opening with a recessed light slot
	g.box(conc, Vector3(x + 2.6, 29.0, zc), Vector3(6.2, 6.0, Site.DOOR_Z1 - Site.DOOR_Z0 + 14.8))
	g.box(Mats.lamp(), Vector3(x + 5.72, 26.15, zc), Vector3(0.05, 0.25, Site.DOOR_Z1 - Site.DOOR_Z0 + 6.0))
	g.box(Mats.dark(), Vector3(x + 5.0, 32.4, zc), Vector3(1.6, 0.8, Site.DOOR_Z1 - Site.DOOR_Z0 + 15.0))
	# concrete fins and weathering on the clad walls either side
	for z in [Site.HANGAR_Z0 + 4.0, Site.HANGAR_Z0 + 10.0, Site.HANGAR_Z1 - 4.0]:
		g.box(conc, Vector3(x + 1.0, 14.5, z), Vector3(2.0, 29.0, 1.4))
	g.commit(root, "Portal", true, false)

static func container_box(g: Geo, c: Vector3, yaw: float, color: Color, long := true) -> void:
	var b := Basis(Vector3.UP, yaw)
	var len := 12.2 if long else 6.1
	var m := Mats.corrugated("Box%s" % color.to_html(false), color, 20)
	g.box(m, c + Vector3(0, 1.3, 0), Vector3(2.45, 2.6, len), b)
	for e: float in [-1.0, 1.0]:
		var end := c + b * Vector3(0, 1.3, e * (len * 0.5 + 0.02))
		g.box(Mats.dark(), end, Vector3(2.5, 2.65, 0.06), b)
		for bar: float in [-0.6, -0.2, 0.2, 0.6]:
			g.box(Mats.steel(), end + b * Vector3(bar, 0, e * 0.05), Vector3(0.06, 2.4, 0.06), b)
	g.box(Mats.dark(), c + Vector3(0, 2.62, 0), Vector3(2.5, 0.06, len + 0.05), b)

static func containers(root: Node3D) -> void:
	var g := Geo.new()
	var cols := [Color(0.55, 0.12, 0.08), Color(0.1, 0.24, 0.42), Color(0.16, 0.36, 0.24), Color(0.62, 0.45, 0.1), Color(0.48, 0.5, 0.52), Color(0.85, 0.85, 0.82)]
	# a stacked block on the north apron, staggered like a working yard
	for row in 3:
		for col in 6:
			var c := Vector3(-150.0 + col * 2.7, 0, -165.0 + row * 13.0)
			var h := 1 + rng.randi() % 3
			for lvl in h:
				container_box(g, c + Vector3(0, lvl * 2.62, 0), 0.0, cols[rng.randi() % cols.size()])
			Geo.box_collider(root, c + Vector3(0, h * 1.31, 0), Vector3(2.5, h * 2.62, 12.2))
	# a few loose units near the quay and slipway
	for spec in [[Vector3(20, 0, -175), 0.3], [Vector3(26, 0, -150), 1.57], [Vector3(-40, 0, 58), 0.1], [Vector3(-34, 0, 62), 0.2]]:
		var c: Vector3 = spec[0]
		container_box(g, c, spec[1], cols[rng.randi() % cols.size()])
		Geo.box_collider(root, c + Vector3(0, 1.3, 0), Vector3(2.5, 2.6, 12.2), Basis(Vector3.UP, spec[1]))
	g.commit(root, "Containers", true, false)

static func light_towers(root: Node3D) -> Array:
	var out := []
	var g := Geo.new()
	for spec in [[Vector3(-30, 0, -150), 0.4], [Vector3(30, 0, -100), 2.4], [Vector3(-6, 0, 8), -0.6], [Vector3(-66, 0, 30), 1.0]]:
		var p: Vector3 = spec[0]
		var b := Basis(Vector3.UP, spec[1])
		g.box(Mats.yellow(), p + Vector3(0, 1.0, 0), Vector3(2.2, 1.4, 3.6), b)
		for wz: float in [-1.0, 1.0]:
			g.cylinder(Mats.rubber(), p + b * Vector3(-1.15, 0.45, wz), p + b * Vector3(1.15, 0.45, wz), 0.45, 12)
		g.beam(Mats.steel(), p + Vector3(0, 1.7, 0), p + Vector3(0, 11.0, 0), 0.25)
		var head := p + Vector3(0, 11.2, 0)
		g.box(Mats.dark(), head, Vector3(3.0, 1.2, 0.4), b)
		for k in 4:
			g.box(Mats.lamp(), head + b * Vector3(-1.1 + k * 0.73, 0, 0.21), Vector3(0.6, 0.9, 0.04), b)
		for s: float in [-1.0, 1.0]:
			g.beam(Mats.steel(), p + b * Vector3(s * 1.8, 0.1, 2.4), p + Vector3(0, 1.5, 0), 0.08)
		Geo.box_collider(root, p + Vector3(0, 1.0, 0), Vector3(2.4, 2.0, 3.8), b)
		var l := SpotLight3D.new()
		l.light_energy = 0.0
		l.spot_range = 45
		l.spot_angle = 45
		l.light_color = Color(1.0, 0.95, 0.85)
		l.add_to_group("night_lights")
		root.add_child(l)
		l.global_position = head
		l.global_basis = b * Basis(Vector3.RIGHT, deg_to_rad(-35))
		out.append(l)
	g.commit(root, "LightTowers", true, false)
	return out

## League flags along the hangar front and a windsock on the quay, all moving in the breeze.
static func flags(root: Node3D, art: Dictionary) -> Array:
	var out := []
	var g := Geo.new()
	var mat := ShaderMaterial.new()
	mat.shader = load("res://shaders/flag.gdshader")
	mat.set_shader_parameter("tex", art.get("flag"))
	for k in 5:
		var p := Vector3(-66.0, 0, -122.0 + k * 6.0)
		g.cylinder(Mats.white(), p, p + Vector3(0, 16, 0), 0.14, 8, 0.08)
		g.cylinder(Mats.steel(), p + Vector3(0, 16, 0), p + Vector3(0, 16.3, 0), 0.22, 8)
		var cloth := MeshInstance3D.new()
		var pm := PlaneMesh.new()
		pm.size = Vector2(5.0, 3.0)
		pm.subdivide_width = 24
		pm.subdivide_depth = 8
		pm.orientation = PlaneMesh.FACE_Z
		cloth.mesh = pm
		cloth.material_override = mat
		root.add_child(cloth)
		cloth.position = p + Vector3(2.5, 14.2, 0)
		out.append(cloth)
	# windsock
	var ws := Vector3(36, 0, -110)
	g.cylinder(Mats.steel(), ws, ws + Vector3(0, 9, 0), 0.12, 8)
	g.cylinder(Mats.dark(), ws + Vector3(0, 9, 0), ws + Vector3(0.6, 9, 0), 0.5, 12, 0.5, false)
	var sock := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.48
	cm.bottom_radius = 0.18
	cm.height = 3.6
	cm.radial_segments = 12
	cm.rings = 6
	sock.mesh = cm
	sock.material_override = Mats.orange()
	root.add_child(sock)
	sock.position = ws + Vector3(2.4, 9, 0)
	sock.rotation_degrees = Vector3(0, 0, 85)
	sock.name = "Windsock"
	out.append(sock)
	g.commit(root, "FlagPoles", true, false)
	return out

## Knuckle-boom crane on the north quay and its pedestal.
static func quay_crane(root: Node3D) -> void:
	var g := Geo.new()
	var p := Vector3(34, 0, -40)
	g.cylinder(Mats.yellow(), p, p + Vector3(0, 4, 0), 1.6, 16)
	g.cylinder(Mats.dark(), p + Vector3(0, 4, 0), p + Vector3(0, 4.6, 0), 1.9, 16)
	g.box(Mats.yellow(), p + Vector3(-0.5, 5.8, 0), Vector3(3.2, 2.4, 2.6))
	g.box(Mats.glass(), p + Vector3(-0.5, 6.2, 1.31), Vector3(2.0, 1.2, 0.05))
	g.beam(Mats.yellow(), p + Vector3(0, 6.5, 0), p + Vector3(9, 15, 0), 1.0)
	g.beam(Mats.yellow(), p + Vector3(9, 15, 0), p + Vector3(17, 11, 0), 0.8)
	g.beam(Mats.dark(), p + Vector3(17, 11, 0), p + Vector3(17, 3, 0), 0.06)
	g.box(Mats.yellow(), p + Vector3(17, 2.6, 0), Vector3(0.8, 0.9, 0.6))
	Geo.box_collider(root, p + Vector3(0, 3.5, 0), Vector3(3.6, 7, 3.6))
	g.commit(root, "QuayCrane", true, false)

static func workboat(root: Node3D) -> Node3D:
	var holder := Node3D.new()
	holder.name = "Workboat"
	root.add_child(holder)
	var g := Geo.new()
	var len := 22.0
	var w := 7.0
	var outline := PackedVector2Array([Vector2(-w / 2, -len / 2), Vector2(w / 2, -len / 2), Vector2(w / 2, len * 0.22), Vector2(0, len / 2), Vector2(-w / 2, len * 0.22)])
	g.prism(Mats.painted("HullOrange", Color(0.85, 0.32, 0.08), 0.5, 0.2), outline, -1.2, 1.4, Mats.lit("DeckGrey", Color(0.4, 0.42, 0.43), 0.8), 4.0, false)
	g.box(Mats.white(), Vector3(0, 3.0, -3), Vector3(5.0, 3.2, 6.0), Basis(), 4.0, [], false)
	g.box(Mats.glass(), Vector3(0, 3.6, -3), Vector3(5.05, 1.2, 5.0), Basis(), 4.0, [], false)
	g.cylinder(Mats.steel(), Vector3(0, 4.6, -4), Vector3(0, 8.5, -4), 0.12, 6, -1.0, true, false)
	g.box(Mats.glow("NavGreen", Color(0.2, 1.0, 0.3), 5.0), Vector3(2.55, 2.0, 2), Vector3(0.1, 0.3, 0.3), Basis(), 1.0, [], false)
	g.box(Mats.glow("NavRed", Color(1.0, 0.15, 0.1), 5.0), Vector3(-2.55, 2.0, 2), Vector3(0.1, 0.3, 0.3), Basis(), 1.0, [], false)
	for k in 5:
		g.cylinder(Mats.rubber(), Vector3(w / 2 + 0.3, 0.6, -8 + k * 3.5), Vector3(w / 2 + 0.3, 0.6, -6.9 + k * 3.5), 0.45, 10, -1.0, true, false)
	g.commit(holder, "Hull", true, false)
	holder.position = Vector3(Site.APRON_X1 + w * 0.5 + 0.9, Site.WATER_Y, -95)
	return holder

static func clutter(root: Node3D) -> void:
	var g := Geo.new()
	var drum_cols := [Color(0.12, 0.3, 0.62), Color(0.9, 0.65, 0.1), Color(0.55, 0.1, 0.08), Color(0.25, 0.27, 0.28)]
	# barrel clusters
	for c in [Vector3(-60, 0, -128), Vector3(-34, 0, -160), Vector3(12, 0, -168), Vector3(-72, 0, 44)]:
		for k in 9:
			var o := Vector3((k % 3) * 1.3, 0, int(k / 3) * 1.3)
			if rng.randf() < 0.15:
				continue
			var col: Color = drum_cols[rng.randi() % drum_cols.size()]
			var m := Mats.painted("Drum%s" % col.to_html(false), col, 0.45, 0.3)
			g.cylinder(m, c + o, c + o + Vector3(0, 1.25, 0), 0.6, 14)
			g.cylinder(Mats.dark(), c + o + Vector3(0, 0.42, 0), c + o + Vector3(0, 0.48, 0), 0.62, 14)
			g.cylinder(Mats.dark(), c + o + Vector3(0, 0.84, 0), c + o + Vector3(0, 0.9, 0), 0.62, 14)
		Geo.box_collider(root, c + Vector3(1.3, 0.65, 1.3), Vector3(4, 1.3, 4))
	# pallets with wrapped loads
	for c in [Vector3(-56, 0, -140), Vector3(-52, 0, -140), Vector3(-48, 0, -140), Vector3(4, 0, -160), Vector3(8, 0, -160)]:
		g.box(Mats.lit("Pallet", Color(0.5, 0.38, 0.24), 0.9), c + Vector3(0, 0.08, 0), Vector3(1.2, 0.16, 1.0))
		g.box(Mats.lit("Wrap", Color(0.82, 0.84, 0.86), 0.3, 0.0), c + Vector3(0, 0.75, 0), Vector3(1.1, 1.2, 0.95))
	# traffic cones marking the course and the slipway head
	var cone_m := Mats.orange()
	for p in [Vector3(-46, 0, -80), Vector3(-46, 0, -68), Vector3(-20, 0, -110), Vector3(-14, 0, -118), Vector3(-8, 0, -126),
			Vector3(8, 0, -66), Vector3(8, 0, -54), Vector3(28, 0, -66), Vector3(28, 0, -54), Vector3(0, 0, 14), Vector3(0, 0, 52)]:
		g.cylinder(Mats.dark(), p, p + Vector3(0, 0.08, 0), 0.5, 4)
		g.cylinder(cone_m, p + Vector3(0, 0.08, 0), p + Vector3(0, 1.0, 0), 0.36, 12, 0.06)
		g.cylinder(Mats.white(), p + Vector3(0, 0.5, 0), p + Vector3(0, 0.64, 0), 0.25, 12, 0.21, false)
	# concrete barriers lining the yard
	for spec in [[Vector3(-90, 0, 40), 0.0], [Vector3(-86, 0, 40), 0.0], [Vector3(-82, 0, 40), 0.0], [Vector3(10, 0, -185), 1.57], [Vector3(10, 0, -181), 1.57]]:
		var p: Vector3 = spec[0]
		var b := Basis(Vector3.UP, spec[1])
		g.box(Mats.concrete("Barrier", Color(0.7, 0.68, 0.64), 0.0), p + Vector3(0, 0.4, 0), Vector3(3.8, 0.8, 0.9), b)
		g.box(Mats.concrete("Barrier", Color(0.7, 0.68, 0.64), 0.0), p + Vector3(0, 1.0, 0), Vector3(3.8, 0.6, 0.45), b)
		g.box(Mats.hazard(), p + Vector3(0, 1.15, 0) + b * Vector3(0, 0, 0.24), Vector3(3.4, 0.25, 0.02), b)
		Geo.box_collider(root, p + Vector3(0, 0.65, 0), Vector3(3.8, 1.3, 0.9), b)
	# hoses and cables snaking out of the hangar
	for k in 3:
		var a := Vector3(-80, 0.12, -60 - k * 3.0)
		var prev := a
		for s in range(1, 14):
			var p := a + Vector3(s * 3.0, 0.12, sin(s * 0.7 + k) * 1.8)
			g.beam(Mats.rubber() if k != 1 else Mats.yellow(), prev, p, 0.18)
			prev = p
	g.commit(root, "Clutter", true, false)

static func signs(root: Node3D, art: Dictionary) -> void:
	for spec in [[Vector3(-40, 0, -60), "sign_speed", PI * 0.5], [Vector3(-2, 0, 12), "sign_slip", 0.0], [Vector3(-70, 0, -126), "sign_crossing", PI * 0.5]]:
		var g := Geo.new()
		var p: Vector3 = spec[0]
		g.cylinder(Mats.steel(), p, p + Vector3(0, 3.4, 0), 0.07, 8)
		g.cylinder(Mats.steel(), p + Vector3(1.6, 0, 0), p + Vector3(1.6, 3.4, 0), 0.07, 8)
		g.commit(root, "SignPosts", true, false)
		var board := MeshInstance3D.new()
		var q := QuadMesh.new()
		q.size = Vector2(2.4, 1.4)
		board.mesh = q
		var m := StandardMaterial3D.new()
		m.albedo_texture = art.get(spec[1])
		m.roughness = 0.6
		m.cull_mode = BaseMaterial3D.CULL_DISABLED
		board.material_override = m
		root.add_child(board)
		board.position = p + Vector3(0.8, 2.7, 0.0)
		board.rotation.y = spec[2]

## Banners hung from the trusses, a mech leg on an engine stand, a welding bay.
static func interior_extras(root: Node3D, art: Dictionary) -> Vector3:
	var bm := StandardMaterial3D.new()
	bm.albedo_texture = art.get("banner")
	bm.cull_mode = BaseMaterial3D.CULL_DISABLED
	bm.roughness = 0.8
	for x: float in [-148.0, -126.0, -104.0]:
		var b := MeshInstance3D.new()
		var q := QuadMesh.new()
		q.size = Vector2(6, 10.5)
		b.mesh = q
		b.material_override = bm
		root.add_child(b)
		b.position = Vector3(x, 20.5, Site.HANGAR_Z0 + 3.0)
	var g := Geo.new()
	var stand := Vector3(-150, 0, -60)
	g.box(Mats.yellow(), stand + Vector3(0, 0.3, 0), Vector3(3.0, 0.6, 2.0))
	g.beam(Mats.yellow(), stand + Vector3(0, 0.6, 0), stand + Vector3(0, 3.4, 0), 0.4)
	var grey := Mats.painted("MechGrey", Color(0.45, 0.47, 0.46), 0.45, 0.55)
	g.box(grey, stand + Vector3(0, 4.4, 0), Vector3(1.6, 2.4, 1.4), Basis(Vector3.RIGHT, 0.2))
	g.cylinder(Mats.dark(), stand + Vector3(0, 3.4, -0.4), stand + Vector3(0, 1.6, 1.6), 0.5, 12)
	g.box(grey, stand + Vector3(0, 1.4, 2.2), Vector3(1.4, 2.6, 1.2), Basis(Vector3.RIGHT, -0.5))
	g.box(Mats.dark(), stand + Vector3(0, 0.3, 3.0), Vector3(1.6, 0.6, 2.2))
	# welding bay: a worker's table, a plate on it, a screen
	var weld := Vector3(-120, 0, -112)
	g.box(Mats.steel(), weld + Vector3(0, 0.5, 0), Vector3(3.0, 1.0, 1.6))
	g.box(grey, weld + Vector3(0, 1.1, 0), Vector3(2.4, 0.2, 1.2))
	g.box(Mats.lit("WeldScreen", Color(0.25, 0.08, 0.05), 0.6), weld + Vector3(2.2, 1.3, 0), Vector3(0.1, 2.6, 2.2))
	g.commit(root, "InteriorExtras", true, false)
	Geo.box_collider(root, stand + Vector3(0, 2.5, 1), Vector3(3, 5, 5))
	return weld + Vector3(0, 1.3, 0)
