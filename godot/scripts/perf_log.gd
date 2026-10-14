class_name PerfLog
extends Node
## Frame timing and memory logger (`--perf <dir>`): one CSV row per second plus a
## summary on exit. Values come from Godot's Performance monitors; the GPU and
## resolution are recorded in the header so runs can be compared.

var out_dir := ""
var label := ""
var _frames := PackedFloat32Array()
var _all := PackedFloat32Array()
var _acc := 0.0
var _f: FileAccess
var _t := 0.0

func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(out_dir)
	_f = FileAccess.open(out_dir.path_join("perf.csv"), FileAccess.WRITE)
	var vp := get_viewport().get_visible_rect().size
	_f.store_line("# %s | GPU %s | %dx%d | %s" % [label, RenderingServer.get_video_adapter_name(), int(vp.x), int(vp.y), Time.get_datetime_string_from_system()])
	_f.store_line("t,fps,frame_ms_avg,frame_ms_max,draw_calls,primitives,vram_mb,static_mem_mb,objects,x,z")

func _process(dt: float) -> void:
	_t += dt
	_frames.append(dt * 1000.0)
	_all.append(dt * 1000.0)
	_acc += dt
	if _acc >= 1.0:
		var avg := 0.0
		var mx := 0.0
		for v in _frames:
			avg += v
			mx = maxf(mx, v)
		avg /= _frames.size()
		var p := get_parent().get("mech") as Node3D
		var pos := p.global_position if p else Vector3.ZERO
		_f.store_line("%.1f,%.1f,%.2f,%.2f,%d,%d,%.0f,%.0f,%d,%.0f,%.0f" % [_t, _frames.size() / _acc, avg, mx,
			Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME), Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME),
			Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / 1048576.0, Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0,
			Performance.get_monitor(Performance.OBJECT_COUNT), pos.x, pos.z])
		_f.flush()
		_frames.clear()
		_acc = 0.0

## Summary over everything logged after `skip` seconds (loading excluded).
func summary(skip := 5.0) -> String:
	var v := PackedFloat32Array()
	var t := 0.0
	for ms in _all:
		t += ms / 1000.0
		if t > skip:
			v.append(ms)
	if v.is_empty():
		return "no samples"
	var sorted := v.duplicate()
	sorted.sort()
	var avg := 0.0
	for ms in v:
		avg += ms
	avg /= v.size()
	var p99 := sorted[int(sorted.size() * 0.99)]
	var line := "frames=%d avg=%.2fms (%.1f fps) p99=%.2fms (1%% low %.1f fps) max=%.1fms vram=%.0fMB static=%.0fMB" % [v.size(), avg, 1000.0 / avg, p99, 1000.0 / p99, sorted[sorted.size() - 1],
		Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / 1048576.0, Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0]
	_f.store_line("# summary " + line)
	_f.flush()
	return line
