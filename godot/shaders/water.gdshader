// Clear coastal water: refraction of the seabed with depth-based absorption
// (turquoise shallows to deep blue), two scrolling wave normal layers, gentle
// vertex swell, shoreline foam where the water meets geometry, and sun glints.
shader_type spatial;
render_mode cull_back, depth_draw_always, specular_schlick_ggx;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D wave_a : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D wave_b : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D foam_noise : filter_linear_mipmap, repeat_enable;

uniform vec3 shallow_color : source_color = vec3(0.16, 0.62, 0.6);
uniform vec3 deep_color : source_color = vec3(0.02, 0.14, 0.28);
uniform float clarity = 6.0;          // meters of visibility before the deep colour wins
uniform float refraction = 0.035;
uniform float wave_scale = 0.045;
uniform float wave_speed = 0.022;
uniform float normal_strength = 0.55;
uniform float swell_height = 0.3;
uniform float foam_width = 0.9;
uniform vec3 foam_color : source_color = vec3(0.92, 0.96, 0.97);

varying vec3 world_pos;

// Sum of directional swells rolling in from the bay; returns height and gradient.
vec3 swell(vec2 p, float t) {
	vec4 dirs[4] = { vec4(0.94, -0.34, 0.045, 0.9), vec4(0.7, 0.71, 0.07, 1.1), vec4(-0.2, 0.98, 0.11, 1.5), vec4(0.99, 0.1, 0.19, 2.1) };
	float amps[4] = { 0.7, 0.45, 0.25, 0.12 };
	float h = 0.0;
	vec2 g = vec2(0.0);
	for (int i = 0; i < 4; i++) {
		vec2 d = normalize(dirs[i].xy);
		float k = dirs[i].z;
		float ph = dot(d, p) * k + t * dirs[i].w;
		// sharpened crests, flatter troughs
		float s = sin(ph) * 0.5 + 0.5;
		h += (pow(s, 1.6) * 2.0 - 1.0) * amps[i];
		g += d * k * amps[i] * 1.6 * pow(max(s, 1e-3), 0.6) * cos(ph);
	}
	return vec3(h, g);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float t = TIME;
	// calm in the slipway and against the quay, fuller swell out in the bay
	float open_water = smoothstep(40.0, 140.0, world_pos.x);
	float amp = swell_height * mix(0.45, 1.0, open_water);
	vec3 sw = swell(world_pos.xz, t);
	VERTEX.y += sw.x * amp;
	NORMAL = normalize(NORMAL + (inverse(mat3(MODEL_MATRIX)) * vec3(-sw.y * amp, 0.0, -sw.z * amp)));
}

void fragment() {
	float t = TIME * wave_speed;
	vec2 uv = world_pos.xz * wave_scale;
	vec3 na = texture(wave_a, uv + vec2(t, t * 0.4)).rgb;
	vec3 nb = texture(wave_b, uv * 2.3 + vec2(-t * 0.7, t * 1.1)).rgb;
	vec3 nc = texture(wave_a, uv * 0.31 + vec2(t * 0.2, -t * 0.25)).rgb;
	vec3 n = normalize((na * 2.0 - 1.0) + (nb * 2.0 - 1.0) * 0.6 + (nc * 2.0 - 1.0) * 0.8);
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), n * 0.5 + 0.5, normal_strength);

	// how much water lies between the surface and whatever is behind it
	float scene_depth = texture(depth_tex, SCREEN_UV).r;
	vec4 world = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene_depth, 1.0);
	float scene_z = -world.z / world.w;
	float water_z = -VERTEX.z;
	float thickness = max(scene_z - water_z, 0.0);

	vec2 offset = n.xy * refraction * clamp(thickness / 4.0, 0.0, 1.0);
	// don't refract things in front of the water
	float d2 = texture(depth_tex, SCREEN_UV + offset).r;
	vec4 w2 = INV_PROJECTION_MATRIX * vec4((SCREEN_UV + offset) * 2.0 - 1.0, d2, 1.0);
	if (-w2.z / w2.w < water_z) offset = vec2(0.0);
	vec3 below = textureLod(screen_tex, SCREEN_UV + offset, 0.0).rgb;

	float absorb = 1.0 - exp(-thickness / clarity);
	vec3 tint = mix(shallow_color, deep_color, clamp(absorb * 1.2, 0.0, 1.0));
	vec3 col = mix(below * mix(vec3(1.0), shallow_color * 1.6, 0.55), tint, clamp(absorb, 0.0, 0.92));

	// foam on contact lines and wave crests
	float fn = texture(foam_noise, world_pos.xz * 0.12 + vec2(TIME * 0.02, 0.0)).r;
	float shore = 1.0 - smoothstep(0.0, foam_width, thickness);
	float foam = clamp(shore * (0.6 + fn) - 0.25, 0.0, 1.0);
	foam += smoothstep(0.78, 0.95, fn) * 0.12 * smoothstep(8.0, 20.0, thickness);
	col = mix(col, foam_color, clamp(foam, 0.0, 1.0));

	ALBEDO = vec3(0.0);
	EMISSION = col * 0.92;
	ROUGHNESS = mix(0.04, 0.5, foam);
	METALLIC = 0.0;
	SPECULAR = 0.5;
}
