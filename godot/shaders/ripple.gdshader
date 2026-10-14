// Expanding ring ripple laid on the water surface; `life` runs 0 -> 1.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float life = 0.0;
uniform vec3 tint : source_color = vec3(0.85, 0.95, 1.0);

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float ring = smoothstep(0.08, 0.0, abs(r - life * 0.9)) + smoothstep(0.06, 0.0, abs(r - life * 0.6)) * 0.5;
	float fade = (1.0 - life) * (1.0 - smoothstep(0.85, 1.0, r));
	ALBEDO = tint * ring * fade * 0.6;
	ALPHA = 1.0;
}
