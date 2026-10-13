class_name MechRig
extends Node3D
## Loads the exported frame, gives every part the mech surface shader, and
## animates the skeleton procedurally: walk cycle, boost lean, airborne tuck,
## hover crouch, wading drag. Rotations are authored around the frame's own
## axes and converted into each bone's rest frame.

var skel: Skeleton3D
var mech: Mech
var _idx := {}
var _rest_rot := {}
var _rest_global := {}
var _right := Vector3.LEFT
var _fwd := Vector3.BACK
var _pelvis_rest := Vector3.ZERO
var _lean := 0.0
var _crouch := 0.0
var _tuck := 0.0
var _boost_glow := 0.0
var _flames: Array[MeshInstance3D] = []
var _boost_light: OmniLight3D
var _surface_mats: Array[ShaderMaterial] = []
var _glow_mat: StandardMaterial3D

const BONES := ["pelvis", "spine", "chest", "head", "booster", "shoulder.L", "upperarm.L", "forearm.L", "hand.L",
	"shoulder.R", "upperarm.R", "forearm.R", "hand.R", "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R"]

static func surface_material(part: String, paint: Color, accent: Color) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = load("res://shaders/mech_surface.gdshader")
	var base := "res://assets/mech/textures/%s_" % part
	m.set_shader_parameter("albedo_tex", load(base + "albedo.png"))
	m.set_shader_parameter("mask_tex", load(base + "mask.png"))
	m.set_shader_parameter("mra_tex", load(base + "mra.png"))
	m.set_shader_parameter("normal_tex", load(base + "normal.png"))
	m.set_shader_parameter("detail_normal", Tex.noise(0.2, 61, 256, true, 4.0))
	m.set_shader_parameter("paint_color", paint)
	m.set_shader_parameter("accent_color", accent)
	return m

func setup(paint: Color, accent: Color, glow: Color) -> void:
	var scene: PackedScene = load("res://assets/mech/kestrel.glb")
	var model := scene.instantiate()
	add_child(model)
	# the export faces +Z; Godot's forward is -Z
	model.rotation.y = PI
	var mats := {}
	for mi in model.find_children("*", "MeshInstance3D", true, false):
		var m: MeshInstance3D = mi
		for s in m.mesh.get_surface_count():
			var src := m.mesh.surface_get_material(s)
			var nm := src.resource_name if src else ""
			var mat: Material
			if nm.begins_with("MB_"):
				var part := nm.substr(3).split(".")[0]
				if not mats.has(part):
					mats[part] = surface_material(part, paint, accent)
					_surface_mats.append(mats[part])
				mat = mats[part]
			elif nm.begins_with("MC_glow"):
				mat = Mats.glow("MechGlow%s" % glow.to_html(), glow, 4.0)
				_glow_mat = mat
			elif nm.begins_with("MC_thrust"):
				mat = Mats.glow("MechThrust", Color(0.4, 0.8, 1.0), 2.0)
			elif nm.begins_with("MC_glass"):
				mat = Mats.lit("MechGlass", Color(0.05, 0.08, 0.1), 0.05, 0.7)
			else:
				mat = Mats.steel()
			m.set_surface_override_material(s, mat)
		m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	var found := model.find_children("*", "Skeleton3D", true, false)
	if found.is_empty():
		push_warning("mech: no skeleton in kestrel.glb")
		return
	skel = found[0]
	for b in BONES:
		var i := skel.find_bone(b)
		if i >= 0:
			_idx[b] = i
			_rest_rot[b] = skel.get_bone_rest(i).basis.get_rotation_quaternion()
			_rest_global[b] = skel.get_bone_global_rest(i).basis.orthonormalized()
	if _idx.has("thigh.L") and _idx.has("thigh.R"):
		var l := skel.get_bone_global_rest(_idx["thigh.L"]).origin
		var r := skel.get_bone_global_rest(_idx["thigh.R"]).origin
		_right = (r - l).normalized()
	if _idx.has("chest") and _idx.has("booster"):
		var c := skel.get_bone_global_rest(_idx["chest"]).origin
		var bo := skel.get_bone_global_rest(_idx["booster"]).origin
		_fwd = Vector3(c.x - bo.x, 0, c.z - bo.z).normalized()
	if _idx.has("pelvis"):
		_pelvis_rest = skel.get_bone_rest(_idx["pelvis"]).origin
	_build_thrusters(glow)

## Power level of the frame's lights, 0 (cold, in the clamps) to 1.
func set_power(p: float) -> void:
	if _glow_mat:
		_glow_mat.emission_energy_multiplier = 4.0 * p
		_glow_mat.albedo_color = _glow_mat.emission * (0.2 + 0.8 * p)

func set_wet(w: float) -> void:
	for m in _surface_mats:
		m.set_shader_parameter("wet", w)

func _build_thrusters(glow: Color) -> void:
	if not _idx.has("booster"):
		return
	# flames hang off the back of the frame (mech space: forward is -Z)
	var att := Node3D.new()
	add_child(att)
	var cone := CylinderMesh.new()
	cone.top_radius = 0.35
	cone.bottom_radius = 0.02
	cone.height = 2.2
	var fm := StandardMaterial3D.new()
	fm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	fm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	fm.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	fm.albedo_color = Color(0.45, 0.8, 1.0, 0.55)
	fm.cull_mode = BaseMaterial3D.CULL_DISABLED
	for s: float in [-0.55, 0.55]:
		var f := MeshInstance3D.new()
		f.mesh = cone
		f.material_override = fm
		f.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		att.add_child(f)
		# booster sits behind the chest; flames point down and back
		f.rotation_degrees = Vector3(-28, 0, 0)
		f.position = Vector3(s, 5.0, 1.55) + f.basis * Vector3(0, -1.1, 0)
		_flames.append(f)
	_boost_light = OmniLight3D.new()
	_boost_light.light_color = Color(0.45, 0.8, 1.0)
	_boost_light.omni_range = 14.0
	_boost_light.light_energy = 0.0
	att.add_child(_boost_light)
	_boost_light.position = Vector3(0, 3.2, 3.0)

## Rotate a bone by `angle` around an axis given in the frame's model space.
func _turn(bone: String, axis: Vector3, angle: float) -> void:
	if not _idx.has(bone):
		return
	var g: Basis = _rest_global[bone]
	var q := Quaternion(axis.normalized(), angle)
	var local := Quaternion((g.inverse() * Basis(q) * g).orthonormalized())
	skel.set_bone_pose_rotation(_idx[bone], _rest_rot[bone] * local)

func _process(dt: float) -> void:
	if skel == null or mech == null:
		return
	var sp := mech.speed()
	var on := mech.is_on_floor()
	var walk := clampf(sp / Mech.WALK, 0.0, 1.0) if on and not mech.boosting else 0.0
	_lean = lerpf(_lean, (0.32 if mech.boosting else 0.0) + walk * 0.06, 1.0 - exp(-dt * 5.0))
	_tuck = lerpf(_tuck, 1.0 if not on else 0.0, 1.0 - exp(-dt * 6.0))
	_crouch = lerpf(_crouch, 0.6 if mech.boosting and on else 0.0, 1.0 - exp(-dt * 6.0))
	var ph := mech.stride * TAU
	var up := Vector3.UP
	var r := _right
	# legs: swing about the hip, bend the knee on the recovery stroke
	for side: float in [1.0, -1.0]:
		var p := ph if side > 0 else ph + PI
		var sw := sin(p) * 0.5 * walk
		var lift := maxf(0.0, cos(p)) * 0.75 * walk
		var s := "L" if side > 0 else "R"
		_turn("thigh." + s, r, -sw - _crouch * 0.45 - _tuck * 0.55)
		_turn("shin." + s, r, lift + _crouch * 0.85 + _tuck * 0.9)
		_turn("foot." + s, r, -lift * 0.4 - _crouch * 0.4 - _tuck * 0.3)
	# body: bob, lean into boosts, counter-twist with the stride
	_turn("spine", r, -_lean * 0.6)
	_turn("chest", up, sin(ph) * 0.06 * walk)
	_turn("head", r, _lean * 0.4)
	if _idx.has("pelvis"):
		var bob := -absf(cos(ph)) * 0.18 * walk - _crouch * 0.6 - _tuck * 0.3
		skel.set_bone_pose_position(_idx["pelvis"], _pelvis_rest + Vector3(0, bob, 0))
	# arms swing opposite the legs; tucked back when boosting
	for side: float in [1.0, -1.0]:
		var p := ph + PI if side > 0 else ph
		var s := "L" if side > 0 else "R"
		_turn("upperarm." + s, r, sin(p) * 0.28 * walk + _lean * 0.5)
		_turn("forearm." + s, r, -0.25 - _lean * 0.4)
	# booster flames
	var level := 1.0 if mech.boosting else (0.7 if mech.hovering else 0.05)
	_boost_glow = lerpf(_boost_glow, level, 1.0 - exp(-dt * 10.0))
	for f in _flames:
		f.scale = Vector3(1.0, 0.2 + _boost_glow * (0.9 + randf() * 0.25), 1.0)
		f.visible = _boost_glow > 0.08
	if _boost_light:
		_boost_light.light_energy = _boost_glow * 3.0
