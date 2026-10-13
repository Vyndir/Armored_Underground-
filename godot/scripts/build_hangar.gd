class_name BuildHangar
extends RefCounted
## The waterfront training hangar: shell, door header with floodlights, league
## banner, roof plant, interior steel frame and trusses, an overhead bridge
## crane, maintenance gantries, workbenches, terminals, workers and the yard
## outside (cabins, crates, a pickup, a forklift, generators).

const X0 := Site.HANGAR_X0
const X1 := Site.HANGAR_X1
const Z0 := Site.HANGAR_Z0
const Z1 := Site.HANGAR_Z1
const H := Site.HANGAR_H
const EAVE := 29.0

static var rng := RandomNumberGenerator.new()

static func build(root: Node3D, art: Dictionary) -> Dictionary:
	rng.seed = 3
	var info := {}
	shell(root, art)
	roof(root)
	interior_frame(root)
	info["crane"] = crane(root)
	gantry(root, Site.SPAWN, true)
	gantry(root, Vector3(-138, 0, -102), false)
	gantry(root, Vector3(-138, 0, -44), false)
	workshop(root, art)
	info["workers"] = workers(root)
	yard(root)
	annex(root)
	info["terminal"] = Vector3(-92.0, 0.0, -56.0)
	return info

# ------------------------------------------------------------------ shell
static func shell(root: Node3D, art: Dictionary) -> void:
	var cladding := Mats.corrugated("Cladding", Color(0.8, 0.8, 0.78), 14)
	var cladding_dark := Mats.corrugated("CladdingDark", Color(0.42, 0.44, 0.46), 14)
	var plinth := Mats.concrete("Plinth", Color(0.55, 0.54, 0.52), 0.0)
	var g := Geo.new()
	# plinth all round
	g.aabb(plinth, Vector3(X0, 0, Z0), Vector3(X1, 3.0, Z0 + 0.8))
	g.aabb(plinth, Vector3(X0, 0, Z1 - 0.8), Vector3(X1, 3.0, Z1))
	g.aabb(plinth, Vector3(X0, 0, Z0), Vector3(X0 + 0.8, 3.0, Z1))
	# clad walls: north, south, west
	for spec in [[Vector3(X0, 3, Z0), Vector3(X1, EAVE, Z0 + 0.4)], [Vector3(X0, 3, Z1 - 0.4), Vector3(X1, EAVE, Z1)], [Vector3(X0, 3, Z0), Vector3(X0 + 0.4, EAVE, Z1)]]:
		g.aabb(cladding, spec[0], spec[1], 4.0)
	# east face: wall either side of the door and the header above it
	g.aabb(cladding, Vector3(X1 - 0.4, 0, Z0), Vector3(X1, EAVE, Site.DOOR_Z0 - 2.2), 4.0)
	g.aabb(cladding, Vector3(X1 - 0.4, 0, Site.DOOR_Z1 + 2.2), Vector3(X1, EAVE, Z1), 4.0)
	g.aabb(cladding_dark, Vector3(X1 - 0.4, Site.DOOR_H, Site.DOOR_Z0 - 2.2), Vector3(X1, EAVE, Site.DOOR_Z1 + 2.2), 4.0)
	# door jambs with hazard striping, and the header box with floodlights
	for jz: float in [Site.DOOR_Z0 - 1.1, Site.DOOR_Z1 + 1.1]:
		g.box(Mats.hazard(), Vector3(X1 + 0.6, Site.DOOR_H * 0.5, jz), Vector3(2.4, Site.DOOR_H, 2.2))
	g.box(Mats.painted("HeaderGrey", Color(0.62, 0.63, 0.63), 0.5, 0.4), Vector3(X1 + 1.6, Site.DOOR_H + 1.8, (Site.DOOR_Z0 + Site.DOOR_Z1) * 0.5), Vector3(4.4, 3.6, Site.DOOR_Z1 - Site.DOOR_Z0 + 6.0))
	for k in 9:
		var z := lerpf(Site.DOOR_Z0 + 2.0, Site.DOOR_Z1 - 2.0, k / 8.0)
		g.box(Mats.dark(), Vector3(X1 + 3.2, Site.DOOR_H - 0.3, z), Vector3(1.2, 0.6, 1.6))
		g.box(Mats.lamp(), Vector3(X1 + 3.2, Site.DOOR_H - 0.62, z), Vector3(1.0, 0.05, 1.4))
	# roller door rolled up inside, its bottom bar just visible
	g.cylinder(Mats.dark(), Vector3(X1 - 2.0, Site.DOOR_H - 1.6, Site.DOOR_Z0), Vector3(X1 - 2.0, Site.DOOR_H - 1.6, Site.DOOR_Z1), 1.5, 16)
	g.box(Mats.yellow(), Vector3(X1 - 0.9, Site.DOOR_H - 0.4, (Site.DOOR_Z0 + Site.DOOR_Z1) * 0.5), Vector3(0.4, 0.5, Site.DOOR_Z1 - Site.DOOR_Z0))
	# external steel pilasters
	for x in range(int(X0) + 6, int(X1), 11):
		for zz: float in [Z0 - 0.35, Z1 + 0.35]:
			g.box(Mats.steel(), Vector3(x, EAVE * 0.5, zz), Vector3(0.9, EAVE, 0.7))
	for z in range(int(Z0) + 6, int(Z1), 11):
		g.box(Mats.steel(), Vector3(X0 - 0.35, EAVE * 0.5, z), Vector3(0.7, EAVE, 0.9))
	# coping
	g.aabb(Mats.dark(), Vector3(X0 - 0.6, EAVE, Z0 - 0.6), Vector3(X1 + 0.6, EAVE + 0.6, Z0 + 0.4))
	g.aabb(Mats.dark(), Vector3(X0 - 0.6, EAVE, Z1 - 0.4), Vector3(X1 + 0.6, EAVE + 0.6, Z1 + 0.6))
	g.aabb(Mats.dark(), Vector3(X0 - 0.6, EAVE, Z0), Vector3(X0 + 0.4, EAVE + 0.6, Z1))
	g.aabb(Mats.dark(), Vector3(X1 - 0.4, EAVE, Z0), Vector3(X1 + 0.6, EAVE + 0.6, Z1))
	# ventilation louvres high on the side walls
	for x in range(int(X0) + 12, int(X1) - 6, 22):
		for zz: float in [Z0 - 0.15, Z1 + 0.15]:
			g.box(Mats.dark(), Vector3(x, 22, zz), Vector3(4.0, 2.0, 0.3))
			for s in 5:
				g.box(Mats.steel(), Vector3(x, 21.3 + s * 0.35, zz + (0.2 if zz > 0 or zz > Z0 else -0.2) * signf(zz - Z0)), Vector3(3.8, 0.08, 0.25))
	# personnel door and stair on the south wall
	g.box(Mats.navy(), Vector3(-100, 1.4, Z1 + 0.1), Vector3(1.8, 2.8, 0.2))
	g.box(Mats.lamp(), Vector3(-100, 3.3, Z1 + 0.35), Vector3(0.6, 0.15, 0.3))
	g.commit(root, "HangarShell", true, false)
	# colliders for the walls
	Geo.box_collider(root, Vector3((X0 + X1) * 0.5, EAVE * 0.5, Z0 + 0.3), Vector3(X1 - X0, EAVE, 1.2))
	Geo.box_collider(root, Vector3((X0 + X1) * 0.5, EAVE * 0.5, Z1 - 0.3), Vector3(X1 - X0, EAVE, 1.2))
	Geo.box_collider(root, Vector3(X0 + 0.3, EAVE * 0.5, (Z0 + Z1) * 0.5), Vector3(1.2, EAVE, Z1 - Z0))
	Geo.box_collider(root, Vector3(X1 - 0.2, EAVE * 0.5, (Z0 + Site.DOOR_Z0 - 2.2) * 0.5), Vector3(1.6, EAVE, Site.DOOR_Z0 - 2.2 - Z0))
	Geo.box_collider(root, Vector3(X1 - 0.2, EAVE * 0.5, (Z1 + Site.DOOR_Z1 + 2.2) * 0.5), Vector3(1.6, EAVE, Z1 - Site.DOOR_Z1 - 2.2))
	Geo.box_collider(root, Vector3(X1, (Site.DOOR_H + EAVE) * 0.5 + 0.4, (Site.DOOR_Z0 + Site.DOOR_Z1) * 0.5), Vector3(5, EAVE - Site.DOOR_H + 1, Site.DOOR_Z1 - Site.DOOR_Z0 + 6))
	for jz: float in [Site.DOOR_Z0 - 1.1, Site.DOOR_Z1 + 1.1]:
		Geo.box_collider(root, Vector3(X1 + 0.6, Site.DOOR_H * 0.5, jz), Vector3(2.4, Site.DOOR_H, 2.2))
	# the league banner and the bay numbers
	var banner := MeshInstance3D.new()
	var bq := QuadMesh.new()
	bq.size = Vector2(15, 26)
	banner.mesh = bq
	var bm := StandardMaterial3D.new()
	bm.albedo_texture = art.get("banner")
	bm.roughness = 0.75
	banner.material_override = bm
	banner.position = Vector3(X1 + 0.3, 15.5, (Z0 + Site.DOOR_Z0) * 0.5 - 1.0)
	banner.rotation.y = PI * 0.5
	root.add_child(banner)
	for spec in [[Vector3(X1 + 0.25, 7.0, Site.DOOR_Z1 + 10.5), PI * 0.5, Vector2(10, 7)], [Vector3((X0 + X1) * 0.5, 21.5, Z1 + 0.25), 0.0, Vector2(14, 10)]]:
		var num := MeshInstance3D.new()
		var nq := QuadMesh.new()
		nq.size = spec[2]
		num.mesh = nq
		var nm := StandardMaterial3D.new()
		nm.albedo_texture = art.get("num03")
		nm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
		nm.roughness = 0.6
		num.material_override = nm
		num.position = spec[0]
		num.rotation.y = spec[1]
		root.add_child(num)
	var sign := MeshInstance3D.new()
	var sq := QuadMesh.new()
	sq.size = Vector2(26, 3.4)
	sign.mesh = sq
	var sm := StandardMaterial3D.new()
	sm.albedo_texture = art.get("sign")
	sm.emission_enabled = true
	sm.emission_texture = art.get("sign")
	sm.emission_energy_multiplier = 0.35
	sign.material_override = sm
	sign.position = Vector3(X1 + 3.85, Site.DOOR_H + 1.8, (Site.DOOR_Z0 + Site.DOOR_Z1) * 0.5)
	sign.rotation.y = PI * 0.5
	root.add_child(sign)
	# floodlights over the apron and warm interior spill
	for k in 3:
		var sl := SpotLight3D.new()
		sl.position = Vector3(X1 + 3.4, Site.DOOR_H - 1.0, lerpf(Site.DOOR_Z0 + 6, Site.DOOR_Z1 - 6, k / 2.0))
		sl.rotation_degrees = Vector3(-62, 90, 0)
		sl.spot_range = 60
		sl.spot_angle = 50
		sl.light_energy = 0.0
		sl.light_color = Color(1.0, 0.9, 0.75)
		sl.add_to_group("night_lights")
		root.add_child(sl)

static func roof(root: Node3D) -> void:
	var g := Geo.new()
	var deck := Mats.painted("RoofDeck", Color(0.36, 0.37, 0.38), 0.8, 0.2)
	g.aabb(deck, Vector3(X0, EAVE - 0.3, Z0), Vector3(X1, EAVE, Z1), 6.0, ["-y"])
	# skylight strips
	for z in range(int(Z0) + 14, int(Z1) - 8, 18):
		g.aabb(Mats.glass(), Vector3(X0 + 8, EAVE, z - 1.6), Vector3(X1 - 8, EAVE + 0.6, z + 1.6))
		g.aabb(Mats.steel(), Vector3(X0 + 8, EAVE, z - 1.8), Vector3(X1 - 8, EAVE + 0.75, z - 1.6))
		g.aabb(Mats.steel(), Vector3(X0 + 8, EAVE, z + 1.6), Vector3(X1 - 8, EAVE + 0.75, z + 1.8))
	# rooftop plant: AC units, exhaust stacks, a water tank, an antenna mast and railings
	for spec: Vector3 in [Vector3(-150, EAVE, -110), Vector3(-138, EAVE, -110), Vector3(-126, EAVE, -110), Vector3(-150, EAVE, -40), Vector3(-104, EAVE, -38)]:
		g.box(Mats.white(), spec + Vector3(0, 1.4, 0), Vector3(8, 2.8, 4.5))
		for f in 2:
			g.cylinder(Mats.dark(), spec + Vector3(-1.9 + f * 3.8, 2.8, 0), spec + Vector3(-1.9 + f * 3.8, 3.0, 0), 1.6, 16)
		g.box(Mats.steel(), spec + Vector3(0, 0.15, 0), Vector3(8.6, 0.3, 5.0))
	for spec: Vector3 in [Vector3(-118, EAVE, -60), Vector3(-114, EAVE, -60), Vector3(-110, EAVE, -60)]:
		g.cylinder(Mats.steel(), spec, spec + Vector3(0, 7, 0), 0.6, 12)
		g.cylinder(Mats.dark(), spec + Vector3(0, 7, 0), spec + Vector3(0, 7.6, 0), 0.75, 12)
	var tank := Vector3(-95, EAVE, -105)
	for lx: float in [-2.5, 2.5]:
		for lz: float in [-2.5, 2.5]:
			g.beam(Mats.steel(), tank + Vector3(lx, 0, lz), tank + Vector3(lx * 0.8, 6, lz * 0.8), 0.3)
	g.cylinder(Mats.white(), tank + Vector3(0, 6, 0), tank + Vector3(0, 11, 0), 3.4, 20)
	g.cylinder(Mats.white(), tank + Vector3(0, 11, 0), tank + Vector3(0, 12.5, 0), 3.4, 20, 0.4)
	var mast := Vector3(-150, EAVE, -60)
	g.cylinder(Mats.steel(), mast, mast + Vector3(0, 22, 0), 0.35, 8, 0.15)
	for k in 6:
		g.beam(Mats.steel(), mast + Vector3(-1.5, 3 + k * 3, 0), mast + Vector3(1.5, 3 + k * 3, 0), 0.12)
	g.cylinder(Mats.white(), mast + Vector3(0.4, 15, 0), mast + Vector3(1.6, 15.4, 0), 1.2, 16, 0.4)
	g.box(Mats.glow("Beacon", Color(1.0, 0.15, 0.1), 8.0), mast + Vector3(0, 22.3, 0), Vector3(0.5, 0.5, 0.5))
	# perimeter guard rail
	for edge in [[Vector3(X0, EAVE + 0.6, Z0 + 0.2), Vector3(X1, EAVE + 0.6, Z0 + 0.2)], [Vector3(X0, EAVE + 0.6, Z1 - 0.2), Vector3(X1, EAVE + 0.6, Z1 - 0.2)], [Vector3(X1 - 0.2, EAVE + 0.6, Z0), Vector3(X1 - 0.2, EAVE + 0.6, Z1)]]:
		var a: Vector3 = edge[0]
		var b: Vector3 = edge[1]
		g.beam(Mats.yellow(), a + Vector3(0, 1.1, 0), b + Vector3(0, 1.1, 0), 0.08)
		g.beam(Mats.yellow(), a + Vector3(0, 0.55, 0), b + Vector3(0, 0.55, 0), 0.06)
		var n := int(a.distance_to(b) / 3.0)
		for k in n + 1:
			var p := a.lerp(b, float(k) / n)
			g.beam(Mats.yellow(), p, p + Vector3(0, 1.1, 0), 0.07)
	g.commit(root, "Roof", true, false)
	Geo.box_collider(root, Vector3((X0 + X1) * 0.5, EAVE - 0.15, (Z0 + Z1) * 0.5), Vector3(X1 - X0, 0.6, Z1 - Z0))

# ------------------------------------------------------------------ interior
static func interior_frame(root: Node3D) -> void:
	var g := Geo.new()
	var steel := Mats.painted("FrameBlue", Color(0.16, 0.22, 0.3), 0.45, 0.6)
	# columns along the side walls carrying crane rails
	for x in range(int(X0) + 6, int(X1), 11):
		for zz: float in [Z0 + 1.2, Z1 - 1.2]:
			g.box(steel, Vector3(x, 13, zz), Vector3(1.0, 26, 1.0))
			g.box(steel, Vector3(x, 20.6, zz + (1.0 if zz < 0 and zz < -70 else -1.0)), Vector3(0.8, 0.8, 2.0))
	for zz: float in [Z0 + 2.4, Z1 - 2.4]:
		g.box(Mats.dark(), Vector3((X0 + X1) * 0.5, 21.2, zz), Vector3(X1 - X0 - 2, 0.8, 0.6))
		g.box(Mats.steel(), Vector3((X0 + X1) * 0.5, 21.7, zz), Vector3(X1 - X0 - 2, 0.2, 0.25))
	# roof trusses spanning the hall
	for x in range(int(X0) + 6, int(X1), 11):
		var y0 := 26.0
		var y1 := EAVE - 0.4
		g.beam(steel, Vector3(x, y0, Z0 + 1), Vector3(x, y0, Z1 - 1), 0.5)
		g.beam(steel, Vector3(x, y1, Z0 + 1), Vector3(x, y1, Z1 - 1), 0.4)
		var n := 16
		for k in n:
			var za := lerpf(Z0 + 1, Z1 - 1, float(k) / n)
			var zb := lerpf(Z0 + 1, Z1 - 1, float(k + 1) / n)
			g.beam(steel, Vector3(x, y0, za), Vector3(x, y1, zb) if k % 2 == 0 else Vector3(x, y1, za), 0.22)
			g.beam(steel, Vector3(x, y1, za), Vector3(x, y0, zb) if k % 2 == 1 else Vector3(x, y0, za), 0.22)
		# high-bay lamps under every other truss
		for z in range(int(Z0) + 10, int(Z1) - 4, 16):
			g.beam(Mats.dark(), Vector3(x, y0, z), Vector3(x, y0 - 2.0, z), 0.05)
			g.cylinder(Mats.dark(), Vector3(x, y0 - 2.0, z), Vector3(x, y0 - 2.8, z), 0.4, 12, 1.2)
			g.cylinder(Mats.lamp(), Vector3(x, y0 - 2.82, z), Vector3(x, y0 - 2.86, z), 1.1, 12)
	g.commit(root, "Frame", true, false)
	# lights: a few shadowed spots plus soft fill
	var idx := 0
	for x in range(int(X0) + 17, int(X1), 22):
		for z in range(int(Z0) + 18, int(Z1) - 4, 32):
			var l := SpotLight3D.new()
			l.position = Vector3(x, 22.5, z)
			l.rotation_degrees = Vector3(-90, 0, 0)
			l.light_color = Color(1.0, 0.88, 0.72)
			l.light_energy = 6.0
			l.spot_range = 36.0
			l.spot_angle = 62.0
			l.shadow_enabled = idx % 2 == 0
			root.add_child(l)
			idx += 1
	var fill := OmniLight3D.new()
	fill.position = Vector3(-120, 12, -74)
	fill.omni_range = 70
	fill.light_energy = 1.2
	fill.light_color = Color(0.95, 0.9, 0.85)
	root.add_child(fill)

static func crane(root: Node3D) -> Node3D:
	var holder := Node3D.new()
	holder.name = "BridgeCrane"
	root.add_child(holder)
	var g := Geo.new()
	var y := 22.2
	g.box(Mats.yellow(), Vector3(0, y + 1.0, 0), Vector3(2.4, 2.4, Z1 - Z0 - 5.0))
	g.box(Mats.yellow(), Vector3(2.6, y + 1.0, 0), Vector3(2.4, 2.4, Z1 - Z0 - 5.0))
	for zz: float in [Z0 + 2.4 - (Z0 + Z1) * 0.5, Z1 - 2.4 - (Z0 + Z1) * 0.5]:
		g.box(Mats.yellow(), Vector3(1.3, y + 0.2, zz), Vector3(6.0, 1.6, 2.2))
		g.box(Mats.dark(), Vector3(1.3, y - 0.2, zz), Vector3(5.0, 0.6, 1.0))
	g.box(Mats.hazard(), Vector3(1.3, y + 2.6, 0), Vector3(5.2, 0.6, 6.0))
	g.commit(holder, "Bridge", true, false)
	var trolley := Node3D.new()
	trolley.name = "Trolley"
	holder.add_child(trolley)
	var gt := Geo.new()
	gt.box(Mats.dark(), Vector3(1.3, y + 2.8, 0), Vector3(5.0, 1.6, 4.0))
	gt.cylinder(Mats.steel(), Vector3(1.3, y + 2.0, -1.2), Vector3(1.3, y + 2.0, 1.2), 0.8, 16)
	for s: float in [-0.4, 0.4]:
		gt.beam(Mats.steel(), Vector3(1.3 + s, y + 1.5, 0), Vector3(1.3 + s, 12.0, 0), 0.05)
	gt.box(Mats.yellow(), Vector3(1.3, 11.6, 0), Vector3(1.4, 1.6, 0.8))
	gt.cylinder(Mats.steel(), Vector3(1.3, 10.8, 0), Vector3(1.3, 10.0, 0), 0.25, 8)
	gt.commit(trolley, "Hook", true, false)
	holder.position = Vector3(-116, 0, (Z0 + Z1) * 0.5)
	return holder

## Yellow service gantry around a mech bay: towers, three deck levels, stairs and rails.
static func gantry(root: Node3D, c: Vector3, open_front: bool) -> void:
	var g := Geo.new()
	var yel := Mats.yellow()
	var grate := Mats.lit("Grating", Color(0.25, 0.26, 0.27), 0.6, 0.7, Tex.tread(), 0.6, Vector3(2, 2, 2))
	for side: float in [-1.0, 1.0]:
		var tx := c.x - 1.0
		var tz := c.z + side * 7.5
		for px: float in [-2.5, 2.5]:
			for pz: float in [-1.6, 1.6]:
				g.box(yel, Vector3(tx + px, 6.5, tz + pz), Vector3(0.35, 13.0, 0.35))
		for ly: float in [3.6, 7.2, 10.8]:
			g.box(grate, Vector3(tx, ly, tz), Vector3(5.4, 0.2, 3.6))
			for pz: float in [-1.75, 1.75]:
				g.beam(yel, Vector3(tx - 2.6, ly + 1.1, tz + pz), Vector3(tx + 2.6, ly + 1.1, tz + pz), 0.07)
				g.beam(yel, Vector3(tx - 2.6, ly + 0.55, tz + pz), Vector3(tx + 2.6, ly + 0.55, tz + pz), 0.05)
			# cross bracing
			g.beam(yel, Vector3(tx - 2.5, ly - 3.4, tz - side * 1.6), Vector3(tx + 2.5, ly, tz - side * 1.6), 0.12)
		# stair flights up the outside face
		for k in 3:
			var y0 := k * 3.6
			var a := Vector3(tx - 2.4 + (0.0 if k % 2 == 0 else 4.8), y0, tz + side * 2.6)
			var b := Vector3(tx + 2.4 - (0.0 if k % 2 == 0 else 4.8), y0 + 3.6, tz + side * 2.6)
			g.beam(grate, a, b, 1.1, 0.12)
			g.beam(yel, a + Vector3(0, 1.0, side * 0.55), b + Vector3(0, 1.0, side * 0.55), 0.06)
		# service arms reaching toward the mech's torso
		g.beam(yel, Vector3(tx, 7.0, tz - side * 1.8), Vector3(c.x, 6.4, c.z + side * 2.4), 0.25)
		g.box(Mats.dark(), Vector3(c.x, 6.4, c.z + side * 2.4), Vector3(0.6, 0.6, 0.6))
		Geo.box_collider(root, Vector3(tx, 6.5, tz), Vector3(5.4, 13, 3.6))
	# rear walkway joining the towers at the top deck
	g.box(grate, Vector3(c.x - 4.2, 10.8, c.z), Vector3(2.0, 0.2, 15.0))
	g.beam(yel, Vector3(c.x - 5.2, 11.9, c.z - 7.5), Vector3(c.x - 5.2, 11.9, c.z + 7.5), 0.07)
	# floor clamps and the bay outline in hazard paint
	for side: float in [-1.0, 1.0]:
		g.box(Mats.hazard(), Vector3(c.x, 0.15, c.z + side * 1.3), Vector3(3.2, 0.3, 1.2))
	# hoses and umbilicals draped from the tower
	for k in 3:
		var a := Vector3(c.x - 1.0, 10.6 - k * 0.3, c.z + 6.0)
		var b := Vector3(c.x - 0.3, 5.5 + k * 0.4, c.z + 1.6)
		var prev := a
		for s in range(1, 9):
			var t := s / 8.0
			var p := a.lerp(b, t) + Vector3(0, -sin(t * PI) * 1.4, 0)
			g.beam(Mats.rubber() if k != 1 else Mats.orange(), prev, p, 0.12)
			prev = p
	g.commit(root, "Gantry", true, false)

static func workshop(root: Node3D, art: Dictionary) -> void:
	var g := Geo.new()
	var red := Mats.painted("ToolRed", Color(0.6, 0.08, 0.06), 0.45, 0.3)
	var bench_top := Mats.lit("BenchTop", Color(0.35, 0.3, 0.24), 0.7)
	# workbenches and tool chests along the north wall
	for x in range(-158, -88, 9):
		var z := Z0 + 3.0
		g.box(Mats.steel(), Vector3(x, 0.45, z), Vector3(4.0, 0.9, 1.6))
		g.box(bench_top, Vector3(x, 0.95, z), Vector3(4.2, 0.1, 1.8))
		g.box(red, Vector3(x + 2.6, 0.7, z), Vector3(1.0, 1.4, 1.0))
		g.box(Mats.dark(), Vector3(x, 2.4, Z0 + 1.0), Vector3(4.0, 2.6, 0.2))   # pegboard
		for t in 5:
			g.box(Mats.steel(), Vector3(x - 1.5 + t * 0.7, 2.6, Z0 + 1.15), Vector3(0.1, 0.6 + rng.randf() * 0.4, 0.08))
		Geo.box_collider(root, Vector3(x, 0.5, z), Vector3(4.2, 1.0, 1.8))
	# lockers on the west wall
	for z in range(-112, -38, 2):
		g.box(Mats.painted("Locker", Color(0.3, 0.38, 0.45), 0.5, 0.4), Vector3(X0 + 1.4, 1.1, z), Vector3(0.9, 2.2, 1.9))
		g.box(Mats.dark(), Vector3(X0 + 1.9, 1.7, z), Vector3(0.05, 0.6, 0.5))
	# spare parts racks: armour plates, an arm, leg actuators
	for x: float in [-158.0, -150.0]:
		for lvl in 3:
			g.box(Mats.yellow(), Vector3(x, 1.5 + lvl * 3.0, Z1 - 3.0), Vector3(6.0, 0.25, 3.0))
			g.box(Mats.painted("MechGrey", Color(0.45, 0.47, 0.46), 0.45, 0.55), Vector3(x + rng.randf_range(-1.5, 1.5), 2.1 + lvl * 3.0, Z1 - 3.0), Vector3(2.4, 1.0, 1.8))
		for px: float in [-2.8, 2.8]:
			g.box(Mats.yellow(), Vector3(x + px, 4.5, Z1 - 3.0), Vector3(0.3, 9.0, 0.3))
	g.cylinder(Mats.painted("MechGrey", Color(0.45, 0.47, 0.46), 0.45, 0.55), Vector3(-148, 0.9, -40), Vector3(-142, 0.9, -38), 0.9, 12)
	g.cylinder(Mats.dark(), Vector3(-142, 0.9, -38), Vector3(-139, 1.0, -37), 0.6, 10)
	# terminals with live screens
	for spec in [[Vector3(-92.0, 0, -56.0), -PI * 0.5], [Vector3(-112.0, 0, -88.0), 0.0], [Vector3(-112.0, 0, -60.0), PI]]:
		var p: Vector3 = spec[0]
		g.box(Mats.dark(), p + Vector3(0, 0.6, 0), Vector3(1.4, 1.2, 1.0))
		g.box(Mats.dark(), p + Vector3(0, 1.75, 0), Vector3(0.2, 1.2, 0.2))
		Geo.box_collider(root, p + Vector3(0, 0.8, 0), Vector3(1.6, 1.6, 1.2))
		var scr := MeshInstance3D.new()
		var q := QuadMesh.new()
		q.size = Vector2(1.6, 1.0)
		scr.mesh = q
		var sm := StandardMaterial3D.new()
		sm.albedo_texture = art.get("screen")
		sm.emission_enabled = true
		sm.emission_texture = art.get("screen")
		sm.emission_energy_multiplier = 1.4
		scr.material_override = sm
		scr.position = p + Vector3(0, 2.5, 0)
		scr.rotation.y = spec[1]
		root.add_child(scr)
	# ammo and parts crates, gas bottles, a cable reel, barrier stands
	for k in 26:
		var p := Vector3(rng.randf_range(-158, -88), 0, rng.randf_range(-115, -33))
		if p.distance_to(Site.SPAWN) < 16 or absf(p.z - Site.SPAWN.z) < 9 and p.x > -130:
			continue
		if p.distance_to(Vector3(-138, 0, -102)) < 10 or p.distance_to(Vector3(-138, 0, -44)) < 10:
			continue
		var s := Vector3(rng.randf_range(1.2, 2.4), rng.randf_range(0.8, 1.4), rng.randf_range(1.0, 1.6))
		var m := Mats.olive() if rng.randf() < 0.6 else Mats.navy()
		var b := Basis(Vector3.UP, rng.randf() * TAU)
		g.box(m, p + Vector3(0, s.y * 0.5, 0), s, b)
		if rng.randf() < 0.5:
			g.box(m, p + Vector3(0, s.y * 1.5, 0), s * 0.9, b)
		Geo.box_collider(root, p + Vector3(0, s.y, 0), s * Vector3(1, 2, 1), b)
	for k in 6:
		var p := Vector3(-160.0 + k * 0.9, 0, -88.0)
		g.cylinder(Mats.lit("GasGreen", Color(0.15, 0.4, 0.2), 0.4, 0.3), p, p + Vector3(0, 1.6, 0), 0.25, 10, 0.18)
	g.cylinder(Mats.lit("ReelWood", Color(0.45, 0.32, 0.2), 0.8), Vector3(-100, 1.2, -108), Vector3(-100, 1.2, -106.6), 1.2, 16)
	g.cylinder(Mats.rubber(), Vector3(-100, 1.2, -107.9), Vector3(-100, 1.2, -106.7), 0.9, 16)
	g.commit(root, "Workshop", true, false)

## Crew in hi-vis: simple standing and working figures.
static func person_mesh(vest: Color) -> ArrayMesh:
	var g := Geo.new()
	var skin := Mats.lit("Skin", Color(0.72, 0.55, 0.43), 0.6)
	var pants := Mats.lit("Workwear", Color(0.14, 0.17, 0.22), 0.8)
	var v := Mats.lit("Vest%s" % vest.to_html(), vest, 0.6)
	var helmet := Mats.lit("Helmet", Color(0.95, 0.8, 0.15), 0.4)
	for s: float in [-0.12, 0.12]:
		g.box(pants, Vector3(s, 0.45, 0), Vector3(0.17, 0.9, 0.22), Basis(), 1.0, [], false)
		g.box(Mats.dark(), Vector3(s, 0.06, 0.04), Vector3(0.18, 0.12, 0.3), Basis(), 1.0, [], false)
	g.box(v, Vector3(0, 1.22, 0), Vector3(0.46, 0.66, 0.26), Basis(), 1.0, [], false)
	for s: float in [-0.3, 0.3]:
		g.box(pants, Vector3(s, 1.2, 0), Vector3(0.12, 0.6, 0.14), Basis(), 1.0, [], false)
	g.cylinder(skin, Vector3(0, 1.58, 0), Vector3(0, 1.82, 0), 0.11, 10, -1.0, true, false)
	g.cylinder(helmet, Vector3(0, 1.8, 0), Vector3(0, 1.92, 0), 0.15, 12, 0.08, true, false)
	var tmp := Node3D.new()
	var mi := g.commit(tmp, "p", true, false)
	var m: ArrayMesh = mi.mesh
	tmp.free()
	return m

static func workers(root: Node3D) -> Array:
	var out := []
	var meshes := [person_mesh(Color(0.95, 0.75, 0.1)), person_mesh(Color(0.95, 0.42, 0.08))]
	var spots := [Vector3(-118, 0, -66), Vector3(-121, 0, -82), Vector3(-148, 0, -112.5), Vector3(-130, 0, -112.5),
		Vector3(-94, 0, -59), Vector3(-73, 0, -100), Vector3(-70, 0, -46), Vector3(-30, 0, 4), Vector3(-28, 0, 5.5),
		Vector3(-10, 0, -120), Vector3(-55, 0, -150), Vector3(20, 0, -40)]
	for i in spots.size():
		var mi := MeshInstance3D.new()
		mi.mesh = meshes[i % 2]
		mi.position = spots[i]
		mi.rotation.y = rng.randf() * TAU
		mi.scale = Vector3.ONE * rng.randf_range(0.95, 1.08)
		mi.name = "Worker%d" % i
		root.add_child(mi)
		out.append(mi)
	return out

# ------------------------------------------------------------------ yard outside
static func yard(root: Node3D) -> void:
	var g := Geo.new()
	# portable cabins north of the hangar front
	for k in 3:
		var c := Vector3(-60.0 + k * 13.5, 0, -150.0)
		cabin(g, c)
		Geo.box_collider(root, c + Vector3(0, 1.6, 0), Vector3(12.2, 3.2, 3.0))
	# dark equipment cases, like the concept's foreground stacks
	for k in 18:
		var c := Vector3(-66.0 + (k % 6) * 2.6, 0, 52.0 + int(k / 6) * 2.4)
		var stack := 1 + int(rng.randf() * 2.2)
		for l in stack:
			g.box(Mats.lit("CaseGreen", Color(0.13, 0.17, 0.15), 0.6, 0.3), c + Vector3(0, 0.6 + l * 1.22, 0), Vector3(2.4, 1.2, 2.2))
			g.box(Mats.dark(), c + Vector3(0, 0.6 + l * 1.22, 1.12), Vector3(1.8, 0.14, 0.06))
		Geo.box_collider(root, c + Vector3(0, stack * 0.61, 0), Vector3(2.4, stack * 1.22, 2.2))
	# yellow tool carts and blue cabinets along the hangar front
	for k in 7:
		var c := Vector3(-73.0 + (k % 2) * 2.0, 0, -112.0 + k * 3.4)
		var m := Mats.yellow() if k % 3 != 2 else Mats.painted("CabinetBlue", Color(0.12, 0.25, 0.55), 0.45, 0.4)
		g.box(m, c + Vector3(0, 0.8, 0), Vector3(1.6, 1.6, 1.2))
		g.box(Mats.dark(), c + Vector3(0, 0.1, 0), Vector3(1.7, 0.2, 1.3))
	# generators and a fuel bowser
	for c: Vector3 in [Vector3(-60, 0, -112), Vector3(-60, 0, -28)]:
		g.box(Mats.painted("GenYellow", Color(0.85, 0.62, 0.1), 0.5, 0.3), c + Vector3(0, 1.3, 0), Vector3(5.0, 2.6, 2.2))
		g.box(Mats.dark(), c + Vector3(2.2, 1.3, 0), Vector3(0.6, 1.8, 1.8))
		g.cylinder(Mats.dark(), c + Vector3(-1.5, 2.6, 0.6), c + Vector3(-1.5, 3.6, 0.6), 0.15, 8)
		Geo.box_collider(root, c + Vector3(0, 1.3, 0), Vector3(5.0, 2.6, 2.2))
	var fb := Vector3(-35, 0, -132)
	g.cylinder(Mats.white(), fb + Vector3(-3, 1.8, 0), fb + Vector3(3, 1.8, 0), 1.3, 18)
	g.box(Mats.dark(), fb + Vector3(0, 0.5, 0), Vector3(7.0, 0.4, 2.0))
	for wx: float in [-2.2, 2.2]:
		for wz: float in [-1.0, 1.0]:
			g.cylinder(Mats.rubber(), fb + Vector3(wx, 0.5, wz * 0.95), fb + Vector3(wx, 0.5, wz * 1.25), 0.5, 12)
	Geo.box_collider(root, fb + Vector3(0, 1.5, 0), Vector3(7.5, 3, 3))
	# safety fence by the slipway head
	for line in [[Vector3(-4, 0, Site.SLIP_Z0 - 2), Vector3(30, 0, Site.SLIP_Z0 - 2)], [Vector3(-4, 0, Site.SLIP_Z1 + 2), Vector3(26, 0, Site.SLIP_Z1 + 2)]]:
		var a: Vector3 = line[0]
		var b: Vector3 = line[1]
		var n := int(a.distance_to(b) / 2.5)
		for k in n + 1:
			var p := a.lerp(b, float(k) / n)
			g.beam(Mats.yellow(), p, p + Vector3(0, 1.1, 0), 0.08)
		g.beam(Mats.yellow(), a + Vector3(0, 1.1, 0), b + Vector3(0, 1.1, 0), 0.07)
		g.beam(Mats.yellow(), a + Vector3(0, 0.6, 0), b + Vector3(0, 0.6, 0), 0.05)
	pickup(g, Vector3(-28, 0, 0), deg_to_rad(-30))
	Geo.box_collider(root, Vector3(-28, 1.0, 0), Vector3(5.6, 2.0, 2.2), Basis(Vector3.UP, deg_to_rad(-30)))
	forklift(g, Vector3(-64, 0, -40), deg_to_rad(80))
	Geo.box_collider(root, Vector3(-64, 1.2, -40), Vector3(3.0, 2.4, 1.6), Basis(Vector3.UP, deg_to_rad(80)))
	g.commit(root, "Yard", true, false)

static func cabin(g: Geo, c: Vector3) -> void:
	var w := Mats.white()
	g.box(w, c + Vector3(0, 1.6, 0), Vector3(12.0, 3.0, 2.8))
	g.box(Mats.dark(), c + Vector3(0, 3.15, 0), Vector3(12.2, 0.2, 3.0))
	for k in 3:
		g.box(Mats.glass(), c + Vector3(-4.0 + k * 3.5, 1.9, 1.42), Vector3(1.6, 1.0, 0.05))
	g.box(Mats.navy(), c + Vector3(5.0, 1.2, 1.42), Vector3(0.95, 2.1, 0.05))
	g.box(Mats.steel(), c + Vector3(-4.5, 3.6, 0), Vector3(1.4, 0.9, 1.0))
	g.box(Mats.dark(), c + Vector3(0, 0.05, 0), Vector3(12.4, 0.1, 3.0))

static func pickup(g: Geo, p: Vector3, yaw: float) -> void:
	var b := Basis(Vector3.UP, yaw)
	var w := Mats.lit("TruckWhite", Color(0.88, 0.89, 0.9), 0.3, 0.5)
	g.box(w, p + b * Vector3(0, 0.95, 0), Vector3(5.4, 0.9, 2.0), b)
	g.box(w, p + b * Vector3(0.6, 1.75, 0), Vector3(2.4, 0.8, 1.9), b)
	g.box(Mats.glass(), p + b * Vector3(0.6, 1.8, 0), Vector3(2.3, 0.6, 1.95), b)
	g.box(Mats.dark(), p + b * Vector3(-1.6, 1.45, 0), Vector3(2.0, 0.2, 1.9), b)
	g.box(Mats.lamp(), p + b * Vector3(2.71, 1.05, 0.65), Vector3(0.05, 0.18, 0.35), b)
	g.box(Mats.lamp(), p + b * Vector3(2.71, 1.05, -0.65), Vector3(0.05, 0.18, 0.35), b)
	for wx: float in [-1.7, 1.7]:
		for wz: float in [-1.0, 1.0]:
			var c := p + b * Vector3(wx, 0.45, wz)
			g.cylinder(Mats.rubber(), c - b.z * 0.15, c + b.z * 0.15, 0.45, 14)

static func forklift(g: Geo, p: Vector3, yaw: float) -> void:
	var b := Basis(Vector3.UP, yaw)
	var y := Mats.yellow()
	g.box(y, p + b * Vector3(0, 0.9, 0), Vector3(2.6, 1.2, 1.4), b)
	g.box(Mats.dark(), p + b * Vector3(-0.2, 2.2, 0), Vector3(1.4, 0.1, 1.3), b)
	for s: float in [-0.6, 0.6]:
		g.beam(Mats.dark(), p + b * Vector3(-0.8, 1.4, s), p + b * Vector3(-0.8, 2.2, s), 0.08)
		g.beam(Mats.dark(), p + b * Vector3(0.5, 1.4, s), p + b * Vector3(0.5, 2.2, s), 0.08)
		g.beam(Mats.dark(), p + b * Vector3(1.5, 0.1, s * 0.6), p + b * Vector3(1.5, 3.4, s * 0.6), 0.14)
		g.box(Mats.steel(), p + b * Vector3(2.2, 0.12, s * 0.5), Vector3(1.4, 0.08, 0.15), b)
	for wx: float in [-0.8, 0.8]:
		for wz: float in [-0.7, 0.7]:
			var c := p + b * Vector3(wx, 0.35, wz)
			g.cylinder(Mats.rubber(), c - b.z * 0.12, c + b.z * 0.12, 0.35, 12)

static func annex(root: Node3D) -> void:
	var g := Geo.new()
	var x0 := -150.0
	var x1 := -96.0
	var z0 := Z1
	var z1 := Z1 + 16.0
	var cl := Mats.corrugated("CladdingWarm", Color(0.74, 0.72, 0.68), 10)
	g.aabb(cl, Vector3(x0, 0, z0), Vector3(x1, 12, z1), 4.0)
	g.aabb(Mats.dark(), Vector3(x0 - 0.3, 12, z0), Vector3(x1 + 0.3, 12.6, z1 + 0.3))
	for fl in 2:
		for k in 8:
			g.box(Mats.glass(), Vector3(x0 + 4 + k * 6.5, 3.2 + fl * 5.0, z1 + 0.02), Vector3(4.2, 2.0, 0.08))
			g.box(Mats.dark(), Vector3(x0 + 4 + k * 6.5, 2.1 + fl * 5.0, z1 + 0.1), Vector3(4.4, 0.15, 0.2))
	g.box(Mats.glass(), Vector3(x1 + 0.02, 2.2, z0 + 8), Vector3(0.08, 4.4, 3.4))
	g.box(Mats.navy(), Vector3(x1 + 0.4, 4.8, z0 + 8), Vector3(0.6, 0.4, 5.0))
	for k in 4:
		g.box(Mats.white(), Vector3(x0 + 8 + k * 12, 13.4, z0 + 8), Vector3(5, 1.6, 3))
	g.commit(root, "Annex", true, false)
	Geo.box_collider(root, Vector3((x0 + x1) * 0.5, 6, (z0 + z1) * 0.5), Vector3(x1 - x0, 12, z1 - z0))
