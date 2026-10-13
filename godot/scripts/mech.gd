class_name Mech
extends CharacterBody3D
## The player's frame. Walk, boost (hold), quick boost (dash), jump and hover on
## booster energy, and wade through the shallows. Movement is relative to the
## camera; the frame turns toward where it is going.

signal quick_boosted
signal landed(impact: float)
signal footstep(side: int)
signal entered_water
signal left_water

const WALK := 13.0
const BOOST := 36.0
const WADE := 0.45
const ACCEL := 46.0
const AIR_ACCEL := 14.0
const TURN := 7.0
const JUMP := 13.5
const HOVER := 40.0
const QB_SPEED := 44.0
const QB_COST := 0.2
const BOOST_DRAIN := 0.16
const HOVER_DRAIN := 0.3
const REGEN := 0.4
const GRAVITY := 30.0

var cam_yaw := 0.0
var input_enabled := true
var energy := 1.0
var boosting := false
var hovering := false
var in_water := false
var overheated := false
var yaw := 0.0
var stride := 0.0
var air_time := 0.0
var quick_boosts := 0
var hover_time := 0.0
var boost_time := 0.0
var _regen_delay := 0.0
var _qb_cd := 0.0
var _qb_vel := Vector3.ZERO
var _base_hv := Vector3.ZERO   # steering velocity, without the quick-boost burst
var _was_floor := true
var _fall_speed := 0.0
var rig: Node

## Simulated input (the autotest drives the frame through this).
var sim_move := Vector2.ZERO
var sim_boost := false
var sim_jump := false
var sim_jump_pressed := false
var sim_qb := false

func _ready() -> void:
	floor_max_angle = deg_to_rad(48.0)
	floor_snap_length = 1.2
	var shape := CapsuleShape3D.new()
	shape.radius = 2.1
	shape.height = 8.4
	var cs := CollisionShape3D.new()
	cs.shape = shape
	cs.position = Vector3(0, 4.25, 0)
	add_child(cs)

func speed() -> float:
	return Vector2(velocity.x, velocity.z).length()

func _physics_process(dt: float) -> void:
	var mv := sim_move
	var want_boost := sim_boost
	var jump_held := sim_jump
	var jump_pressed := sim_jump_pressed
	var qb := sim_qb
	sim_jump_pressed = false
	sim_qb = false
	if input_enabled:
		mv += Input.get_vector("move_left", "move_right", "move_forward", "move_back")
		want_boost = want_boost or Input.is_action_pressed("boost")
		jump_held = jump_held or Input.is_action_pressed("jump")
		jump_pressed = jump_pressed or Input.is_action_just_pressed("jump")
		qb = qb or Input.is_action_just_pressed("quick_boost")
	mv = mv.limit_length(1.0)
	var wish := Basis(Vector3.UP, cam_yaw) * Vector3(mv.x, 0, mv.y)
	var moving := wish.length() > 0.1

	# water
	var wet := global_position.y < Site.WATER_Y - 0.5
	if wet != in_water:
		in_water = wet
		if wet: entered_water.emit()
		else: left_water.emit()

	# energy
	boosting = want_boost and moving and energy > 0.02 and not overheated
	var floor_now := is_on_floor()
	hovering = jump_held and not floor_now and energy > 0.02 and not overheated and velocity.y < 9.0
	if boosting:
		energy -= BOOST_DRAIN * dt
		boost_time += dt
	if hovering:
		energy -= HOVER_DRAIN * dt
		hover_time += dt
	if boosting or hovering:
		_regen_delay = 0.6
	else:
		_regen_delay -= dt
		if _regen_delay <= 0.0:
			energy = minf(1.0, energy + REGEN * dt)
	if energy <= 0.0:
		energy = 0.0
		overheated = true
	if overheated and energy > 0.35:
		overheated = false

	# horizontal movement
	var top := BOOST if boosting else WALK
	if in_water and floor_now:
		top *= WADE
	var target := wish * top
	var hv := _base_hv
	var accel := (ACCEL * (1.4 if boosting else 1.0)) if floor_now else AIR_ACCEL * (2.2 if boosting else 1.0)
	if not moving and floor_now:
		accel *= 1.6
	hv = hv.move_toward(target, accel * dt)
	# quick boost: a sharp dash along the stick (or straight ahead)
	_qb_cd -= dt
	if qb and _qb_cd <= 0.0 and energy > QB_COST * 0.5:
		var d := wish.normalized() if moving else -global_transform.basis.z
		_qb_vel = d * QB_SPEED
		energy -= QB_COST
		_regen_delay = 0.8
		_qb_cd = 0.45
		quick_boosts += 1
		quick_boosted.emit()
	_qb_vel = _qb_vel.lerp(Vector3.ZERO, 1.0 - exp(-dt * 5.0))
	velocity.x = hv.x + _qb_vel.x
	velocity.z = hv.z + _qb_vel.z

	# vertical
	if jump_pressed and floor_now and not overheated:
		velocity.y = JUMP
	if hovering:
		velocity.y += (HOVER - maxf(velocity.y, 0.0) * 2.0) * dt
	velocity.y -= GRAVITY * dt
	if in_water and velocity.y < -6.0:
		velocity.y = lerpf(velocity.y, -6.0, 1.0 - exp(-dt * 6.0))
	_fall_speed = -velocity.y

	move_and_slide()
	# whatever the slide left us with, minus the burst, is next frame's steering velocity
	_base_hv = Vector3(velocity.x - _qb_vel.x, 0, velocity.z - _qb_vel.z)
	if _base_hv.length() > BOOST * 1.2:
		_base_hv = _base_hv.limit_length(BOOST * 1.2)

	var on := is_on_floor()
	if on and not _was_floor:
		landed.emit(_fall_speed)
	if not on:
		air_time += dt
	_was_floor = on

	# facing: turn toward travel, or toward the camera when strafing slowly
	if moving:
		var tgt := atan2(-wish.x, -wish.z)
		yaw = lerp_angle(yaw, tgt, 1.0 - exp(-dt * TURN * (1.6 if boosting else 1.0)))
	rotation.y = yaw

	# stride for animation and footsteps
	var sp := speed()
	if on and sp > 0.5 and not boosting:
		var before := stride
		stride += dt * sp * 0.16
		if floori(before * 2.0) != floori(stride * 2.0):
			footstep.emit(floori(stride * 2.0) % 2)

func teleport(p: Vector3, facing: float) -> void:
	global_position = p
	velocity = Vector3.ZERO
	_base_hv = Vector3.ZERO
	_qb_vel = Vector3.ZERO
	yaw = facing
	rotation.y = yaw
