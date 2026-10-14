// Distant city buildings (MultiMesh of unit boxes). Per instance: COLOR is the
// facade colour; INSTANCE_CUSTOM.x picks the style (0 masonry with punched
// windows, 1 glass curtain wall), .y a seed. Windows are laid out in world space
// by floor and bay, antialiased and faded to an average tone with distance so the
// grid never shimmers; roofs are plain and darker. At night (night > 0) a share
// of windows glows warm or cool.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform float night = 0.0;
uniform float floor_h = 3.6;
uniform float bay_w = 3.0;

varying vec3 wpos;
varying vec3 wnrm;
varying vec3 base;
varying float style;
varying float seed;

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	base = COLOR.rgb;
	style = INSTANCE_CUSTOM.x;
	seed = INSTANCE_CUSTOM.y;
}

void fragment() {
	if (wnrm.y > 0.7) {
		ALBEDO = base * 0.55;
		ROUGHNESS = 0.9;
	} else {
		float u = abs(wnrm.x) > abs(wnrm.z) ? wpos.z : wpos.x;
		vec2 cell = vec2(u / bay_w, wpos.y / floor_h);
		vec2 f = fract(cell);
		vec2 fw = fwidth(cell);
		float glass = step(0.5, style);
		vec2 lo = mix(vec2(0.18, 0.28), vec2(0.06, 0.12), glass);
		vec2 hi = mix(vec2(0.82, 0.82), vec2(0.94, 0.94), glass);
		vec2 aa = clamp(fw * 1.5, 0.002, 0.5);
		vec2 win2 = smoothstep(lo - aa, lo + aa, f) * (1.0 - smoothstep(hi - aa, hi + aa, f));
		float win = win2.x * win2.y;
		// far away: replace the pattern by its average coverage
		float cover = (hi.x - lo.x) * (hi.y - lo.y);
		float far = smoothstep(0.25, 0.6, max(fw.x, fw.y));
		win = mix(win, cover, far);
		vec3 glass_col = mix(vec3(0.2, 0.27, 0.33), vec3(0.42, 0.55, 0.66), hash(floor(cell) + seed));
		vec3 wall = base * (0.92 + hash(vec2(floor(cell.y), seed)) * 0.08);
		ALBEDO = mix(wall, glass_col * 0.7, win * (0.85 + glass * 0.15));
		ROUGHNESS = mix(0.85, 0.12, win);
		METALLIC = win * 0.5;
		// lit windows at night: about a third, mixing warm homes and cool offices
		float on = step(0.62, hash(floor(cell) * 1.7 + seed * 13.0));
		vec3 warm = mix(vec3(1.0, 0.72, 0.42), vec3(0.75, 0.85, 1.0), step(0.7, hash(floor(cell) + 3.1)));
		// far away a facade averages its windows: about a third of them lit
		EMISSION = warm * mix(win * on * 0.9, cover * 0.07, far) * night;
		ALBEDO *= mix(1.0, 0.35, night);   // walls fall dark at night
	}
}
