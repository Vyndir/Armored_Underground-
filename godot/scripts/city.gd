class_name City
extends RefCounted
## The city around the bay, as cheap distant scenery (one MultiMesh of boxes per
## district, the city_block shader for windows and night lights; no shadows, no
## collision). Districts follow the aerial map:
##   Downtown        across the bay beyond the bridge: glass towers in the core,
##                   stone and brick mid-rises around them, a few landmark crowns,
##                   the domed capitol on the water
##   Bridge Quarter  waterfront blocks where the bridge lands on the far bank
##   Bayside         homes and offices on the far shore opposite the hangar, the
##                   view straight out of the hangar door
##   West Approach   a brick neighbourhood behind the bridge's west landing

static var _rng := RandomNumberGenerator.new()
static var _mat: ShaderMaterial

const MASONRY := [Color(0.82, 0.78, 0.7), Color(0.74, 0.66, 0.56), Color(0.66, 0.48, 0.38), Color(0.86, 0.84, 0.8),
	Color(0.58, 0.6, 0.62), Color(0.78, 0.7, 0.58), Color(0.7, 0.56, 0.44)]
const GLASS := [Color(0.5, 0.58, 0.64), Color(0.62, 0.66, 0.68), Color(0.44, 0.52, 0.58)]

static func material() -> ShaderMaterial:
	if _mat:
		return _mat
	_mat = ShaderMaterial.new()
	_mat.shader = load("res://shaders/city_block.gdshader")
	_mat.resource_name = "CityBlock"
	return _mat

static func _box_mesh() -> BoxMesh:
	var b := BoxMesh.new()
	b.size = Vector3.ONE
	return b

static func build(root: Node3D) -> void:
	_rng.seed = 33
	var holder := Node3D.new()
	holder.name = "City"
	root.add_child(holder)
	_district(holder, "Downtown", _downtown())
	_district(holder, "BridgeQuarter", _bridge_quarter())
	_district(holder, "Bayside", _bayside())
	_district(holder, "WestApproach", _west_approach())
	_capitol(holder)

## [position (base centre), size, colour, style, seed]
static func _add(out: Array, p: Vector3, size: Vector3, glass: bool) -> void:
	var col: Color = (GLASS if glass else MASONRY)[_rng.randi() % (GLASS.size() if glass else MASONRY.size())]
	col = col * _rng.randf_range(0.9, 1.06)
	col.a = 1.0
	out.append([p, size, col, 1.0 if glass else 0.0, _rng.randf() * 97.0])

static func _tower(out: Array, p: Vector3, w: float, d: float, h: float, glass: bool) -> void:
	# setback tiers for the taller ones, a crown on the tallest
	if h > 110.0:
		var h1 := h * _rng.randf_range(0.55, 0.7)
		_add(out, p, Vector3(w, h1, d), glass)
		_add(out, p + Vector3(0, h1, 0), Vector3(w * 0.78, h - h1, d * 0.78), glass)
		if h > 180.0:
			_add(out, p + Vector3(0, h, 0), Vector3(w * 0.4, h * 0.1, d * 0.4), false)
	else:
		_add(out, p, Vector3(w, h, d), glass)

static func _downtown() -> Array:
	var out := []
	for k in 230:
		var x := _rng.randf_range(1320.0, 2750.0)
		var z := _rng.randf_range(-2650.0, -1880.0)
		if x - Site.east_coast_x(z) < 40.0:
			continue
		var core := 1.0 - clampf(Vector2(x - 2000.0, z + 2180.0).length() / 650.0, 0.0, 1.0)
		var h := lerpf(18.0, 270.0, pow(core, 1.5)) * _rng.randf_range(0.55, 1.15)
		var w := _rng.randf_range(22.0, 44.0)
		_tower(out, Vector3(x, Site.east_ground(x, z) - 1.0, z), w, w * _rng.randf_range(0.7, 1.3), h, h > 75.0 and _rng.randf() < 0.75)
	return out

static func _bridge_quarter() -> Array:
	var out := []
	for k in 80:
		var z := _rng.randf_range(-1860.0, -1450.0)
		var x := Site.east_coast_x(z) + _rng.randf_range(60.0, 420.0)
		if absf(z - BuildNature.BRIDGE_Z) < 30.0:
			continue
		var h := _rng.randf_range(14.0, 60.0)
		_add(out, Vector3(x, Site.east_ground(x, z) - 1.0, z), Vector3(_rng.randf_range(18.0, 36.0), h, _rng.randf_range(16.0, 34.0)), h > 45.0 and _rng.randf() < 0.4)
	return out

static func _bayside() -> Array:
	var out := []
	for k in 260:
		var z := _rng.randf_range(-1450.0, -180.0)
		var x := Site.east_coast_x(z) + 62.0 + pow(_rng.randf(), 1.6) * 700.0
		var d := x - Site.east_coast_x(z)
		# taller near the water and toward the bridge, low homes inland
		var h := _rng.randf_range(9.0, 26.0) + (1.0 - smoothstep(60.0, 420.0, d)) * _rng.randf_range(0.0, 34.0) + smoothstep(-700.0, -1400.0, z) * _rng.randf_range(0.0, 30.0)
		_add(out, Vector3(x, Site.east_ground(x, z) - 1.0, z), Vector3(_rng.randf_range(14.0, 32.0), h, _rng.randf_range(12.0, 28.0)), h > 50.0 and _rng.randf() < 0.35)
	return out

static func _west_approach() -> Array:
	var out := []
	for k in 110:
		var z := _rng.randf_range(-2450.0, -1560.0)
		var x := _rng.randf_range(-720.0, -190.0)
		if absf(z - BuildNature.BRIDGE_Z) < 40.0 or not Site.on_land(x, z):
			continue
		var h := _rng.randf_range(8.0, 34.0)
		_add(out, Vector3(x, Site.ground(x, z) - 1.0, z), Vector3(_rng.randf_range(14.0, 30.0), h, _rng.randf_range(12.0, 26.0)), false)
	return out

static func _district(holder: Node3D, name: String, items: Array) -> void:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.use_custom_data = true
	mm.mesh = _box_mesh()
	mm.instance_count = items.size()
	for i in items.size():
		var it: Array = items[i]
		var p: Vector3 = it[0]
		var size: Vector3 = it[1]
		var yaw := _rng.randf_range(-0.08, 0.08)
		mm.set_instance_transform(i, Transform3D(Basis(Vector3.UP, yaw).scaled(size), p + Vector3(0, size.y * 0.5, 0)))
		mm.set_instance_color(i, it[2])
		mm.set_instance_custom_data(i, Color(it[3], it[4], 0, 0))
	var mmi := MultiMeshInstance3D.new()
	mmi.name = name
	mmi.multimesh = mm
	mmi.material_override = material()
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	holder.add_child(mmi)

static func _capitol(holder: Node3D) -> void:
	var stone := Mats.lit("FarStone", Color(0.84, 0.81, 0.75), 0.8)
	var g := Geo.new()
	var c := Vector3(1550, Site.east_ground(1550, -1900), -1900)
	g.box(stone, c + Vector3(0, 15, 0), Vector3(110, 30, 60), Basis(), 20.0, [], false)
	g.cylinder(stone, c + Vector3(0, 30, 0), c + Vector3(0, 46, 0), 20, 24, -1.0, true, false)
	var dome := SphereMesh.new()
	dome.radius = 21
	dome.height = 42
	dome.is_hemisphere = true
	g.mesh(Mats.lit("FarDome", Color(0.35, 0.52, 0.62), 0.35, 0.6), dome, Transform3D(Basis(), c + Vector3(0, 46, 0)))
	var mi := g.commit(holder, "Capitol", false, false)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
