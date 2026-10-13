// World-space concrete for the apron, quay and slipway: two noise layers for
// tone, a slab-joint grid, damp patches that go glossy, oil stains, and an
// optional painted-markings texture laid over a rectangle of the world.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D detail_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D markings : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 markings_rect = vec4(0.0, 0.0, 1.0, 1.0);   // x, z, width, depth in metres
uniform float use_markings = 0.0;
uniform vec3 base_color : source_color = vec3(0.6, 0.59, 0.56);
uniform float slab = 6.0;          // joint spacing
uniform float wetness = 0.35;
uniform float grooves = 0.0;       // slipway anti-slip grooves (per metre)
uniform float triplanar_walls = 0.0;

varying vec3 wpos;
varying vec3 wnrm;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 p = wpos.xz;
	if (triplanar_walls > 0.5 && abs(wnrm.y) < 0.6) p = abs(wnrm.x) > abs(wnrm.z) ? wpos.zy : wpos.xy;
	float n1 = texture(noise_a, p * 0.031).r;
	float n2 = texture(noise_b, p * 0.27).r;
	float n3 = texture(noise_b, p * 1.9).r;
	vec3 col = base_color * (0.9 + (n1 - 0.5) * 0.12 + (n2 - 0.5) * 0.08 + (n3 - 0.5) * 0.05);
	// slab joints
	vec2 g = abs(fract(p / slab) - 0.5) * slab;
	float joint = 1.0 - smoothstep(0.02, 0.06, min(g.x, g.y) - (slab * 0.5 - 0.06));
	joint = 1.0 - smoothstep(slab * 0.5 - 0.07, slab * 0.5 - 0.03, max(g.x, g.y));
	col *= mix(0.62, 1.0, joint);
	// oil and tyre stains
	float stain = smoothstep(0.68, 0.85, texture(noise_a, p * 0.05 + vec2(3.1, 7.7)).r);
	col *= 1.0 - stain * 0.18;
	// grooves (slipway)
	float gr = grooves > 0.0 ? smoothstep(0.35, 0.5, abs(fract(wpos.x * grooves) - 0.5)) : 0.0;
	col *= 1.0 - gr * 0.35;
	// painted lines
	if (use_markings > 0.5) {
		vec2 muv = (wpos.xz - markings_rect.xy) / markings_rect.zw;
		if (muv.x >= 0.0 && muv.x <= 1.0 && muv.y >= 0.0 && muv.y <= 1.0) {
			vec4 mk = texture(markings, muv);
			float wear = smoothstep(0.25, 0.6, n2 + n3 * 0.3);
			col = mix(col, mk.rgb, mk.a * mix(0.55, 0.95, wear));
		}
	}
	// damp patches: darker and glossy, like the reference apron after a wash-down
	float damp = smoothstep(0.55, 0.8, texture(noise_a, p * 0.009 + vec2(11.0, 2.0)).r) * wetness;
	ALBEDO = col * mix(1.0, 0.82, damp);
	ROUGHNESS = mix(0.82 - n2 * 0.1, 0.12, damp) + gr * 0.1;
	SPECULAR = 0.45;
	vec3 dn = texture(detail_nrm, p * 0.35).rgb;
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), dn, mix(0.6, 0.2, damp));
}
