class_name MechTuning
extends Resource
## Walking feel of the frame, exposed for review (F4 in game opens the live panel).
##
## The v02 preset carries the provisional baseline values given for comparison:
## peak walk 13.5 m/s, ground-turn response 6.5, step period 0.36 s and a 0.09 s
## foot-loading phase. How they are used here:
##   walk_speed     top speed of the walk, reached between foot loadings
##   turn_response  exponential rate (1/s) at which the frame's heading follows
##                  the stick: about 63% of a turn is done in 1/turn_response s
##   step_period    time from one foot plant to the next at full walking speed;
##                  slower walking lengthens it (see slow_step_factor)
##   load_phase     after each plant the frame settles its weight onto that foot
##                  for this long: pelvis dips, knee flexes, speed eases off
## These may not match the definitions in the earlier prototype; compare the feel.

@export_range(4.0, 20.0, 0.1) var walk_speed := 13.5
@export_range(1.0, 14.0, 0.1) var turn_response := 6.5
@export_range(0.15, 0.7, 0.01) var step_period := 0.36
@export_range(0.0, 0.25, 0.005) var load_phase := 0.09
@export_range(0.0, 0.5, 0.01) var load_speed_dip := 0.22      # fraction of speed shed mid-loading
@export_range(0.0, 0.6, 0.01) var pelvis_dip := 0.24          # model units at full walk
@export_range(0.0, 0.3, 0.01) var pelvis_sway := 0.1          # sideways shift onto the stance leg
@export_range(0.0, 0.8, 0.01) var knee_load := 0.28           # stance-knee flex while loading (rad)
@export_range(0.3, 1.5, 0.01) var stride_scale := 1.0         # leg swing relative to step length
@export_range(1.0, 2.5, 0.05) var slow_step_factor := 1.7     # step period multiplier at a crawl
@export_range(10.0, 80.0, 1.0) var accel := 46.0
@export var continuous_gait := false                          # previous controller: smooth sine legs, no loading

static func v02() -> MechTuning:
	return MechTuning.new()

## The controller as it was before this change, for side-by-side comparison:
## 13 m/s, turn rate 7, legs cycling continuously (about 0.24 s per step at full
## walk) with no loading phase.
static func previous() -> MechTuning:
	var t := MechTuning.new()
	t.walk_speed = 13.0
	t.turn_response = 7.0
	t.step_period = 0.24
	t.load_phase = 0.0
	t.load_speed_dip = 0.0
	t.pelvis_dip = 0.18
	t.pelvis_sway = 0.0
	t.knee_load = 0.0
	t.slow_step_factor = 1.0
	t.continuous_gait = true
	return t
