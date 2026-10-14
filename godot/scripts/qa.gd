class_name QA
extends Node
## Automated checks for the waterfront slice (`--autotest <dir>`). Prints one
## `[qa]` line per measurement and saves screenshots to <dir>.
##   scale       helmet top, antenna top, soles and collider, measured in-engine
##   onboarding  plays the seven required steps with the normal controls
##   movement    peak walk speed, step interval, turn time, jump apex, short boost
##   water       knee height vs the surface on the shelf; exits by both ramps
##   boundary    walks, boosts, jumps, hovers and dashes into every edge and corner
##   slopes      climbs the coastal slopes behind the yard and the north yard
##   camera      backs the frame against walls and the boundary
##   views       hangar exit, training apron, water edge, road to the city, overview
## `--shots-only` skips the measurements (used for door and night comparisons).

var world: HangarWorld
var dir := ""
var mech: Mech
var cam: ChaseCamera
var shot_cam: Camera3D
var _last_pos := Vector3.ZERO
var _max_jump := 0.0       # largest single-frame position jump seen (teleport detector)

static func shot(node: Node, out_dir: String, name: String) -> void:
	await RenderingServer.frame_post_draw
	var img := node.get_viewport().get_texture().get_image()
	DirAccess.make_dir_recursive_absolute(out_dir)
	img.save_png(out_dir.path_join(name + ".png"))
	print("[autotest] ", name)

func _shot(name: String) -> void:
	await QA.shot(self, dir, name)

func _wait(sec: float) -> void:
	await get_tree().create_timer(sec).timeout

func _log(s: String) -> void:
	print("[qa] ", s)

func _physics_process(_dt: float) -> void:
	if mech:
		var d := mech.global_position.distance_to(_last_pos)
		if _last_pos != Vector3.ZERO:
			_max_jump = maxf(_max_jump, d)
		_last_pos = mech.global_position

func run() -> void:
	mech = world.mech
	cam = world.cam
	cam.input_enabled = false
	mech.input_enabled = false
	shot_cam = Camera3D.new()
	shot_cam.fov = 60
	shot_cam.far = 9000
	world.add_child(shot_cam)
	var shots_only := OS.get_cmdline_user_args().has("--shots-only")
	await _wait(1.5)
	if OS.get_cmdline_user_args().has("--review"):
		await review_views()
		get_tree().quit()
		return
	if OS.get_cmdline_user_args().has("--perf-tour"):
		await perf_tour()
		get_tree().quit()
		return
	if not shots_only:
		await measure_scale()
		await onboarding()
		await movement()
		await water()
		await boundary()
		await slopes()
		await camera_checks()
	await views()
	var perf: PerfLog = world._perf
	if perf:
		_log("perf " + perf.summary())
	_log("done step=%d done=%s" % [world.step, str(world._done)])
	get_tree().quit()

# ------------------------------------------------------------------ helpers
func _view(name: String, pos: Vector3, look: Vector3, settle := 0.8) -> void:
	shot_cam.global_position = pos
	shot_cam.look_at(look)
	shot_cam.current = true
	await _wait(settle)
	await _shot(name)
	cam.cam.current = true

func _place(p: Vector3, yaw: float) -> void:
	mech.teleport(p + Vector3(0, 0.3, 0), yaw)
	cam.yaw = yaw
	cam.global_position = mech.global_position + Vector3(0, 5.5, 0)
	_last_pos = Vector3.ZERO
	await _wait(0.6)

func _release() -> void:
	mech.sim_move = Vector2.ZERO
	mech.sim_boost = false
	mech.sim_jump = false

## Steer toward a point by turning the camera and holding forward.
func _drive(target: Vector3, boost := false, timeout := 30.0, radius := 5.0) -> bool:
	var t := 0.0
	while t < timeout:
		var to := target - mech.global_position
		to.y = 0
		if to.length() < radius:
			_release()
			return true
		cam.yaw = lerp_angle(cam.yaw, atan2(-to.x, -to.z), 0.2)
		mech.sim_move = Vector2(0, -1)
		mech.sim_boost = boost and mech.energy > 0.25
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
	_release()
	_log("drive timeout toward %s at %s" % [target.snapped(Vector3.ONE), mech.global_position.snapped(Vector3.ONE)])
	return false

static func outside_dist(p: Vector3) -> float:
	var poly := PackedVector2Array()
	for v: Vector2 in Site.BOUNDARY:
		poly.append(v)
	var q := Vector2(p.x, p.z)
	if Geometry2D.is_point_in_polygon(q, poly):
		return 0.0
	var best := INF
	for i in poly.size() - 1:
		best = minf(best, q.distance_to(Geometry2D.get_closest_point_to_segment(q, poly[i], poly[i + 1])))
	return best

# ------------------------------------------------------------------ scale
func measure_scale() -> void:
	await _place(Vector3(-48, 0, -74), -PI * 0.5)
	await _wait(1.0)
	var rig: Node3D = world.rig
	var model: Node3D = rig.get_child(0)
	var base := mech.global_position.y
	var inv := rig.global_transform.affine_inverse()
	var helmet := -INF
	var antenna := -INF
	var sole := INF
	var s := Site.MECH_SCALE
	for mi in model.find_children("*", "MeshInstance3D", true, false):
		var m: MeshInstance3D = mi
		var xf := m.global_transform
		for si in m.mesh.get_surface_count():
			var verts: PackedVector3Array = m.mesh.surface_get_arrays(si)[Mesh.ARRAY_VERTEX]
			for v in verts:
				var w := xf * v
				var local := inv * w
				sole = minf(sole, w.y)
				antenna = maxf(antenna, w.y)
				if String(m.name).contains("head"):
					# model units, in the frame's own axes: drop the antenna, whip and crest fin
					var mx := absf(local.x) / s
					var my := (local.y / s) + Site.MECH_MODEL_SOLE
					var thin := my > 7.6 and ((mx > 0.17 and mx < 0.33) or mx < 0.03)
					if not thin:
						helmet = maxf(helmet, w.y)
	var shape: CapsuleShape3D = (mech.get_child(0) as CollisionShape3D).shape
	_log("scale factor=%.4f helmet_top=%.3f m antenna_top=%.3f m sole=%.3f m (ground %.3f) collider: capsule h=%.2f r=%.2f top=%.2f" % [s, helmet - base, antenna - base, sole - base, base, shape.height, shape.radius, shape.height])
	var sk: Skeleton3D = rig.find_children("*", "Skeleton3D", true, false)[0]
	var knee := (sk.global_transform * sk.get_bone_global_pose(sk.find_bone("shin.L"))).origin.y - base
	_log("knee joint height=%.2f m, hip height=%.2f m" % [knee, (sk.global_transform * sk.get_bone_global_pose(sk.find_bone("thigh.L"))).origin.y - base])

# ------------------------------------------------------------------ onboarding
func onboarding() -> void:
	await _place(Site.SPAWN, -PI * 0.5)
	world._start_step(0)
	await _shot("o0_spawn")
	cam.look_amount += 3.0
	await _wait(0.5)
	await _drive(HangarWorld.PAD1, false, 30.0, 4.0)
	await _wait(0.5)
	await _shot("o1_pad1")
	# turn toward the bridge with a short walk
	var t := 0.0
	while world.step == 2 and t < 8.0:
		cam.yaw = lerp_angle(cam.yaw, world.yaw_to(HangarWorld.BRIDGE), 0.25)
		mech.sim_move = Vector2(0, -0.35)
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
	_release()
	_log("turn step done in %.2fs, step=%d" % [t, world.step])
	await _shot("o2_turned_to_bridge")
	mech.sim_jump_pressed = true
	await _wait(1.6)
	_log("jump step -> step=%d" % world.step)
	await _drive(HangarWorld.PAD2, true, 30.0, 4.0)
	await _shot("o4_pad2_boost")
	await _drive(Vector3(22, 0, -50), false, 30.0, 3.0)
	await _drive(Vector3(52, 0, -50), false, 20.0, 3.0)
	await _drive(HangarWorld.MARKER, false, 30.0, 5.0)
	await _wait(1.0)
	await _shot("o5_wading_marker")
	_log("wade -> step=%d in_water=%s y=%.2f" % [world.step, str(mech.in_water), mech.global_position.y])
	await _drive(Vector3(60, 0, 33), false, 30.0, 4.0)
	await _drive(Vector3(-2, 0, 33), false, 30.0, 4.0)
	await _drive(HangarWorld.PAD3, false, 30.0, 4.0)
	await _wait(1.0)
	await _shot("o6_complete")
	_log("onboarding step=%d done=%s max_frame_move=%.2fm" % [world.step, str(world._done), _max_jump])

# ------------------------------------------------------------------ movement
func movement() -> void:
	# peak walk speed and step interval on the open field
	await _place(Vector3(-60, 0, -150), PI)
	var steps := []
	var cb := func(_s: int) -> void: steps.append(Time.get_ticks_msec())
	mech.footstep.connect(cb)
	var peak := 0.0
	var avg := 0.0
	var n := 0
	var t := 0.0
	while t < 6.0:
		mech.sim_move = Vector2(0, -1)
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
		if t > 2.0:
			peak = maxf(peak, mech.speed())
			avg += mech.speed()
			n += 1
	_release()
	mech.footstep.disconnect(cb)
	var iv := []
	for i in range(1, steps.size()):
		if steps[i - 1] > 0:
			iv.append((steps[i] - steps[i - 1]) / 1000.0)
	var mean_iv := 0.0
	for v in iv.slice(maxi(0, iv.size() - 8)):
		mean_iv += v
	mean_iv /= maxf(1.0, minf(8.0, iv.size()))
	_log("walk peak=%.2f m/s avg=%.2f m/s step_interval=%.3fs (tuning: walk %.1f, step %.2fs, load %.2fs, turn %.1f)" % [peak, avg / maxf(1, n), mean_iv,
		mech.tuning.walk_speed, mech.tuning.step_period, mech.tuning.load_phase, mech.tuning.turn_response])
	await _wait(1.0)
	# 180 degree turn while walking
	var y0 := mech.yaw
	cam.yaw = y0 + PI
	t = 0.0
	while absf(angle_difference(mech.yaw, y0 + PI)) > deg_to_rad(10.0) and t < 5.0:
		mech.sim_move = Vector2(0, -1)
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
	_release()
	_log("turn 180 deg to within 10 deg: %.2fs" % t)
	await _wait(1.5)
	# jump apex from standing
	var gy := mech.global_position.y
	var apex := gy
	mech.sim_jump_pressed = true
	t = 0.0
	while t < 2.0:
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
		apex = maxf(apex, mech.global_position.y)
	_log("jump apex=%.2f m (tap, no hover)" % (apex - gy))
	# short boost: one second from a standstill
	await _wait(1.0)
	var p0 := mech.global_position
	t = 0.0
	var vmax := 0.0
	while t < 1.0:
		mech.sim_move = Vector2(0, -1)
		mech.sim_boost = true
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
		vmax = maxf(vmax, mech.speed())
	_release()
	_log("short boost 1.0s: distance=%.1f m top=%.1f m/s energy=%.2f" % [Vector2(mech.global_position.x - p0.x, mech.global_position.z - p0.z).length(), vmax, mech.energy])

# ------------------------------------------------------------------ water
func water() -> void:
	# jump off the quay edge straight onto the shelf: no damage, no reset
	await _place(Vector3(32, 0, -20), -PI * 0.5)
	var before := mech.global_position
	_max_jump = 0.0
	await _drive(Vector3(70, 0, -20), false, 15.0, 4.0)
	await _wait(1.5)
	var sk: Skeleton3D = world.rig.find_children("*", "Skeleton3D", true, false)[0]
	var knee := (sk.global_transform * sk.get_bone_global_pose(sk.find_bone("shin.L"))).origin.y
	var feet := mech.global_position.y
	_log("shelf: feet y=%.2f floor=%.2f water=%.2f depth=%.2f m knee y=%.2f (water %.2f m below knee) in_water=%s max_frame_move=%.2f m (from %s)" % [feet, Site.SHELF_FLOOR, Site.WATER_Y,
		Site.WATER_Y - feet, knee, knee - Site.WATER_Y, str(mech.in_water), _max_jump, before.snapped(Vector3.ONE)])
	cam.yaw = PI * 0.5   # camera east of the frame, looking back at the hangar
	await _wait(1.0)
	await _shot("w1_water_edge_looking_back")
	await _view("w2_depth_side", mech.global_position + Vector3(4, 2.4, 10), mech.global_position + Vector3(0, 2.0, 0))
	# out by the north ramp
	await _drive(Vector3(52, 0, -50), false, 20.0, 3.0)
	var ok1 := await _drive(Vector3(4, 0, -50), false, 20.0, 3.0)
	_log("exit by north ramp: ok=%s y=%.2f in_water=%s" % [str(ok1), mech.global_position.y, str(mech.in_water)])
	# back in by the slipway, out again
	await _drive(Vector3(60, 0, 33), false, 30.0, 4.0)
	var ok2 := await _drive(Vector3(-4, 0, 33), false, 30.0, 3.0)
	_log("exit by slipway: ok=%s y=%.2f in_water=%s max_frame_move=%.2f m" % [str(ok2), mech.global_position.y, str(mech.in_water), _max_jump])

# ------------------------------------------------------------------ boundary
func _assault(name: String, start: Vector3, target: Vector3, mode: String) -> float:
	var yaw := atan2(-(target.x - start.x), -(target.z - start.z))
	await _place(start, yaw)
	mech.energy = 1.0
	var worst := 0.0
	var top := 0.0
	var t := 0.0
	while t < 7.0:
		var to := target - mech.global_position
		cam.yaw = atan2(-to.x, -to.z)
		mech.sim_move = Vector2(0, -1)
		mech.sim_boost = mode != "walk"
		if mode == "jump" or mode == "hover":
			if mech.is_on_floor():
				mech.sim_jump_pressed = true
			mech.sim_jump = mode == "hover"
		if mode == "dash" and fmod(t, 0.5) < get_physics_process_delta_time():
			mech.sim_qb = true
		await get_tree().physics_frame
		t += get_physics_process_delta_time()
		worst = maxf(worst, outside_dist(mech.global_position))
		top = maxf(top, mech.global_position.y)
	_release()
	_log("boundary %-22s %-6s outside_max=%.2f m top_y=%.1f end=%s" % [name, mode, worst, top, mech.global_position.snapped(Vector3.ONE)])
	return worst

func boundary() -> void:
	var cases := [
		["north road checkpoint", Vector3(-40, 0, -440), Vector3(-40, 0, -520)],
		["north strip end", Vector3(27, 0, -440), Vector3(27, 0, -520)],
		["NE corner quay/north", Vector3(30, 0, -450), Vector3(70, 0, -500)],
		["quay deep water", Vector3(25, 0, -300), Vector3(90, 0, -300)],
		["shelf N boom", Vector3(80, 0, -35), Vector3(80, 0, -100)],
		["shelf NE corner", Vector3(100, 0, -40), Vector3(150, 0, -90)],
		["shelf E boom", Vector3(95, 0, 5), Vector3(170, 0, 5)],
		["shelf SE corner", Vector3(100, 0, 55), Vector3(150, 0, 100)],
		["shelf S boom to shore", Vector3(60, 0, 50), Vector3(60, 0, 120)],
		["south fence rocks", Vector3(-90, 0, 60), Vector3(-90, 0, 140)],
		["SW corner", Vector3(-190, 0, 75), Vector3(-260, 0, 140)],
		["west hill fence", Vector3(-185, 0, -200), Vector3(-280, 0, -200)],
		["NW corner", Vector3(-190, 0, -440), Vector3(-260, 0, -520)],
	]
	var worst := 0.0
	for c in cases:
		for mode in ["boost", "jump", "hover", "dash"]:
			var s: Vector3 = c[1]
			s.y = maxf(Site.ground(s.x, s.z), Site.SHELF_FLOOR if Site.in_shelf(s.x, s.z) else 0.0)
			worst = maxf(worst, await _assault(c[0], s, c[2], mode))
	_log("boundary worst outside distance=%.2f m" % worst)
	await _place(Vector3(-40, 0, -455), PI)
	cam.yaw = 0.0
	await _wait(1.0)
	await _shot("b1_checkpoint_from_inside")

# ------------------------------------------------------------------ slopes
func slopes() -> void:
	for spec in [["behind yard", Vector3(-170, 0, -60), Vector3(-212, 0, -60)], ["north yard west", Vector3(-72, 0, -330), Vector3(-212, 0, -330)],
			["south rocks", Vector3(-60, 0, 74), Vector3(-60, 0, 92)]]:
		var a: Vector3 = spec[1]
		a.y = Site.ground(a.x, a.z) if not Site.in_paved(a.x, a.z) else 0.0
		await _place(a, atan2(-(spec[2].x - a.x), -(spec[2].z - a.z)))
		var y0 := mech.global_position.y
		var ok := await _drive(spec[2], false, 20.0, 4.0)
		_log("slope %-16s reached=%s climb=%.1f m end=%s slope_at_end=%.2f" % [spec[0], str(ok), mech.global_position.y - y0, mech.global_position.snapped(Vector3.ONE), Site.slope(mech.global_position.x, mech.global_position.z)])
	await _shot("s1_west_slope")

# ------------------------------------------------------------------ camera
func camera_checks() -> void:
	# backed against the hangar's back wall, camera swung round toward the wall
	await _place(Vector3(-126, 0, -84), PI * 0.5)
	cam.yaw = PI * 0.5 + PI
	await _wait(1.2)
	_log("camera vs hangar wall: arm=%.2f/%.2f m cam=%s inside_hangar=%s" % [cam.arm.get_hit_length(), cam.arm.spring_length, cam.cam.global_position.snapped(Vector3.ONE * 0.1),
		str(cam.cam.global_position.x > Site.HANGAR_X0)])
	await _shot("c1_camera_wall")
	# at the boundary fence looking out: the wall must not shove the camera in
	await _place(Vector3(-200, 0, -200), PI * 0.5)
	cam.yaw = -PI * 0.5
	await _wait(1.2)
	_log("camera at boundary: arm=%.2f/%.2f m" % [cam.arm.get_hit_length(), cam.arm.spring_length])

# ------------------------------------------------------------------ review views
## Matching before/after views from the gameplay camera (and two fixed cameras),
## each captured clean (HUD hidden) and, for the key ones, with the HUD.
func _gameplay(name: String, pos: Vector3, yaw: float, cam_yaw: float, pitch := -0.18, hud_too := false) -> void:
	await _place(pos, yaw)
	if Site.in_shelf(pos.x, pos.z):
		mech.teleport(Vector3(pos.x, Site.SHELF_FLOOR + 0.2, pos.z), yaw)
	cam.yaw = cam_yaw
	cam.pitch = pitch
	world.hud.visible = false
	await _wait(1.6)
	await _shot(name + "_clean")
	if hud_too:
		world.hud.visible = true
		await _wait(0.3)
		await _shot(name + "_hud")
	world.hud.visible = true

static func _tris(m: Mesh) -> int:
	if m == null:
		return 0
	var n := 0
	for s in m.get_surface_count():
		var arr := m.surface_get_arrays(s)
		var idx: PackedInt32Array = arr[Mesh.ARRAY_INDEX] if arr[Mesh.ARRAY_INDEX] != null else PackedInt32Array()
		n += idx.size() / 3 if idx.size() > 0 else (arr[Mesh.ARRAY_VERTEX] as PackedVector3Array).size() / 3
	return n

## Static scene cost: triangles by top-level node (x instances for multimeshes),
## how much of it casts shadows, surfaces (draw calls per pass) and lights.
func scene_costs() -> void:
	var rows := {}
	var lights := 0
	var shadow_lights := 0
	var labels := 0
	for n in world.find_children("*", "", true, false):
		var top: Node = n
		while top.get_parent() != world:
			top = top.get_parent()
		var key := String(top.name)
		var t := 0
		var surf := 0
		var shadow := false
		if n is MeshInstance3D:
			var mi := n as MeshInstance3D
			if not mi.visible:
				continue
			t = _tris(mi.mesh)
			surf = mi.mesh.get_surface_count() if mi.mesh else 0
			shadow = mi.cast_shadow != GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		elif n is MultiMeshInstance3D:
			var mm := (n as MultiMeshInstance3D).multimesh
			if mm:
				t = _tris(mm.mesh) * mm.instance_count
				surf = mm.mesh.get_surface_count() if mm.mesh else 0
			shadow = (n as GeometryInstance3D).cast_shadow != GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		elif n is Light3D:
			lights += 1
			if (n as Light3D).shadow_enabled:
				shadow_lights += 1
			continue
		elif n is Label3D:
			labels += 1
			continue
		else:
			continue
		if not rows.has(key):
			rows[key] = [0, 0, 0]
		rows[key][0] += t
		rows[key][1] += surf
		if shadow:
			rows[key][2] += t
	var keys := rows.keys()
	keys.sort_custom(func(a, b): return rows[a][0] > rows[b][0])
	var total := 0
	var total_sh := 0
	var total_s := 0
	for k in keys:
		total += rows[k][0]
		total_sh += rows[k][2]
		total_s += rows[k][1]
	_log("costs total_tris=%.2fM shadow_casting_tris=%.2fM surfaces=%d lights=%d shadowed_lights=%d label3d=%d" % [total / 1e6, total_sh / 1e6, total_s, lights, shadow_lights, labels])
	for k in keys.slice(0, 28):
		_log("cost %-22s tris=%8d surfaces=%4d shadow_tris=%8d" % [k, rows[k][0], rows[k][1], rows[k][2]])

func review_views() -> void:
	await scene_costs()
	var tag := ("night_" if world.night else "") + "%dx%d" % [int(Site.DOOR_W), int(Site.DOOR_H)]
	var east := -PI * 0.5
	var west := PI * 0.5
	await _wait(2.0)
	await _gameplay("r1_interior_%s" % tag, Vector3(-96, 0, -74), west, west, -0.12)
	await _gameplay("r2_arrival_spawn_%s" % tag, Site.SPAWN, east, east, -0.1, true)
	await _gameplay("r3_doorway_to_bay_%s" % tag, Vector3(-84, 0, -74), east, east - 0.12, -0.08, true)
	await _gameplay("r4_apron_back_to_hangar_%s" % tag, Vector3(-28, 0, -70), west, west + 0.1, -0.1)
	await _gameplay("r5_shallow_water_%s" % tag, Vector3(92, 0, -8), world.yaw_to(Vector3(-90, 0, -74)), PI * 0.5 + 0.35, -0.06)
	var to_bridge := atan2(-(HangarWorld.BRIDGE.x - 30.0), -(HangarWorld.BRIDGE.z + 120.0))
	await _gameplay("r6_bridge_city_panorama_%s" % tag, Vector3(30, 0, -120), to_bridge, to_bridge + 0.15, -0.02)
	# the doorway with the frame standing in it, seen from the yard
	await _place(Vector3(-80, 0, -74), east)
	world.hud.visible = false
	await _view("r7_doorway_scale_%s_clean" % tag, Vector3(-36, 4.5, -52), Vector3(-80, 8.0, -74), 1.2)
	world.hud.visible = true

# ------------------------------------------------------------------ performance tour
## A fixed gameplay route with the normal chase camera, no teleports: out of the
## hangar, across the yard, up the strip, down onto the shelf, back, up the road.
func perf_tour() -> void:
	var perf: PerfLog = world._perf
	await _wait(3.0)
	var t0 := Time.get_ticks_msec()
	for p: Vector3 in [Vector3(-60, 0, -74), Vector3(-20, 0, -120), Vector3(27, 0, -200), Vector3(27, 0, -70), Vector3(22, 0, -50),
			Vector3(52, 0, -50), Vector3(85, 0, 0), Vector3(60, 0, 33), Vector3(-4, 0, 33), Vector3(-30, 0, -100), Vector3(-40, 0, -195),
			Vector3(-40, 0, -440), Vector3(-40, 0, -300)]:
		await _drive(p, false, 40.0, 4.0)
		cam.yaw += 0.6
	_log("perf tour %.0fs: %s" % [(Time.get_ticks_msec() - t0) / 1000.0, perf.summary(3.0) if perf else "no --perf"])

# ------------------------------------------------------------------ views
func views() -> void:
	var tag := "%dx%d%s" % [int(Site.DOOR_W), int(Site.DOOR_H), "_night" if world.night else ""]
	# hangar exit from the chase camera: frame in the doorway, looking out
	await _place(Vector3(-84, 0, -74), -PI * 0.5)
	await _wait(1.0)
	await _shot("v1_hangar_exit_%s" % tag)
	# door from outside at gameplay height, frame standing in the opening
	await _view("v2_door_outside_%s" % tag, Vector3(-40, 5.5, -48), Vector3(-82, 6.5, -74))
	# the full training apron, from the frame at pad 1 and from above the door
	await _place(HangarWorld.PAD1 + Vector3(4, 0, 0), -PI * 0.4)
	await _wait(1.0)
	await _shot("v3_apron_gameplay_%s" % tag)
	await _view("v4_apron_elevated_%s" % tag, Vector3(-74, 26, -10), Vector3(0, 0, -100))
	# shallow-water edge looking back at the hangar
	await _place(Vector3(95, 0, -10), PI * 0.5)
	mech.teleport(Vector3(95, Site.SHELF_FLOOR + 0.3, -10), PI * 0.5)
	cam.yaw = PI * 0.5
	await _wait(1.5)
	await _shot("v5_water_edge_back_%s" % tag)
	# on the road toward the city
	await _place(Vector3(-40, 0, -420), 0.0)
	await _wait(1.0)
	await _shot("v6_road_to_city_%s" % tag)
	await _view("v7_road_to_city_eye_%s" % tag, Vector3(-46, 4.0, -440), Vector3(200, 30, -1700))
	# elevated overview: hangar, shoreline, road, bridge and downtown together
	await _view("v8_overview_%s" % tag, Vector3(420, 360, 420), Vector3(150, 0, -1000), 1.5)
	await _view("v9_overview_high_%s" % tag, Vector3(-500, 900, 700), Vector3(500, 0, -1300), 1.5)
	# arena silhouettes from the third-person camera on the quay
	await _place(Vector3(30, 0, -120), PI * 0.5)
	cam.yaw = -PI * 0.5 - 0.15
	await _wait(1.0)
	await _shot("v10_arena_from_quay_%s" % tag)
