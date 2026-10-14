// Late-morning coastal sky: deep zenith, hazy horizon, sun disk and glow, and
// soft cumulus banks along the mountains drifting slowly.
shader_type sky;
render_mode use_half_res_pass;

uniform vec3 zenith : source_color = vec3(0.17, 0.4, 0.82);
uniform vec3 horizon : source_color = vec3(0.72, 0.83, 0.95);
uniform vec3 ground : source_color = vec3(0.42, 0.47, 0.5);
uniform float cloud_cover = 0.5;
uniform float cloud_light = 1.0;   // overall cloud brightness (low at night)

// sin-free hash: stays stable at the large coordinates of the high octaves
float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) {
	float s = 0.0, a = 0.5;
	mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	for (int k = 0; k < 6; k++) { s += a * noise(p); p = r * p * 2.02 + vec2(3.1, 1.7); a *= 0.5; }
	return s;
}

void sky() {
	vec3 d = EYEDIR;
	float h = clamp(d.y, 0.0, 1.0);
	vec3 col = mix(horizon, zenith, pow(h, 0.45));
	if (d.y < 0.0) col = mix(horizon, ground, clamp(-d.y * 6.0, 0.0, 1.0));
	vec3 sd = LIGHT0_DIRECTION;
	float sdot = max(dot(d, sd), 0.0);
	col += LIGHT0_COLOR * (pow(sdot, 2400.0) * 30.0 + pow(sdot, 14.0) * 0.18 + pow(sdot, 3.0) * 0.05);
	if (d.y > 0.0 && !AT_HALF_RES_PASS) {
		vec2 cp = d.xz / (d.y + 0.3) * 2.2 + TIME * vec2(0.004, 0.0015);
		float c = fbm(cp);
		float cover = smoothstep(1.0 - cloud_cover, 1.0 - cloud_cover + 0.28, c);
		// banks sit low over the horizon like the reference
		cover *= smoothstep(0.0, 0.05, d.y) * (1.0 - smoothstep(0.25, 0.7, d.y) * 0.6);
		float shade = fbm(cp * 1.8 + 4.0);
		vec3 cc = mix(vec3(1.0), vec3(0.72, 0.76, 0.84), shade * 0.8);
		cc += vec3(1.0, 0.95, 0.85) * pow(sdot, 5.0) * 0.4;
		cc *= cloud_light;
		col = mix(col, cc, cover * 0.95);
	}
	COLOR = col;
}
