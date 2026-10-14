// Weathered off-white industrial cladding (world-space, for axis-aligned walls).
//   ribs      corrugated profile from a normal texture, running vertically
//   panels    horizontal seams every panel_h, the odd replaced panel in a
//             slightly different tone
//   weather   soft rain streaks running down from the seams and the eave,
//             a darker splash band at the base, a little rust weeping from
//             fixings: looked after, with history, not derelict
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D ribs : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform vec3 base_color : source_color = vec3(0.8, 0.78, 0.72);
uniform float rib_pitch = 0.2;      // metres per corrugation
uniform float panel_h = 3.0;        // horizontal seam spacing (m)
uniform float panel_w = 1.0;        // panel width (m) for per-panel tone
uniform float eave = 21.0;          // height where streaks start
uniform float weather = 1.0;

varying vec3 wpos;
varying vec3 wnrm;

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	bool flat_top = abs(wnrm.y) > 0.6;
	float u = abs(wnrm.x) > abs(wnrm.z) ? wpos.z : wpos.x;
	float y = wpos.y;
	if (flat_top) { u = wpos.x; y = wpos.z; }
	vec2 panel = vec2(floor(u / panel_w), floor(y / panel_h));
	float ph = hash(panel + vec2(17.0, 3.0));
	vec3 col = base_color * (0.97 + (ph - 0.5) * 0.06);
	col = mix(col, base_color * vec3(1.05, 1.05, 1.02), step(0.96, ph));   // replaced panel
	// horizontal seams: a thin dark line with a lap shadow under it
	float sy = fract(y / panel_h) * panel_h;
	float fw = max(fwidth(sy), 0.002);
	float seam = 1.0 - smoothstep(0.02 - fw, 0.02 + fw, sy);
	float lap = 1.0 - smoothstep(0.0, 0.25, sy);
	col *= 1.0 - seam * 0.45 - lap * 0.08;
	// rain streaks: long vertical noise, strongest under the seams and the eave
	float streak = texture(noise_a, vec2(u * 0.9, y * 0.035)).r;
	float under_seam = 1.0 - smoothstep(0.0, panel_h * 0.85, panel_h - sy);
	float from_top = 1.0 - smoothstep(0.0, 6.0, eave - y);
	float streaks = smoothstep(0.5, 0.8, streak) * (0.35 + under_seam * 0.4 + from_top * 0.6);
	col *= 1.0 - streaks * 0.16 * weather;
	// splash band and ground dirt
	float base = 1.0 - smoothstep(0.0, 2.6, y);
	col *= 1.0 - base * (0.22 + texture(noise_b, vec2(u, y) * 0.5).r * 0.12) * weather;
	// rust weeping from the fixings at seams, kept rare
	float rust = smoothstep(0.86, 0.95, texture(noise_b, vec2(u * 2.0, y * 0.25)).r) * under_seam;
	col = mix(col, vec3(0.42, 0.24, 0.13) * base_color.r, rust * 0.35 * weather);
	ALBEDO = col;
	ROUGHNESS = 0.55 + streaks * 0.15 + base * 0.2;
	METALLIC = 0.25;
	vec3 rn = texture(ribs, vec2(u / (rib_pitch * 16.0), y / 4.0)).rgb;
	NORMAL_MAP = flat_top ? vec3(0.5, 0.5, 1.0) : rn;
	NORMAL_MAP_DEPTH = 1.2;
}
