// Cloth that ripples in the sea breeze: displacement grows away from the pole
// (UV.x = 0 at the hoist).
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform float strength = 0.6;
uniform float speed = 3.2;

void vertex() {
	float k = UV.x;
	float w = sin(UV.x * 7.0 - TIME * speed + UV.y * 1.5) * 0.5 + sin(UV.x * 13.0 - TIME * speed * 1.7) * 0.2;
	VERTEX.z += w * strength * k;
	VERTEX.y -= k * k * 0.25 * strength;
	NORMAL = normalize(NORMAL + vec3(0.0, 0.0, -cos(UV.x * 7.0 - TIME * speed) * k * 0.6));
}

void fragment() {
	vec4 c = texture(tex, UV);
	ALBEDO = c.rgb;
	ROUGHNESS = 0.85;
	BACKLIGHT = c.rgb * 0.35;
}
