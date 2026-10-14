// Coastal terrain: grass and pine litter on gentle ground, granite on slopes and
// at the waterline, wet sand in the splash zone, and sand with sunlight
// caustics under the water.
shader_type spatial;

uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D rock_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 grass : source_color = vec3(0.22, 0.33, 0.13);
uniform vec3 litter : source_color = vec3(0.33, 0.29, 0.19);
uniform vec3 rock : source_color = vec3(0.43, 0.41, 0.38);
uniform vec3 sand : source_color = vec3(0.74, 0.68, 0.54);
uniform float water_level = -2.2;
uniform float caustic_level = 1.0;   // off at night

varying vec3 wpos;
varying vec3 wnrm;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float caustic(vec2 p, float t) {
	vec2 q = p;
	float c = 0.0;
	for (int i = 0; i < 3; i++) {
		q += vec2(sin(q.y * 1.7 + t), cos(q.x * 1.3 - t * 0.8)) * 0.6;
		c += abs(sin(q.x + q.y));
	}
	return pow(1.0 - c / 3.0, 6.0);
}

void fragment() {
	float n1 = texture(noise_a, wpos.xz * 0.02).r;
	float n2 = texture(noise_b, wpos.xz * 0.25).r;
	float n3 = texture(noise_b, wpos.xz * 1.7).r;
	float slope = 1.0 - wnrm.y;
	vec3 g = mix(grass, litter, smoothstep(0.45, 0.7, n1)) * (0.8 + n2 * 0.35);
	// granite: banded and speckled, picked out by slope and the shoreline
	vec2 rp = abs(wnrm.x) > abs(wnrm.z) ? wpos.zy : wpos.xy;
	float rn = texture(noise_b, rp * 0.18).r;
	vec3 r = rock * (0.7 + rn * 0.45 + (n3 - 0.5) * 0.15);
	// bedding strata on the larger faces
	r *= 0.86 + 0.14 * sin(wpos.y * 0.9 + rn * 5.0 + n1 * 3.0);
	// lichen and moss on the granite, more on gentler faces
	float lichen = smoothstep(0.55, 0.75, texture(noise_b, rp * 0.6 + vec2(4.0, 9.0)).r) * (1.0 - smoothstep(0.5, 0.8, slope));
	r = mix(r, vec3(0.27, 0.32, 0.17), lichen * 0.55);
	float h = wpos.y - water_level;
	float rocky = clamp(smoothstep(0.42, 0.7, slope + (n1 - 0.5) * 0.25) + (1.0 - smoothstep(0.5, 2.5, h)) * 0.8, 0.0, 1.0);
	vec3 col = mix(g, r, rocky);
	float sandy = (1.0 - smoothstep(-0.2, 0.6, h)) * (1.0 - smoothstep(0.35, 0.6, slope));
	col = mix(col, sand * (0.75 + n2 * 0.3), sandy);
	float under = 1.0 - smoothstep(-0.6, 0.0, h);
	vec3 bed = mix(sand, rock * 0.8, smoothstep(0.4, 0.62, slope + n1 * 0.3)) * (0.78 + n3 * 0.25);
	col = mix(col, bed, under);
	float wet = (1.0 - smoothstep(0.0, 0.8, h)) * (1.0 - under);
	ALBEDO = col * mix(1.0, 0.62, wet);
	ROUGHNESS = mix(mix(0.92, 0.75, rocky), 0.25, wet);
	float depth = -h;
	// caustic web: where two drifting noise fields meet, a thin bright line
	float ca = texture(noise_b, wpos.xz * 0.18 + vec2(TIME * 0.013, TIME * 0.007)).r;
	float cb = texture(noise_b, wpos.xz * 0.23 + vec2(-TIME * 0.009, TIME * 0.011) + vec2(7.0, 3.0)).r;
	float c = smoothstep(0.955, 1.0, 1.0 - abs(ca - cb)) * exp(-max(depth, 0.0) / 5.0) * under;
	EMISSION = vec3(0.85, 1.0, 0.95) * c * 0.4 * caustic_level;
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), texture(rock_nrm, rp * 0.12).rgb, rocky * 0.8 + 0.2);
}
