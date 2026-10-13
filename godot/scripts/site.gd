class_name Site
extends RefCounted
## Layout of the Waterfront Training Hangar, in metres. X runs east (out to the
## bay), Z runs south (toward the rocky foreground of the concept), Y is up.
##
## The hangar sits on the west of a concrete apron with its doors facing east.
## The apron ends in a quay wall along the bay; a slipway runs down into the
## shallows in the south-east corner, fenced off by a buoy line. A wooded
## headland rises to the north, hills climb behind the hangar, and the Central
## Bridge, Downtown and the mountains fill the far side of the bay.

const WATER_Y := -2.2
const APRON_X0 := -175.0
const APRON_X1 := 40.0       # quay edge
const APRON_Z0 := -190.0
const APRON_Z1 := 72.0
const SLIP_Z0 := 16.0
const SLIP_Z1 := 50.0
const SLIP_X0 := 2.0          # top of the slipway, at apron level
const SLIP_X1 := 70.0         # foot of the slipway, under water
const SLIP_BOTTOM := -7.5

const HANGAR_X0 := -165.0
const HANGAR_X1 := -78.0      # door face
const HANGAR_Z0 := -118.0
const HANGAR_Z1 := -30.0
const HANGAR_H := 32.0
const DOOR_Z0 := -98.0
const DOOR_Z1 := -50.0
const DOOR_H := 26.0

const SPAWN := Vector3(-128.0, 0.0, -74.0)   # in the maintenance bay, facing the doors

# coastline: x of the shore for a given z (land is west of it)
const COAST := [
	Vector2(420, -60), Vector2(260, -40), Vector2(150, -14), Vector2(105, 8), Vector2(80, 18),
	Vector2(72, 26), Vector2(SLIP_Z1, 40), Vector2(SLIP_Z0, 40),
	Vector2(-190, 40), Vector2(-210, 64), Vector2(-240, 105), Vector2(-275, 140), Vector2(-315, 150),
	Vector2(-350, 130), Vector2(-385, 80), Vector2(-420, 10), Vector2(-470, -45), Vector2(-560, -70), Vector2(-900, -90),
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
	return -90.0

static func in_apron(x: float, z: float, margin := 0.0) -> bool:
	return x >= APRON_X0 - margin and x <= APRON_X1 + margin and z >= APRON_Z0 - margin and z <= APRON_Z1 + margin

static func slip_y(x: float) -> float:
	return lerpf(0.0, SLIP_BOTTOM, clampf((x - SLIP_X0) / (SLIP_X1 - SLIP_X0), 0.0, 1.0))

## Natural ground height (the apron and quay are built on top of this).
static func ground(x: float, z: float) -> float:
	var d := coast_x(z) - x                     # > 0 on land
	# distance outside the apron rectangle
	var dx := maxf(maxf(APRON_X0 - x, x - APRON_X1), 0.0)
	var dz := maxf(maxf(APRON_Z0 - z, z - APRON_Z1), 0.0)
	var outside := sqrt(dx * dx + dz * dz)
	if in_apron(x, z) and x <= APRON_X1:
		return -1.0
	var rough := n2(x, z, 0.035, 3) * 2.2 + n2(x, z, 0.15, 9) * 0.6
	if d < 0.0:
		# under the bay: shelf near the shore, then deeper
		var bed := WATER_Y - 1.2 + d * 0.14 + rough * 0.6
		if x > SLIP_X0 and z > SLIP_Z0 - 4 and z < SLIP_Z1 + 4:
			bed = minf(bed, slip_y(x) - 0.6)
		return maxf(bed, -28.0)
	var shore := minf(d * 0.55, 6.0)
	var hills := smoothstep(0.0, 260.0, outside) * (34.0 + 60.0 * (0.5 + 0.5 * n2(x, z, 0.004, 21)))
	var headland := 30.0 * exp(-pow(Vector2(x - 40.0, z + 300.0).length() / 110.0, 2.0))
	var h := WATER_Y + 1.0 + shore + hills + headland + rough * clampf(outside / 25.0, 0.2, 1.0)
	# blend down to apron level at its edges
	var to_apron := clampf(outside / 22.0, 0.0, 1.0)
	return lerpf(-0.2, h, to_apron) if outside < 22.0 else h

static func on_land(x: float, z: float) -> bool:
	return x < coast_x(z)
