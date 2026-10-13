class_name Mats
extends RefCounted
## Shared materials for the training hangar site.

static var _m := {}

static func lit(key: String, color: Color, rough := 0.7, metal := 0.0, nrm: Texture2D = null, nrm_scale := 1.0, uv := Vector3.ONE, triplanar := false) -> StandardMaterial3D:
	if _m.has(key):
		return _m[key]
	var m := StandardMaterial3D.new()
	m.resource_name = key
	m.albedo_color = color
	m.roughness = rough
	m.metallic = metal
	if nrm:
		m.normal_enabled = true
		m.normal_texture = nrm
		m.normal_scale = nrm_scale
	m.uv1_scale = uv
	m.uv1_triplanar = triplanar
	if triplanar:
		m.uv1_world_triplanar = true
	_m[key] = m
	return m

static func glow(key: String, color: Color, energy := 3.0) -> StandardMaterial3D:
	if _m.has(key):
		return _m[key]
	var m := StandardMaterial3D.new()
	m.resource_name = key
	m.albedo_color = color
	m.emission_enabled = true
	m.emission = color
	m.emission_energy_multiplier = energy
	m.roughness = 0.4
	_m[key] = m
	return m

static func shader(key: String, path: String, params := {}) -> ShaderMaterial:
	if _m.has(key):
		return _m[key]
	var m := ShaderMaterial.new()
	m.resource_name = key
	m.shader = load(path)
	for k in params:
		m.set_shader_parameter(k, params[k])
	_m[key] = m
	return m

static func get_m(key: String) -> Material:
	return _m.get(key)

# ------------------------------------------------------------------ the palette
static func concrete(key := "Concrete", color := Color(0.6, 0.59, 0.56), wet := 0.35, grooves := 0.0) -> ShaderMaterial:
	return shader(key, "res://shaders/concrete.gdshader", {
		"noise_a": Tex.noise(0.01, 11), "noise_b": Tex.noise(0.04, 12),
		"detail_nrm": Tex.noise(0.06, 13, 512, true, 5.0), "base_color": color,
		"wetness": wet, "grooves": grooves, "triplanar_walls": 1.0,
	})

static func painted(key: String, color: Color, rough := 0.5, metal := 0.35) -> StandardMaterial3D:
	return lit(key, color, rough, metal, Tex.noise(0.08, key.hash() % 97, 256, true, 2.0), 0.35, Vector3(0.25, 0.25, 0.25), true)

static func corrugated(key: String, color: Color, ribs := 16) -> StandardMaterial3D:
	var m := lit(key, color, 0.55, 0.55, Tex.corrugated(ribs), 1.2, Vector3(0.12, 0.12, 0.12), true)
	return m

static func steel() -> StandardMaterial3D: return painted("Steel", Color(0.32, 0.34, 0.37), 0.42, 0.85)
static func dark() -> StandardMaterial3D: return painted("DarkSteel", Color(0.09, 0.1, 0.11), 0.5, 0.7)
static func yellow() -> StandardMaterial3D: return painted("SafetyYellow", Color(0.92, 0.66, 0.08), 0.45, 0.3)
static func orange() -> StandardMaterial3D: return painted("BuoyOrange", Color(0.95, 0.36, 0.06), 0.4, 0.0)
static func white() -> StandardMaterial3D: return painted("PaintWhite", Color(0.86, 0.87, 0.86), 0.5, 0.1)
static func navy() -> StandardMaterial3D: return painted("Navy", Color(0.08, 0.11, 0.2), 0.5, 0.2)
static func olive() -> StandardMaterial3D: return painted("CrateOlive", Color(0.22, 0.25, 0.2), 0.65, 0.3)
static func rubber() -> StandardMaterial3D: return lit("Rubber", Color(0.04, 0.04, 0.04), 0.85, 0.0)
static func glass() -> StandardMaterial3D:
	if _m.has("Glass"):
		return _m["Glass"]
	var m := lit("Glass", Color(0.12, 0.17, 0.2), 0.05, 0.6)
	return m
static func lamp() -> StandardMaterial3D: return glow("LampWarm", Color(1.0, 0.86, 0.66), 6.0)
static func hazard() -> StandardMaterial3D:
	if _m.has("Hazard"):
		return _m["Hazard"]
	var img := Image.create(64, 64, false, Image.FORMAT_RGB8)
	for y in 64:
		for x in 64:
			var stripe := int((x + y) / 16) % 2 == 0
			img.set_pixel(x, y, Color(0.92, 0.66, 0.08) if stripe else Color(0.06, 0.06, 0.06))
	img.generate_mipmaps()
	var m := lit("Hazard", Color.WHITE, 0.55, 0.2)
	m.albedo_texture = ImageTexture.create_from_image(img)
	m.uv1_triplanar = true
	m.uv1_world_triplanar = true
	m.uv1_scale = Vector3(0.5, 0.5, 0.5)
	return m
