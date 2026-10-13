// Mech armour, ported from the Unity AU/MechSurface shader: baked albedo with
// weathering, paint masks (R primary, G accent, B wear) tinted per pilot,
// metallic/roughness/AO, baked normals, plus a fine tiling detail normal and a
// clear-coat on painted panels.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D mask_tex : filter_linear_mipmap_anisotropic;
uniform sampler2D mra_tex : filter_linear_mipmap_anisotropic;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D detail_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec3 paint_color : source_color = vec3(0.42, 0.44, 0.42);
uniform vec3 accent_color : source_color = vec3(0.95, 0.5, 0.1);
uniform float paint_gain = 1.8;
uniform float detail_scale = 5.0;
uniform float detail_strength = 0.12;
uniform float clear_coat = 0.55;
uniform float wet = 0.0;   // set while wading: darkens and slicks the lower legs

varying float model_y;

void vertex() {
	model_y = VERTEX.y;
}

void fragment() {
	vec3 a = texture(albedo_tex, UV).rgb;
	vec4 m = texture(mask_tex, UV);
	vec3 mra = texture(mra_tex, UV).rgb;
	a = mix(a, a * paint_color * paint_gain, m.r);
	a = mix(a, a * accent_color * paint_gain * 0.95, m.g);
	float wetness = wet * (1.0 - smoothstep(1.2, 3.2, model_y));
	ALBEDO = a * mix(1.0, 0.6, wetness);
	METALLIC = mra.r;
	ROUGHNESS = mix(mra.g, 0.08, wetness);
	AO = mix(1.0, mra.b, 0.85);
	AO_LIGHT_AFFECT = 0.4;
	vec3 nb = texture(normal_tex, UV).rgb * 2.0 - 1.0;
	vec3 nd = texture(detail_normal, UV * detail_scale).rgb * 2.0 - 1.0;
	vec3 n = normalize(vec3(nb.xy + nd.xy * detail_strength, nb.z));
	NORMAL_MAP = n * 0.5 + 0.5;
	float painted = clamp(m.r + m.g, 0.0, 1.0);
	CLEARCOAT = painted * clear_coat;
	CLEARCOAT_ROUGHNESS = 0.12;
}
