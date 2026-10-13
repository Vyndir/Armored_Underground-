extends Node3D
## Waterfront Training Hangar: builds the site, spawns KESTREL in its service
## bay, and runs the onboarding course that teaches movement, camera, boost,
## quick boost, jump/hover, wading and interacting.
##
## Run with `-- --autotest <dir>` to fly preset cameras and play the course
## automatically, saving screenshots.

const STEPS := [
	{"title": "SYSTEMS CHECK", "radio": "Give the head cameras a once-over. Look around the bay.", "text": "Look around the bay to calibrate the head cameras.",
		"prompts": [[["Mouse"], ["R-Stick"], "LOOK"]]},
	{"title": "WALK OUT", "radio": "Good. Walk her out onto the apron. Pad one, straight ahead.", "text": "Walk KESTREL out of the hangar to training pad 1.",
		"prompts": [[["W", "A", "S", "D"], ["L-Stick"], "MOVE"], [["C"], ["R3"], "RECENTER CAMERA"]], "pad": Vector3(-58, 0, -74), "name": "PAD 1"},
	{"title": "BOOST", "radio": "Now open her up. Hold boost all the way down the north apron.", "text": "Hold boost and run the north apron to pad 2.",
		"prompts": [[["Shift"], ["RB"], "BOOST (HOLD)"], [["W"], ["L-Stick"], "MOVE"]], "pad": Vector3(-2, 0, -138), "name": "PAD 2"},
	{"title": "QUICK BOOST", "radio": "Quick boosts are your dodge. Three dashes, any direction.", "text": "Dash three times. Quick boosts dodge fire but cost energy.",
		"prompts": [[["Q"], ["B"], "QUICK BOOST"], [["A", "D"], ["L-Stick"], "DIRECTION"]]},
	{"title": "JUMP & HOVER", "radio": "Pad three's by the quay. Jump and hold to hover.", "text": "Reach pad 3, then jump and hold to hover for two seconds.",
		"prompts": [[["Space"], ["A"], "JUMP"], [["Space"], ["A"], "HOLD TO HOVER"]], "pad": Vector3(18, 0, -60), "name": "PAD 3"},
	{"title": "WADE", "radio": "Take the slipway into the shallows. She'll feel heavy in the water.", "text": "Take the slipway down into the shallows to the buoy line.",
		"prompts": [[["W"], ["L-Stick"], "MOVE"], [["Shift"], ["RB"], "BOOST OUT OF THE WATER"]], "pad": Vector3(84, -5, 33), "name": "BUOY LINE"},
	{"title": "REPORT IN", "radio": "Nice run. Bring her home and log it at the workshop terminal.", "text": "Return to the hangar and log the run at the workshop terminal.",
		"prompts": [[["E"], ["Y"], "INTERACT"]], "pad": Vector3(-92, 0, -56), "name": "WORKSHOP TERMINAL"},
]

var mech: Mech
var cam: ChaseCamera
var hud: Hud
var rig: MechRig
var hour := 10.7
var step := 0
var _step_start := {}
var _done := false
var _autotest_dir := ""
var _shot_cam: Camera3D
var _splash: GPUParticles3D
var _paused := false
var _info := {}
var _sim_interact := false
var _intro: Intro
var _exhaust: GPUParticles3D
var _was_boost := false

func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	var ai := args.find("--autotest")
	if ai >= 0 and ai + 1 < args.size():
		_autotest_dir = args[ai + 1]
	_environment()
	var sfx := Sfx.new()
	add_child(sfx)
	var fx := Fx.new()
	add_child(fx)
	var art: Dictionary = await Art.make(self)
	BuildSite.terrain(self)
	BuildSite.water(self)
	BuildSite.apron(self, art["markings"])
	BuildSite.quay(self)
	BuildSite.buoys(self)
	BuildNature.rocks(self)
	BuildNature.trees(self)
	BuildNature.far_side(self)
	var boats: Array = BuildNature.boats(self)
	_info = BuildHangar.build(self, art)
	var props := BuildProps.build(self, art)
	var life := Life.new()
	add_child(life)
	life.setup(_info, props, boats)
	sfx.start_ambience(Vector3(-120, 12, -74), self)
	_display_mechs()
	_spawn_player()
	hud = Hud.new()
	add_child(hud)
	hud.device_changed.connect(_refresh_prompts)
	hud.fade_in(2.0)
	if _autotest_dir == "":
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	else:
		_autotest_setup()
	if OS.get_cmdline_user_args().has("--skip-intro"):
		(_info["rig"]["arms"] as Node3D).position = Vector3(-2.0, 4.5, 0)
		(_info["rig"]["clamps"] as Node3D).position.y = -0.32
	else:
		_intro = Intro.new()
		_intro.world = self
		_intro.mech = mech
		_intro.rig = rig
		_intro.cam = cam
		_intro.hud = hud
		_intro.gantry = _info["rig"]
		add_child(_intro)
		if _autotest_dir != "":
			_intro.beat.connect(func(n: String): _shot("i_" + n))
		await _intro.run()
		_intro.queue_free()
		_intro = null
	_start_step(0)
	if _autotest_dir != "":
		_autotest()

# ------------------------------------------------------------------ environment
func _environment() -> void:
	var we := WorldEnvironment.new()
	var env := Environment.new()
	var sky := Sky.new()
	var sm := ShaderMaterial.new()
	sm.shader = load("res://shaders/sky.gdshader")
	sky.sky_material = sm
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.9
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 0.95
	env.tonemap_white = 6.0
	env.ssao_enabled = true
	env.ssao_radius = 2.2
	env.ssao_intensity = 1.6
	env.ssil_enabled = true
	env.ssr_enabled = true
	env.ssr_max_steps = 72
	env.sdfgi_enabled = true
	env.sdfgi_use_occlusion = true
	env.sdfgi_min_cell_size = 0.4
	env.sdfgi_cascades = 6
	env.glow_enabled = true
	env.glow_intensity = 0.5
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.2
	env.fog_enabled = true
	env.fog_light_color = Color(0.66, 0.76, 0.88)
	env.fog_density = 0.00045
	env.fog_aerial_perspective = 0.75
	env.fog_sky_affect = 0.25
	env.volumetric_fog_enabled = true
	env.volumetric_fog_density = 0.0025
	env.volumetric_fog_albedo = Color(0.9, 0.92, 0.95)
	env.volumetric_fog_length = 160.0
	env.adjustment_enabled = true
	env.adjustment_contrast = 1.06
	env.adjustment_saturation = 1.1
	we.environment = env
	add_child(we)
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-44, 42, 0)
	sun.light_color = Color(1.0, 0.95, 0.86)
	sun.light_energy = 1.35
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_max_distance = 420.0
	sun.directional_shadow_blend_splits = true
	sun.light_angular_distance = 0.6
	sun.light_volumetric_fog_energy = 0.6
	add_child(sun)

# ------------------------------------------------------------------ frames
func _display_mechs() -> void:
	var specs := [[Vector3(-138, 0, -102), Color(0.16, 0.2, 0.32), Color(0.85, 0.15, 0.12), Color(1.0, 0.3, 0.2)],
		[Vector3(-138, 0, -44), Color(0.55, 0.53, 0.47), Color(0.12, 0.4, 0.75), Color(0.3, 0.8, 1.0)]]
	for s in specs:
		var r := MechRig.new()
		add_child(r)
		r.setup(s[1], s[2], s[3])
		r.position = s[0]
		r.rotation.y = -PI * 0.5   # facing the doors (east)

func _spawn_player() -> void:
	mech = Mech.new()
	mech.name = "KESTREL"
	add_child(mech)
	rig = MechRig.new()
	mech.add_child(rig)
	rig.mech = mech
	mech.rig = rig
	rig.setup(Color(0.68, 0.66, 0.6), Color(0.95, 0.52, 0.1), Color(0.3, 0.85, 1.0))
	mech.teleport(Site.SPAWN + Vector3(0, 0.3, 0), -PI * 0.5)
	cam = ChaseCamera.new()
	add_child(cam)
	cam.target = mech
	cam.yaw = mech.yaw
	cam.global_position = mech.global_position + Vector3(0, 6.2, 0)
	mech.footstep.connect(_on_footstep)
	mech.landed.connect(_on_landed)
	mech.quick_boosted.connect(_on_quick_boost)
	mech.entered_water.connect(_on_entered_water)
	_exhaust = Fx.I.emitter(self, Vector3.ZERO, "exhaust")
	_exhaust.emitting = false
	# spray when wading
	_splash = GPUParticles3D.new()
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 55.0
	pm.initial_velocity_min = 3.0
	pm.initial_velocity_max = 7.0
	pm.gravity = Vector3(0, -14, 0)
	pm.scale_min = 0.6
	pm.scale_max = 1.8
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 2.2
	_splash.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(1.2, 1.2)
	var qm := StandardMaterial3D.new()
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	qm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	var gt := GradientTexture2D.new()
	gt.fill = GradientTexture2D.FILL_RADIAL
	gt.fill_from = Vector2(0.5, 0.5)
	gt.fill_to = Vector2(1.0, 0.5)
	var grad := Gradient.new()
	grad.set_color(0, Color(1, 1, 1, 0.8))
	grad.set_color(1, Color(1, 1, 1, 0.0))
	gt.gradient = grad
	qm.albedo_texture = gt
	q.material = qm
	_splash.draw_pass_1 = q
	_splash.amount = 160
	_splash.lifetime = 0.9
	_splash.emitting = false
	add_child(_splash)

# ------------------------------------------------------------------ frame feedback
func _foot(side: int) -> Vector3:
	var b := Basis(Vector3.UP, mech.yaw)
	return mech.global_position + b * Vector3((side * 2 - 1) * 1.1, 0, 0)

func _on_footstep(side: int) -> void:
	var p := _foot(side)
	cam.add_trauma(0.12 if not mech.in_water else 0.05)
	if mech.in_water:
		var w := Vector3(p.x, Site.WATER_Y + 0.05, p.z)
		Fx.I.splash(w)
		Fx.I.ripple(w, 3.0)
		Sfx.I.play_at("splash", w, -8.0, 1.2, 18)
	else:
		Fx.I.dust(p + Vector3(0, 0.2, 0))
		Sfx.I.play_at("step", p, -2.0 if not mech.boosting else 0.0, 1.0, 22)
		if randf() < 0.35:
			Sfx.I.play_at("clank", p + Vector3(0, 4, 0), -14.0, 1.3, 16)

func _on_landed(v: float) -> void:
	cam.add_trauma(clampf((v - 8.0) / 40.0, 0.0, 0.6))
	var p := mech.global_position
	if mech.in_water:
		Fx.I.splash(Vector3(p.x, Site.WATER_Y + 0.05, p.z))
		Fx.I.ripple(Vector3(p.x, Site.WATER_Y + 0.05, p.z), 8.0)
		Sfx.I.play_at("splash", p, 0.0, 0.8, 30)
	else:
		Sfx.I.play_at("land", p, clampf(-12.0 + v * 0.5, -12.0, 4.0), 1.0, 30)
		if v > 12.0:
			Fx.I.shockwave(p + Vector3(0, 0.15, 0))

func _on_quick_boost() -> void:
	var back := Basis(Vector3.UP, mech.yaw) * Vector3(0, 0, 1)
	Fx.I.qb_burst(mech.global_position + Vector3(0, 4.5, 0) + back * 1.8, back)
	Sfx.I.play_at("qb", mech.global_position, 2.0, 1.0, 30)
	cam.add_trauma(0.25)

func _on_entered_water() -> void:
	hud.toast("ENTERING WATER  ·  TRACTION REDUCED", 2.0)
	var p := mech.global_position
	Fx.I.splash(Vector3(p.x, Site.WATER_Y + 0.05, p.z))
	Fx.I.ripple(Vector3(p.x, Site.WATER_Y + 0.05, p.z), 7.0)
	Sfx.I.play_at("splash", p, 0.0, 1.0, 30)

# ------------------------------------------------------------------ onboarding
func _start_step(i: int) -> void:
	step = i
	_step_start = {"qb": mech.quick_boosts, "hover": mech.hover_time, "boost": mech.boost_time, "look": cam.look_amount, "t": Time.get_ticks_msec()}
	if i >= STEPS.size():
		_done = true
		hud.set_objective(0, STEPS.size(), "TRAINING COMPLETE", "KESTREL is cleared for the Qualifier.")
		hud.set_prompts([])
		hud.show_complete(true)
		Sfx.I.play("confirm", -4.0, 0.8)
		hud.subtitle("HARBOUR CONTROL", "Clean run, pilot. KESTREL is cleared for the Qualifier.", 6.0)
		return
	var s: Dictionary = STEPS[i]
	hud.set_objective(i + 1, STEPS.size(), s["title"], s["text"])
	_refresh_prompts()
	if i > 0:
		hud.toast("%s  ·  COMPLETE" % STEPS[i - 1]["title"], 2.2)
		Sfx.I.play("confirm", -6.0)
	hud.subtitle("HARBOUR CONTROL", s["radio"], 5.0)

func _refresh_prompts() -> void:
	if step < STEPS.size():
		hud.set_prompts(STEPS[step]["prompts"])

func _waypoint() -> Variant:
	if _done or step >= STEPS.size():
		return null
	var s: Dictionary = STEPS[step]
	if step == 3:
		return null
	return s.get("pad")

func _check_step() -> void:
	if _done:
		return
	var p := mech.global_position
	var pad: Variant = _waypoint()
	var near := pad != null and Vector2(p.x - pad.x, p.z - pad.z).length() < 8.0
	match step:
		0:
			if cam.look_amount - _step_start["look"] > 2.4:
				_start_step(1)
		1:
			if near:
				_start_step(2)
		2:
			if near and mech.boost_time - _step_start["boost"] > 1.5:
				_start_step(3)
			elif near:
				hud.toast("HOLD BOOST ON THE WAY  ·  TRY AGAIN FROM PAD 1", 2.0)
		3:
			if mech.quick_boosts - _step_start["qb"] >= 3:
				_start_step(4)
		4:
			if Vector2(p.x - 18, p.z + 60).length() < 14.0 and mech.hover_time - _step_start["hover"] > 2.0:
				_start_step(5)
		5:
			if mech.in_water and Vector2(p.x - 84, p.z - 33).length() < 14.0:
				_start_step(6)
		6:
			if near:
				hud.show_interact("ACCESS WORKSHOP TERMINAL   [%s]" % ("Y" if hud.gamepad else "E"))
				if Input.is_action_just_pressed("interact") or _sim_interact:
					hud.show_interact("")
					_start_step(7)
			else:
				hud.show_interact("")
	_sim_interact = false

func _process(dt: float) -> void:
	if mech == null or hud == null:
		return
	# frame feedback runs even while the intro has control
	Sfx.I.set_boost(1.0 if mech.boosting else (0.45 if mech.hovering else 0.0))
	Sfx.I.set_wind(clampf((mech.global_position.y - 2.0) / 20.0 + mech.speed() / 60.0, 0.0, 1.0))
	var back := Basis(Vector3.UP, mech.yaw) * Vector3(0, 0, 1)
	_exhaust.global_position = mech.global_position + Vector3(0, 4.2, 0) + back * 1.9
	_exhaust.emitting = mech.boosting or mech.hovering
	if _intro:
		return
	if Input.is_action_just_pressed("pause"):
		_paused = not _paused
		get_tree().paused = false
		mech.input_enabled = not _paused
		cam.input_enabled = not _paused
		hud.set_paused(_paused)
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if _paused else Input.MOUSE_MODE_CAPTURED
	if _paused and Input.is_key_pressed(KEY_Q):
		get_tree().quit()
	if Input.is_action_just_pressed("toggle_help"):
		hud.toggle_help()
	if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED and not _paused and _autotest_dir == "" and Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_check_step()
	var wp: Variant = _waypoint()
	var wname := ""
	if wp != null:
		wname = STEPS[step]["name"]
	hud.update_frame(mech, cam.cam, cam.yaw, hour, wp, wname)
	rig.set_wet(1.0 if mech.in_water else maxf(0.0, 0.0))
	_splash.global_position = mech.global_position + Vector3(0, Site.WATER_Y - mech.global_position.y + 0.3, 0) if mech.in_water else mech.global_position
	_splash.emitting = mech.in_water and mech.speed() > 2.0

# ------------------------------------------------------------------ autotest
func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	DirAccess.make_dir_recursive_absolute(_autotest_dir)
	img.save_png(_autotest_dir.path_join(name + ".png"))
	print("[autotest] ", name)

func _wait(sec: float) -> void:
	await get_tree().create_timer(sec).timeout

func _view(name: String, pos: Vector3, look: Vector3, settle := 0.6) -> void:
	_shot_cam.global_position = pos
	_shot_cam.look_at(look)
	_shot_cam.current = true
	await _wait(settle)
	await _shot(name)

## Steer the frame toward a point by turning the camera and holding forward.
func _drive(target: Vector3, boost := false, timeout := 30.0, radius := 5.0) -> void:
	var t := 0.0
	while t < timeout:
		var to := target - mech.global_position
		to.y = 0
		if to.length() < radius:
			break
		var want := atan2(-to.x, -to.z)
		cam.yaw = lerp_angle(cam.yaw, want, 0.2)
		mech.sim_move = Vector2(0, -1)
		mech.sim_boost = boost and mech.energy > 0.25
		await get_tree().process_frame
		t += get_process_delta_time()
		if fmod(t, 1.0) < get_process_delta_time():
			print("[autotest] drive pos=", mech.global_position.snapped(Vector3.ONE), " v=", mech.velocity.snapped(Vector3.ONE), " e=", snappedf(mech.energy, 0.01))
	mech.sim_move = Vector2.ZERO
	mech.sim_boost = false

func _autotest_setup() -> void:
	_shot_cam = Camera3D.new()
	_shot_cam.fov = 60
	_shot_cam.far = 9000
	add_child(_shot_cam)

func _autotest() -> void:
	cam.input_enabled = false
	mech.input_enabled = false
	await _wait(3.0)
	await _shot("i_handover")
	if OS.get_cmdline_user_args().has("--quick"):
		cam.cam.current = true
		await _wait(1.0)
		print("[autotest] cam=", cam.cam.global_position, " mech=", mech.global_position, " hit=", cam.arm.get_hit_length())
		var ms := rig.find_children("*", "MeshInstance3D", true, false)
		for mm in ms.slice(0, 4):
			print("[autotest] mesh ", mm.name, " aabb=", (mm as MeshInstance3D).get_aabb(), " gscale=", (mm as MeshInstance3D).global_transform.basis.get_scale())
		get_tree().quit()
		return
	await _view("v_ref_concept", Vector3(-70, 36, 150), Vector3(40, 4, -160), 2.0)
	await _view("v_hangar_front", Vector3(-8, 11, -46), Vector3(-90, 12, -74))
	await _view("v_interior", Vector3(-96, 8, -58), Vector3(-145, 7, -84))
	await _view("v_bay_closeup", Vector3(-110, 6, -70), Vector3(-130, 6, -74))
	await _view("v_slipway", Vector3(70, 7, 78), Vector3(0, 0, 30))
	await _view("v_quay", Vector3(28, 7, -178), Vector3(42, 2, 0))
	await _view("v_aerial", Vector3(170, 190, 230), Vector3(-60, 0, -70))
	await _view("v_bay_view", Vector3(60, 5, -10), Vector3(700, 60, -1500))
	cam.cam.current = true
	await _wait(1.0)
	print("[autotest] cam=", cam.cam.global_position, " pivot=", cam.global_position, " mech=", mech.global_position, " hit=", cam.arm.get_hit_length(), " len=", cam.arm.spring_length, " scale=", rig.get_child(0).scale if rig.get_child_count() > 0 else Vector3.ZERO)
	for c in rig.get_children():
		print("[autotest] rig child ", c.name, " ", c.get_class(), " aabb=", (c as Node3D).global_transform.origin)
	var meshes := rig.find_children("*", "MeshInstance3D", true, false)
	if meshes.size() > 0:
		var m: MeshInstance3D = meshes[0]
		print("[autotest] mesh0 aabb=", m.get_aabb(), " gscale=", m.global_transform.basis.get_scale())
	await _shot("p00_spawn")
	cam.look_amount += 3.0
	await _wait(0.5)
	await _drive(Vector3(-58, 0, -74))
	await _shot("p01_pad1")
	await _drive(Vector3(-2, 0, -138), true)
	await _shot("p02_pad2_boost")
	for k in 3:
		mech.sim_move = Vector2(1 if k % 2 == 0 else -1, 0)
		mech.sim_qb = true
		await _wait(0.6)
	mech.sim_move = Vector2.ZERO
	await _shot("p03_quickboost")
	await _drive(Vector3(18, 0, -60), true)
	await _wait(2.5)
	print("[autotest] before jump pos=", mech.global_position, " e=", mech.energy)
	mech.sim_jump_pressed = true
	mech.sim_jump = true
	await _wait(1.0)
	print("[autotest] hover pos=", mech.global_position, " v=", mech.velocity, " e=", mech.energy, " hovering=", mech.hovering)
	await _shot("p04_hover")
	await _wait(1.6)
	mech.sim_jump = false
	await _wait(1.5)
	await _drive(Vector3(-6, 0, 33), true)
	await _drive(Vector3(50, 0, 33), false, 20.0)
	await _shot("p05_slipway")
	await _drive(Vector3(84, 0, 33), false, 30.0, 8.0)
	await _wait(1.0)
	await _shot("p06_wading")
	cam.yaw += PI * 0.8
	await _wait(1.0)
	await _shot("p06b_wading_front")
	await _drive(Vector3(-4, 0, 30), true, 30.0)
	await _drive(Vector3(-40, 0, -70), true, 30.0)
	await _drive(Vector3(-90, 0, -60), false, 30.0, 4.0)
	await _wait(0.5)
	await _shot("p07_terminal")
	_sim_interact = true
	await _wait(1.0)
	await _shot("p08_complete")
	print("[autotest] step=", step, " done=", _done)
	get_tree().quit()
