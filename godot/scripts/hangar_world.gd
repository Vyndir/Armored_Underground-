class_name HangarWorld
extends Node3D
## Waterfront Training Hangar: builds the site, spawns the frame in its service
## bay, and runs a short onboarding course (look, walk out, turn, jump, short
## boost, wade the training shelf, return to the yard), then free roam. Quick
## boost and hover remain available as optional test features.
##
## Command-line options (after `--`):
##   --autotest <dir>   play the intro and the course, run the QA checks, save screenshots
##   --skip-intro       start in the bay with control
##   --door=WxH         hangar door size for comparison (default 24x11)
##   --night            night lighting preset
##   --laptop           lighter rendering preset (no SDFGI/SSIL/SSR/volumetric fog, shorter shadows)
##   --perf <dir>       log frame timing and memory to <dir>/perf.csv while playing

const BRIDGE := Vector3(750.0, 47.0, -1700.0)
const PAD1 := Vector3(-48.0, 0.0, -74.0)
const PAD2 := Vector3(27.0, 0.0, -150.0)
const PAD3 := Vector3(-24.0, 0.0, -10.0)
const MARKER := Vector3(78.0, Site.SHELF_FLOOR, -20.0)

const STEPS := [
	{"title": "SYSTEMS CHECK", "radio": "Give the head cameras a once-over. Look around the bay.", "text": "Look around the bay to calibrate the head cameras.",
		"prompts": [[["Mouse"], ["R-Stick"], "LOOK"]]},
	{"title": "WALK OUT", "radio": "Walk her out onto the yard. Pad one, straight ahead.", "text": "Walk out of the hangar to training pad 1.",
		"prompts": [[["W", "A", "S", "D"], ["L-Stick"], "MOVE"], [["C"], ["R3"], "RECENTER CAMERA"]], "pad": PAD1, "name": "PAD 1"},
	{"title": "TURN", "radio": "Bring her round to face the bridge. That's the city out there.", "text": "Turn to face the Central Bridge across the bay.",
		"prompts": [[["A", "D"], ["L-Stick"], "TURN"], [["Mouse"], ["R-Stick"], "LOOK"]], "pad": BRIDGE, "name": "CENTRAL BRIDGE"},
	{"title": "JUMP", "radio": "Test the legs. One jump, land it clean.", "text": "Jump, and land.",
		"prompts": [[["Space"], ["A"], "JUMP"]]},
	{"title": "SHORT BOOST", "radio": "Now a short boost. Run the quay strip up to pad two.", "text": "Hold boost along the quay strip to pad 2.",
		"prompts": [[["Shift"], ["RB"], "BOOST (HOLD)"], [["W"], ["L-Stick"], "MOVE"]], "pad": PAD2, "name": "PAD 2"},
	{"title": "WADE", "radio": "Take the north ramp down onto the shelf. Shin deep, she'll feel heavy.", "text": "Walk down the north ramp onto the training shelf to the marker buoy.",
		"prompts": [[["W"], ["L-Stick"], "MOVE"]], "pad": MARKER, "name": "MARKER BUOY"},
	{"title": "RETURN", "radio": "Good. Climb out by either ramp and bring her back to the yard.", "text": "Leave the water by either ramp and return to pad 3.",
		"prompts": [[["W"], ["L-Stick"], "MOVE"], [["Shift"], ["RB"], "BOOST"]], "pad": PAD3, "name": "PAD 3"},
]

var mech: Mech
var cam: ChaseCamera
var hud: Hud
var rig: MechRig
var tuning_panel: TuningPanel
var hour := 10.7
var step := 0
var night := false
var _step_start := {}
var _done := false
var _autotest_dir := ""
var _splash: GPUParticles3D
var _paused := false
var _info := {}
var _intro: Intro
var _exhaust: GPUParticles3D
var _env: Environment
var _sun: DirectionalLight3D
var _sky: ShaderMaterial
var _perf: PerfLog

func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	Site.configure(args)
	var ai := args.find("--autotest")
	if ai >= 0 and ai + 1 < args.size():
		_autotest_dir = args[ai + 1]
	night = args.has("--night")
	_environment(args.has("--laptop"))
	var sfx := Sfx.new()
	add_child(sfx)
	var fx := Fx.new()
	add_child(fx)
	var art: Dictionary = await Art.make(self)
	BuildSite.terrain(self)
	BuildSite.water(self)
	BuildSite.apron(self, art["markings"])
	BuildSite.quay(self)
	BuildBounds.build(self)
	BuildNature.rocks(self)
	BuildNature.trees(self)
	BuildNature.far_side(self)
	BuildDistrict.build(self)
	var boats: Array = BuildNature.boats(self)
	_info = BuildHangar.build(self, art)
	var props := BuildProps.build(self, art)
	_marker_buoy()
	var life := Life.new()
	add_child(life)
	life.setup(_info, props, boats)
	sfx.start_ambience(Vector3(-108, 9, -74), self)
	_display_mechs()
	_spawn_player()
	_set_night_lights(night)
	if night:
		_apply_night()
	hud = Hud.new()
	add_child(hud)
	hud.device_changed.connect(_refresh_prompts)
	hud.fade_in(2.0)
	tuning_panel = TuningPanel.new()
	add_child(tuning_panel)
	tuning_panel.attach(mech)
	tuning_panel.opened.connect(_on_tuning_opened)
	var pi := args.find("--perf")
	if pi >= 0 and pi + 1 < args.size():
		_perf = PerfLog.new()
		_perf.out_dir = args[pi + 1]
		_perf.label = "%s%s door=%dx%d" % ["laptop" if args.has("--laptop") else "default", " night" if night else "", int(Site.DOOR_W), int(Site.DOOR_H)]
		add_child(_perf)
	if _autotest_dir == "":
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	if args.has("--skip-intro"):
		(_info["rig"]["arms"] as Node3D).position = Intro.ARMS_OPEN
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
			_intro.beat.connect(func(n: String): QA.shot(self, _autotest_dir, "i_" + n))
		await _intro.run()
		_intro.queue_free()
		_intro = null
	_start_step(0)
	if _autotest_dir != "":
		var qa := QA.new()
		qa.world = self
		qa.dir = _autotest_dir
		add_child(qa)
		qa.run()

# ------------------------------------------------------------------ environment
func _environment(laptop: bool) -> void:
	var we := WorldEnvironment.new()
	var env := Environment.new()
	var sky := Sky.new()
	var sm := ShaderMaterial.new()
	sm.shader = load("res://shaders/sky.gdshader")
	sky.sky_material = sm
	_sky = sm
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.75
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.0
	env.tonemap_white = 6.0
	env.ssao_enabled = true
	env.ssao_radius = 2.2
	env.ssao_intensity = 1.6
	env.ssil_enabled = not laptop
	env.ssr_enabled = not laptop
	env.ssr_max_steps = 72
	env.sdfgi_enabled = not laptop
	env.sdfgi_use_occlusion = true
	env.sdfgi_min_cell_size = 0.5
	env.sdfgi_cascades = 4
	env.glow_enabled = true
	env.glow_intensity = 0.5
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.2
	env.fog_enabled = true
	env.fog_light_color = Color(0.66, 0.76, 0.88)
	env.fog_density = 0.00022
	env.fog_aerial_perspective = 0.3
	# fog thins with height so mountain slopes keep their green
	env.fog_height = 0.0
	env.fog_height_density = 0.0025
	env.fog_sky_affect = 0.25
	env.volumetric_fog_enabled = not laptop
	env.volumetric_fog_density = 0.0012
	env.volumetric_fog_albedo = Color(0.9, 0.92, 0.95)
	env.volumetric_fog_length = 160.0
	env.adjustment_enabled = true
	env.adjustment_contrast = 1.06
	env.adjustment_saturation = 1.1
	we.environment = env
	add_child(we)
	_env = env
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	# sun from the south-south-east: the facade and the bay are side-lit from the
	# arrival view instead of backlit
	sun.rotation_degrees = Vector3(-40, 30, 0)
	sun.light_color = Color(1.0, 0.93, 0.82)
	sun.light_energy = 1.45
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS if laptop else DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_max_distance = 200.0 if laptop else 320.0
	sun.directional_shadow_blend_splits = true
	sun.light_angular_distance = 0.6
	sun.light_volumetric_fog_energy = 0.6
	add_child(sun)
	_sun = sun

## Lamps that only matter after dark are hidden by day so they cost nothing.
func _set_night_lights(on: bool) -> void:
	for n in get_tree().get_nodes_in_group("night_lights"):
		(n as Node3D).visible = on

## Night: moonlight from the same side as the day sun, a dark sky, lamps on.
func _apply_night() -> void:
	hour = 22.2
	_sun.rotation_degrees = Vector3(-38, 52, 0)
	_sun.light_color = Color(0.55, 0.64, 0.9)
	_sun.light_energy = 0.12
	_sky.set_shader_parameter("zenith", Color(0.01, 0.015, 0.035))
	_sky.set_shader_parameter("horizon", Color(0.05, 0.07, 0.12))
	_sky.set_shader_parameter("ground", Color(0.02, 0.025, 0.03))
	_sky.set_shader_parameter("cloud_cover", 0.35)
	_sky.set_shader_parameter("cloud_light", 0.06)
	_env.ambient_light_energy = 0.35
	_env.fog_light_color = Color(0.08, 0.1, 0.16)
	_env.tonemap_exposure = 1.25
	_env.glow_intensity = 0.8
	City.material().set_shader_parameter("night", 1.0)
	var water := Mats.get_m("Water") as ShaderMaterial
	if water:
		water.set_shader_parameter("light_level", 0.16)
	for key in ["Terrain", "Granite"]:
		var m := Mats.get_m(key) as ShaderMaterial
		if m:
			m.set_shader_parameter("caustic_level", 0.0)
	for n in get_tree().get_nodes_in_group("night_lights"):
		if n is SpotLight3D:
			(n as SpotLight3D).light_energy = 5.0
		elif n is OmniLight3D:
			(n as OmniLight3D).light_energy = 2.4

# ------------------------------------------------------------------ frames
func _display_mechs() -> void:
	var specs := [[Site.BAYS[0], Color(0.16, 0.2, 0.32), Color(0.85, 0.15, 0.12), Color(1.0, 0.3, 0.2)],
		[Site.BAYS[2], Color(0.55, 0.53, 0.47), Color(0.12, 0.4, 0.75), Color(0.3, 0.8, 1.0)]]
	for s in specs:
		var r := MechRig.new()
		add_child(r)
		r.setup(s[1], s[2], s[3])
		r.position = s[0]
		r.rotation.y = -PI * 0.5   # facing the door (east)
		# parked frames only matter near the hangar
		for mi in r.find_children("*", "MeshInstance3D", true, false):
			(mi as MeshInstance3D).visibility_range_end = 240.0

## The wading target: a tall orange spar buoy standing on the shelf.
func _marker_buoy() -> void:
	var g := Geo.new()
	var p := MARKER
	g.cylinder(Mats.orange(), p, p + Vector3(0, Site.SHELF_DEPTH + 3.5, 0), 0.5, 12)
	g.box(Mats.white(), p + Vector3(0, Site.SHELF_DEPTH + 2.6, 0), Vector3(1.1, 0.4, 1.1))
	g.box(Mats.glow("MarkerLamp", Color(1.0, 0.75, 0.2), 6.0), p + Vector3(0, Site.SHELF_DEPTH + 3.8, 0), Vector3(0.5, 0.5, 0.5))
	g.commit(self, "MarkerBuoy", true, false)

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
	cam.global_position = mech.global_position + Vector3(0, 6.2 * Site.MECH_SCALE, 0)
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
	pm.initial_velocity_min = 2.5
	pm.initial_velocity_max = 5.5
	pm.gravity = Vector3(0, -14, 0)
	pm.scale_min = 0.6
	pm.scale_max = 1.6
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 1.8
	_splash.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(1.1, 1.1)
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
	_splash.amount = 140
	_splash.lifetime = 0.9
	_splash.emitting = false
	add_child(_splash)

# ------------------------------------------------------------------ frame feedback
func _foot(side: int) -> Vector3:
	var b := Basis(Vector3.UP, mech.yaw)
	return mech.global_position + b * Vector3((side * 2 - 1) * 1.0, 0, 0)

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
			Sfx.I.play_at("clank", p + Vector3(0, 3.5, 0), -14.0, 1.3, 16)

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
	Fx.I.qb_burst(mech.global_position + Vector3(0, 4.0, 0) + back * 1.6, back)
	Sfx.I.play_at("qb", mech.global_position, 2.0, 1.0, 30)
	cam.add_trauma(0.25)

func _on_entered_water() -> void:
	hud.toast("ENTERING WATER  ·  TRACTION REDUCED", 2.0)
	var p := mech.global_position
	Fx.I.splash(Vector3(p.x, Site.WATER_Y + 0.05, p.z))
	Fx.I.ripple(Vector3(p.x, Site.WATER_Y + 0.05, p.z), 7.0)
	Sfx.I.play_at("splash", p, 0.0, 1.0, 30)

func _on_tuning_opened(on: bool) -> void:
	mech.input_enabled = not on
	cam.input_enabled = not on
	if _autotest_dir == "":
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if on else Input.MOUSE_MODE_CAPTURED

# ------------------------------------------------------------------ onboarding
func _start_step(i: int) -> void:
	step = i
	_step_start = {"qb": mech.quick_boosts, "hover": mech.hover_time, "boost": mech.boost_time, "look": cam.look_amount,
		"jumps": mech.jumps, "yaw": mech.yaw, "t": Time.get_ticks_msec()}
	if i >= STEPS.size():
		_done = true
		hud.set_objective(0, STEPS.size(), "TRAINING COMPLETE", "Free roam. Optional: quick boost (Q / B), hover (hold jump).")
		hud.set_prompts([])
		hud.show_complete(true)
		Sfx.I.play("confirm", -4.0, 0.8)
		hud.subtitle("HARBOUR CONTROL", "Clean run, pilot. The yard's yours. Explore as you like.", 6.0)
		# clear the card for free roam
		get_tree().create_timer(6.0).timeout.connect(func() -> void: hud.show_complete(false))
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
	return (STEPS[step] as Dictionary).get("pad")

## Heading (yaw) from the frame toward a point.
func yaw_to(p: Vector3) -> float:
	var d := p - mech.global_position
	return atan2(-d.x, -d.z)

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
			var off := absf(angle_difference(mech.yaw, yaw_to(BRIDGE)))
			if off < deg_to_rad(15.0) and absf(angle_difference(mech.yaw, _step_start["yaw"])) > 0.5:
				_start_step(3)
		3:
			if mech.jumps > _step_start["jumps"] and mech.is_on_floor() and mech.air_time > 0.0:
				_start_step(4)
		4:
			if near and mech.boost_time - _step_start["boost"] > 1.0:
				_start_step(5)
			elif near:
				hud.toast("HOLD BOOST ON THE WAY  ·  TRY AGAIN FROM THE STRIP", 2.0)
		5:
			if mech.in_water and Vector2(p.x - MARKER.x, p.z - MARKER.z).length() < 10.0:
				_start_step(6)
		6:
			if near and not mech.in_water and p.y > -0.6:
				_start_step(7)

func _unhandled_input(e: InputEvent) -> void:
	if e is InputEventKey and (e as InputEventKey).pressed and (e as InputEventKey).keycode == KEY_F4 and not (e as InputEventKey).echo:
		tuning_panel.toggle()

func _process(_dt: float) -> void:
	if mech == null or hud == null:
		return
	# frame feedback runs even while the intro has control
	Sfx.I.set_boost(1.0 if mech.boosting else (0.45 if mech.hovering else 0.0))
	Sfx.I.set_wind(clampf((mech.global_position.y - 2.0) / 20.0 + mech.speed() / 60.0, 0.0, 1.0))
	var back := Basis(Vector3.UP, mech.yaw) * Vector3(0, 0, 1)
	_exhaust.global_position = mech.global_position + Vector3(0, 3.7, 0) + back * 1.7
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
	if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED and not _paused and _autotest_dir == "" and not tuning_panel.visible and Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_check_step()
	var wp: Variant = _waypoint()
	var wname := ""
	if wp != null:
		wname = STEPS[step]["name"]
	hud.update_frame(mech, cam.cam, cam.yaw, hour, wp, wname)
	rig.set_wet(1.0 if mech.in_water else 0.0)
	_splash.global_position = mech.global_position + Vector3(0, Site.WATER_Y - mech.global_position.y + 0.3, 0) if mech.in_water else mech.global_position
	_splash.emitting = mech.in_water and mech.speed() > 2.0
