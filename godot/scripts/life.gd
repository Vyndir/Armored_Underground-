class_name Life
extends Node3D
## Everything that moves on its own: gulls wheeling over the quay, boats
## crossing the bay with wakes, the workboat bobbing at the quay, crew walking
## their rounds, a forklift on its loop, the bridge crane shuttling, the
## windsock, the lighthouse beam, roof steam, hangar dust and welding sparks.

class Gull:
	var node: Node3D
	var wl: Node3D
	var wr: Node3D
	var center: Vector3
	var radius: float
	var speed: float
	var phase: float
	var flap: float

var _gulls: Array[Gull] = []
var _boats := []
var _walkers := []
var _forklift: Node3D
var _fork_t := 0.0
var _crane: Node3D
var _crane_t := 0.0
var _windsock: Node3D
var _beam: Node3D
var _workboat: Node3D
var _weld_light: OmniLight3D
var _gull_cry := 3.0
var _rng := RandomNumberGenerator.new()

const FORK_LOOP := [Vector3(-60, 0, -136), Vector3(-24, 0, -136), Vector3(-24, 0, -104), Vector3(-60, 0, -104)]

func setup(info: Dictionary, props: Dictionary, boats: Array) -> void:
	_rng.seed = 9
	_crane = info.get("crane")
	_workboat = props.get("workboat")
	for f in props.get("flags", []):
		if f.name == "Windsock":
			_windsock = f
	_build_gulls()
	for b in boats:
		var n: Node3D = b
		_boats.append({"node": n, "c": n.position, "r": _rng.randf_range(60, 160), "w": _rng.randf_range(0.01, 0.025) * (1 if _rng.randf() < 0.5 else -1), "a": _rng.randf() * TAU})
		Fx.I.emitter(n, Vector3(0, 0.3, 6), "wake")
	_setup_walkers(info.get("workers", []))
	_build_forklift()
	_build_lighthouse_beam()
	# roof stacks steaming, dust in the hall's light, sparks in the welding bay
	for x: float in [-118.0, -114.0, -110.0]:
		Fx.I.emitter(self, Vector3(x, Site.HANGAR_H - 3.0 + 7.6, -60), "steam")
	Fx.I.emitter(self, Vector3(-120, 11, -74), "motes")
	var weld: Vector3 = props.get("weld", Vector3(-120, 1.3, -112))
	Fx.I.emitter(self, weld, "sparks")
	_weld_light = OmniLight3D.new()
	_weld_light.light_color = Color(0.7, 0.85, 1.0)
	_weld_light.omni_range = 9
	add_child(_weld_light)
	_weld_light.global_position = weld + Vector3(0, 0.6, 0)

func _build_gulls() -> void:
	var white := Mats.lit("GullWhite", Color(0.92, 0.92, 0.9), 0.7)
	var grey := Mats.lit("GullGrey", Color(0.55, 0.58, 0.6), 0.7)
	var body := BoxMesh.new()
	body.size = Vector3(0.28, 0.24, 0.95)
	var wing := BoxMesh.new()
	wing.size = Vector3(0.95, 0.03, 0.34)
	for k in 18:
		var gnode := Node3D.new()
		add_child(gnode)
		var b := MeshInstance3D.new()
		b.mesh = body
		b.material_override = white
		gnode.add_child(b)
		var g := Gull.new()
		g.node = gnode
		for side: float in [-1.0, 1.0]:
			var pivot := Node3D.new()
			pivot.position = Vector3(side * 0.12, 0.05, 0)
			gnode.add_child(pivot)
			var w := MeshInstance3D.new()
			w.mesh = wing
			w.material_override = grey
			w.position = Vector3(side * 0.48, 0, 0)
			pivot.add_child(w)
			if side < 0: g.wl = pivot
			else: g.wr = pivot
		g.center = Vector3(_rng.randf_range(-20, 160), _rng.randf_range(16, 48), _rng.randf_range(-170, 90))
		g.radius = _rng.randf_range(18, 60)
		g.speed = _rng.randf_range(0.12, 0.25) * (1 if _rng.randf() < 0.5 else -1)
		g.phase = _rng.randf() * TAU
		g.flap = _rng.randf() * TAU
		gnode.scale = Vector3.ONE * 1.6
		_gulls.append(g)

func _setup_walkers(workers: Array) -> void:
	var routes := [
		[Vector3(-118, 0, -66), Vector3(-96, 0, -66)],
		[Vector3(-73, 0, -100), Vector3(-73, 0, -60), Vector3(-58, 0, -60)],
		[Vector3(-30, 0, 4), Vector3(-12, 0, 4), Vector3(-12, 0, -20)],
		[Vector3(-55, 0, -150), Vector3(-20, 0, -150)],
		[Vector3(20, 0, -40), Vector3(30, 0, -90)],
	]
	for i in mini(workers.size(), 12):
		var w: Node3D = workers[i]
		if i in [0, 5, 7, 10, 11]:
			var r: Array = routes[[0, 5, 7, 10, 11].find(i)]
			_walkers.append({"node": w, "route": r, "i": 0, "wait": _rng.randf_range(0, 3), "phase": _rng.randf() * TAU})

func _build_forklift() -> void:
	_forklift = Node3D.new()
	_forklift.name = "Forklift"
	add_child(_forklift)
	var g := Geo.new()
	BuildHangar.forklift(g, Vector3.ZERO, 0.0)
	g.box(Mats.olive(), Vector3(2.2, 0.9, 0), Vector3(1.4, 1.2, 1.2))
	g.commit(_forklift, "Body", true, false)
	var beacon := OmniLight3D.new()
	beacon.light_color = Color(1.0, 0.55, 0.1)
	beacon.omni_range = 6
	beacon.position = Vector3(-0.2, 2.6, 0)
	beacon.name = "Beacon"
	_forklift.add_child(beacon)
	var lamp := MeshInstance3D.new()
	var sm := SphereMesh.new()
	sm.radius = 0.16
	sm.height = 0.3
	lamp.mesh = sm
	lamp.material_override = Mats.glow("BeaconAmber", Color(1.0, 0.55, 0.1), 6.0)
	lamp.position = Vector3(-0.2, 2.35, 0)
	_forklift.add_child(lamp)

func _build_lighthouse_beam() -> void:
	_beam = Node3D.new()
	add_child(_beam)
	_beam.position = Vector3(420, Site.WATER_Y + 16 + 22, -620)
	var cone := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.6
	cm.bottom_radius = 9.0
	cm.height = 120.0
	cone.mesh = cm
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	m.albedo_color = Color(1.0, 0.95, 0.8, 0.05)
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	cone.material_override = m
	cone.rotation_degrees = Vector3(0, 0, 90)
	cone.position = Vector3(60, 0, 0)
	cone.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_beam.add_child(cone)

func _process(dt: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0
	for g in _gulls:
		g.phase += g.speed * dt
		var p := g.center + Vector3(cos(g.phase) * g.radius, sin(g.phase * 2.3) * 3.0, sin(g.phase) * g.radius)
		var tangent := Vector3(-sin(g.phase), 0, cos(g.phase)) * signf(g.speed)
		g.node.global_position = p
		g.node.look_at(p + tangent, Vector3.UP)
		g.node.rotate_object_local(Vector3.FORWARD, -signf(g.speed) * 0.35)
		# flap in bursts, then glide
		var gliding := sin(t * 0.4 + g.flap) > 0.3
		var a := 0.15 if gliding else sin(t * 9.0 + g.flap) * 0.7
		g.wl.rotation.z = -a
		g.wr.rotation.z = a
	_gull_cry -= dt
	if _gull_cry <= 0.0 and Sfx.I and not _gulls.is_empty():
		_gull_cry = _rng.randf_range(3.0, 8.0)
		var g: Gull = _gulls[_rng.randi() % _gulls.size()]
		Sfx.I.play_at("gull", g.node.global_position, -4.0, _rng.randf_range(0.85, 1.2), 40.0)
	for b in _boats:
		b["a"] += b["w"] * dt
		var n: Node3D = b["node"]
		var c: Vector3 = b["c"]
		var r: float = b["r"]
		n.position = c + Vector3(cos(b["a"]) * r, sin(t * 0.8 + r) * 0.15, sin(b["a"]) * r * 0.6)
		var tangent := Vector3(-sin(b["a"]) * r, 0, cos(b["a"]) * r * 0.6) * signf(b["w"])
		n.rotation.y = atan2(-tangent.x, -tangent.z) + PI
		n.rotation.z = sin(t * 0.7 + r) * 0.03
	if _workboat:
		_workboat.position.y = Site.WATER_Y + sin(t * 0.9) * 0.18
		_workboat.rotation.z = sin(t * 0.7) * 0.025
		_workboat.rotation.x = sin(t * 0.55 + 1.0) * 0.015
	for w in _walkers:
		var n: Node3D = w["node"]
		var route: Array = w["route"]
		if w["wait"] > 0.0:
			w["wait"] -= dt
			n.position.y = 0.0
			continue
		var target: Vector3 = route[w["i"]]
		var to := target - n.position
		to.y = 0
		if to.length() < 0.3:
			w["i"] = (w["i"] + 1) % route.size()
			w["wait"] = _rng.randf_range(1.5, 5.0)
			continue
		n.position += to.normalized() * 1.4 * dt
		n.rotation.y = atan2(-to.x, -to.z)
		w["phase"] += dt * 9.0
		n.position.y = absf(sin(w["phase"])) * 0.05
	if _forklift:
		_fork_t += dt * 4.5
		var total := 0.0
		var segs := []
		for i in FORK_LOOP.size():
			var a: Vector3 = FORK_LOOP[i]
			var b: Vector3 = FORK_LOOP[(i + 1) % FORK_LOOP.size()]
			segs.append([a, b, a.distance_to(b)])
			total += a.distance_to(b)
		var s := fmod(_fork_t, total)
		for seg in segs:
			if s <= seg[2]:
				var a: Vector3 = seg[0]
				var b: Vector3 = seg[1]
				_forklift.position = a.lerp(b, s / seg[2])
				var d := b - a
				_forklift.rotation.y = lerp_angle(_forklift.rotation.y, atan2(-d.x, -d.z) - PI * 0.5, 1.0 - exp(-dt * 4.0))
				break
			s -= seg[2]
		(_forklift.get_node("Beacon") as OmniLight3D).light_energy = 1.5 if fmod(t, 0.8) < 0.4 else 0.2
	if _crane:
		_crane_t += dt
		var cycle := fmod(_crane_t, 40.0)
		var x := lerpf(-140.0, -100.0, smoothstep(0.0, 14.0, cycle) - smoothstep(20.0, 34.0, cycle))
		_crane.position.x = x
		var trolley := _crane.get_node_or_null("Trolley") as Node3D
		if trolley:
			trolley.position.z = sin(_crane_t * 0.15) * 15.0
	if _windsock:
		_windsock.rotation_degrees = Vector3(sin(t * 1.3) * 6.0, sin(t * 0.4) * 10.0, 80.0 + sin(t * 2.1) * 4.0)
	if _beam:
		_beam.rotation.y = t * 0.6
	if _weld_light:
		_weld_light.light_energy = 0.0 if _rng.randf() < 0.3 else _rng.randf_range(1.5, 4.0)
