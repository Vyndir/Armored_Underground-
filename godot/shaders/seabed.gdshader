// Sand and stone under the shallows, with animated sunlight caustics that fade with depth.
shader_type spatial;

uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform vec3 sand : source_color = vec3(0.72, 0.66, 0.52);
uniform vec3 weed : source_color = vec3(0.24, 0.32, 0.2);
uniform float water_level = -2.5;

varying vec3 wpos;

void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

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
	float n1 = texture(noise_a, wpos.xz * 0.04).r;
	float n2 = texture(noise_b, wpos.xz * 0.6).r;
	vec3 col = mix(sand, weed, smoothstep(0.55, 0.75, n1)) * (0.8 + n2 * 0.3);
	float depth = water_level - wpos.y;
	float c = caustic(wpos.xz * 0.45, TIME * 0.7) * exp(-max(depth, 0.0) / 6.0) * step(0.0, depth);
	ALBEDO = col;
	EMISSION = vec3(0.9, 1.0, 0.95) * c * 0.6;
	ROUGHNESS = 0.9;
}
