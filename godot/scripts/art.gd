class_name Art
extends RefCounted
## Painted artwork for the site: league banner, bay numbers, the door sign,
## terminal screens, and the apron's line markings.

static func make(host: Node) -> Dictionary:
	var out := {}
	out["banner"] = await Tex.paint(host, Vector2i(512, 896), func(c: Control): Tex.banner(c), false)
	out["num03"] = await Tex.paint(host, Vector2i(512, 360), func(c: Control): Tex.stencil(c, "03", Tex.CREAM))
	out["sign"] = await Tex.paint(host, Vector2i(1536, 200), func(c: Control): Tex.sign_panel(c, "IRON ASCENDANT  ·  TRAINING HANGAR 03", "WATERFRONT FACILITY  ·  AUTHORISED FRAMES ONLY"), false)
	out["screen"] = await Tex.paint(host, Vector2i(512, 320), func(c: Control): Tex.screen(c, "KESTREL  ·  SYSTEMS CHECK", ["LEG ACTUATORS ...... OK", "BOOSTER BANK ....... OK", "GYRO / BALANCE ..... OK", "WADING SEALS ....... OK", "FCS ................ STANDBY"]), false)
	out["flag"] = await Tex.paint(host, Vector2i(512, 300), func(c: Control): Tex.flag(c), false)
	out["sign_speed"] = await Tex.paint(host, Vector2i(480, 280), func(c: Control): Tex.road_sign(c, "FRAME SPEED", "15 KM/H IN YARD", Tex.NAVY), false)
	out["sign_slip"] = await Tex.paint(host, Vector2i(480, 280), func(c: Control): Tex.road_sign(c, "SLIPWAY 1", "WADING ZONE AHEAD", Tex.ORANGE.darkened(0.2)), false)
	out["sign_crossing"] = await Tex.paint(host, Vector2i(480, 280), func(c: Control): Tex.road_sign(c, "MECH CROSSING", "GIVE WAY TO FRAMES", Tex.NAVY), false)
	out["markings"] = await Tex.paint(host, Vector2i(2048, 2496), func(c: Control): apron_paint(c))
	return out

static func apron_paint(c: Control) -> void:
	var W := Site.APRON_X1 - Site.APRON_X0
	var D := Site.APRON_Z1 - Site.APRON_Z0
	var sx := c.size.x / W
	var sz := c.size.y / D
	var P := func(x: float, z: float) -> Vector2: return Vector2((x - Site.APRON_X0) * sx, (z - Site.APRON_Z0) * sz)
	var yellow := Color(0.95, 0.72, 0.1)
	var white := Color(0.94, 0.94, 0.92)
	var black := Color(0.05, 0.05, 0.05)
	var m := sx   # pixels per metre
	# guide line from the bay out of the doors, across the apron and down to the slipway
	var lane := PackedVector2Array()
	var pts := [Vector2(-140, -74), Vector2(-78, -74), Vector2(-45, -74), Vector2(-22, -60), Vector2(-8, -30), Vector2(-4, 0), Vector2(-2, 26), Vector2(8, 33)]
	for p in pts:
		lane.append(P.call(p.x, p.y))
	c.draw_polyline(lane, black, 0.55 * m, true)
	c.draw_polyline(lane, yellow, 0.32 * m, true)
	# branch toward the north apron (boost course)
	var north := PackedVector2Array([P.call(-45, -74), P.call(-30, -95), P.call(-12, -125), P.call(0, -150)])
	c.draw_polyline(north, black, 0.55 * m, true)
	c.draw_polyline(north, yellow, 0.32 * m, true)
	# white chevrons along the lane, pointing toward the water
	for i in range(1, pts.size() - 1):
		var a: Vector2 = pts[i]
		var b: Vector2 = pts[i + 1]
		var dirv := (b - a).normalized()
		var right := Vector2(-dirv.y, dirv.x)
		var mid := (a + b) * 0.5
		for k: float in [-4.0, 4.0]:
			var cpt: Vector2 = mid + right * k * 0.0 + dirv * k
			var tip: Vector2 = cpt + dirv * 1.6
			var poly := PackedVector2Array([P.call(cpt.x - right.x * 2.6, cpt.y - right.y * 2.6), P.call(tip.x, tip.y), P.call(cpt.x + right.x * 2.6, cpt.y + right.y * 2.6),
				P.call(cpt.x + right.x * 2.6 - dirv.x * 1.0, cpt.y + right.y * 2.6 - dirv.y * 1.0), P.call(tip.x - dirv.x * 1.0, tip.y - dirv.y * 1.0), P.call(cpt.x - right.x * 2.6 - dirv.x * 1.0, cpt.y - right.y * 2.6 - dirv.y * 1.0)])
			c.draw_colored_polygon(poly, Color(white.r, white.g, white.b, 0.9))
	# quay edge line, 2.5 m back from the edge
	c.draw_line(P.call(Site.APRON_X1 - 2.5, Site.APRON_Z0), P.call(Site.APRON_X1 - 2.5, Site.SLIP_Z0 - 2.5), yellow, 0.25 * m)
	# hatched no-go boxes: slipway head, cabins, generators
	for r in [Rect2(-4, Site.SLIP_Z0 - 2, 6, Site.SLIP_Z1 - Site.SLIP_Z0 + 4), Rect2(-68, -154, 40, 8), Rect2(-64, -115, 8, 6), Rect2(-64, -31, 8, 6)]:
		var rect: Rect2 = r
		var a: Vector2 = P.call(rect.position.x, rect.position.y)
		var b: Vector2 = P.call(rect.end.x, rect.end.y)
		c.draw_rect(Rect2(a, b - a), yellow, false, 0.22 * m)
		var step := 1.4 * m
		var x := a.x - (b.y - a.y)
		while x < b.x:
			var s0 := Vector2(maxf(x, a.x), a.y + maxf(0.0, a.x - x))
			var e0 := Vector2(minf(x + (b.y - a.y), b.x), a.y + minf(b.y - a.y, b.x - x))
			c.draw_line(s0, e0, Color(yellow.r, yellow.g, yellow.b, 0.85), 0.25 * m)
			x += step
	# training pads: painted rings with numbers where the onboarding stops
	var f := Tex.paint_font()
	var pads := [[Vector2(-58, -74), "1"], [Vector2(-2, -138), "2"], [Vector2(18, -60), "3"]]
	for pd in pads:
		var cpos: Vector2 = P.call(pd[0].x, pd[0].y)
		c.draw_arc(cpos, 7.0 * m, 0, TAU, 72, white, 0.35 * m, true)
		c.draw_arc(cpos, 5.6 * m, 0, TAU, 72, Color(white.r, white.g, white.b, 0.6), 0.15 * m, true)
		var fs := int(6 * m)
		var tw := f.get_string_size(pd[1], HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
		c.draw_string(f, cpos + Vector2(-tw * 0.5, fs * 0.36), pd[1], HORIZONTAL_ALIGNMENT_LEFT, -1, fs, white)
	# bay outlines inside the hangar
	for bz: float in [-102.0, -74.0, -44.0]:
		var a: Vector2 = P.call(-152, bz - 9)
		var b: Vector2 = P.call(-112, bz + 9)
		c.draw_rect(Rect2(a, b - a), yellow, false, 0.3 * m)
		var fs := int(3.5 * m)
		c.draw_string(f, P.call(-150, bz + 7.6), "BAY %02d" % int(3 + (bz + 102) / 28.0), HORIZONTAL_ALIGNMENT_LEFT, -1, fs, yellow)
	# big bay number in front of the doors
	var fs2 := int(16 * m)
	var t := "03"
	var tw2 := f.get_string_size(t, HORIZONTAL_ALIGNMENT_LEFT, -1, fs2).x
	c.draw_set_transform(P.call(-60, -74), -PI * 0.5, Vector2.ONE)
	c.draw_string(f, Vector2(-tw2 * 0.5, -10.0 * m), t, HORIZONTAL_ALIGNMENT_LEFT, -1, fs2, Color(white.r, white.g, white.b, 0.85))
	c.draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
	# vehicle bays along the south edge
	for k in 8:
		var x0 := -150.0 + k * 6.0
		c.draw_line(P.call(x0, 58), P.call(x0, 68), white, 0.15 * m)
	var txt := "SLIPWAY 1  ·  FRAMES ONLY"
	c.draw_set_transform(P.call(-7, 34), -PI * 0.5, Vector2.ONE)
	var fs3 := int(2.2 * m)
	c.draw_string(f, Vector2(-f.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, fs3).x * 0.5, 0), txt, HORIZONTAL_ALIGNMENT_LEFT, -1, fs3, yellow)
	c.draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
