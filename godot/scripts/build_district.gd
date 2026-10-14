class_name BuildDistrict
extends RefCounted
## How the training hangar joins the city.
##   inside the slice   the service gate, the waterfront road up to the checkpoint,
##                      sheds between road and quay, league billboards, road lamps
##   scenery beyond     the road on along the shore through the hangar district and
##                      up the approach onto the Central Bridge; on the far bank the
##                      bridge comes down into Downtown and a shore boulevard runs
##                      south to the Championship Arena; the Ground Arena sits inland
## Everything beyond the slice is low-detail, shadowless and has no collision.

static var rng := RandomNumberGenerator.new()

static func build(root: Node3D) -> void:
	rng.seed = 77
	gate(root)
	slice_road(root)
	sheds(root)
	billboards(root)
	var far := Node3D.new()
	far.name = "CityScenery"
	root.add_child(far)
	road_to_bridge(far)
	hangar_district(far)
	east_bank_roads(far)
	arenas(far)

static func _asphalt() -> StandardMaterial3D:
	return Mats.lit("Asphalt", Color(0.16, 0.165, 0.17), 0.85, 0.0, Tex.noise(0.3, 61, 256, true, 2.0), 0.4, Vector3(6, 6, 6), true)

# ------------------------------------------------------------------ inside the slice
## Service entrance where the road meets the yard: sign gantry over the road (9 m
## clear, so a frame walks under it), booth and a raised barrier arm.
static func gate(root: Node3D) -> void:
	var g := Geo.new()
	var x := Site.ROAD_X
	var z := Site.GATE_Z
	var hw := Site.ROAD_W * 0.5 + 1.0
	for s: float in [-hw, hw]:
		g.box(Mats.steel(), Vector3(x + s, 5.2, z), Vector3(0.7, 10.4, 0.7))
		g.box(Mats.concrete("Plinth", Color(0.55, 0.54, 0.52), 0.0), Vector3(x + s, 0.3, z), Vector3(1.4, 0.6, 1.4))
		Geo.box_collider(root, Vector3(x + s, 5.2, z), Vector3(0.8, 10.4, 0.8))
	g.box(Mats.navy(), Vector3(x, 10.2, z), Vector3(hw * 2.0 + 1.2, 1.6, 0.5))
	var booth := Vector3(x - hw - 3.0, 0, z - 2.0)
	g.box(Mats.white(), booth + Vector3(0, 1.5, 0), Vector3(3.0, 3.0, 3.0))
	g.box(Mats.glass(), booth + Vector3(1.52, 1.9, 0), Vector3(0.05, 1.2, 2.4))
	g.box(Mats.dark(), booth + Vector3(0, 3.1, 0), Vector3(3.6, 0.2, 3.6))
	Geo.box_collider(root, booth + Vector3(0, 1.5, 0), Vector3(3.0, 3.0, 3.0))
	g.box(Mats.dark(), Vector3(x - hw + 0.4, 0.6, z + 1.0), Vector3(0.8, 1.2, 0.8))
	g.box(Mats.hazard(), Vector3(x - hw + 0.4, 3.5, z + 1.0), Vector3(0.2, 5.6, 0.2), Basis(Vector3.BACK, -0.12))
	g.commit(root, "Gate", true, false)
	_label(root, "WATERFRONT TRAINING HANGAR 03  ·  SERVICE ENTRANCE", Vector3(x, 10.2, z + 0.27), 0.0, 0.016, Color(0.95, 0.95, 0.92))
	_label(root, "WATERFRONT TRAINING HANGAR 03  ·  SERVICE ENTRANCE", Vector3(x, 10.2, z - 0.27), PI, 0.016, Color(0.95, 0.95, 0.92))

## The waterfront road from the gate to the checkpoint: asphalt, centre dashes,
## kerbs, a raised footpath on the west side and street lamps.
static func slice_road(root: Node3D) -> void:
	var g := Geo.new()
	var x := Site.ROAD_X
	var hw := Site.ROAD_W * 0.5
	var z0 := Site.SLICE_Z
	var z1 := Site.GATE_Z
	g.aabb(_asphalt(), Vector3(x - hw, 0.0, z0), Vector3(x + hw, 0.04, z1), 6.0, ["-y"])
	var z := z0 + 2.0
	while z < z1 - 3.0:
		g.box(Mats.white(), Vector3(x, 0.045, z + 1.5), Vector3(0.18, 0.01, 3.0))
		z += 9.0
	for s: float in [-1.0, 1.0]:
		g.box(Mats.white(), Vector3(x + s * (hw - 0.4), 0.045, (z0 + z1) * 0.5), Vector3(0.14, 0.01, z1 - z0))
		g.box(Mats.concrete("Kerb", Color(0.7, 0.69, 0.66), 0.0), Vector3(x + s * (hw + 0.15), 0.12, (z0 + z1) * 0.5), Vector3(0.3, 0.24, z1 - z0))
	g.aabb(Mats.concrete("Footpath", Color(0.66, 0.65, 0.62), 0.2), Vector3(x - hw - 3.3, 0.0, z0), Vector3(x - hw - 0.3, 0.18, z1), 4.0, ["-y"])
	var lamps := Geo.new()
	var lz := z0 + 18.0
	while lz < z1:
		var p := Vector3(x - hw - 1.6, 0.18, lz)
		BuildSite.lamp_post(lamps, p)
		var l := OmniLight3D.new()
		l.position = p + Vector3(0, 10.4, 0)
		l.light_color = Color(1.0, 0.85, 0.65)
		l.light_energy = 0.0
		l.omni_range = 22
		l.add_to_group("night_lights")
		root.add_child(l)
		Geo.box_collider(root, p + Vector3(0, 5.5, 0), Vector3(0.6, 11, 0.6))
		lz += 32.0
	g.commit(root, "SliceRoad", true, false)
	lamps.commit(root, "RoadLamps", true, false)

## Working sheds between the road and the quay strip, gable roofs and roller
## doors sized for trucks (5 m) beside 2.1 m personnel doors: a human-scale cue.
static func sheds(root: Node3D) -> void:
	var g := Geo.new()
	var wall := Mats.facade("ShedCladding", Color(0.62, 0.64, 0.63), 9.0, 2.4)
	var roof := Mats.corrugated("ShedRoof", Color(0.36, 0.38, 0.4), 12)
	for spec in [[-246.0, 30.0, Color(0.62, 0.64, 0.63)], [-320.0, 40.0, Color(0.7, 0.66, 0.58)], [-410.0, 34.0, Color(0.55, 0.6, 0.64)]]:
		var zc: float = spec[0]
		var len: float = spec[1]
		var x0 := Site.ROAD_X + Site.ROAD_W * 0.5 + 6.0
		var x1 := Site.STRIP_X0 - 4.0
		var h := 9.0
		var c := Vector3((x0 + x1) * 0.5, 0, zc)
		g.box(Mats.facade("Shed%s" % (spec[2] as Color).to_html(), spec[2], 9.0, 2.4), c + Vector3(0, h * 0.5, 0), Vector3(x1 - x0, h, len))
		# gable roof
		var w := x1 - x0
		for s: float in [-1.0, 1.0]:
			g.box(roof, c + Vector3(s * w * 0.25, h + 1.2, 0), Vector3(w * 0.56, 0.25, len + 1.0), Basis(Vector3.BACK, s * -0.22))
		# roller doors facing the road, personnel doors, lamps
		for k in 2:
			var dz := zc - len * 0.25 + k * len * 0.5
			g.box(Mats.painted("RollerDoor", Color(0.42, 0.45, 0.47), 0.5, 0.5), Vector3(x0 - 0.05, 2.5, dz), Vector3(0.1, 5.0, 5.0))
			g.box(Mats.yellow(), Vector3(x0 - 0.1, 0.4, dz - 2.7), Vector3(0.3, 0.8, 0.3))
			g.box(Mats.yellow(), Vector3(x0 - 0.1, 0.4, dz + 2.7), Vector3(0.3, 0.8, 0.3))
		g.box(Mats.navy(), Vector3(x0 - 0.05, 1.05, zc), Vector3(0.1, 2.1, 1.0))
		g.box(Mats.lamp(), Vector3(x0 - 0.3, 2.6, zc), Vector3(0.3, 0.12, 0.5))
		Geo.box_collider(root, c + Vector3(0, h * 0.5 + 0.6, 0), Vector3(x1 - x0, h + 1.2, len))
	# a parked flatbed and a container pair by the middle shed
	g.box(Mats.painted("TruckCab", Color(0.85, 0.3, 0.12), 0.45, 0.3), Vector3(Site.ROAD_X + 9.0, 1.6, -365.0), Vector3(2.5, 3.0, 2.6))
	g.box(Mats.dark(), Vector3(Site.ROAD_X + 9.0, 1.1, -357.0), Vector3(2.5, 0.5, 11.0))
	g.commit(root, "Sheds", true, false)

## League billboards facing traffic on the road: event signage, not an arena.
static func billboards(root: Node3D) -> void:
	var g := Geo.new()
	var specs := [[Vector3(Site.ROAD_X - 18.0, 0, -270.0), 0.0, "IRON LEAGUE QUALIFIER\nCHAMPIONSHIP ARENA · SAT 20:00"],
		[Vector3(Site.ROAD_X - 18.0, 0, -420.0), 0.0, "CROSS THE BAY\nGRAND FINAL · LIVE TONIGHT"],
		[Vector3(-80.0, 0, -186.0), 0.0, "PILOTS WANTED · TRAIN AT HANGAR 03"]]
	for s in specs:
		var p: Vector3 = s[0]
		for dx: float in [-5.0, 5.0]:
			g.box(Mats.steel(), p + Vector3(dx, 5.0, 0), Vector3(0.5, 10.0, 0.5))
			Geo.box_collider(root, p + Vector3(dx, 5.0, 0), Vector3(0.6, 10.0, 0.6))
		g.box(Mats.dark(), p + Vector3(0, 11.5, 0), Vector3(14.6, 6.4, 0.5))
		g.box(Mats.painted("BillboardFace", Color(0.1, 0.13, 0.22), 0.5, 0.1), p + Vector3(0, 11.5, 0.27), Vector3(14.0, 5.8, 0.05))
		g.box(Mats.orange(), p + Vector3(0, 9.0, 0.3), Vector3(14.0, 0.5, 0.05))
		_label(root, s[2], p + Vector3(0, 12.0, 0.31), 0.0, 0.0135, Color(0.98, 0.96, 0.9))
	g.commit(root, "Billboards", true, false)

static func _label(root: Node3D, text: String, pos: Vector3, yaw: float, px: float, col: Color) -> Label3D:
	var l := Label3D.new()
	l.text = text
	l.font = Tex.paint_font()
	l.font_size = 64
	l.pixel_size = px
	l.modulate = col
	l.outline_size = 0
	l.double_sided = false
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(l)
	l.position = pos
	l.rotation.y = yaw
	return l

# ------------------------------------------------------------------ scenery beyond the slice
## Asphalt ribbon through (x, z, y) points; y < -50 means "follow the ground".
## Elevated stretches get a deck slab and piers.
static func _ribbon(g: Geo, pts: Array, width: float, piers := true) -> void:
	var samples := []
	for i in pts.size() - 1:
		var a: Vector3 = pts[i]
		var b: Vector3 = pts[i + 1]
		var n := maxi(1, int(Vector2(a.x, a.z).distance_to(Vector2(b.x, b.z)) / 15.0))
		for k in n:
			samples.append(a.lerp(b, float(k) / n))
	samples.append(pts[pts.size() - 1])
	var hy := []
	for p: Vector3 in samples:
		var gy := Site.ground(p.x, p.z) if p.x < 900.0 else Site.east_ground(p.x, p.z)
		hy.append(maxf(gy + 0.25, p.y) if p.y > -50.0 else gy + 0.25)
	for i in samples.size() - 1:
		var a: Vector3 = samples[i]
		var b: Vector3 = samples[i + 1]
		var ya: float = hy[i]
		var yb: float = hy[i + 1]
		var d := Vector3(b.x - a.x, 0, b.z - a.z).normalized()
		var side := Vector3(-d.z, 0, d.x) * width * 0.5
		var pa := Vector3(a.x, ya, a.z)
		var pb := Vector3(b.x, yb, b.z)
		g.quad(_asphalt(), pa - side, pa + side, pb + side, pb - side, Vector3.UP, 8.0, Vector2.ZERO, false)
		var gya := Site.ground(a.x, a.z) if a.x < 900.0 else Site.east_ground(a.x, a.z)
		if piers and ya - gya > 3.0:
			g.box(Mats.concrete("BridgeDeck", Color(0.7, 0.68, 0.64), 0.0), (pa + pb) * 0.5 - Vector3(0, 0.9, 0), Vector3(width + 1.0, 1.6, pa.distance_to(pb) + 0.2), Basis.looking_at(pb - pa, Vector3.UP), 8.0, [], false)
			if i % 2 == 0:
				g.box(Mats.concrete("BridgeDeck", Color(0.7, 0.68, 0.64), 0.0), Vector3(a.x, (ya + gya) * 0.5 - 1.0, a.z), Vector3(3.0, ya - gya, 3.0), Basis(), 8.0, [], false)

## The waterfront road north from the checkpoint and up the approach onto the bridge.
static func road_to_bridge(root: Node3D) -> void:
	var g := Geo.new()
	var deck := 47.0
	_ribbon(g, [Vector3(Site.ROAD_X, -99, Site.SLICE_Z), Vector3(-40, -99, -700), Vector3(-80, -99, -950), Vector3(-140, -99, -1200),
		Vector3(-140, 6.0, -1320), Vector3(-140, 30.0, -1520), Vector3(-130, 42.0, -1630), Vector3(-90, deck, -1690), Vector3(-30, deck, -1700)], Site.ROAD_W)
	var mi := g.commit(root, "RoadToBridge", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	# street lamps as simple posts along the first stretch beyond the checkpoint
	var lg := Geo.new()
	var z := Site.SLICE_Z - 20.0
	while z > -1150.0:
		var x := lerpf(-40.0, -140.0, clampf((-z - 700.0) / 500.0, 0.0, 1.0))
		var p := Vector3(x - 8.0, Site.ground(x - 8.0, z), z)
		lg.cylinder(Mats.dark(), p, p + Vector3(0, 10.5, 0), 0.16, 6)
		lg.box(Mats.lamp(), p + Vector3(0, 10.5, 0), Vector3(0.9, 0.1, 0.4))
		z -= 40.0
	var lm := lg.commit(root, "RoadLampsFar", false, false)
	lm.visibility_range_end = 1500.0

## Low-detail working waterfront between the road and the shore on the way to the
## bridge: sheds, warehouses, a pier and dock cranes. Visibility-limited.
static func hangar_district(root: Node3D) -> void:
	var g := Geo.new()
	var cols := [Color(0.6, 0.6, 0.58), Color(0.52, 0.48, 0.44), Color(0.66, 0.62, 0.55), Color(0.45, 0.5, 0.55)]
	var z := -540.0
	while z > -1560.0:
		var len := rng.randf_range(35.0, 80.0)
		var road_x := -40.0 if z > -700.0 else lerpf(-40.0, -140.0, clampf((-z - 700.0) / 500.0, 0.0, 1.0))
		var shore := Site.coast_x(z)
		var x0 := road_x + 14.0
		var x1 := minf(shore - 14.0, x0 + rng.randf_range(35.0, 70.0))
		if x1 - x0 > 12.0:
			var h := rng.randf_range(10.0, 24.0)
			var c := Vector3((x0 + x1) * 0.5, Site.ground((x0 + x1) * 0.5, z - len * 0.5) - 0.5, z - len * 0.5)
			var col: Color = cols[rng.randi() % cols.size()]
			g.box(Mats.lit("FarShed%d" % cols.find(col), col, 0.8), c + Vector3(0, h * 0.5, 0), Vector3(x1 - x0, h, len - 6.0), Basis(), 12.0, [], false)
			g.box(Mats.lit("FarRoof", Color(0.3, 0.32, 0.34), 0.7), c + Vector3(0, h + 0.4, 0), Vector3(x1 - x0 + 1.0, 0.8, len - 5.0), Basis(), 12.0, [], false)
		# warehouses on the land side of the road
		if rng.randf() < 0.6:
			var wx := road_x - 30.0 - rng.randf_range(0.0, 30.0)
			var h2 := rng.randf_range(12.0, 30.0)
			g.box(Mats.lit("FarShed0", cols[0], 0.8), Vector3(wx, Site.ground(wx, z - len * 0.5) + h2 * 0.5 - 0.5, z - len * 0.5), Vector3(36.0, h2, len - 8.0), Basis(), 12.0, [], false)
		z -= len
	# dock cranes along the shore
	for cz: float in [-880.0, -1060.0, -1240.0]:
		var sx := Site.coast_x(cz) - 4.0
		var base := Vector3(sx, Site.WATER_Y + 3.0, cz)
		for lz: float in [-6.0, 6.0]:
			for lx: float in [-5.0, 5.0]:
				g.beam(Mats.painted("CraneRed", Color(0.65, 0.16, 0.1), 0.5, 0.3), base + Vector3(lx, 0, lz), base + Vector3(lx * 0.6, 34.0, lz * 0.6), 1.0)
		g.box(Mats.painted("CraneRed", Color(0.65, 0.16, 0.1), 0.5, 0.3), base + Vector3(10.0, 36.0, 0), Vector3(70.0, 3.0, 4.0), Basis(), 8.0, [], false)
		g.box(Mats.white(), base + Vector3(-2.0, 30.0, 0), Vector3(8.0, 5.0, 6.0), Basis(), 8.0, [], false)
	var mi := g.commit(root, "HangarDistrict", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.visibility_range_end = 5000.0

## The far bank: the bridge's east approach down into Downtown, an avenue through
## it, and the shore boulevard south to the Championship Arena.
static func east_bank_roads(root: Node3D) -> void:
	var g := Geo.new()
	var deck := 47.0
	_ribbon(g, [Vector3(1700, deck, -1700), Vector3(1850, 40.0, -1700), Vector3(2050, 14.0, -1700), Vector3(2200, -99, -1700), Vector3(3000, -99, -1700)], 16.0)
	var shore_pts := [Vector3(2200, -99, -1700)]
	for z in [-1550.0, -1300.0, -1000.0, -700.0, -400.0, -100.0, 150.0, 300.0]:
		shore_pts.append(Vector3(Site.east_coast_x(z) + 45.0, -99, z))
	_ribbon(g, shore_pts, 14.0, false)
	for xz: float in [2300.0, 2550.0]:
		_ribbon(g, [Vector3(xz, -99, -1400), Vector3(xz, -99, -2700)], 12.0, false)
	var mi := g.commit(root, "EastBankRoads", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

## Arena silhouettes (provisional placement): the Championship Arena on the far
## shore across the bay, the Ground Arena inland on the hills to the north-west.
static func arenas(root: Node3D) -> void:
	var stone := Mats.lit("ArenaStone", Color(0.86, 0.84, 0.8), 0.7)
	var steel := Mats.lit("ArenaSteel", Color(0.7, 0.72, 0.74), 0.4, 0.6)
	var g := Geo.new()
	# Championship Arena: oval bowl, upper rim, two great arches over it
	var ca := Vector3(1480.0, Site.east_ground(1480.0, 260.0), 260.0)
	var oval := Basis().scaled(Vector3(1.0, 1.0, 0.72))
	var tiers := [[150.0, 0.0, 30.0, stone], [140.0, 30.0, 44.0, steel]]
	for t in tiers:
		var cm := CylinderMesh.new()
		cm.top_radius = t[0]
		cm.bottom_radius = t[0] + 6.0
		cm.height = t[2] - t[1]
		cm.radial_segments = 48
		g.mesh(t[3], cm, Transform3D(oval, ca + Vector3(0, (t[1] + t[2]) * 0.5, 0)))
	for s: float in [-1.0, 1.0]:
		var prev := Vector3.ZERO
		for k in 13:
			var a := PI * k / 12.0
			var p := ca + Vector3(cos(a) * 160.0, 44.0 + sin(a) * 75.0, s * 70.0)
			if k > 0:
				g.beam(steel, prev, p, 5.0)
			prev = p
	# screens on the land side (lit at night)
	g.box(Mats.glow("ArenaScreen", Color(0.3, 0.55, 0.9), 1.5), ca + Vector3(-150.0, 22.0, 0), Vector3(2.0, 18.0, 46.0))
	# Ground Arena: a stone coliseum on the north-west hills, with flag masts
	var ga := Vector3(-640.0, 0, -1430.0)
	ga.y = Site.ground(ga.x, ga.z) - 2.0
	var cg := CylinderMesh.new()
	cg.top_radius = 105.0
	cg.bottom_radius = 112.0
	cg.height = 36.0
	cg.radial_segments = 40
	g.mesh(stone, cg, Transform3D(Basis().scaled(Vector3(1.0, 1.0, 0.8)), ga + Vector3(0, 18.0, 0)))
	for k in 10:
		var a := TAU * k / 10.0
		var p := ga + Vector3(cos(a) * 110.0, 36.0, sin(a) * 88.0)
		g.cylinder(steel, p, p + Vector3(0, 22.0, 0), 0.8, 6)
		g.box(Mats.glow("ArenaFlag", Color(0.85, 0.2, 0.15), 0.6), p + Vector3(3.0, 20.0, 0), Vector3(6.0, 3.5, 0.2))
	var mi := g.commit(root, "Arenas", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
