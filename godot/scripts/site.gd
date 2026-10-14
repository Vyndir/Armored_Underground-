class_name Site
extends RefCounted
## Layout of the Waterfront Training Hangar, in metres. X runs east (out to the
## bay), Z runs south (toward the rocky foreground of the concept), Y is up.
##
## The hangar sits on the west of a concrete yard with its door facing east. The
## yard ends in a quay that runs north along the shore as a long test strip to the
## edge of the playable slice, beside the waterfront road that leads on to the
## Central Bridge and Downtown. A shallow training shelf lies off the yard, closed
## by booms, with ramps down onto it at both ends.

const WATER_Y := -2.2

# ---- the mech, measured on the in-game model (see docs/scale_notes.md)
const MECH_HEIGHT := 6.8              # ground to helmet top (crown plate), neutral standing pose
const MECH_MODEL_HELMET := 7.59       # helmet top in model units (crown plate; excludes antenna, whip and crest fin)
const MECH_MODEL_SOLE := -0.065       # underside of the soles in model units
const MECH_SCALE := MECH_HEIGHT / (MECH_MODEL_HELMET - MECH_MODEL_SOLE)

# ---- the yard (training apron) in front of the hangar
const APRON_X0 := -175.0
const APRON_X1 := 40.0       # quay edge
const APRON_Z0 := -190.0
const APRON_Z1 := 72.0
# ---- the north quay yard: the long shoreline strip, the road and the sheds, up to the slice limit
const NYARD_X0 := -70.0
const SLICE_Z := -470.0       # northern limit of the playable slice (checkpoint line)
const STRIP_X0 := 15.0        # shoreline test strip: x 15..40, the full length of the quay
const ROAD_X := -40.0         # waterfront road centre line
const ROAD_W := 12.0
const GATE_Z := -192.0        # service gate where the road enters the yard

# ---- shallow training shelf beside the apron (water reaches the mech's shins)
const SHELF_X0 := 40.0
const SHELF_X1 := 120.0       # boom line
const SHELF_Z0 := -60.0       # north boom
const SHELF_Z1 := 70.0        # south boom
const SHELF_DEPTH := 1.4      # set so the surface sits below the knee joint of the scaled mech
const SHELF_FLOOR := WATER_Y - SHELF_DEPTH
const SHELF_MARGIN := 15.0    # the shelf floor carries on past the booms
# slipway (south return ramp) and the north ramp, both down onto the shelf floor
const SLIP_Z0 := 16.0
const SLIP_Z1 := 50.0
const SLIP_X0 := 2.0
const SLIP_X1 := 50.0
const SLIP_BOTTOM := SHELF_FLOOR
const RAMP_Z0 := -56.0
const RAMP_Z1 := -44.0
const RAMP_X0 := 14.0
const RAMP_X1 := 48.0

# ---- hangar blockout: ~60 x 70 x 21 m; door size switchable for comparison
const HANGAR_X0 := -138.0
const HANGAR_X1 := -78.0      # door face
const HANGAR_Z0 := -109.0
const HANGAR_Z1 := -39.0
const HANGAR_H := 21.0
static var DOOR_W := 24.0
static var DOOR_H := 17.0     # selected clear opening height (24 x 17 m)
static var DOOR_Z0 := -86.0
static var DOOR_Z1 := -62.0
## Planning assumption only (not verified): future wing/transform span the opening must clear.
const WING_CLEARANCE_ASSUMED := 14.0
const BAYS := [Vector3(-114.0, 0.0, -94.0), Vector3(-114.0, 0.0, -74.0), Vector3(-114.0, 0.0, -54.0)]
const SPAWN := Vector3(-114.0, 0.0, -74.0)   # centre service bay, facing the door

## Closed playable boundary (x, z), clockwise from the south-west: hillside fence,
## north checkpoint line, quay railing, shelf booms, south fence over the rocks.
const BOUNDARY := [Vector2(-215, 95), Vector2(-215, SLICE_Z), Vector2(40, SLICE_Z), Vector2(40, SHELF_Z0),
	Vector2(SHELF_X1, SHELF_Z0), Vector2(SHELF_X1, SHELF_Z1), Vector2(30, 74), Vector2(-215, 95)]

## Read `--door=WxH` (e.g. --door=24x17) for door comparisons.
static func configure(args: PackedStringArray) -> void:
	for a in args:
		if a.begins_with("--door="):
			var wh := a.substr(7).split("x")
			DOOR_W = float(wh[0])
			DOOR_H = float(wh[1])
	DOOR_Z0 = SPAWN.z - DOOR_W * 0.5
	DOOR_Z1 = SPAWN.z + DOOR_W * 0.5

# coastline: x of the shore for a given z (land is west of it); a straight quay
# from the yard to the slice limit, then the waterfront curves on toward the bridge
const COAST := [
	Vector2(420, -60), Vector2(260, -40), Vector2(150, -14), Vector2(105, 8), Vector2(80, 18),
	Vector2(72, 30), Vector2(SLIP_Z1, 40), Vector2(SLIP_Z0, 40),
	Vector2(-60, 40), Vector2(SLICE_Z, 40), Vector2(-560, 32), Vector2(-700, 20), Vector2(-1000, -10),
	Vector2(-1400, -40), Vector2(-1800, -60), Vector2(-2600, -100),
]

static var _noise: FastNoiseLite

static func n2(x: float, z: float, f: float, seed := 0) -> float:
	if _noise == null:
		_noise = FastNoiseLite.new()
		_noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
		_noise.fractal_octaves = 5
	_noise.seed = seed
	_noise.frequency = f
	return _noise.get_noise_2d(x, z)

static func coast_x(z: float) -> float:
	# COAST is ordered by descending z
	for i in COAST.size() - 1:
		var a: Vector2 = COAST[i]
		var b: Vector2 = COAST[i + 1]
		if z <= a.x and z >= b.x:
			var t := (z - a.x) / (b.x - a.x) if absf(b.x - a.x) > 0.001 else 0.0
			return lerpf(a.y, b.y, t)
	return -100.0 if z < -2600.0 else -60.0

static func in_apron(x: float, z: float, margin := 0.0) -> bool:
	return x >= APRON_X0 - margin and x <= APRON_X1 + margin and z >= APRON_Z0 - margin and z <= APRON_Z1 + margin

static func in_north_yard(x: float, z: float, margin := 0.0) -> bool:
	return x >= NYARD_X0 - margin and x <= APRON_X1 + margin and z >= SLICE_Z - margin and z <= APRON_Z0 + margin

## Paved ground: the yard and the north quay yard.
static func in_paved(x: float, z: float, margin := 0.0) -> bool:
	return in_apron(x, z, margin) or in_north_yard(x, z, margin)

static func in_shelf(x: float, z: float, margin := 0.0) -> bool:
	return x >= SHELF_X0 - margin and x <= SHELF_X1 + margin and z >= SHELF_Z0 - margin and z <= SHELF_Z1 + margin

static func slip_y(x: float) -> float:
	return lerpf(0.0, SLIP_BOTTOM, clampf((x - SLIP_X0) / (SLIP_X1 - SLIP_X0), 0.0, 1.0))

static func ramp_y(x: float) -> float:
	return lerpf(0.0, SHELF_FLOOR, clampf((x - RAMP_X0) / (RAMP_X1 - RAMP_X0), 0.0, 1.0))

## Width of the low coastal plain between the shore and the hills: wide behind the
## yard and along the waterfront road, narrowing to nothing at the rocky south point.
static func lowland_w(z: float) -> float:
	if z > APRON_Z1:
		return lerpf(215.0, 0.0, smoothstep(APRON_Z1, APRON_Z1 + 50.0, z))
	return lerpf(215.0, 170.0, smoothstep(-200.0, -320.0, z))

static func _dist_outside_paved(x: float, z: float) -> float:
	var d := INF
	for r: Rect2 in [Rect2(APRON_X0, APRON_Z0, APRON_X1 - APRON_X0, APRON_Z1 - APRON_Z0), Rect2(NYARD_X0, SLICE_Z, APRON_X1 - NYARD_X0, APRON_Z0 - SLICE_Z)]:
		var dx := maxf(maxf(r.position.x - x, x - r.end.x), 0.0)
		var dz := maxf(maxf(r.position.y - z, z - r.end.y), 0.0)
		d = minf(d, sqrt(dx * dx + dz * dz))
	return d

## Sea bed: the flat training shelf, ramps onto it, then a drop to the deep bay.
static func bed(x: float, z: float, d: float, rough: float) -> float:
	if x > SLIP_X0 and x < SLIP_X1 + 2.0 and z > SLIP_Z0 - 2 and z < SLIP_Z1 + 2:
		return slip_y(x) - 0.4
	if x > RAMP_X0 and x < RAMP_X1 + 2.0 and z > RAMP_Z0 - 2 and z < RAMP_Z1 + 2:
		return ramp_y(x) - 0.4
	var ex := maxf(maxf(SHELF_X0 - SHELF_MARGIN - x, x - SHELF_X1 - SHELF_MARGIN), 0.0)
	var ez := maxf(maxf(SHELF_Z0 - SHELF_MARGIN - z, z - SHELF_Z1 - SHELF_MARGIN), 0.0)
	var e := sqrt(ex * ex + ez * ez)   # distance beyond the shelf floor
	var deep := maxf(WATER_Y - 6.5 + d * 0.08 + rough * 0.6, -28.0)
	if z > SHELF_Z1 + SHELF_MARGIN:
		# natural shore round the rocky south point: shallow by the rocks
		deep = maxf(WATER_Y - 1.2 + d * 0.14 + rough * 0.6, -28.0)
	if e <= 0.0:
		return SHELF_FLOOR
	return lerpf(SHELF_FLOOR, deep, smoothstep(0.0, 10.0, e))

## Natural ground height (paved areas, quay and ramps are built on top of this).
static func ground(x: float, z: float) -> float:
	if in_paved(x, z) and x <= APRON_X1:
		return -1.0
	var d := coast_x(z) - x                     # > 0 on land
	var rough := n2(x, z, 0.035, 3) * 2.2 + n2(x, z, 0.15, 9) * 0.6
	if d < 0.0:
		return bed(x, z, d, rough)
	var outside := _dist_outside_paved(x, z)
	# low coastal plain, then a bluff into the hills whose width wanders along the
	# coast, broken by ledges and outcrops
	var plain := WATER_Y + 1.0 + minf(d * 0.4, 3.0) + rough * 0.35
	var dd := d - lowland_w(z)
	var hills_amt := 34.0 + 60.0 * (0.5 + 0.5 * n2(x, z, 0.004, 21))
	var bluff_w := 45.0 + 75.0 * (0.5 + 0.5 * n2(x, z, 0.006, 5))
	var p := clampf(dd / (bluff_w * 2.2), 0.0, 1.0)
	var rise := smoothstep(0.0, 1.0, p) + sin(p * 18.0 + n2(x, z, 0.02, 7) * 6.0) * 0.035 * p * (1.0 - p)
	var crags := absf(n2(x, z, 0.05, 11)) * 6.0 * sin(p * PI)
	var h := plain + hills_amt * clampf(rise, 0.0, 1.0) + crags + rough * 0.65 * clampf(dd / 25.0, 0.0, 1.0)
	# blend down to the paving at its edges
	return lerpf(-0.2, h, clampf(outside / 22.0, 0.0, 1.0)) if outside < 22.0 else h

## Ground gradient magnitude (rise over run) from central differences.
static func slope(x: float, z: float, e := 3.0) -> float:
	var gx := (ground(x + e, z) - ground(x - e, z)) / (2.0 * e)
	var gz := (ground(x, z + e) - ground(x, z - e)) / (2.0 * e)
	return sqrt(gx * gx + gz * gz)

static func on_land(x: float, z: float) -> bool:
	return x < coast_x(z)

# ---- the far bank of the bay (Downtown side), simplified
const EAST_COAST := [Vector2(1400, 1300), Vector2(600, 1260), Vector2(0, 1250), Vector2(-600, 1380),
	Vector2(-1200, 1520), Vector2(-1700, 1600), Vector2(-1850, 1300), Vector2(-2200, 1260), Vector2(-3400, 1220)]

static func east_coast_x(z: float) -> float:
	for i in EAST_COAST.size() - 1:
		var a: Vector2 = EAST_COAST[i]
		var b: Vector2 = EAST_COAST[i + 1]
		if z <= a.x and z >= b.x:
			return lerpf(a.y, b.y, (z - a.x) / (b.x - a.x))
	return 1250.0

## Ground of the east bank: a low city plain rising into hills toward the mountains.
static func east_ground(x: float, z: float) -> float:
	var d := x - east_coast_x(z)
	if d < 0.0:
		return WATER_Y - 8.0 + maxf(d * 0.05, -20.0)
	var plain := WATER_Y + 1.6 + minf(d * 0.2, 2.2)
	var hills := smoothstep(900.0, 1900.0, d) * (60.0 + 80.0 * (0.5 + 0.5 * n2(x, z, 0.002, 41)))
	return plain + hills + n2(x, z, 0.01, 42) * 1.2 * clampf(d / 60.0, 0.0, 1.0)
