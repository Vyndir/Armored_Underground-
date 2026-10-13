class_name Fx
extends Node3D
## One-shot and continuous effects: footstep dust, landing shockwaves, quick
## boost bursts, booster exhaust, wading spray and ripples, welding sparks,
## steam and dust motes. Particle systems are pooled per effect.

static var I: Fx
var _soft: GradientTexture2D
var _pools := {}

func _ready() -> void:
	I = self
	_soft = GradientTexture2D.new()
	_soft.fill = GradientTexture2D.FILL_RADIAL
	_soft.fill_from = Vector2(0.5, 0.5)
	_soft.fill_to = Vector2(1.0, 0.5)
	var g := Gradient.new()
	g.set_color(0, Color(1, 1, 1, 1))
	g.set_color(1, Color(1, 1, 1, 0))
	_soft.gradient = g

func soft_mat(col: Color, additive := false, unshaded := true) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED if unshaded else BaseMaterial3D.SHADING_MODE_PER_PIXEL
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD if additive else BaseMaterial3D.BLEND_MODE_MIX
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.albedo_color = col
	m.albedo_texture = _soft
	m.disable_receive_shadows = true
	return m

func _system(key: String, amount: int, lifetime: float, pm: ParticleProcessMaterial, size: float, mat: Material, one_shot := true) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.name = key
	p.amount = amount
	p.lifetime = lifetime
	p.one_shot = one_shot
	p.explosiveness = 0.95 if one_shot else 0.0
	p.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(size, size)
	q.material = mat
	p.draw_pass_1 = q
	p.emitting = false
	p.local_coords = false
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.visibility_aabb = AABB(Vector3(-40, -10, -40), Vector3(80, 60, 80))
	add_child(p)
	return p

func _fade_curve() -> CurveTexture:
	var c := Curve.new()
	c.add_point(Vector2(0, 0.3))
	c.add_point(Vector2(0.2, 1.0))
	c.add_point(Vector2(1, 1.6))
	var t := CurveTexture.new()
	t.curve = c
	return t

func _alpha_ramp(a: Color, b: Color) -> GradientTexture1D:
	var g := Gradient.new()
	g.set_color(0, a)
	g.set_color(1, b)
	var t := GradientTexture1D.new()
	t.gradient = g
	return t

## Grab a pooled one-shot system (built by `builder` on first use), move it and fire it.
func _burst(key: String, pos: Vector3, builder: Callable, pool := 6) -> GPUParticles3D:
	if not _pools.has(key):
		_pools[key] = {"list": [], "i": 0}
		for k in pool:
			_pools[key]["list"].append(builder.call())
	var entry: Dictionary = _pools[key]
	var p: GPUParticles3D = entry["list"][entry["i"]]
	entry["i"] = (entry["i"] + 1) % entry["list"].size()
	p.global_position = pos
	p.restart()
	p.emitting = true
	return p

func _mk_dust() -> GPUParticles3D:
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_RING
	pm.emission_ring_axis = Vector3.UP
	pm.emission_ring_radius = 1.2
	pm.emission_ring_inner_radius = 0.6
	pm.emission_ring_height = 0.1
	pm.direction = Vector3(0, 0.4, 0)
	pm.spread = 80.0
	pm.initial_velocity_min = 2.0
	pm.initial_velocity_max = 5.0
	pm.gravity = Vector3(0, 0.6, 0)
	pm.damping_min = 3.0
	pm.damping_max = 5.0
	pm.scale_curve = _fade_curve()
	pm.color_ramp = _alpha_ramp(Color(0.72, 0.7, 0.66, 0.45), Color(0.72, 0.7, 0.66, 0.0))
	return _system("Dust", 18, 1.4, pm, 1.6, soft_mat(Color.WHITE, false, false))

func _mk_shock() -> GPUParticles3D:
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_RING
	pm.emission_ring_axis = Vector3.UP
	pm.emission_ring_radius = 2.0
	pm.emission_ring_inner_radius = 1.5
	pm.emission_ring_height = 0.1
	pm.direction = Vector3(1, 0.05, 0)
	pm.spread = 180.0
	pm.flatness = 0.9
	pm.initial_velocity_min = 10.0
	pm.initial_velocity_max = 16.0
	pm.damping_min = 9.0
	pm.damping_max = 12.0
	pm.scale_curve = _fade_curve()
	pm.color_ramp = _alpha_ramp(Color(0.75, 0.72, 0.68, 0.6), Color(0.75, 0.72, 0.68, 0.0))
	return _system("Shock", 48, 1.6, pm, 2.4, soft_mat(Color.WHITE, false, false))

func _mk_qb() -> GPUParticles3D:
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0, 0, 1)
	pm.spread = 25.0
	pm.initial_velocity_min = 12.0
	pm.initial_velocity_max = 22.0
	pm.damping_min = 14.0
	pm.damping_max = 18.0
	pm.gravity = Vector3.ZERO
	pm.scale_curve = _fade_curve()
	pm.color_ramp = _alpha_ramp(Color(0.6, 0.9, 1.0, 0.9), Color(0.3, 0.5, 1.0, 0.0))
	return _system("QB", 32, 0.5, pm, 1.4, soft_mat(Color.WHITE, true))

func _mk_splash() -> GPUParticles3D:
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 1.2
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 40.0
	pm.initial_velocity_min = 4.0
	pm.initial_velocity_max = 10.0
	pm.gravity = Vector3(0, -18, 0)
	pm.scale_min = 0.5
	pm.scale_max = 1.6
	pm.color_ramp = _alpha_ramp(Color(0.95, 0.98, 1.0, 0.85), Color(0.9, 0.95, 1.0, 0.0))
	return _system("Splash", 60, 1.1, pm, 0.9, soft_mat(Color.WHITE))

func dust(pos: Vector3) -> void:
	_burst("dust", pos, _mk_dust, 10)

func shockwave(pos: Vector3) -> void:
	_burst("shock", pos, _mk_shock, 4)

func qb_burst(pos: Vector3, dir: Vector3) -> void:
	var p := _burst("qb", pos, _mk_qb, 4)
	(p.process_material as ParticleProcessMaterial).direction = -dir

func splash(pos: Vector3) -> void:
	_burst("splash", pos, _mk_splash, 10)

var _ripples: Array[MeshInstance3D] = []
var _ripple_i := 0
func ripple(pos: Vector3, size := 6.0) -> void:
	if _ripples.is_empty():
		var sh := load("res://shaders/ripple.gdshader")
		for k in 16:
			var mi := MeshInstance3D.new()
			var q := PlaneMesh.new()
			q.size = Vector2(1, 1)
			mi.mesh = q
			var m := ShaderMaterial.new()
			m.shader = sh
			mi.material_override = m
			mi.visible = false
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_child(mi)
			_ripples.append(mi)
	var r := _ripples[_ripple_i]
	_ripple_i = (_ripple_i + 1) % _ripples.size()
	r.global_position = Vector3(pos.x, Site.WATER_Y + 0.08, pos.z)
	r.scale = Vector3(size, 1, size)
	r.visible = true
	var m: ShaderMaterial = r.material_override
	m.set_shader_parameter("life", 0.0)
	var tw := create_tween()
	tw.tween_method(func(v: float): m.set_shader_parameter("life", v), 0.0, 1.0, 2.2)
	tw.parallel().tween_property(r, "scale", Vector3(size * 2.6, 1, size * 2.6), 2.2)
	tw.tween_callback(func(): r.visible = false)

## A continuous emitter (exhaust, steam, sparks, dust motes) left running in place.
func emitter(parent: Node3D, pos: Vector3, kind: String) -> GPUParticles3D:
	var pm := ParticleProcessMaterial.new()
	var p: GPUParticles3D
	match kind:
		"exhaust":
			pm.direction = Vector3(0, -0.4, 1)
			pm.spread = 12.0
			pm.initial_velocity_min = 8.0
			pm.initial_velocity_max = 14.0
			pm.gravity = Vector3(0, 1.5, 0)
			pm.damping_min = 2.0
			pm.damping_max = 3.0
			pm.scale_curve = _fade_curve()
			pm.color_ramp = _alpha_ramp(Color(0.85, 0.9, 1.0, 0.18), Color(0.7, 0.72, 0.75, 0.0))
			p = _system("Exhaust", 60, 1.2, pm, 2.2, soft_mat(Color.WHITE, false, false), false)
		"steam":
			pm.direction = Vector3(0, 1, 0)
			pm.spread = 10.0
			pm.initial_velocity_min = 2.5
			pm.initial_velocity_max = 4.0
			pm.gravity = Vector3(0.8, 0.4, 0.2)
			pm.scale_curve = _fade_curve()
			pm.color_ramp = _alpha_ramp(Color(1, 1, 1, 0.22), Color(1, 1, 1, 0.0))
			p = _system("Steam", 40, 5.0, pm, 3.5, soft_mat(Color.WHITE, false, false), false)
		"vent":
			# pressurised release at the bay floor, rolling outward and rising
			pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
			pm.emission_sphere_radius = 0.4
			pm.direction = Vector3(0, 0.3, 1)
			pm.spread = 75.0
			pm.initial_velocity_min = 3.0
			pm.initial_velocity_max = 6.0
			pm.gravity = Vector3(0, 0.8, 0)
			pm.damping_min = 2.5
			pm.damping_max = 4.0
			pm.scale_curve = _fade_curve()
			pm.color_ramp = _alpha_ramp(Color(1, 1, 1, 0.14), Color(1, 1, 1, 0.0))
			p = _system("Vent", 50, 2.2, pm, 2.2, soft_mat(Color.WHITE, false, false), false)
		"wake":
			pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
			pm.emission_box_extents = Vector3(2.0, 0.1, 0.5)
			pm.direction = Vector3(0, 0.2, 1)
			pm.spread = 50.0
			pm.initial_velocity_min = 1.0
			pm.initial_velocity_max = 3.0
			pm.gravity = Vector3.ZERO
			pm.damping_min = 1.0
			pm.damping_max = 2.0
			pm.scale_curve = _fade_curve()
			pm.color_ramp = _alpha_ramp(Color(1, 1, 1, 0.55), Color(1, 1, 1, 0.0))
			p = _system("Wake", 90, 6.0, pm, 3.0, soft_mat(Color.WHITE), false)
		"sparks":
			pm.direction = Vector3(0, 0.6, 0.4)
			pm.spread = 70.0
			pm.initial_velocity_min = 3.0
			pm.initial_velocity_max = 7.0
			pm.gravity = Vector3(0, -12, 0)
			pm.scale_min = 0.08
			pm.scale_max = 0.18
			pm.color_ramp = _alpha_ramp(Color(1.0, 0.85, 0.5, 1.0), Color(1.0, 0.4, 0.1, 0.0))
			p = _system("Sparks", 70, 0.7, pm, 0.35, soft_mat(Color(1.0, 0.9, 0.6) * 4.0, true), false)
			p.explosiveness = 0.3
		_:
			pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
			pm.emission_box_extents = Vector3(38, 10, 38)
			pm.direction = Vector3(0, 1, 0)
			pm.spread = 180.0
			pm.initial_velocity_min = 0.05
			pm.initial_velocity_max = 0.25
			pm.gravity = Vector3(0, -0.02, 0)
			pm.scale_min = 0.03
			pm.scale_max = 0.08
			pm.color_ramp = _alpha_ramp(Color(1.0, 0.95, 0.85, 0.0), Color(1.0, 0.95, 0.85, 0.0))
			var g := Gradient.new()
			g.offsets = PackedFloat32Array([0.0, 0.3, 0.7, 1.0])
			g.colors = PackedColorArray([Color(1, 0.95, 0.85, 0), Color(1, 0.95, 0.85, 0.6), Color(1, 0.95, 0.85, 0.6), Color(1, 0.95, 0.85, 0)])
			var gt := GradientTexture1D.new()
			gt.gradient = g
			pm.color_ramp = gt
			pm.scale_min = 1.0
			pm.scale_max = 1.0
			p = _system("Motes", 260, 14.0, pm, 0.06, soft_mat(Color(0.5, 0.46, 0.4), true), false)
			p.visibility_aabb = AABB(Vector3(-45, -15, -45), Vector3(90, 30, 90))
	remove_child(p)
	parent.add_child(p)
	p.position = pos
	p.emitting = true
	return p
