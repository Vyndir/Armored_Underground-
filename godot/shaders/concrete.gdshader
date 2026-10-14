// World-space concrete for the yard, quay, ramps and walls.
//   slabs     saw-cut joints (antialiased), dirt gathered along them, per-slab
//             tone, and an occasional repaired slab with a fresher, smoother face
//   surface   macro tone, aggregate speckle, oil and tyre stains, scuffs
//   markings  painted lines laid over a world rectangle, worn through by
//             traffic and dulled by grime so they belong to the surface
//   damp      concentrated toward the shoreline (shore_x) with a few puddles;
//             the rest of the yard stays dry and matte
//   grooves   slipway anti-slip grooves
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D detail_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D markings : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 markings_rect = vec4(0.0, 0.0, 1.0, 1.0);   // x, z, width, depth in metres
uniform float use_markings = 0.0;
uniform vec3 base_color : source_color = vec3(0.5, 0.49, 0.46);
uniform float slab = 6.0;          // joint spacing (m)
uniform float wetness = 0.35;      // strength of damp areas
uniform float shore_x = 1.0e9;     // world x of the waterline edge; damp rises toward it
uniform float grooves = 0.0;       // slipway anti-slip grooves (per metre)
uniform float triplanar_walls = 0.0;

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
	bool wall = triplanar_walls > 0.5 && abs(wnrm.y) < 0.6;
	vec2 p = wpos.xz;
	if (wall) p = abs(wnrm.x) > abs(wnrm.z) ? wpos.zy : wpos.xy;
	float n1 = texture(noise_a, p * 0.031).r;
	float n2 = texture(noise_b, p * 0.27).r;
	float n3 = texture(noise_b, p * 2.3).r;
	float n4 = texture(noise_a, p * 0.11 + vec2(5.3, 1.7)).r;
	// per-slab identity
	vec2 cell = floor(p / slab);
	float h = hash(cell);
	vec3 col = base_color * (0.94 + (h - 0.5) * 0.1);
	col *= 0.92 + (n1 - 0.5) * 0.16 + (n2 - 0.5) * 0.07;
	// aggregate: fine speckle of lighter and darker stones
	float agg = smoothstep(0.62, 0.75, n3) - smoothstep(0.25, 0.12, n3) * 0.6;
	col *= 1.0 + agg * 0.07;
	// repaired slab: a little lighter, smoother, crisp edge
	float repaired = step(0.93, h) * (wall ? 0.0 : 1.0);
	col = mix(col, base_color * vec3(1.06, 1.06, 1.04), repaired * 0.75);
	// saw-cut joints, antialiased, with dirt gathered in a soft band beside them
	vec2 g = abs(fract(p / slab) - 0.5) * slab;      // distance from slab centre
	float edge = slab * 0.5 - max(g.x, g.y);          // distance to nearest joint
	float fw = max(fwidth(edge), 0.002);
	float cut = 1.0 - smoothstep(0.012 - fw, 0.012 + fw, edge);
	float grime = 1.0 - smoothstep(0.0, 0.35 + n4 * 0.25, edge);
	if (!wall) {
		col *= 1.0 - cut * 0.42;
		col *= 1.0 - grime * 0.1 * (0.6 + n2);
	}
	// oil drips and tyre scuffs
	float oil = smoothstep(0.8, 0.9, texture(noise_a, p * 0.045 + vec2(3.1, 7.7)).r) * smoothstep(0.45, 0.7, n1) * (wall ? 0.0 : 1.0);
	float scuff = smoothstep(0.6, 0.9, texture(noise_b, vec2(p.x * 0.04, p.y * 0.6) + vec2(9.0, 1.0)).r) * 0.5;
	col *= 1.0 - oil * 0.22 - scuff * 0.06;
	// slipway grooves
	float gr = grooves > 0.0 ? smoothstep(0.35, 0.5, abs(fract(wpos.x * grooves) - 0.5)) : 0.0;
	col *= 1.0 - gr * 0.35;
	// painted markings, worn by traffic and dulled with grime
	float paint = 0.0;
	if (use_markings > 0.5 && !wall) {
		vec2 muv = (wpos.xz - markings_rect.xy) / markings_rect.zw;
		if (muv.x >= 0.0 && muv.x <= 1.0 && muv.y >= 0.0 && muv.y <= 1.0) {
			vec4 mk = texture(markings, muv);
			float wear = smoothstep(0.22, 0.62, n2 * 0.7 + n3 * 0.45 + n4 * 0.2);
			paint = mk.a * mix(0.35, 0.9, wear) * (1.0 - cut);
			vec3 dulled = mk.rgb * (0.78 + n1 * 0.12) * mix(vec3(1.0), vec3(0.92, 0.9, 0.84), 0.6);
			col = mix(col, dulled, paint);
		}
	}
	// damp: rises toward the shoreline, a few puddles in low spots elsewhere
	float shore = 1.0 - smoothstep(4.0, 26.0, shore_x - wpos.x);
	float patches = smoothstep(0.5, 0.75, texture(noise_a, p * 0.012 + vec2(11.0, 2.0)).r);
	float puddle = smoothstep(0.78, 0.86, texture(noise_a, p * 0.02 + vec2(1.0, 21.0)).r);
	float damp = wall ? 0.0 : clamp(shore * (0.45 + patches * 0.55) + puddle * 0.6, 0.0, 1.0) * wetness;
	ALBEDO = col * mix(1.0, 0.7, damp);
	ROUGHNESS = clamp(mix(0.86 - n2 * 0.08 - repaired * 0.12 + oil * -0.25, 0.1, damp) + gr * 0.1 - paint * 0.15, 0.06, 1.0);
	SPECULAR = 0.45;
	vec3 dn = texture(detail_nrm, p * 0.35).rgb;
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), dn, mix(0.55, 0.12, damp) * (1.0 - repaired * 0.5));
}
