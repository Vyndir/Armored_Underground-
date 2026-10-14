class_name TuningPanel
extends CanvasLayer
## Live movement tuning for review (F4). Sliders edit the frame's MechTuning in
## place; presets switch between the v02 baseline and the previous controller;
## Save writes user://mech_tuning.tres, which is loaded on the next start.

signal opened(on: bool)

const SAVE_PATH := "user://mech_tuning.tres"
const FIELDS := [
	["walk_speed", "Peak walk (m/s)", 4.0, 20.0, 0.1],
	["turn_response", "Ground-turn response", 1.0, 14.0, 0.1],
	["step_period", "Step period (s)", 0.15, 0.7, 0.01],
	["load_phase", "Foot-loading phase (s)", 0.0, 0.25, 0.005],
	["load_speed_dip", "Speed eased while loading", 0.0, 0.5, 0.01],
	["pelvis_dip", "Pelvis dip", 0.0, 0.6, 0.01],
	["pelvis_sway", "Pelvis sway", 0.0, 0.3, 0.01],
	["knee_load", "Knee flex on load", 0.0, 0.8, 0.01],
	["stride_scale", "Stride swing", 0.3, 1.5, 0.01],
	["slow_step_factor", "Slow-walk step lengthening", 1.0, 2.5, 0.05],
	["accel", "Acceleration (m/s²)", 10.0, 80.0, 1.0],
]

var mech: Mech
var _root: PanelContainer
var _rows := {}
var _title: Label

func _ready() -> void:
	layer = 20
	_root = PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.03, 0.06, 0.1, 0.9)
	sb.border_color = Color(0.18, 0.9, 0.86, 0.6)
	sb.set_border_width_all(1)
	sb.set_content_margin_all(14)
	_root.add_theme_stylebox_override("panel", sb)
	_root.position = Vector2(40, 120)
	add_child(_root)
	var v := VBoxContainer.new()
	v.custom_minimum_size = Vector2(460, 0)
	_root.add_child(v)
	_title = Label.new()
	_title.text = "MOVEMENT TUNING  (F4 to close)"
	v.add_child(_title)
	for f in FIELDS:
		var h := HBoxContainer.new()
		var l := Label.new()
		l.text = f[1]
		l.custom_minimum_size = Vector2(220, 0)
		h.add_child(l)
		var sl := HSlider.new()
		sl.min_value = f[2]
		sl.max_value = f[3]
		sl.step = f[4]
		sl.custom_minimum_size = Vector2(160, 0)
		sl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		h.add_child(sl)
		var val := Label.new()
		val.custom_minimum_size = Vector2(56, 0)
		h.add_child(val)
		var key: String = f[0]
		sl.value_changed.connect(func(x: float) -> void:
			if mech:
				mech.tuning.set(key, x)
			val.text = str(snappedf(x, f[4])))
		v.add_child(h)
		_rows[key] = sl
	var cg := CheckBox.new()
	cg.text = "Continuous gait (previous controller)"
	cg.toggled.connect(func(on: bool) -> void:
		if mech:
			mech.tuning.continuous_gait = on)
	v.add_child(cg)
	_rows["continuous_gait"] = cg
	var buttons := HBoxContainer.new()
	for spec in [["v02 baseline", "v02"], ["Previous controller", "prev"], ["Save", "save"]]:
		var b := Button.new()
		b.text = spec[0]
		var id: String = spec[1]
		b.pressed.connect(func() -> void: _button(id))
		buttons.add_child(b)
	v.add_child(buttons)
	visible = false

func attach(m: Mech) -> void:
	mech = m
	if ResourceLoader.exists(SAVE_PATH):
		var saved := load(SAVE_PATH) as MechTuning
		if saved:
			mech.tuning = saved.duplicate()
	_refresh()

func _refresh() -> void:
	if mech == null:
		return
	for f in FIELDS:
		(_rows[f[0]] as HSlider).set_value_no_signal(mech.tuning.get(f[0]))
		(_rows[f[0]] as HSlider).value_changed.emit(mech.tuning.get(f[0]))
	(_rows["continuous_gait"] as CheckBox).set_pressed_no_signal(mech.tuning.continuous_gait)

func _button(id: String) -> void:
	if mech == null:
		return
	match id:
		"v02":
			mech.tuning = MechTuning.v02()
		"prev":
			mech.tuning = MechTuning.previous()
		"save":
			ResourceSaver.save(mech.tuning, SAVE_PATH)
			_title.text = "MOVEMENT TUNING  ·  saved to %s" % SAVE_PATH
	_refresh()

func toggle() -> void:
	visible = not visible
	opened.emit(visible)
