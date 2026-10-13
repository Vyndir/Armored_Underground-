class_name Intro
extends Node
## Opening sequence: a flyover from the bay into the hangar, the title card,
## harbour control on the radio, then KESTREL's release from the service bay
## (arms retract, clamps drop, venting, power-up, HUD boot) before control is
## handed to the pilot. Hold jump or press interact to skip.

signal beat(name: String)
signal finished

const PATH := [Vector3(430, 42, 40), Vector3(250, 22, 20), Vector3(130, 12, 34), Vector3(46, 9, 36),
	Vector3(-20, 10, -36), Vector3(-66, 11, -74), Vector3(-106, 8.5, -76)]
const FLY_TIME := 15.0

var world: Node3D
var mech: Mech
var rig: MechRig
var cam: ChaseCamera
var hud: Hud
var gantry: Dictionary
var _cam: Camera3D
var _curve := Curve3D.new()
var _t := 0.0
var _flying := false
var _skipped := false
var _hold := 0.0

func run() -> void:
	for i in PATH.size():
		var prev: Vector3 = PATH[maxi(i - 1, 0)]
		var next: Vector3 = PATH[mini(i + 1, PATH.size() - 1)]
		var tan := (next - prev) / 6.0
		_curve.add_point(PATH[i], -tan, tan)
	_curve.bake_interval = 0.5
	_cam = Camera3D.new()
	_cam.fov = 58
	_cam.far = 9000
	_cam.near = 0.25
	world.add_child(_cam)
	_cam.current = true
	mech.input_enabled = false
	cam.input_enabled = false
	rig.set_power(0.0)
	hud.set_cinematic(true)
	_flying = true
	_place(0.0)
	await _wait(1.5)
	beat.emit("bay")
	hud.title_card(true, 2.0)
	await _wait(2.5)
	hud.subtitle("HARBOUR CONTROL", "Morning, pilot. Welcome to the Waterfront Training Hangar.", 4.5)
	await _wait(3.0)
	hud.title_card(false, 1.5)
	beat.emit("slipway")
	await _wait(3.0)
	hud.subtitle("HARBOUR CONTROL", "KESTREL is prepped in bay one. Let's get you on your feet.", 4.5)
	await _wait(3.2)
	beat.emit("door")
	await _wait(FLY_TIME - _t)
	_flying = false
	await _release()
	await _handover()
	finished.emit()

func _place(t: float) -> void:
	var u := clampf(t / FLY_TIME, 0.0, 1.0)
	# fast over the water, settling as it reaches the bay
	var e := 1.0 - pow(1.0 - u, 2.2)
	var pos := _curve.sample_baked(e * _curve.get_baked_length(), true)
	var look := Vector3(-100, 14, -74).lerp(mech.global_position + Vector3(0, 6.5, 0), smoothstep(0.55, 1.0, u))
	_cam.global_position = pos
	_cam.look_at(look)

func _process(dt: float) -> void:
	if _cam == null:
		return
	if _flying:
		_t += dt
		_place(_t)
	if not _skipped:
		if Input.is_action_pressed("jump"):
			_hold += dt
		else:
			_hold = 0.0
		if _hold > 0.5 or Input.is_action_just_pressed("interact"):
			_skipped = true

## Wait, unless the player skips.
func _wait(sec: float) -> void:
	var t := 0.0
	while t < sec and not _skipped:
		await world.get_tree().process_frame
		t += world.get_process_delta_time()

func _release() -> void:
	var arms: Node3D = gantry.get("arms")
	var clamps: Node3D = gantry.get("clamps")
	if _skipped:
		if arms: arms.position = Vector3(-2.0, 4.5, 0)
		if clamps: clamps.position.y = -0.32
		rig.set_power(1.0)
		return
	_cam.global_position = Vector3(-106, 8.5, -76)
	_cam.look_at(mech.global_position + Vector3(0, 6.5, 0))
	hud.subtitle("BAY CREW", "Releasing clamps. Stand clear of the frame.", 3.5)
	var at := mech.global_position
	if Sfx.I:
		Sfx.I.play_at("clank", at + Vector3(0, 0.5, 0), 2.0, 0.8, 30)
	var tw := world.create_tween()
	if clamps:
		tw.tween_property(clamps, "position:y", -0.32, 0.5).set_trans(Tween.TRANS_QUAD)
	await _wait(0.8)
	if Sfx.I:
		Sfx.I.play_at("servo", at + Vector3(0, 7, 0), 0.0, 1.0, 30)
		Sfx.I.play_at("hiss", at + Vector3(0, 2, 0), 0.0, 1.0, 30)
	var vents: Array[GPUParticles3D] = []
	if Fx.I:
		for s: float in [-1.6, 1.6]:
			var v := Fx.I.emitter(world, at + Vector3(0, 0.4, s), "vent")
			v.rotation.y = 0.0 if s > 0 else PI
			vents.append(v)
	beat.emit("release")
	if arms:
		var ta := world.create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		ta.tween_property(arms, "position", Vector3(-2.0, 4.5, 0), 2.6)
	# push in slowly while the frame wakes up
	var tc := world.create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tc.tween_property(_cam, "global_position", Vector3(-112, 7.4, -79), 4.5)
	await _wait(1.6)
	hud.subtitle("KESTREL", "Reactor online. Actuators nominal.", 3.0)
	var tp := world.create_tween()
	tp.tween_method(rig.set_power, 0.0, 1.0, 1.6).set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_IN)
	await _wait(1.0)
	if Sfx.I:
		Sfx.I.play("boot", -6.0, 0.7)
	cam.add_trauma(0.3)
	await _wait(1.4)
	beat.emit("powered")
	for v in vents:
		v.emitting = false
	if _skipped:
		tw.kill()
		if arms: arms.position = Vector3(-2.0, 4.5, 0)
		if clamps: clamps.position.y = -0.32
		rig.set_power(1.0)

func _handover() -> void:
	# glide into the chase camera, then hand over control
	cam.snap_behind()
	await world.get_tree().process_frame
	var target := cam.cam.global_transform
	if not _skipped:
		var tw := world.create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tw.tween_property(_cam, "global_transform", target, 1.4)
		await tw.finished
	cam.cam.current = true
	_cam.queue_free()
	_cam = null
	hud.set_cinematic(false)
	hud.subtitle("HARBOUR CONTROL", "Systems are yours, pilot. Run the training course.", 4.0)
	mech.input_enabled = true
	cam.input_enabled = true
	await hud.boot(["KESTREL OS 4.2  ·  LEAGUE FRAME L-07", "> reactor ........ 100%", "> actuators ...... OK",
		"> boosters ....... OK", "> head cameras ... CALIBRATING", "> nav uplink ..... HARBOUR CONTROL", "> pilot link ..... ESTABLISHED"])
