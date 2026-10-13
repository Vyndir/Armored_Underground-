class_name Tex
extends RefCounted
## Procedural textures: tiling noise and normal maps, corrugated panels, and
## canvas-drawn artwork (league banners, stencils, apron paint) rendered once
## through an offscreen viewport and baked to mipmapped textures.

static var _cache := {}

static func noise(freq: float, seed: int, size := 512, normal := false, bump := 6.0, octaves := 5) -> NoiseTexture2D:
	var key := "n%s_%d_%d_%s_%s" % [freq, seed, size, normal, bump]
	if _cache.has(key):
		return _cache[key]
	var fn := FastNoiseLite.new()
	fn.seed = seed
	fn.frequency = freq
	fn.fractal_octaves = octaves
	fn.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	var t := NoiseTexture2D.new()
	t.width = size
	t.height = size
	t.seamless = true
	t.noise = fn
	t.as_normal_map = normal
	t.bump_strength = bump
	t.generate_mipmaps = true
	_cache[key] = t
	return t

## Normal map of vertical corrugation ribs (sheet metal walls and roller doors).
static func corrugated(ribs := 16, size := 256) -> ImageTexture:
	var key := "corr%d" % ribs
	if _cache.has(key):
		return _cache[key]
	var img := Image.create(size, size, false, Image.FORMAT_RGB8)
	for y in size:
		for x in size:
			var h := 0.5 + 0.5 * sin(float(x) / size * TAU * ribs)
			if y % 64 < 2:
				h *= 0.3
			img.set_pixel(x, y, Color(h, h, h))
	img.bump_map_to_normal_map(3.0)
	img.generate_mipmaps()
	var t := ImageTexture.create_from_image(img)
	_cache[key] = t
	return t

## Diamond-plate tread for walkways and the slipway apron.
static func tread(size := 256) -> ImageTexture:
	if _cache.has("tread"):
		return _cache["tread"]
	var img := Image.create(size, size, false, Image.FORMAT_RGB8)
	for y in size:
		for x in size:
			var cx := fposmod(x, 32.0) - 16.0
			var cy := fposmod(y + (16 if int(x / 32) % 2 == 0 else 0), 32.0) - 16.0
			var d := absf(cx * 0.7 + cy * 0.7)
			var e := absf(cx * 0.2 - cy * 0.2)
			var h := 1.0 if d < 2.0 and e < 2.5 else 0.0
			img.set_pixel(x, y, Color(h, h, h))
	img.bump_map_to_normal_map(2.0)
	img.generate_mipmaps()
	var t := ImageTexture.create_from_image(img)
	_cache["tread"] = t
	return t

## A font that reads like the HUD mockups where available.
static func paint_font() -> Font:
	return ThemeDB.fallback_font

static func font(bold := true) -> Font:
	var f := SystemFont.new()
	f.font_names = PackedStringArray(["Bahnschrift", "Segoe UI", "Arial"])
	f.font_weight = 700 if bold else 400
	return f

# ------------------------------------------------------------------ canvas painting

class Canvas extends Control:
	var fn: Callable
	func _draw() -> void:
		fn.call(self)

## Draw with `fn(canvas)` into a texture of `size`. Awaitable: returns an ImageTexture.
static func paint(host: Node, size: Vector2i, fn: Callable, transparent := true) -> ImageTexture:
	var vp := SubViewport.new()
	vp.size = size
	vp.transparent_bg = transparent
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.disable_3d = true
	var c := Canvas.new()
	c.fn = fn
	c.size = Vector2(size)
	vp.add_child(c)
	host.add_child(vp)
	for k in 6:
		await RenderingServer.frame_post_draw
	var img := vp.get_texture().get_image()
	vp.queue_free()
	img.generate_mipmaps()
	return ImageTexture.create_from_image(img)

# ------------------------------------------------------------------ artwork

const NAVY := Color(0.07, 0.1, 0.19)
const ORANGE := Color(0.95, 0.55, 0.12)
const CREAM := Color(0.92, 0.9, 0.85)
const STEEL := Color(0.2, 0.22, 0.25)

static func chevron(c: Control, center: Vector2, w: float, col: Color) -> void:
	var h := w * 0.62
	var pts := PackedVector2Array([
		center + Vector2(-w * 0.5, -h * 0.5), center + Vector2(0, h * 0.18), center + Vector2(w * 0.5, -h * 0.5),
		center + Vector2(w * 0.5, -h * 0.18), center + Vector2(0, h * 0.5), center + Vector2(-w * 0.5, -h * 0.18)])
	c.draw_colored_polygon(pts, col)

## Front-on mech with tall shoulder fins, after the "03" hero frame.
static func mech_silhouette(c: Control, r: Rect2, col: Color) -> void:
	var polys := [
		[0.36, 1.0, 0.47, 1.0, 0.46, 0.62, 0.38, 0.62], [0.53, 1.0, 0.64, 1.0, 0.62, 0.62, 0.54, 0.62],
		[0.31, 1.0, 0.49, 1.0, 0.48, 0.95, 0.33, 0.95], [0.51, 1.0, 0.69, 1.0, 0.67, 0.95, 0.52, 0.95],
		[0.37, 0.64, 0.63, 0.64, 0.66, 0.52, 0.34, 0.52], [0.32, 0.54, 0.68, 0.54, 0.74, 0.3, 0.26, 0.3],
		[0.45, 0.31, 0.55, 0.31, 0.56, 0.22, 0.44, 0.22],
		[0.18, 0.38, 0.3, 0.3, 0.28, 0.55, 0.19, 0.64, 0.14, 0.5], [0.82, 0.38, 0.7, 0.3, 0.72, 0.55, 0.81, 0.64, 0.86, 0.5],
		[0.15, 0.62, 0.22, 0.62, 0.23, 0.82, 0.14, 0.82], [0.85, 0.62, 0.78, 0.62, 0.77, 0.82, 0.86, 0.82],
		[0.2, 0.34, 0.27, 0.32, 0.33, 0.0, 0.27, 0.04], [0.8, 0.34, 0.73, 0.32, 0.67, 0.0, 0.73, 0.04],
	]
	for p in polys:
		var pts := PackedVector2Array()
		for i in range(0, p.size(), 2):
			pts.append(r.position + Vector2(p[i] * r.size.x, p[i + 1] * r.size.y))
		c.draw_colored_polygon(pts, col)

static func banner(c: Control) -> void:
	var s := c.size
	c.draw_rect(Rect2(Vector2.ZERO, s), CREAM.darkened(0.08))
	c.draw_rect(Rect2(Vector2(0, 0), Vector2(s.x, s.y * 0.06)), NAVY)
	c.draw_rect(Rect2(Vector2(0, s.y * 0.94), Vector2(s.x, s.y * 0.06)), NAVY)
	chevron(c, Vector2(s.x * 0.5, s.y * 0.15), s.x * 0.55, ORANGE)
	chevron(c, Vector2(s.x * 0.5, s.y * 0.24), s.x * 0.38, ORANGE.darkened(0.15))
	mech_silhouette(c, Rect2(Vector2(s.x * 0.08, s.y * 0.3), Vector2(s.x * 0.84, s.y * 0.56)), STEEL)
	chevron(c, Vector2(s.x * 0.5, s.y * 0.9), s.x * 0.25, ORANGE)

static func stencil(c: Control, text: String, col: Color, bg := Color(0, 0, 0, 0)) -> void:
	if bg.a > 0.0:
		c.draw_rect(Rect2(Vector2.ZERO, c.size), bg)
	var f := paint_font()
	var fs := int(c.size.y * 0.8)
	var w := f.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
	c.draw_string(f, Vector2((c.size.x - w) * 0.5, c.size.y * 0.82), text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, col)

static func sign_panel(c: Control, title: String, sub: String) -> void:
	var s := c.size
	c.draw_rect(Rect2(Vector2.ZERO, s), NAVY)
	c.draw_rect(Rect2(Vector2(0, s.y - 10), Vector2(s.x, 10)), ORANGE)
	chevron(c, Vector2(s.y * 0.55, s.y * 0.45), s.y * 0.6, ORANGE)
	var f := paint_font()
	c.draw_string(f, Vector2(s.y * 1.05, s.y * 0.52), title, HORIZONTAL_ALIGNMENT_LEFT, -1, int(s.y * 0.3), CREAM)
	c.draw_string(f, Vector2(s.y * 1.07, s.y * 0.82), sub, HORIZONTAL_ALIGNMENT_LEFT, -1, int(s.y * 0.17), CREAM.darkened(0.25))

static func screen(c: Control, title: String, lines: Array) -> void:
	var s := c.size
	c.draw_rect(Rect2(Vector2.ZERO, s), Color(0.02, 0.06, 0.1))
	for y in range(0, int(s.y), 4):
		c.draw_line(Vector2(0, y), Vector2(s.x, y), Color(0.1, 0.5, 0.6, 0.08))
	var f := paint_font()
	c.draw_string(f, Vector2(16, 40), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 30, Color(0.4, 0.95, 0.9))
	var y := 80.0
	for l in lines:
		c.draw_string(f, Vector2(16, y), str(l), HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color(0.75, 0.9, 0.95))
		y += 28.0
	mech_silhouette(c, Rect2(Vector2(s.x - 150, 30), Vector2(120, 180)), Color(0.2, 0.85, 0.8, 0.5))
