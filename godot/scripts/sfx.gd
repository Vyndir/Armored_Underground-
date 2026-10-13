class_name Sfx
extends Node
## Synthesised sound: every effect and ambience loop is generated at startup
## (no audio files), then played in 2D or positioned in 3D.
##   ambience: sea swell, coastal wind, hangar machinery hum, distant gulls
##   frame:    heavy footsteps, booster roar, quick-boost bursts, landings, splashes
##   ui:       blips, confirms, radio chirps, boot-up tones

const RATE := 22050
static var I: Sfx

var _streams := {}
var _boost: AudioStreamPlayer
var _wind: AudioStreamPlayer
var _rng := RandomNumberGenerator.new()

func _ready() -> void:
	I = self
	_rng.seed = 5
	_streams["sea"] = _loop(_gen_sea(6.0))
	_streams["wind"] = _loop(_gen_wind(5.0))
	_streams["hum"] = _loop(_gen_hum(2.0))
	_streams["boost"] = _loop(_gen_boost(1.5))
	_streams["step"] = _wav(_gen_step())
	_streams["clank"] = _wav(_gen_clank())
	_streams["land"] = _wav(_gen_land())
	_streams["qb"] = _wav(_gen_qb())
	_streams["splash"] = _wav(_gen_splash())
	_streams["gull"] = _wav(_gen_gull())
	_streams["blip"] = _wav(_tone([1320.0], 0.06, 0.25))
	_streams["confirm"] = _wav(_tone([880.0, 1320.0, 1760.0], 0.07, 0.3))
	_streams["radio"] = _wav(_gen_radio())
	_streams["boot"] = _wav(_tone([440.0, 660.0, 990.0, 1320.0], 0.09, 0.25))
	_streams["servo"] = _wav(_gen_servo())
	_streams["hiss"] = _wav(_gen_hiss())

func start_ambience(hangar_center: Vector3, parent: Node3D) -> void:
	var sea := AudioStreamPlayer.new()
	sea.stream = _streams["sea"]
	sea.volume_db = -10
	add_child(sea)
	sea.play()
	_wind = AudioStreamPlayer.new()
	_wind.stream = _streams["wind"]
	_wind.volume_db = -18
	add_child(_wind)
	_wind.play()
	var hum := AudioStreamPlayer3D.new()
	hum.stream = _streams["hum"]
	hum.unit_size = 30
	hum.max_distance = 140
	hum.volume_db = -4
	parent.add_child(hum)
	hum.global_position = hangar_center
	hum.play()
	_boost = AudioStreamPlayer.new()
	_boost.stream = _streams["boost"]
	_boost.volume_db = -80
	add_child(_boost)
	_boost.play()

## Booster loop follows how hard the frame is burning.
func set_boost(level: float) -> void:
	if _boost:
		_boost.volume_db = lerpf(-60.0, -6.0, clampf(level, 0.0, 1.0)) if level > 0.02 else -80.0
		_boost.pitch_scale = 0.85 + level * 0.35

func set_wind(level: float) -> void:
	if _wind:
		_wind.volume_db = lerpf(-22.0, -9.0, clampf(level, 0.0, 1.0))

func play(name: String, vol := 0.0, pitch := 1.0) -> void:
	if not _streams.has(name):
		return
	var p := AudioStreamPlayer.new()
	p.stream = _streams[name]
	p.volume_db = vol
	p.pitch_scale = pitch * _rng.randf_range(0.95, 1.05)
	add_child(p)
	p.play()
	p.finished.connect(p.queue_free)

func play_at(name: String, pos: Vector3, vol := 0.0, pitch := 1.0, size := 20.0) -> void:
	if not _streams.has(name):
		return
	var p := AudioStreamPlayer3D.new()
	p.stream = _streams[name]
	p.volume_db = vol
	p.unit_size = size
	p.max_distance = size * 12.0
	p.pitch_scale = pitch * _rng.randf_range(0.93, 1.07)
	get_tree().current_scene.add_child(p)
	p.global_position = pos
	p.play()
	p.finished.connect(p.queue_free)

# ------------------------------------------------------------------ synthesis helpers
func _wav(samples: PackedFloat32Array) -> AudioStreamWAV:
	var w := AudioStreamWAV.new()
	w.format = AudioStreamWAV.FORMAT_16_BITS
	w.mix_rate = RATE
	w.stereo = false
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in samples.size():
		data.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32000.0))
	w.data = data
	return w

func _loop(samples: PackedFloat32Array) -> AudioStreamWAV:
	# crossfade the tail into the head so the loop point is seamless
	var n := samples.size()
	var fade := int(RATE * 0.3)
	for i in fade:
		var t := float(i) / fade
		samples[i] = samples[i] * t + samples[n - fade + i] * (1.0 - t)
	samples.resize(n - fade)
	var w := _wav(samples)
	w.loop_mode = AudioStreamWAV.LOOP_FORWARD
	w.loop_begin = 0
	w.loop_end = samples.size()
	return w

func _noise() -> float:
	return _rng.randf_range(-1.0, 1.0)

## One-pole low-pass over white noise; cutoff in Hz.
func _lp(prev: float, x: float, cutoff: float) -> float:
	var a := 1.0 - exp(-TAU * cutoff / RATE)
	return prev + (x - prev) * a

func _gen_sea(sec: float) -> PackedFloat32Array:
	var n := int(sec * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	var mid := 0.0
	for i in n:
		var t := float(i) / RATE
		var swell := 0.55 + 0.45 * sin(t * TAU / 3.0) * sin(t * TAU / 6.0 + 1.0)
		lo = _lp(lo, _noise(), 300.0)
		mid = _lp(mid, _noise(), 1600.0)
		var wash := maxf(0.0, sin(t * TAU / 3.0 - 0.6)) * 0.6
		out[i] = lo * 1.6 * swell + (mid - lo) * 0.35 * wash
	return out

func _gen_wind(sec: float) -> PackedFloat32Array:
	var n := int(sec * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	var lo2 := 0.0
	for i in n:
		var t := float(i) / RATE
		var gust := 0.6 + 0.4 * sin(t * 0.9) * sin(t * 2.3 + 0.7)
		lo = _lp(lo, _noise(), 500.0 + 300.0 * gust)
		lo2 = _lp(lo2, lo, 900.0)
		out[i] = lo2 * 1.3 * gust
	return out

func _gen_hum(sec: float) -> PackedFloat32Array:
	var n := int(sec * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	for i in n:
		var t := float(i) / RATE
		lo = _lp(lo, _noise(), 180.0)
		out[i] = sin(TAU * 60.0 * t) * 0.18 + sin(TAU * 120.0 * t) * 0.12 + sin(TAU * 180.0 * t) * 0.05 + lo * 0.5
	return out

func _gen_boost(sec: float) -> PackedFloat32Array:
	var n := int(sec * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	var hi := 0.0
	for i in n:
		var t := float(i) / RATE
		lo = _lp(lo, _noise(), 700.0)
		hi = _lp(hi, _noise(), 4000.0)
		out[i] = lo * 1.4 + (hi - lo) * 0.3 + sin(TAU * 220.0 * t) * 0.06 + sin(TAU * 1180.0 * t) * 0.03
	return out

func _gen_step() -> PackedFloat32Array:
	var n := int(0.55 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	for i in n:
		var t := float(i) / RATE
		var env := exp(-t * 9.0)
		lo = _lp(lo, _noise(), 260.0)
		var thud := sin(TAU * (42.0 + 30.0 * exp(-t * 20.0)) * t) * env
		var clank := sin(TAU * 410.0 * t) * sin(TAU * 637.0 * t) * exp(-t * 22.0) * 0.35
		out[i] = thud * 0.95 + lo * env * 0.8 + clank
	return out

func _gen_clank() -> PackedFloat32Array:
	var n := int(0.8 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	for i in n:
		var t := float(i) / RATE
		out[i] = (sin(TAU * 523.0 * t) + sin(TAU * 811.0 * t) * 0.7 + sin(TAU * 1307.0 * t) * 0.4) * exp(-t * 6.0) * 0.35
	return out

func _gen_land() -> PackedFloat32Array:
	var n := int(1.0 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	for i in n:
		var t := float(i) / RATE
		lo = _lp(lo, _noise(), 220.0)
		out[i] = sin(TAU * (34.0 + 40.0 * exp(-t * 12.0)) * t) * exp(-t * 4.5) + lo * exp(-t * 3.0) * 1.2
	return out

func _gen_qb() -> PackedFloat32Array:
	var n := int(0.7 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	for i in n:
		var t := float(i) / RATE
		var env := minf(t * 40.0, 1.0) * exp(-t * 5.0)
		lo = _lp(lo, _noise(), 900.0 + 2500.0 * exp(-t * 6.0))
		out[i] = lo * 1.6 * env + sin(TAU * (180.0 - 90.0 * t) * t) * env * 0.4
	return out

func _gen_splash() -> PackedFloat32Array:
	var n := int(0.9 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	var hi := 0.0
	for i in n:
		var t := float(i) / RATE
		var env := minf(t * 30.0, 1.0) * exp(-t * 4.0)
		lo = _lp(lo, _noise(), 600.0)
		hi = _lp(hi, _noise(), 3500.0)
		out[i] = (lo * 0.8 + (hi - lo) * 0.7) * env * 1.4
	return out

func _gen_gull() -> PackedFloat32Array:
	var n := int(1.1 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var ph := 0.0
	for i in n:
		var t := float(i) / RATE
		# two cries: a rising squeal falling away
		var k := fmod(t, 0.42) / 0.42
		var f := 1500.0 + 900.0 * sin(k * PI) - 500.0 * k
		ph += TAU * f / RATE
		var env := sin(k * PI) * (1.0 if t < 0.84 else 0.0)
		out[i] = (sin(ph) * 0.5 + sin(ph * 2.0) * 0.25 + sin(ph * 3.01) * 0.12) * env * 0.5
	return out

func _tone(freqs: Array, each: float, vol: float) -> PackedFloat32Array:
	var n := int(each * freqs.size() * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	for i in n:
		var t := float(i) / RATE
		var k := mini(int(t / each), freqs.size() - 1)
		var lt := t - k * each
		out[i] = sin(TAU * float(freqs[k]) * t) * vol * minf(lt * 200.0, 1.0) * exp(-lt * 18.0)
	return out

func _gen_radio() -> PackedFloat32Array:
	var n := int(0.35 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var hi := 0.0
	for i in n:
		var t := float(i) / RATE
		hi = _lp(hi, _noise(), 3000.0)
		var chirp := sin(TAU * (2200.0 - t * 2400.0) * t) * (1.0 if t < 0.08 else 0.0) * 0.3
		out[i] = chirp + hi * 0.25 * exp(-t * 6.0)
	return out

func _gen_servo() -> PackedFloat32Array:
	var n := int(1.4 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var lo := 0.0
	for i in n:
		var t := float(i) / RATE
		lo = _lp(lo, _noise(), 400.0)
		var env := minf(t * 6.0, 1.0) * minf((1.4 - t) * 4.0, 1.0)
		out[i] = (sin(TAU * (140.0 + t * 60.0) * t) * 0.35 + sin(TAU * (280.0 + t * 120.0) * t) * 0.15 + lo * 0.4) * env
	return out

func _gen_hiss() -> PackedFloat32Array:
	var n := int(1.6 * RATE)
	var out := PackedFloat32Array()
	out.resize(n)
	var hi := 0.0
	for i in n:
		var t := float(i) / RATE
		hi = _lp(hi, _noise(), 5000.0)
		out[i] = hi * minf(t * 20.0, 1.0) * exp(-t * 1.6) * 0.8
	return out
