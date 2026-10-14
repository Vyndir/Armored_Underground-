class_name BuildBounds
extends RefCounted
## The edge of the playable slice, one closed loop (Site.BOUNDARY) with something
## visible on every metre of it and collision on exactly the same line:
##   hillside and south   security fence with posts, top rail and limit signs
##   north line           fence, plus a road checkpoint and the strip's end barrier
##   quay (deep water)    the unbroken parapet with a rail and DEEP WATER signs
##   training shelf       floating booms between steel piles with lamps and signs
## The walls are tall enough that jumping, boosting or hovering cannot clear them,
## and sit on their own physics layer so they stop the mech but not the camera.

const LAYER := 2           # physics layer for boundary walls (mech mask includes it)
const WALL_H := 90.0

static func build(root: Node3D) -> void:
	var pts: Array = Site.BOUNDARY
	var g := Geo.new()
	for i in pts.size() - 1:
		var a: Vector2 = pts[i]
		var b: Vector2 = pts[i + 1]
		_wall(root, a, b)
		var quay := a.x == Site.APRON_X1 and b.x == Site.APRON_X1
		var mid := (a + b) * 0.5
		if quay:
			_quay_rail(g, a, b)
		elif not Site.on_land(mid.x, mid.y):
			_boom(g, a, b)
		else:
			_fence(g, a, b)
	_checkpoint(g)
	g.commit(root, "Boundary", true, false)
	_signs(root)

static func _wall(root: Node3D, a: Vector2, b: Vector2) -> void:
	var d := b - a
	var c := (a + b) * 0.5
	var body := Geo.box_collider(root, Vector3(c.x, WALL_H * 0.5 - 12.0, c.y), Vector3(1.6, WALL_H, d.length() + 3.0), Basis(Vector3.UP, atan2(d.x, d.y)))
	body.collision_layer = 1 << (LAYER - 1)
	body.collision_mask = 0
	body.name = "BoundaryWall"

static func _y(x: float, z: float) -> float:
	if Site.in_paved(x, z, 0.5):
		return 0.0
	return maxf(Site.ground(x, z), Site.WATER_Y)

## Security fence: steel posts every 4 m, rails, mesh panel, sized against the mech
## (about thigh height, 4.5 m) so it reads as a barrier from the chase camera.
static func _fence(g: Geo, a: Vector2, b: Vector2) -> void:
	var post := Mats.painted("FencePost", Color(0.32, 0.34, 0.36), 0.5, 0.8)
	var mesh := _mesh_mat()
	var n := maxi(1, int(a.distance_to(b) / 4.0))
	var prev := Vector3.ZERO
	for k in n + 1:
		var p2 := a.lerp(b, float(k) / n)
		var p := Vector3(p2.x, _y(p2.x, p2.y), p2.y)
		if not _gap(p):
			g.cylinder(post, p - Vector3(0, 0.6, 0), p + Vector3(0, 4.6, 0), 0.1, 6)
			if k % 10 == 0:
				g.box(Mats.hazard(), p + Vector3(0, 0.6, 0), Vector3(0.5, 1.2, 0.5))
		if k > 0 and not _gap((prev + p) * 0.5) and not _gap(prev) and not _gap(p):
			g.beam(post, prev + Vector3(0, 4.5, 0), p + Vector3(0, 4.5, 0), 0.08)
			g.beam(post, prev + Vector3(0, 0.3, 0), p + Vector3(0, 0.3, 0), 0.06)
			var mid := (prev + p) * 0.5 + Vector3(0, 2.4, 0)
			var dir := p - prev
			g.box(mesh, mid, Vector3(0.02, 4.2, dir.length()), Basis(Vector3.UP, atan2(dir.x, dir.z)), 4.0, [], false)
		prev = p

## Openings in the north fence line: the road (closed by the checkpoint) and the
## test strip (closed by jersey barriers); the wall behind them is continuous.
static func _gap(p: Vector3) -> bool:
	return absf(p.z - Site.SLICE_Z) < 0.5 and (absf(p.x - Site.ROAD_X) < Site.ROAD_W * 0.5 + 1.0 or p.x > Site.STRIP_X0 - 1.0)

static var _mesh_m: StandardMaterial3D
static func _mesh_mat() -> StandardMaterial3D:
	if _mesh_m:
		return _mesh_m
	var img := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))
	for i in 64:
		for w in 3:
			img.set_pixel(i, (i + w) % 64, Color(0.6, 0.62, 0.64, 1))
			img.set_pixel(i, (63 - i + w) % 64, Color(0.6, 0.62, 0.64, 1))
	img.generate_mipmaps()
	_mesh_m = StandardMaterial3D.new()
	_mesh_m.albedo_texture = ImageTexture.create_from_image(img)
	_mesh_m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
	_mesh_m.alpha_scissor_threshold = 0.4
	_mesh_m.cull_mode = BaseMaterial3D.CULL_DISABLED
	_mesh_m.metallic = 0.7
	_mesh_m.roughness = 0.45
	_mesh_m.uv1_scale = Vector3(1, 1, 1)
	_mesh_m.uv1_triplanar = true
	_mesh_m.uv1_scale = Vector3(1.6, 1.6, 1.6)
	return _mesh_m

## Floating boom between steel piles: a line of yellow floats with a dark skirt,
## a pile every 20 m standing 6 m out of the water with an amber lamp.
static func _boom(g: Geo, a: Vector2, b: Vector2) -> void:
	var y := Site.WATER_Y
	var len := a.distance_to(b)
	var n := maxi(1, int(len / 1.6))
	for k in n:
		var p2 := a.lerp(b, (k + 0.5) / n)
		var dir := (b - a).normalized()
		var c := Vector3(p2.x, y + 0.25, p2.y)
		g.cylinder(Mats.yellow(), c - Vector3(dir.x, 0, dir.y) * 0.7, c + Vector3(dir.x, 0, dir.y) * 0.7, 0.45, 10)
	g.beam(Mats.dark(), Vector3(a.x, y - 0.3, a.y), Vector3(b.x, y - 0.3, b.y), 0.08, 1.0)
	var np := maxi(1, int(len / 20.0))
	for k in np + 1:
		var p2 := a.lerp(b, float(k) / np)
		var p := Vector3(p2.x, y, p2.y)
		g.cylinder(Mats.steel(), p - Vector3(0, 6, 0), p + Vector3(0, 6.0, 0), 0.45, 10)
		g.box(Mats.hazard(), p + Vector3(0, 4.6, 0), Vector3(1.0, 1.4, 1.0))
		g.box(Mats.glow("PileLamp", Color(1.0, 0.6, 0.15), 5.0), p + Vector3(0, 6.2, 0), Vector3(0.4, 0.4, 0.4))

## Steel rail on top of the quay parapet along the deep-water edge.
static func _quay_rail(g: Geo, a: Vector2, b: Vector2) -> void:
	var x := a.x - 1.4
	var n := maxi(1, int(a.distance_to(b) / 3.0))
	for k in n + 1:
		var z := lerpf(a.y, b.y, float(k) / n)
		g.beam(Mats.yellow(), Vector3(x, 3.2, z), Vector3(x, 4.4, z), 0.07)
	for yy: float in [3.8, 4.4]:
		g.beam(Mats.yellow(), Vector3(x, yy, a.y), Vector3(x, yy, b.y), 0.06)

## Road checkpoint on the north line: a booth, a lowered barrier arm, a row of
## jersey barriers across both lanes, and the end of the test strip.
static func _checkpoint(g: Geo) -> void:
	var z := Site.SLICE_Z + 1.5
	var x := Site.ROAD_X
	var booth := Vector3(x - Site.ROAD_W * 0.5 - 3.0, 0, z + 3.0)
	g.box(Mats.white(), booth + Vector3(0, 1.5, 0), Vector3(3.0, 3.0, 3.0))
	g.box(Mats.glass(), booth + Vector3(1.52, 1.9, 0), Vector3(0.05, 1.2, 2.4))
	g.box(Mats.dark(), booth + Vector3(0, 3.1, 0), Vector3(3.6, 0.2, 3.6))
	g.box(Mats.dark(), Vector3(x - Site.ROAD_W * 0.5 - 0.6, 0.6, z), Vector3(0.8, 1.2, 0.8))
	g.box(Mats.hazard(), Vector3(x, 1.1, z), Vector3(Site.ROAD_W - 1.0, 0.2, 0.2))
	for k in 6:
		var bx := x - Site.ROAD_W * 0.5 + 1.0 + k * 2.0
		g.box(Mats.concrete("Jersey", Color(0.75, 0.74, 0.7), 0.0), Vector3(bx, 0.55, z + 1.2), Vector3(1.9, 1.1, 0.7))
		g.box(Mats.orange(), Vector3(bx, 0.85, z + 0.84), Vector3(1.9, 0.18, 0.02))
	for k in 12:
		var bx := Site.STRIP_X0 + 1.0 + k * 2.0
		g.box(Mats.concrete("Jersey", Color(0.75, 0.74, 0.7), 0.0), Vector3(bx, 0.55, z + 1.2), Vector3(1.9, 1.1, 0.7))

## Readable limit signs: large Label3D panels on posts.
static func _signs(root: Node3D) -> void:
	var specs := [
		[Vector3(Site.ROAD_X, 6.0, Site.SLICE_Z + 1.0), 0.0, "ROAD CLOSED · EVENT TRAFFIC\nEND OF TRAINING AREA"],
		[Vector3(27.0, 5.0, Site.SLICE_Z + 1.0), 0.0, "END OF TEST STRIP"],
		[Vector3(36.5, 6.2, -300.0), -PI * 0.5, "DEEP WATER · NO FRAMES BEYOND QUAY"],
		[Vector3(36.5, 6.2, -150.0), -PI * 0.5, "DEEP WATER · NO FRAMES BEYOND QUAY"],
		[Vector3(Site.SHELF_X1 - 0.6, 3.8, 5.0), -PI * 0.5, "TRAINING LIMIT · DEEP WATER BEYOND BOOM"],
		[Vector3(80.0, 3.8, Site.SHELF_Z0 + 0.6), 0.0, "TRAINING LIMIT"],
		[Vector3(80.0, 3.8, Site.SHELF_Z1 - 0.6), PI, "TRAINING LIMIT"],
		[Vector3(-212.0, 0.0, -120.0), -PI * 0.5, "AREA LIMIT"],
		[Vector3(-212.0, 0.0, -330.0), -PI * 0.5, "AREA LIMIT"],
		[Vector3(-90.0, 0.0, 86.0), PI, "AREA LIMIT"],
	]
	for s in specs:
		var p: Vector3 = s[0]
		if p.y == 0.0:
			p.y = maxf(Site.ground(p.x, p.z), 0.0) + 5.4
		var panel := MeshInstance3D.new()
		var bm := BoxMesh.new()
		var text: String = s[2]
		var w := clampf(text.length() * 0.38, 4.0, 13.0) if not text.contains("\n") else 12.0
		bm.size = Vector3(w, 1.9 if text.contains("\n") else 1.2, 0.12)
		panel.mesh = bm
		panel.material_override = Mats.painted("SignYellow", Color(0.95, 0.72, 0.1), 0.5, 0.1)
		root.add_child(panel)
		panel.position = p
		panel.rotation.y = s[1]
		var l := Label3D.new()
		l.text = text
		l.font = Tex.paint_font()
		l.font_size = 64
		l.pixel_size = 0.012
		l.modulate = Color(0.08, 0.08, 0.08)
		l.outline_size = 0
		l.position = Vector3(0, 0, 0.07)
		l.double_sided = false
		panel.add_child(l)
		var back := l.duplicate() as Label3D
		back.position = Vector3(0, 0, -0.07)
		back.rotation.y = PI
		panel.add_child(back)
		var post := MeshInstance3D.new()
		var cm := CylinderMesh.new()
		cm.top_radius = 0.12
		cm.bottom_radius = 0.12
		cm.height = p.y + 4.0
		post.mesh = cm
		post.material_override = Mats.steel()
		root.add_child(post)
		post.position = Vector3(p.x, p.y * 0.5 - 2.0, p.z) - Basis(Vector3.UP, s[1]) * Vector3(0, 0, 0.15)
