class_name ChaseCamera
extends Node3D
## Third-person orbit camera on a spring arm: mouse or right stick to look,
## widens and pulls back with speed, shakes on heavy footsteps and landings,
## and can recenter behind the frame.

var target: Mech
var yaw := 0.0
var pitch := -0.18
var mouse_sens := 0.0026
var stick_sens := 2.6
var invert_y := false
var look_amount := 0.0     # accumulated look rotation (onboarding uses it)
var input_enabled := true
var cam: Camera3D
var arm: SpringArm3D
var _trauma := 0.0
var _fov := 62.0
var _recenter := 0.0
var _excluded := false

func _ready() -> void:
	arm = SpringArm3D.new()
	arm.spring_length = 15.0 * Site.MECH_SCALE
	arm.margin = 0.4
	var sphere := SphereShape3D.new()
	sphere.radius = 0.6
	arm.shape = sphere
	add_child(arm)
	cam = Camera3D.new()
	cam.fov = 62.0
	cam.near = 0.25
	cam.far = 9000.0
	arm.add_child(cam)
	cam.current = true

func _unhandled_input(e: InputEvent) -> void:
	if not input_enabled:
		return
	if e is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		var m: InputEventMouseMotion = e
		yaw -= m.relative.x * mouse_sens
		pitch -= m.relative.y * mouse_sens * (-1.0 if invert_y else 1.0)
		look_amount += absf(m.relative.x * mouse_sens) + absf(m.relative.y * mouse_sens)

func add_trauma(t: float) -> void:
	_trauma = minf(1.0, _trauma + t)

func snap_behind() -> void:
	if target:
		yaw = target.yaw
		_recenter = 0.0

func _process(dt: float) -> void:
	if target == null:
		return
	if not _excluded:
		arm.add_excluded_object(target.get_rid())
		_excluded = true
	if input_enabled:
		var look := Input.get_vector("look_left", "look_right", "look_up", "look_down")
		if look.length() > 0.0:
			yaw -= look.x * stick_sens * dt
			pitch -= look.y * stick_sens * 0.7 * dt * (-1.0 if invert_y else 1.0)
			look_amount += (absf(look.x) + absf(look.y)) * stick_sens * dt
		if Input.is_action_just_pressed("recenter_camera"):
			_recenter = 0.35
	if _recenter > 0.0:
		_recenter -= dt
		yaw = lerp_angle(yaw, target.yaw, 1.0 - exp(-dt * 12.0))
	pitch = clampf(pitch, -1.1, 0.55)
	var sp := target.speed()
	var pivot := target.global_position + Vector3(0, 6.2 * Site.MECH_SCALE, 0)
	global_position = global_position.lerp(pivot, 1.0 - exp(-dt * 14.0)) if global_position.distance_to(pivot) < 30.0 else pivot
	rotation = Vector3(pitch, yaw, 0)
	arm.spring_length = lerpf(arm.spring_length, (15.0 + clampf(sp / 36.0, 0.0, 1.0) * 4.0) * Site.MECH_SCALE, 1.0 - exp(-dt * 3.0))
	_fov = lerpf(_fov, 62.0 + clampf(sp / 36.0, 0.0, 1.0) * 12.0, 1.0 - exp(-dt * 3.0))
	cam.fov = _fov
	_trauma = maxf(0.0, _trauma - dt * 1.8)
	var s := _trauma * _trauma
	var t := Time.get_ticks_msec() * 0.03
	cam.h_offset = sin(t * 1.3) * s * 0.5
	cam.v_offset = cos(t * 1.7) * s * 0.5
	target.cam_yaw = yaw
