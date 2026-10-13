class_name Hud
extends CanvasLayer
## League-broadcast HUD for the training hangar, after the Iron Ascendant
## mockups: location and clock, training objective with step pips, a heading
## compass, a north-up minimap rendered from above, the KESTREL status card,
## a 3D waypoint marker, contextual control prompts that switch glyphs between
## keyboard/mouse and gamepad, toasts, a controls overlay and a pause card.

const TEAL := Color(0.18, 0.9, 0.86)
const GOLD := Color(0.98, 0.72, 0.2)
const INK := Color(0.92, 0.94, 0.97)
const DIM := Color(0.6, 0.66, 0.74)
const GLASS := Color(0.03, 0.06, 0.1, 0.78)
const RED := Color(0.95, 0.3, 0.22)

class Chamfer extends Control:
	var fill := Color(0.03, 0.06, 0.1, 0.78)
	var line := Color(0.18, 0.9, 0.86, 0.5)
	var cut := 12.0
	var accent := Color(0, 0, 0, 0)   # optional left bar
	func _draw() -> void:
		var s := size
		var pts := PackedVector2Array([Vector2(cut, 0), Vector2(s.x, 0), Vector2(s.x, s.y - cut), Vector2(s.x - cut, s.y), Vector2(0, s.y), Vector2(0, cut)])
		draw_colored_polygon(pts, fill)
		var loop := pts.duplicate()
		loop.append(pts[0])
		draw_polyline(loop, line, 1.5, true)
		if accent.a > 0.0:
			draw_rect(Rect2(Vector2(0, cut), Vector2(3, s.y - cut)), accent)

class Diamond extends Control:
	var col := Color(0.18, 0.9, 0.86)
	var hollow := true
	func _draw() -> void:
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.5
		var pts := PackedVector2Array([c + Vector2(0, -r), c + Vector2(r, 0), c + Vector2(0, r), c + Vector2(-r, 0)])
		if hollow:
			var loop := pts.duplicate()
			loop.append(pts[0])
			draw_polyline(loop, col, 2.0, true)
			var r2 := r * 0.3
			draw_colored_polygon(PackedVector2Array([c + Vector2(0, -r2), c + Vector2(r2, 0), c + Vector2(0, r2), c + Vector2(-r2, 0)]), col)
		else:
			draw_colored_polygon(pts, col)

class Compass extends Control:
	var heading := 0.0      # radians, 0 = north, clockwise
	var marker := -1.0      # bearing of the waypoint, or -1
	func _draw() -> void:
		var w := size.x
		draw_rect(Rect2(Vector2.ZERO, size), Color(0.03, 0.06, 0.1, 0.55))
		var span := deg_to_rad(120.0)
		var f := Tex.font()
		for d in range(0, 360, 15):
			var a := deg_to_rad(d)
			var rel := wrapf(a - heading, -PI, PI)
			if absf(rel) > span * 0.5:
				continue
			var x := w * 0.5 + rel / span * w
			var major := d % 45 == 0
			draw_line(Vector2(x, size.y - (12 if major else 6)), Vector2(x, size.y), Color(1, 1, 1, 0.6 if major else 0.3), 1.5)
			if major:
				var lbl: String = {0: "N", 45: "NE", 90: "E", 135: "SE", 180: "S", 225: "SW", 270: "W", 315: "NW"}[d]
				var fs := 16 if lbl.length() == 1 else 12
				var tw := f.get_string_size(lbl, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
				draw_string(f, Vector2(x - tw * 0.5, 17), lbl, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, GOLD if d == 0 else INK)
		if marker >= 0.0:
			var rel := wrapf(marker - heading, -PI, PI)
			var x := clampf(w * 0.5 + rel / span * w, 6.0, w - 6.0)
			draw_colored_polygon(PackedVector2Array([Vector2(x, size.y + 2), Vector2(x - 6, size.y + 10), Vector2(x + 6, size.y + 10)]), TEAL)
		draw_line(Vector2(w * 0.5, 0), Vector2(w * 0.5, size.y), GOLD, 2.0)

class MapOverlay extends Control:
	var heading := 0.0
	var marker_dir := Vector2.ZERO
	var marker_on := false
	func _draw() -> void:
		var c := size * 0.5
		var r := size.x * 0.5
		# player arrow (north-up map, the arrow turns)
		var fwd := Vector2(sin(heading), -cos(heading))
		var right := Vector2(-fwd.y, fwd.x)
		draw_colored_polygon(PackedVector2Array([c + fwd * 11, c - fwd * 7 + right * 7, c - fwd * 3, c - fwd * 7 - right * 7]), Color.WHITE)
		draw_arc(c, r - 1.5, 0, TAU, 64, Color(1, 1, 1, 0.35), 3.0, true)
		var f := Tex.font()
		draw_string(f, Vector2(c.x - 5, 15), "N", HORIZONTAL_ALIGNMENT_LEFT, -1, 14, GOLD)
		if marker_on:
			var m := marker_dir
			if m.length() > r - 10:
				m = m.normalized() * (r - 10)
			var p := c + m
			draw_colored_polygon(PackedVector2Array([p + Vector2(0, -8), p + Vector2(8, 0), p + Vector2(0, 8), p + Vector2(-8, 0)]), TEAL)

var gamepad := false
var _loc: Label
var _clock: Label
var _obj_title: Label
var _obj_text: Label
var _pips: Array[Control] = []
var _compass: Compass
var _map: TextureRect
var _map_over: MapOverlay
var _map_cam: Camera3D
var _map_vp: SubViewport
var _card_status: Label
var _boost_segs: Array[ColorRect] = []
var _speed: Label
var _prompt_box: HBoxContainer
var _prompt_panel: Chamfer
var _toast: Label
var _toast_t := 0.0
var _wp: Control
var _wp_label: Label
var _help: Control
var _pause: Control
var _complete: Control
var _fade: ColorRect
var _interact: Chamfer
var _interact_label: Label

func _ready() -> void:
	layer = 10
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	_build_top_left(root)
	_build_top(root)
	_build_minimap(root)
	_build_card(root)
	_build_prompt(root)
	_build_waypoint(root)
	_build_overlays(root)

## Anchor a control to a preset corner/edge and place it by offsets from that anchor.
func _anchor(c: Control, preset: int, pos: Vector2, size: Vector2) -> void:
	c.set_anchors_preset(preset)
	c.offset_left = pos.x
	c.offset_top = pos.y
	c.offset_right = pos.x + size.x
	c.offset_bottom = pos.y + size.y

func _label(parent: Control, text: String, size: int, col: Color, bold := true) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", Tex.font(bold))
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.5))
	l.add_theme_constant_override("shadow_offset_x", 1)
	l.add_theme_constant_override("shadow_offset_y", 1)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l

func _panel(parent: Control, pos: Vector2, size: Vector2, line := Color(0.18, 0.9, 0.86, 0.5)) -> Chamfer:
	var p := Chamfer.new()
	p.position = pos
	p.size = size
	p.line = line
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(p)
	return p

func _build_top_left(root: Control) -> void:
	var lp := _panel(root, Vector2(36, 30), Vector2(430, 66))
	_loc = _label(lp, "WATERFRONT TRAINING HANGAR", 24, INK)
	_loc.position = Vector2(22, 6)
	_clock = _label(lp, "10:42 AM", 15, DIM, false)
	_clock.position = Vector2(22, 38)
	var op := _panel(root, Vector2(36, 106), Vector2(470, 92), Color(GOLD.r, GOLD.g, GOLD.b, 0.55))
	op.accent = GOLD
	var d := Diamond.new()
	d.col = GOLD
	d.position = Vector2(18, 28)
	d.size = Vector2(30, 30)
	op.add_child(d)
	_obj_title = _label(op, "SYSTEMS CHECK", 16, GOLD)
	_obj_title.position = Vector2(62, 10)
	_obj_text = _label(op, "", 19, INK, false)
	_obj_text.position = Vector2(62, 34)
	_obj_text.size = Vector2(395, 50)
	_obj_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	var pips := HBoxContainer.new()
	pips.position = Vector2(62, 204)
	pips.add_theme_constant_override("separation", 6)
	root.add_child(pips)
	for k in 7:
		var c := ColorRect.new()
		c.custom_minimum_size = Vector2(46, 5)
		c.color = Color(1, 1, 1, 0.18)
		pips.add_child(c)
		_pips.append(c)

func _build_top(root: Control) -> void:
	_compass = Compass.new()
	_compass.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_compass)
	_anchor(_compass, Control.PRESET_CENTER_TOP, Vector2(-260, 24), Vector2(520, 28))
	var hint := _label(root, "F1  CONTROLS", 13, Color(1, 1, 1, 0.45), false)
	_anchor(hint, Control.PRESET_TOP_RIGHT, Vector2(-150, 34), Vector2(130, 20))

func _build_minimap(root: Control) -> void:
	_map_vp = SubViewport.new()
	_map_vp.size = Vector2i(320, 320)
	_map_vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_map_vp.world_3d = get_viewport().world_3d
	_map_cam = Camera3D.new()
	_map_cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	_map_cam.size = 260.0
	_map_cam.far = 600.0
	_map_cam.rotation_degrees = Vector3(-90, 0, 0)
	_map_cam.cull_mask = 1
	_map_vp.add_child(_map_cam)
	add_child(_map_vp)
	_map = TextureRect.new()
	_map.texture = _map_vp.get_texture()
	_map.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_map.stretch_mode = TextureRect.STRETCH_SCALE
	var sh := ShaderMaterial.new()
	var s := Shader.new()
	s.code = "shader_type canvas_item;\nvoid fragment(){ vec4 c = texture(TEXTURE, UV); float d = distance(UV, vec2(0.5)); COLOR = vec4(mix(c.rgb, vec3(0.03,0.06,0.1), 0.35), (1.0 - smoothstep(0.48, 0.5, d)) * 0.95); }"
	sh.shader = s
	_map.material = sh
	_map.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_map)
	_anchor(_map, Control.PRESET_BOTTOM_LEFT, Vector2(40, -330), Vector2(250, 250))
	_map_over = MapOverlay.new()
	_map_over.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_map_over)
	_anchor(_map_over, Control.PRESET_BOTTOM_LEFT, Vector2(40, -330), Vector2(250, 250))
	var lbl := HBoxContainer.new()
	lbl.add_theme_constant_override("separation", 10)
	root.add_child(lbl)
	_anchor(lbl, Control.PRESET_BOTTOM_LEFT, Vector2(44, -64), Vector2(500, 26))
	var d := Diamond.new()
	d.custom_minimum_size = Vector2(20, 20)
	lbl.add_child(d)
	_wp_label = _label(lbl, "", 18, INK)

func _build_card(root: Control) -> void:
	var cp := _panel(root, Vector2.ZERO, Vector2(380, 130))
	_anchor(cp, Control.PRESET_BOTTOM_RIGHT, Vector2(-416, -170), Vector2(380, 130))
	var icon := TextureRect.new()
	icon.texture = _mech_icon()
	icon.position = Vector2(16, 12)
	icon.size = Vector2(78, 104)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.modulate = Color(0.85, 0.95, 1.0, 0.85)
	cp.add_child(icon)
	_label(cp, "KESTREL", 24, INK).position = Vector2(110, 10)
	var rule := ColorRect.new()
	rule.color = Color(1, 1, 1, 0.2)
	rule.position = Vector2(110, 46)
	rule.size = Vector2(250, 1)
	cp.add_child(rule)
	_label(cp, "BOOST", 15, INK).position = Vector2(110, 56)
	for k in 4:
		var c := ColorRect.new()
		c.position = Vector2(186 + k * 40, 61)
		c.size = Vector2(36, 12)
		c.color = TEAL
		cp.add_child(c)
		_boost_segs.append(c)
	_speed = _label(cp, "0 km/h", 15, DIM, false)
	_speed.position = Vector2(110, 80)
	_card_status = _label(cp, "SYSTEMS NORMAL", 15, TEAL)
	_card_status.position = Vector2(110, 100)

func _mech_icon() -> ImageTexture:
	var w := 96
	var h := 128
	var img := Image.create(w, h, false, Image.FORMAT_RGBA8)
	var polys := [
		[0.36, 1.0, 0.47, 1.0, 0.46, 0.62, 0.38, 0.62], [0.53, 1.0, 0.64, 1.0, 0.62, 0.62, 0.54, 0.62],
		[0.37, 0.64, 0.63, 0.64, 0.66, 0.52, 0.34, 0.52], [0.32, 0.54, 0.68, 0.54, 0.74, 0.3, 0.26, 0.3],
		[0.45, 0.31, 0.55, 0.31, 0.56, 0.22, 0.44, 0.22],
		[0.18, 0.38, 0.3, 0.3, 0.28, 0.55, 0.19, 0.64, 0.14, 0.5], [0.82, 0.38, 0.7, 0.3, 0.72, 0.55, 0.81, 0.64, 0.86, 0.5],
		[0.15, 0.62, 0.22, 0.62, 0.23, 0.82, 0.14, 0.82], [0.85, 0.62, 0.78, 0.62, 0.77, 0.82, 0.86, 0.82],
		[0.2, 0.34, 0.27, 0.32, 0.33, 0.0, 0.27, 0.04], [0.8, 0.34, 0.73, 0.32, 0.67, 0.0, 0.73, 0.04],
	]
	var fill := PackedByteArray()
	fill.resize(w * h)
	for p in polys:
		var pts := PackedVector2Array()
		for i in range(0, p.size(), 2):
			pts.append(Vector2(p[i] * w, p[i + 1] * h))
		for y in h:
			for x in w:
				if Geometry2D.is_point_in_polygon(Vector2(x + 0.5, y + 0.5), pts):
					fill[y * w + x] = 1
	for y in h:
		for x in w:
			if fill[y * w + x] == 0:
				continue
			var edge := x == 0 or y == 0 or x == w - 1 or y == h - 1 or fill[y * w + x - 1] == 0 or fill[y * w + x + 1] == 0 or fill[(y - 1) * w + x] == 0 or fill[(y + 1) * w + x] == 0
			img.set_pixel(x, y, Color(1, 1, 1, 1.0 if edge else (0.25 if y % 13 == 0 else 0.07)))
	return ImageTexture.create_from_image(img)

func _build_prompt(root: Control) -> void:
	_prompt_panel = _panel(root, Vector2.ZERO, Vector2(600, 58))
	_anchor(_prompt_panel, Control.PRESET_CENTER_BOTTOM, Vector2(-300, -132), Vector2(600, 58))
	_prompt_box = HBoxContainer.new()
	_prompt_box.position = Vector2(18, 10)
	_prompt_box.add_theme_constant_override("separation", 10)
	_prompt_panel.add_child(_prompt_box)
	_interact = _panel(root, Vector2.ZERO, Vector2(360, 70))
	_anchor(_interact, Control.PRESET_CENTER_BOTTOM, Vector2(60, -320), Vector2(360, 66))
	_interact_label = _label(_interact, "", 22, INK)
	_interact_label.position = Vector2(20, 18)
	_interact.visible = false
	_toast = _label(root, "", 30, GOLD)
	_anchor(_toast, Control.PRESET_CENTER_TOP, Vector2(-400, 140), Vector2(800, 40))
	_toast.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

func _build_waypoint(root: Control) -> void:
	_wp = Control.new()
	_wp.size = Vector2(40, 40)
	_wp.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_wp)
	var d := Diamond.new()
	d.size = Vector2(36, 36)
	d.position = Vector2(2, 2)
	_wp.add_child(d)
	var l := _label(_wp, "", 17, INK)
	l.name = "Dist"
	l.position = Vector2(-60, 40)
	l.size = Vector2(160, 22)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

func _build_overlays(root: Control) -> void:
	_help = _panel(root, Vector2.ZERO, Vector2(660, 460))
	_anchor(_help, Control.PRESET_CENTER, Vector2(-330, -230), Vector2(660, 460))
	var t := _label(_help, "CONTROLS", 26, INK)
	t.position = Vector2(28, 18)
	var rows := [["MOVE", "W A S D", "Left stick"], ["LOOK", "Mouse", "Right stick"], ["BOOST", "Shift (hold)", "RB (hold)"],
		["JUMP / HOVER", "Space (hold)", "A (hold)"], ["QUICK BOOST", "Q", "B"], ["INTERACT", "E", "Y"],
		["RECENTER CAMERA", "C", "Right stick click"], ["CONTROLS", "F1", "Back / View"], ["PAUSE", "Esc", "Start / Menu"]]
	for i in rows.size():
		var y := 70 + i * 38
		_label(_help, rows[i][0], 17, GOLD).position = Vector2(28, y)
		_label(_help, rows[i][1], 17, INK, false).position = Vector2(270, y)
		_label(_help, rows[i][2], 17, DIM, false).position = Vector2(460, y)
	_help.visible = false
	_pause = _panel(root, Vector2.ZERO, Vector2(440, 220))
	_anchor(_pause, Control.PRESET_CENTER, Vector2(-220, -110), Vector2(440, 220))
	_label(_pause, "PAUSED", 30, INK).position = Vector2(30, 24)
	_label(_pause, "Esc / Start  resume\nF1 / Back  controls\nQ (while paused)  quit to desktop", 17, DIM, false).position = Vector2(30, 80)
	_pause.visible = false
	_complete = _panel(root, Vector2.ZERO, Vector2(600, 240), Color(GOLD.r, GOLD.g, GOLD.b, 0.7))
	_anchor(_complete, Control.PRESET_CENTER, Vector2(-300, -150), Vector2(600, 240))
	_label(_complete, "TRAINING COMPLETE", 34, GOLD).position = Vector2(30, 26)
	var sub := _label(_complete, "KESTREL is cleared for the Qualifier.\nThe Ground Arena registration gate is open.", 19, INK, false)
	sub.position = Vector2(30, 90)
	_complete.visible = false
	_fade = ColorRect.new()
	_fade.color = Color(0, 0, 0, 1)
	_fade.set_anchors_preset(Control.PRESET_FULL_RECT)
	_fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_fade)

# ------------------------------------------------------------------ prompts with device-aware glyphs
func _key(text: String) -> Control:
	var p := Chamfer.new()
	p.cut = 5
	p.fill = Color(0.92, 0.94, 0.97, 0.95)
	p.line = Color(1, 1, 1, 0.9)
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", Tex.font())
	l.add_theme_font_size_override("font_size", 16)
	l.add_theme_color_override("font_color", Color(0.04, 0.07, 0.12))
	var w := maxf(34.0, Tex.font().get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 16).x + 18.0)
	p.custom_minimum_size = Vector2(w, 34)
	l.position = Vector2((w - Tex.font().get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 16).x) * 0.5, 6)
	p.add_child(l)
	return p

## entries: Array of [keyboard keys (Array of String), pad keys (Array of String), action label]
func set_prompts(entries: Array) -> void:
	for c in _prompt_box.get_children():
		c.queue_free()
	var width := 18.0
	for e in entries:
		var keys: Array = e[1] if gamepad else e[0]
		for k in keys:
			var kc := _key(k)
			_prompt_box.add_child(kc)
			width += kc.custom_minimum_size.x + 10
		var l := Label.new()
		l.text = e[2] + "   "
		l.add_theme_font_override("font", Tex.font())
		l.add_theme_font_size_override("font_size", 17)
		l.add_theme_color_override("font_color", INK)
		_prompt_box.add_child(l)
		width += Tex.font().get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, 17).x + 10
	_anchor(_prompt_panel, Control.PRESET_CENTER_BOTTOM, Vector2(-(width + 8) * 0.5, -132), Vector2(width + 8, 58))
	_prompt_panel.visible = entries.size() > 0

func set_objective(step: int, total: int, title: String, text: String) -> void:
	_obj_title.text = ("TRAINING  ·  STEP %d / %d   ·   " % [step, total] if step > 0 else "") + title
	_obj_text.text = text
	for k in _pips.size():
		_pips[k].visible = k < total
		_pips[k].color = GOLD if k < step - 1 else (TEAL if k == step - 1 else Color(1, 1, 1, 0.18))

func toast(text: String, dur := 2.5) -> void:
	_toast.text = text
	_toast_t = dur

func show_interact(text: String) -> void:
	_interact.visible = text != ""
	_interact_label.text = text
	_anchor(_interact, Control.PRESET_CENTER_BOTTOM, Vector2(60, -320), Vector2(Tex.font().get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 22).x + 40, 66))

func show_complete(on: bool) -> void:
	_complete.visible = on

func toggle_help() -> void:
	_help.visible = not _help.visible

func set_paused(on: bool) -> void:
	_pause.visible = on

func fade_in(t := 1.5) -> void:
	var tw := create_tween()
	tw.tween_property(_fade, "color:a", 0.0, t)

func update_frame(mech: Mech, cam: Camera3D, cam_yaw: float, hour: float, waypoint: Variant, waypoint_name: String) -> void:
	var hh := int(hour) % 24
	var mm := int((hour - floorf(hour)) * 60.0)
	_clock.text = "%d:%02d %s" % [12 if hh % 12 == 0 else hh % 12, mm, "AM" if hh < 12 else "PM"]
	# boost segments
	for k in 4:
		var seg := clampf(mech.energy * 4.0 - k, 0.0, 1.0)
		_boost_segs[k].color = Color(RED.r, RED.g, RED.b, 0.4 + seg * 0.6) if mech.overheated else (TEAL if seg >= 0.99 else Color(1, 1, 1, 0.15).lerp(TEAL, seg))
	_speed.text = "%d km/h" % int(mech.speed() * 3.6)
	if mech.overheated:
		_card_status.text = "BOOSTERS OVERHEATED"
		_card_status.add_theme_color_override("font_color", RED)
	elif mech.in_water:
		_card_status.text = "WADING  ·  TRACTION REDUCED"
		_card_status.add_theme_color_override("font_color", GOLD)
	else:
		_card_status.text = "SYSTEMS NORMAL"
		_card_status.add_theme_color_override("font_color", TEAL)
	# compass: camera heading, north = -Z
	var heading := wrapf(-cam_yaw, 0.0, TAU)
	_compass.heading = heading
	# minimap camera rides above the frame, north-up
	_map_cam.global_position = mech.global_position + Vector3(0, 300, 0)
	_map_over.heading = wrapf(-mech.yaw, 0.0, TAU)
	if waypoint != null:
		var wp: Vector3 = waypoint
		var to := wp - mech.global_position
		_compass.marker = wrapf(atan2(to.x, -to.z), 0.0, TAU)
		var d := Vector2(to.x, to.z) / _map_cam.size * 250.0
		_map_over.marker_dir = d
		_map_over.marker_on = true
		var dist := to.length()
		_wp_label.text = "%s  ·  %s" % [waypoint_name, ("%.1f km" % (dist / 1000.0)) if dist >= 1000.0 else ("%d m" % int(dist))]
		_wp_label.get_parent().visible = true
		var world := wp + Vector3(0, 9.0, 0)
		var on_screen := not cam.is_position_behind(world)
		var sp := cam.unproject_position(world)
		var vs := get_viewport().get_visible_rect().size
		if not on_screen:
			sp = Vector2(vs.x - sp.x, vs.y - 120)
		sp.x = clampf(sp.x, 50, vs.x - 50)
		sp.y = clampf(sp.y, 120, vs.y - 160)
		_wp.position = sp - _wp.size * 0.5
		(_wp.get_node("Dist") as Label).text = "%d m" % int(dist)
		_wp.visible = dist > 10.0
	else:
		_compass.marker = -1.0
		_map_over.marker_on = false
		_wp.visible = false
		_wp_label.get_parent().visible = false
	_compass.queue_redraw()
	_map_over.queue_redraw()
	if _toast_t > 0.0:
		_toast_t -= get_process_delta_time()
		_toast.modulate.a = clampf(_toast_t * 3.0, 0.0, 1.0)
	else:
		_toast.text = ""

func _input(e: InputEvent) -> void:
	var pad := e is InputEventJoypadButton or (e is InputEventJoypadMotion and absf((e as InputEventJoypadMotion).axis_value) > 0.4)
	var kb := e is InputEventKey or e is InputEventMouseButton or (e is InputEventMouseMotion and (e as InputEventMouseMotion).relative.length() > 2.0)
	if pad and not gamepad:
		gamepad = true
		device_changed.emit()
	elif kb and gamepad:
		gamepad = false
		device_changed.emit()

signal device_changed
