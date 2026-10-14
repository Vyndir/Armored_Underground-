class_name Mech
extends CharacterBody3D
## The player's frame. Walk, boost (hold), quick boost (dash), jump and hover on
## booster energy, and wade through the shallows. Movement is relative to the
## camera; the frame turns toward where it is going.
##
## Walking runs on a step clock (see MechTuning): each foot plant starts a short
## loading phase where the weight settles onto that foot and speed eases off,
## then the other leg swings through. Boost, quick boost, jump and hover are
## unchanged by the gait. Quick boost and hover are optional test features.

signal quick_boosted
signal landed(impact: float)
signal footstep(side: int)
signal entered_water
signal left_water

const WALK := 13.5          # nominal walk; the live value is tuning.walk_speed
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

var tuning: MechTuning = MechTuning.v02()
var cam_yaw := 0.0
var input_enabled := true
# gait state, read by the rig
var gait_t := 0.0          # 0..1 through the current step (0 = foot plant)
var gait_side := 0         # which foot is planted (0 left, 1 right)
var gait_period := 0.36    # current step period
var walk_amt := 0.0        # 0 standing .. 1 full walk
var gait_mult := 1.0       # speed multiplier from weight transfer
var jumps := 0
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
	# capsule from the ground to the helmet top of the 6.8 m frame
	var shape := CapsuleShape3D.new()
	shape.radius = 1.85
	shape.height = Site.MECH_HEIGHT + 0.05
	var cs := CollisionShape3D.new()
	cs.shape = shape
	cs.position = Vector3(0, shape.height * 0.5, 0)
	add_child(cs)
	collision_mask = 1 | (1 << (BuildBounds.LAYER - 1))

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
	var top := BOOST if boosting else tuning.walk_speed
	if in_water and floor_now:
		top *= WADE
	var target := wish * top
	var hv := _base_hv
	var accel := (tuning.accel * (1.4 if boosting else 1.0)) if floor_now else AIR_ACCEL * (2.2 if boosting else 1.0)
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
	# weight transfer: speed eases off while a foot takes the load (walking only)
	var gm := gait_mult if floor_now and not boosting else 1.0
	velocity.x = hv.x * gm + _qb_vel.x
	velocity.z = hv.z * gm + _qb_vel.z

	# vertical
	if jump_pressed and floor_now and not overheated:
		velocity.y = JUMP
		jumps += 1
	if hovering:
		velocity.y += (HOVER - maxf(velocity.y, 0.0) * 2.0) * dt
	velocity.y -= GRAVITY * dt
	if in_water and velocity.y < -6.0:
		velocity.y = lerpf(velocity.y, -6.0, 1.0 - exp(-dt * 6.0))
	_fall_speed = -velocity.y

	move_and_slide()
	# whatever the slide left us with, minus the burst, is next frame's steering velocity
	_base_hv = Vector3(velocity.x - _qb_vel.x, 0, velocity.z - _qb_vel.z) / maxf(gm, 0.01)
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
		yaw = lerp_angle(yaw, tgt, 1.0 - exp(-dt * tuning.turn_response * (1.6 if boosting else 1.0)))
	rotation.y = yaw

	_gait(dt, on, moving)

## Step clock: advance through the step, plant the other foot when it completes.
func _gait(dt: float, on: bool, _moving: bool) -> void:
	var sp := Vector2(_base_hv.x, _base_hv.z).length()
	var walking := on and not boosting and sp > 0.6
	walk_amt = move_toward(walk_amt, clampf(sp / tuning.walk_speed, 0.0, 1.0) if walking else 0.0, dt * 4.0)
	var frac := clampf(sp / tuning.walk_speed, 0.05, 1.0)
	if tuning.continuous_gait:
		gait_period = tuning.step_period / frac
	else:
		gait_period = tuning.step_period * lerpf(tuning.slow_step_factor, 1.0, frac)
	if walking or (gait_t > 0.0 and gait_t < 1.0 and walk_amt > 0.05):
		gait_t += dt / gait_period
		if gait_t >= 1.0:
			gait_t -= 1.0
			gait_side = 1 - gait_side
			if walking:
				footstep.emit(gait_side)
				stride += 0.5
	# speed eases off while the planted foot takes the load
	var load := tuning.load_phase * gait_period / tuning.step_period if not tuning.continuous_gait else 0.0
	var t_in := gait_t * gait_period
	if load > 0.001 and t_in < load and walking:
		gait_mult = 1.0 - tuning.load_speed_dip * sin(PI * t_in / load)
	else:
		gait_mult = 1.0

## Current loading-phase length as a fraction of the step (0 for the continuous gait).
func load_frac() -> float:
	if tuning.continuous_gait or gait_period <= 0.0:
		return 0.0
	return clampf(tuning.load_phase * gait_period / tuning.step_period / gait_period, 0.0, 0.6)

func teleport(p: Vector3, facing: float) -> void:
	global_position = p
	velocity = Vector3.ZERO
	_base_hv = Vector3.ZERO
	_qb_vel = Vector3.ZERO
	yaw = facing
	rotation.y = yaw
