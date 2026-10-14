class_name HangarDetail
extends RefCounted
## Second detail layer for the training hangar, over the primary shell and
## frame in BuildHangar:
##   structure  I-section columns with base plates, wall girts and bracing
##   services   HVAC ducts, pipe runs (fire main, gas, water), cable trays,
##              sprinkler lines, electrical cabinets, fire points, wall lamps
##   interior   control-room mezzanine with stairs, floor trenches and
##              grates, tie-down points
##   exterior   roller-door slats and guide rails, panel flashing, gutters and
##              downpipes, a caged roof ladder, exit doors with canopies, a
##              transformer compound

const X0 := Site.HANGAR_X0
const X1 := Site.HANGAR_X1
const Z0 := Site.HANGAR_Z0
const Z1 := Site.HANGAR_Z1
const EAVE := BuildHangar.EAVE
const COLS := [-132.0, -121.0, -110.0, -99.0, -88.0]   # range(X0 + 6, X1, 11)
const CRANE_Y := BuildHangar.CRANE_Y
const TRUSS_Y := EAVE - 2.6

static func build(root: Node3D) -> void:
	structure(root)
	services(root)
	mezzanine(root)
	floor_detail(root)
	exterior(root)
	practicals(root)

static func _frame_blue() -> StandardMaterial3D:
	return Mats.painted("FrameBlue", Color(0.16, 0.22, 0.3), 0.45, 0.6)

static func _grate() -> StandardMaterial3D:
	return Mats.lit("Grating", Color(0.42, 0.43, 0.44), 0.55, 0.7, Tex.tread(), 0.6, Vector3(2, 2, 2))

static func _control_glass() -> Material:
	if Mats.get_m("ControlGlass"):
		return Mats.get_m("ControlGlass")
	var m := StandardMaterial3D.new()
	m.resource_name = "ControlGlass"
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.albedo_color = Color(0.62, 0.8, 0.88, 0.32)
	m.roughness = 0.04
	m.metallic = 0.1
	m.metallic_specular = 0.9
	m.emission_enabled = true
	m.emission = Color(0.35, 0.6, 0.75)
	m.emission_energy_multiplier = 0.25
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	Mats._m["ControlGlass"] = m
	return m

static func _red() -> StandardMaterial3D:
	return Mats.painted("FireRed", Color(0.7, 0.07, 0.05), 0.45, 0.2)

static func _galv() -> StandardMaterial3D:
	return Mats.painted("Galvanised", Color(0.55, 0.57, 0.58), 0.38, 0.9)

## Bolt heads in a square pattern on a horizontal plate.
static func _plate_bolts(g: Geo, c: Vector3, half: float) -> void:
	for sx: float in [-half, half]:
		for sz: float in [-half, half]:
			g.cylinder(Mats.steel(), c + Vector3(sx, 0, sz), c + Vector3(sx, 0.12, sz), 0.07, 6)

# ------------------------------------------------------------------ structure
static func structure(root: Node3D) -> void:
	var g := Geo.new()
	var blue := _frame_blue()
	for x: float in COLS:
		for zz: float in [Z0 + 1.2, Z1 - 1.2]:
			# flanges turn the square posts into I-sections
			for s: float in [-0.56, 0.56]:
				g.box(blue, Vector3(x + s, TRUSS_Y * 0.5, zz), Vector3(0.12, TRUSS_Y, 1.3))
			g.box(Mats.steel(), Vector3(x, 0.06, zz), Vector3(1.9, 0.12, 1.9))
			_plate_bolts(g, Vector3(x, 0.12, zz), 0.7)
			# stiffeners under the crane corbel
			var inward := 1.0 if zz < -70.0 else -1.0
			g.beam(blue, Vector3(x, CRANE_Y - 3.6, zz + inward * 0.5), Vector3(x, CRANE_Y - 1.4, zz + inward * 1.8), 0.14, 0.5)
			# hazard sleeve at the foot of each column
			g.box(Mats.hazard(), Vector3(x, 0.9, zz), Vector3(1.4, 1.6, 1.4))
	# girts: horizontal rails on the inside of the side and back walls
	for y: float in [5.5, 10.0, 15.5]:
		for zz: float in [Z0 + 0.62, Z1 - 0.62]:
			g.box(blue, Vector3((X0 + X1) * 0.5, y, zz), Vector3(X1 - X0 - 1.0, 0.36, 0.22))
		g.box(blue, Vector3(X0 + 0.62, y, (Z0 + Z1) * 0.5), Vector3(0.22, 0.36, Z1 - Z0 - 1.0))
	# cross bracing in the end bays of each long wall
	for pair in [[-132.0, -121.0], [-99.0, -88.0]]:
		for zz: float in [Z0 + 0.7, Z1 - 0.7]:
			var a: float = pair[0] + 0.6
			var b: float = pair[1] - 0.6
			g.beam(blue, Vector3(a, 5.5, zz), Vector3(b, 15.5, zz), 0.16)
			g.beam(blue, Vector3(b, 5.5, zz), Vector3(a, 15.5, zz), 0.16)
	# bracing on the back wall between pilaster lines
	for z0: float in [-106.0, -52.0]:
		g.beam(blue, Vector3(X0 + 0.7, 10.0, z0), Vector3(X0 + 0.7, 15.5, z0 + 7.0), 0.16)
		g.beam(blue, Vector3(X0 + 0.7, 15.5, z0), Vector3(X0 + 0.7, 10.0, z0 + 7.0), 0.16)
	# purlins between the trusses
	for z in range(int(Z0) + 4, int(Z1), 6):
		g.box(blue, Vector3((X0 + X1) * 0.5, EAVE - 0.75, z), Vector3(X1 - X0 - 2.0, 0.3, 0.18))
	g.commit(root, "HangarStructure", true, false)

# ------------------------------------------------------------------ services
static func services(root: Node3D) -> void:
	var g := Geo.new()
	var duct := _galv()
	var red := _red()
	var gas := Mats.painted("GasYellow", Color(0.85, 0.65, 0.1), 0.45, 0.3)
	var water := Mats.painted("WaterGreen", Color(0.12, 0.42, 0.28), 0.45, 0.3)
	# HVAC: two round mains threaded through the open-web trusses, above the crane
	for z: float in [-96.0, -52.0]:
		var y := EAVE - 1.5
		g.cylinder(duct, Vector3(X0 + 3, y, z), Vector3(X1 - 3, y, z), 0.55, 18)
		var x := X0 + 4.0
		while x < X1 - 3.0:
			g.cylinder(Mats.steel(), Vector3(x, y, z), Vector3(x + 0.14, y, z), 0.62, 18)
			x += 3.2
		for cx: float in COLS:
			var dx := cx + 5.5
			if dx > X1 - 4.0:
				continue
			g.cylinder(duct, Vector3(dx, y, z), Vector3(dx, y - 0.9, z), 0.35, 12)
			g.box(duct, Vector3(dx, y - 1.1, z), Vector3(1.4, 0.4, 1.4))
			g.box(Mats.dark(), Vector3(dx, y - 1.32, z), Vector3(1.15, 0.05, 1.15))
	# cable trays along the hall, hung from the trusses
	for z: float in [Z0 + 5.0, Z1 - 5.0]:
		var y := TRUSS_Y - 0.7
		g.box(Mats.steel(), Vector3((X0 + X1) * 0.5, y, z), Vector3(X1 - X0 - 4.0, 0.06, 0.9))
		for s: float in [-0.45, 0.45]:
			g.box(Mats.steel(), Vector3((X0 + X1) * 0.5, y + 0.12, z + s), Vector3(X1 - X0 - 4.0, 0.25, 0.04))
		for k in 4:
			g.cylinder(Mats.rubber(), Vector3(X0 + 2.5, y + 0.12, z - 0.3 + k * 0.2), Vector3(X1 - 2.5, y + 0.12, z - 0.3 + k * 0.2), 0.07, 6)
		for cx: float in COLS:
			for s: float in [-0.45, 0.45]:
				g.beam(Mats.dark(), Vector3(cx, y, z + s), Vector3(cx, TRUSS_Y, z + s), 0.04)
	# sprinkler lines with drop heads
	for z in range(int(Z0) + 8, int(Z1) - 4, 12):
		g.cylinder(red, Vector3(X0 + 2, EAVE - 1.8, z), Vector3(X1 - 2, EAVE - 1.8, z), 0.07, 6)
		var x2 := X0 + 5.0
		while x2 < X1 - 2.0:
			g.cylinder(red, Vector3(x2, EAVE - 1.8, z), Vector3(x2, EAVE - 2.2, z), 0.035, 5)
			g.cylinder(Mats.steel(), Vector3(x2, EAVE - 2.2, z), Vector3(x2, EAVE - 2.3, z), 0.08, 6)
			x2 += 6.0
	# pipe runs along the north and south walls, with brackets and vertical drops
	for side: float in [-1.0, 1.0]:
		var zw := Z0 + 0.9 if side < 0 else Z1 - 0.9
		var inward := -side
		var runs := [[red, 5.6, 0.2, 0.0], [gas, 6.2, 0.1, 0.35], [water, 6.7, 0.14, 0.15]]
		for r in runs:
			var m: Material = r[0]
			var y: float = r[1]
			var rad: float = r[2]
			var off: float = r[3]
			g.cylinder(m, Vector3(X0 + 1.5, y, zw + inward * off), Vector3(X1 - 1.5, y, zw + inward * off), rad, 10)
			var x := X0 + 3.0
			while x < X1 - 2.0:
				g.cylinder(m, Vector3(x, y, zw + inward * off), Vector3(x + 0.12, y, zw + inward * off), rad * 1.3, 10)
				x += 4.0
		for cx: float in COLS:
			g.box(Mats.steel(), Vector3(cx + 2.0, 6.1, zw - inward * 0.2), Vector3(0.12, 1.6, 0.9))
		# fire main drops to a hydrant valve every other bay (the north wall is lined with benches)
		for k in (range(0, COLS.size() - 1, 2) if side > 0 else []):
			var x: float = COLS[k] + 5.5
			g.cylinder(red, Vector3(x, 5.6, zw), Vector3(x, 1.2, zw), 0.2, 10)
			g.cylinder(red, Vector3(x, 1.2, zw), Vector3(x, 1.2, zw + inward * 0.6), 0.16, 10)
			g.cylinder(red, Vector3(x - 0.45, 1.6, zw + inward * 0.25), Vector3(x + 0.45, 1.6, zw + inward * 0.25), 0.05, 8)
			g.cylinder(red, Vector3(x, 1.6, zw + inward * 0.18), Vector3(x, 1.6, zw + inward * 0.32), 0.45, 14)
	# electrical cabinets, fire points and wall lamps along the south wall
	for k in COLS.size() - 1:
		var x: float = COLS[k] + 3.0
		var zw := Z1 - 0.7
		if k % 2 == 1:
			g.box(Mats.painted("CabinetGrey", Color(0.5, 0.52, 0.52), 0.5, 0.4), Vector3(x, 1.4, zw - 0.35), Vector3(2.4, 2.8, 0.7))
			for d: float in [-0.6, 0.6]:
				g.box(Mats.dark(), Vector3(x + d, 1.5, zw - 0.71), Vector3(1.1, 2.5, 0.03))
			g.box(Mats.glow("CabinetLed", Color(0.2, 1.0, 0.4), 4.0), Vector3(x - 0.9, 2.55, zw - 0.73), Vector3(0.1, 0.1, 0.02))
			g.box(Mats.steel(), Vector3(x, 4.2, zw - 0.15), Vector3(0.6, 2.8, 0.3))
		else:
			g.box(red, Vector3(x, 1.8, zw - 0.05), Vector3(1.2, 1.6, 0.1))
			g.cylinder(red, Vector3(x - 0.3, 0.3, zw - 0.3), Vector3(x - 0.3, 1.1, zw - 0.3), 0.15, 10)
			g.cylinder(red, Vector3(x + 0.3, 1.2, zw - 0.2), Vector3(x + 0.3, 1.2, zw - 0.6), 0.4, 14)
	for x: float in COLS:
		for zz: float in [Z0 + 0.5, Z1 - 0.5]:
			var inward := 1.0 if zz < -70.0 else -1.0
			g.box(Mats.dark(), Vector3(x + 2.8, 9.0, zz + inward * 0.2), Vector3(0.9, 0.5, 0.4))
			g.box(Mats.lamp(), Vector3(x + 2.8, 8.75, zz + inward * 0.25), Vector3(0.7, 0.04, 0.3))
	g.commit(root, "HangarServices", true, false)

# ------------------------------------------------------------------ control mezzanine
static func mezzanine(root: Node3D) -> void:
	var g := Geo.new()
	var blue := _frame_blue()
	var grate := _grate()
	var x0 := X0 + 0.5
	var x1 := X0 + 11.0
	var za := -98.0
	var zb := -58.0
	var deck := 7.2
	g.aabb(grate, Vector3(x0, deck - 0.25, za), Vector3(x1, deck, zb))
	g.box(blue, Vector3(x1 - 0.2, deck - 0.6, (za + zb) * 0.5), Vector3(0.4, 0.7, zb - za))
	for z in range(int(za) + 1, int(zb) + 1, 8):
		g.box(blue, Vector3(x1 - 0.4, (deck - 0.95) * 0.5, z), Vector3(0.5, deck - 0.95, 0.5))
		g.box(Mats.steel(), Vector3(x1 - 0.4, 0.06, z), Vector3(1.0, 0.12, 1.0))
		Geo.box_collider(root, Vector3(x1 - 0.4, deck * 0.5, z), Vector3(0.6, deck, 0.6))
	# railing on the open edge
	g.beam(Mats.yellow(), Vector3(x1 - 0.1, deck + 1.1, za), Vector3(x1 - 0.1, deck + 1.1, zb), 0.08)
	g.beam(Mats.yellow(), Vector3(x1 - 0.1, deck + 0.55, za), Vector3(x1 - 0.1, deck + 0.55, zb), 0.06)
	var n := int((zb - za) / 2.0)
	for k in n + 1:
		var z := lerpf(za, zb, float(k) / n)
		g.beam(Mats.yellow(), Vector3(x1 - 0.1, deck, z), Vector3(x1 - 0.1, deck + 1.1, z), 0.07)
	g.box(Mats.yellow(), Vector3(x1 - 0.1, deck + 0.1, (za + zb) * 0.5), Vector3(0.05, 0.2, zb - za))
	# glazed control room on the deck, looking over the bays
	var cz0 := -94.0
	var cz1 := -66.0
	var cx1 := x1 - 2.2
	var ch := 3.6
	var wall := Mats.painted("ControlWall", Color(0.58, 0.6, 0.6), 0.55, 0.2)
	g.aabb(wall, Vector3(x0, deck, cz0), Vector3(cx1, deck + ch, cz0 + 0.25))
	g.aabb(wall, Vector3(x0, deck, cz1 - 0.25), Vector3(cx1, deck + ch, cz1))
	g.aabb(wall, Vector3(x0, deck + ch, cz0), Vector3(cx1 + 0.4, deck + ch + 0.35, cz1))
	g.aabb(wall, Vector3(cx1 - 0.2, deck, cz0), Vector3(cx1, deck + 0.9, cz1))
	# light, partly see-through glazing on its own shadowless mesh, so the lit room reads from the floor
	var glaze := Geo.new()
	glaze.aabb(_control_glass(), Vector3(cx1 - 0.1, deck + 0.9, cz0), Vector3(cx1 - 0.05, deck + ch, cz1))
	glaze.commit(root, "ControlGlass", false, false)
	var mull := cz0
	while mull <= cz1:
		g.box(Mats.dark(), Vector3(cx1 - 0.08, deck + ch * 0.5 + 0.45, mull), Vector3(0.14, ch - 0.9, 0.14))
		mull += 4.0
	# consoles and screens inside
	var screen := Mats.glow("ConsoleScreen", Color(0.2, 0.75, 0.9), 2.5)
	var z := cz0 + 2.0
	while z < cz1 - 1.5:
		g.box(Mats.dark(), Vector3(cx1 - 1.1, deck + 0.5, z), Vector3(1.0, 1.0, 2.6))
		g.box(Mats.dark(), Vector3(cx1 - 1.4, deck + 1.35, z), Vector3(0.1, 0.8, 2.2), Basis(Vector3.FORWARD, 0.2))
		g.box(screen, Vector3(cx1 - 1.33, deck + 1.35, z), Vector3(0.02, 0.65, 2.0), Basis(Vector3.FORWARD, 0.2))
		z += 4.0
	# ceiling light panels and lamps spaced down the room
	var lz := cz0 + 4.0
	while lz < cz1 - 2.0:
		g.box(Mats.glow("ControlPanelLight", Color(0.85, 0.93, 1.0), 3.0), Vector3((x0 + cx1) * 0.5, deck + ch - 0.03, lz), Vector3(2.4, 0.04, 1.2))
		var lamp := OmniLight3D.new()
		lamp.position = Vector3((x0 + cx1) * 0.5, deck + ch - 0.5, lz)
		lamp.light_color = Color(0.8, 0.9, 1.0)
		lamp.omni_range = 9.0
		lamp.light_energy = 2.2
		root.add_child(lamp)
		lz += 8.0
	# stair down to the hall floor at the south end
	var steps := 18
	var sx := x1 - 1.6
	for k in steps:
		var t := float(k) / steps
		var y := deck - t * deck
		var zz := zb + t * 10.0
		g.box(grate, Vector3(sx, y - 0.1, zz + 0.3), Vector3(1.8, 0.08, 0.5))
	for s: float in [-1.0, 1.0]:
		g.beam(blue, Vector3(sx + s * 0.95, deck, zb), Vector3(sx + s * 0.95, 0.0, zb + 10.0), 0.12, 0.35)
		g.beam(Mats.yellow(), Vector3(sx + s * 0.95, deck + 1.0, zb), Vector3(sx + s * 0.95, 1.0, zb + 10.0), 0.06)
	g.commit(root, "Mezzanine", true, false)
	Geo.box_collider(root, Vector3((x0 + x1) * 0.5, deck - 0.12, (za + zb) * 0.5), Vector3(x1 - x0, 0.25, zb - za))

# ------------------------------------------------------------------ floor
static func floor_detail(root: Node3D) -> void:
	var g := Geo.new()
	var grate := _grate()
	var frame := Mats.steel()
	# drainage trench across the door threshold and down the centre aisle
	var trenches := [[Vector3(X1 - 3.0, 0, Site.DOOR_Z0 + 1.0), Vector3(X1 - 2.2, 0, Site.DOOR_Z1 - 1.0)],
		[Vector3(-126.0, 0, -84.4), Vector3(-82.0, 0, -83.6)], [Vector3(-126.0, 0, -64.4), Vector3(-82.0, 0, -63.6)]]
	for t in trenches:
		var a: Vector3 = t[0]
		var b: Vector3 = t[1]
		g.aabb(frame, Vector3(a.x - 0.12, 0.0, a.z - 0.12), Vector3(b.x + 0.12, 0.025, b.z + 0.12))
		g.aabb(grate, Vector3(a.x, 0.0, a.z), Vector3(b.x, 0.03, b.z))
	# tie-down points around every bay
	for c: Vector3 in Site.BAYS:
		for sx: float in [-4.5, 4.5]:
			for sz: float in [-4.5, 4.5]:
				var p := c + Vector3(sx, 0, sz)
				g.cylinder(Mats.steel(), p, p + Vector3(0, 0.04, 0), 0.35, 10)
				g.cylinder(Mats.dark(), p + Vector3(-0.18, 0.08, 0), p + Vector3(0.18, 0.08, 0), 0.05, 6)
	# floor access hatches near the walls
	for x in range(-124, -84, 12):
		for z: float in [-105.0, -43.0]:
			g.aabb(frame, Vector3(x - 0.9, 0.0, z - 0.9), Vector3(x + 0.9, 0.02, z + 0.9))
			g.aabb(Mats.dark(), Vector3(x - 0.75, 0.0, z - 0.75), Vector3(x + 0.75, 0.025, z + 0.75))
	g.commit(root, "FloorDetail", false, false)

# ------------------------------------------------------------------ exterior
static func exterior(root: Node3D) -> void:
	var g := Geo.new()
	var flash := Mats.painted("Flashing", Color(0.3, 0.32, 0.34), 0.45, 0.6)
	var zc := (Site.DOOR_Z0 + Site.DOOR_Z1) * 0.5
	# roller door: slat curtain below the header and steel guide rails on the jambs
	var y := Site.DOOR_H - 0.25
	while y > Site.DOOR_H - 0.85:
		g.box(Mats.painted("DoorSlat", Color(0.52, 0.54, 0.55), 0.45, 0.6), Vector3(X1 - 0.9, y, zc), Vector3(0.18, 0.22, Site.DOOR_Z1 - Site.DOOR_Z0 + 0.4))
		y -= 0.25
	for jz: float in [Site.DOOR_Z0 + 0.15, Site.DOOR_Z1 - 0.15]:
		g.box(Mats.steel(), Vector3(X1 - 0.9, Site.DOOR_H * 0.5, jz), Vector3(0.5, Site.DOOR_H, 0.3))
	# panel flashing bands round the clad walls
	for yy: float in [3.2, 9.0, 15.0]:
		g.box(flash, Vector3((X0 + X1) * 0.5, yy, Z0 - 0.08), Vector3(X1 - X0, 0.18, 0.16))
		g.box(flash, Vector3((X0 + X1) * 0.5, yy, Z1 + 0.08), Vector3(X1 - X0, 0.18, 0.16))
		g.box(flash, Vector3(X0 - 0.08, yy, (Z0 + Z1) * 0.5), Vector3(0.16, 0.18, Z1 - Z0))
	# gutters and downpipes
	for zz: float in [Z0 - 0.9, Z1 + 0.9]:
		g.box(_galv(), Vector3((X0 + X1) * 0.5, EAVE - 0.35, zz), Vector3(X1 - X0 + 1.2, 0.5, 0.6))
	var k := 0
	for x in range(int(X0) + 6, int(X1), 11):
		k += 1
		if k % 2 == 0:
			continue
		for zz: float in [Z0 - 0.9, Z1 + 0.9]:
			var px := x + 1.6
			g.cylinder(_galv(), Vector3(px, EAVE - 0.4, zz), Vector3(px, 0.6, zz), 0.16, 8)
			g.cylinder(_galv(), Vector3(px, 0.6, zz), Vector3(px, 0.25, zz + signf(zz - Z0 - 1.0) * 0.5), 0.16, 8)
			for yy: float in [5.0, 11.0, 17.0]:
				g.box(Mats.steel(), Vector3(px, yy, zz + signf(zz - Z0 - 1.0) * -0.35), Vector3(0.4, 0.12, 0.5))
	# bulkhead lamps on the north pilasters
	for x in range(int(X0) + 6, int(X1), 11):
		g.box(Mats.dark(), Vector3(x, 7.0, Z0 - 0.9), Vector3(0.6, 0.4, 0.4))
		g.box(Mats.lamp(), Vector3(x, 6.78, Z0 - 0.95), Vector3(0.5, 0.04, 0.3))
	# caged ladder to the roof on the north wall
	var lx := -86.0
	var lz := Z0 - 0.6
	for s: float in [-0.35, 0.35]:
		g.beam(Mats.yellow(), Vector3(lx + s, 0, lz), Vector3(lx + s, EAVE + 1.2, lz), 0.07)
	var ry := 0.4
	while ry < EAVE + 1.0:
		g.beam(Mats.yellow(), Vector3(lx - 0.35, ry, lz), Vector3(lx + 0.35, ry, lz), 0.04)
		ry += 0.3
	var hy := 2.6
	while hy < EAVE + 1.0:
		var prev := Vector3(lx - 0.4, hy, lz)
		for a in range(1, 9):
			var ang := PI * a / 8.0
			var p := Vector3(lx - cos(ang) * 0.4, hy, lz - sin(ang) * 0.75)
			g.beam(Mats.yellow(), prev, p, 0.04)
			prev = p
		hy += 1.2
	for a in [0.2, 0.5, 0.8]:
		var ang: float = PI * a
		g.beam(Mats.yellow(), Vector3(lx - cos(ang) * 0.4, 2.6, lz - sin(ang) * 0.75), Vector3(lx - cos(ang) * 0.4, EAVE + 1.0, lz - sin(ang) * 0.75), 0.035)
	# exit doors with canopies, lamps and steps on the north wall
	for dx: float in [-126.0, -96.0]:
		g.box(Mats.navy(), Vector3(dx, 1.25, Z0 - 0.05), Vector3(2.0, 2.5, 0.12))
		g.box(Mats.steel(), Vector3(dx, 1.25, Z0 - 0.1), Vector3(2.3, 2.7, 0.06))
		g.box(Mats.steel(), Vector3(dx + 0.6, 1.1, Z0 - 0.2), Vector3(0.5, 0.06, 0.06))
		g.box(Mats.dark(), Vector3(dx, 3.2, Z0 - 0.9), Vector3(3.2, 0.15, 1.8))
		for s: float in [-1.4, 1.4]:
			g.beam(Mats.dark(), Vector3(dx + s, 3.2, Z0 - 1.7), Vector3(dx + s, 4.4, Z0 - 0.1), 0.06)
		g.box(Mats.glow("ExitGreen", Color(0.1, 0.9, 0.3), 4.0), Vector3(dx, 2.85, Z0 - 0.12), Vector3(0.9, 0.3, 0.05))
		g.box(Mats.concrete("Plinth", Color(0.55, 0.54, 0.52), 0.0), Vector3(dx, 0.1, Z0 - 0.9), Vector3(2.8, 0.2, 1.6))
	# transformer compound by the north wall
	var tc := Vector3(-124.0, 0, Z0 - 6.0)
	g.box(Mats.concrete("Plinth", Color(0.55, 0.54, 0.52), 0.0), tc + Vector3(0, 0.15, 0), Vector3(8.0, 0.3, 5.0))
	g.box(Mats.painted("TransformerGreen", Color(0.24, 0.32, 0.26), 0.55, 0.4), tc + Vector3(0, 1.6, 0), Vector3(3.6, 2.6, 2.4))
	for f in 9:
		g.box(Mats.painted("TransformerGreen", Color(0.24, 0.32, 0.26), 0.55, 0.4), tc + Vector3(-1.6 + f * 0.4, 1.5, 1.45), Vector3(0.06, 2.0, 0.5))
	for b: float in [-1.0, 0.0, 1.0]:
		g.cylinder(Mats.white(), tc + Vector3(b, 2.9, 0), tc + Vector3(b, 3.7, 0), 0.12, 8)
	g.cylinder(Mats.dark(), tc + Vector3(0, 2.0, -1.2), tc + Vector3(0, 6.0, -5.5), 0.2, 8)
	for fx: float in [-4.0, 4.0]:
		g.beam(Mats.steel(), tc + Vector3(fx, 0, -2.6), tc + Vector3(fx, 0, 2.6), 0.05)
		for fz: float in [-2.5, 0.0, 2.5]:
			g.beam(Mats.steel(), tc + Vector3(fx, 0, fz), tc + Vector3(fx, 2.2, fz), 0.08)
		g.beam(Mats.steel(), tc + Vector3(fx, 2.2, -2.5), tc + Vector3(fx, 2.2, 2.5), 0.06)
	for fz: float in [2.6]:
		g.beam(Mats.steel(), tc + Vector3(-4, 2.2, fz), tc + Vector3(4, 2.2, fz), 0.06)
		for fx2 in range(-4, 5, 2):
			g.beam(Mats.steel(), tc + Vector3(fx2, 0, fz), tc + Vector3(fx2, 2.2, fz), 0.08)
	g.box(Mats.lit("FenceMesh", Color(0.4, 0.42, 0.44), 0.6, 0.6), tc + Vector3(0, 1.1, 2.6), Vector3(8.0, 2.1, 0.02))
	Geo.box_collider(root, tc + Vector3(0, 1.2, 0), Vector3(8.0, 2.4, 5.2))
	# bollards along the north wall
	for x in range(int(X0) + 3, int(X1), 6):
		g.cylinder(Mats.yellow(), Vector3(x, 0, Z0 - 2.4), Vector3(x, 1.1, Z0 - 2.4), 0.16, 10)
		g.cylinder(Mats.dark(), Vector3(x, 0.8, Z0 - 2.4), Vector3(x, 0.9, Z0 - 2.4), 0.17, 10)
	g.commit(root, "HangarExterior", true, false)

# ------------------------------------------------------------------ practicals
## Human-scale entrance on the yard face and warm working light: a personnel
## door with a canopy, stair and wall lamps beside the main opening, wall-pack
## lamps high on the facade, and warm fill just inside the doorway so the hall
## reads from the bright yard. Lights cast no shadows; night-only ones are in
## the night_lights group.
static func practicals(root: Node3D) -> void:
	var g := Geo.new()
	var x := X1
	var dz := -43.0
	# personnel door, frame, canopy and a short stair from the yard (2.1 m door: human scale)
	g.box(Mats.navy(), Vector3(x + 0.08, 1.05 + 0.6, dz), Vector3(0.1, 2.1, 1.0))
	g.box(Mats.steel(), Vector3(x + 0.05, 1.15 + 0.6, dz), Vector3(0.08, 2.4, 1.3))
	g.box(Mats.glass(), Vector3(x + 0.14, 2.05 + 0.6, dz), Vector3(0.02, 0.5, 0.4))
	g.box(Mats.dark(), Vector3(x + 1.0, 3.0, dz), Vector3(2.0, 0.12, 2.0))
	for k in 3:
		g.box(Mats.concrete("Plinth", Color(0.55, 0.54, 0.52), 0.0), Vector3(x + 0.6 + k * 0.4, 0.1 + (2 - k) * 0.2, dz), Vector3(0.4 + (2 - k) * 0.0, 0.2 + (2 - k) * 0.4, 1.6))
	for s: float in [-0.9, 0.9]:
		g.beam(Mats.yellow(), Vector3(x + 1.8, 0.0, dz + s), Vector3(x + 1.8, 1.6, dz + s), 0.05)
		g.beam(Mats.yellow(), Vector3(x + 0.3, 1.6, dz + s), Vector3(x + 1.8, 1.6, dz + s), 0.05)
	g.box(Mats.lamp(), Vector3(x + 0.12, 3.4, dz), Vector3(0.1, 0.18, 0.6))
	g.box(Mats.glow("DoorSign", Color(0.2, 0.9, 0.4), 3.0), Vector3(x + 0.12, 3.0, dz - 0.9), Vector3(0.05, 0.25, 0.5))
	# wall packs high on the facade either side of the opening
	for wz: float in [Z0 + 3.0, Z1 - 3.0, Site.DOOR_Z0 - 1.5, Site.DOOR_Z1 + 1.5]:
		g.box(Mats.dark(), Vector3(x + 0.25, 12.0, wz), Vector3(0.5, 0.45, 0.7))
		g.box(Mats.lamp(), Vector3(x + 0.5, 11.8, wz), Vector3(0.05, 0.12, 0.55))
	g.commit(root, "FacadePracticals", true, false)
	for wz: float in [Z0 + 3.0, Z1 - 3.0]:
		var l := OmniLight3D.new()
		l.position = Vector3(x + 2.0, 11.0, wz)
		l.light_color = Color(1.0, 0.82, 0.6)
		l.light_energy = 2.0
		l.omni_range = 18.0
		l.add_to_group("night_lights")
		root.add_child(l)
	var porch := OmniLight3D.new()
	porch.position = Vector3(x + 1.5, 3.2, dz)
	porch.light_color = Color(1.0, 0.8, 0.58)
	porch.light_energy = 1.4
	porch.omni_range = 8.0
	porch.add_to_group("night_lights")
	root.add_child(porch)
	# warm fill inside the doorway, always on: the hall glows against the cool yard
	for fz: float in [Site.DOOR_Z0 + 4.0, Site.DOOR_Z1 - 4.0]:
		var f := OmniLight3D.new()
		f.position = Vector3(x - 9.0, 9.0, fz)
		f.light_color = Color(1.0, 0.84, 0.62)
		f.light_energy = 1.6
		f.omni_range = 26.0
		root.add_child(f)
