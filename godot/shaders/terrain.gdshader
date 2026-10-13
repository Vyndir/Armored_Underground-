// Coastal terrain: grass and pine litter on gentle ground, granite on slopes and
// at the waterline, wet sand in the splash zone, and sand with sunlight
// caustics under the water.
shader_type spatial;

uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D rock_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 grass : source_color = vec3(0.27, 0.34, 0.15);
uniform vec3 litter : source_color = vec3(0.36, 0.3, 0.2);
uniform vec3 rock : source_color = vec3(0.5, 0.48, 0.45);
uniform vec3 sand : source_color = vec3(0.74, 0.68, 0.54);
uniform float water_level = -2.2;

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
	float h = wpos.y - water_level;
	float rocky = clamp(smoothstep(0.28, 0.5, slope + (n1 - 0.5) * 0.2) + (1.0 - smoothstep(0.5, 2.5, h)) * 0.8, 0.0, 1.0);
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
	float c = caustic(wpos.xz * 0.45, TIME * 0.7) * exp(-max(depth, 0.0) / 5.0) * under;
	EMISSION = vec3(0.85, 1.0, 0.95) * c * 0.55;
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), texture(rock_nrm, rp * 0.12).rgb, rocky * 0.8 + 0.2);
}
