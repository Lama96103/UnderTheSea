shader_type spatial;
render_mode cull_back,diffuse_burley,specular_schlick_ggx, blend_mix;

uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

// Surface settings:
uniform vec2 	sampler_scale 	 = vec2(0.25, 0.25); 			// Scale for the sampler
uniform vec2	sampler_direction= vec2(0.05, 0.04); 			// Direction and speed for the sampler offset

uniform sampler2D uv_sampler : hint_anisotropy; 						// UV motion sampler for shifting the normalmap
uniform vec2 	uv_sampler_scale = vec2(0.25, 0.25); 			// UV sampler scale
uniform float 	uv_sampler_strength = 0.04; 					// UV shifting strength

uniform sampler2D normalmap_a_sampler : hint_normal;			// Normalmap sampler A
uniform sampler2D normalmap_b_sampler : hint_normal;			// Normalmap sampler B

uniform sampler2D foam_sampler : hint_default_black;					// Foam sampler

// Volume settings:
uniform float 	refraction 		 = 0.075;						// Refraction of the water

uniform vec4 	color_deep : source_color;						// Color for deep places in the water, medium to dark blue
uniform vec4 	color_shallow : source_color;						// Color for lower places in the water, bright blue - green
uniform float 	beers_law		 = 2.0;							// Beers law value, regulates the blending size to the deep water level
uniform float 	depth_offset	 = -0.75;						// Offset for the blending

uniform sampler2DArray caustic_sampler : hint_default_black;			// Caustic sampler, (Texture array with 16 Textures for the animation)


group_uniforms Wave1;
uniform vec2 directionWave1;
uniform float amplitudeWave1;
uniform float steepnessWave1;
uniform float frequencyWave1;
uniform float speedWave1;

group_uniforms Wave2;
uniform vec2 directionWave2;
uniform float amplitudeWave2;
uniform float steepnessWave2;
uniform float frequencyWave2;
uniform float speedWave2;



vec3 gerstner_wave_position(vec2 position, float time, vec2 direction, float amp, float steep, float freq, float speed) {
    vec3 wave_position = vec3(0, 0, 0);
	float proj = dot(position, direction),
              phase = time * speed,
              theta = proj * freq + phase,
              height = amp * sin(theta);
	wave_position.y += height;
	
	float maximum_width = steep * amp,
			width = maximum_width * cos(theta),
			x = direction.x,
			y = direction.y;
	
	wave_position.x += x * width;
	wave_position.z += y * width;
	return wave_position;
}

vec3 gerstner_wave_normal(vec3 position, float time, vec2 direction, float amp, float steep, float freq, float speed) {
    vec3 wave_normal = vec3(0.0, 1.0, 0.0);
	float proj = dot(position.xz, direction),
		phase = time * speed,
		psi = proj * freq + phase,
		Af = amp * freq,
		alpha = Af * sin(psi);
	
	wave_normal.y -= steep * alpha;
	float x = direction.x,
		y = direction.y,
		omega = Af * cos(psi);

	wave_normal.x -= x * omega;
	wave_normal.z -= y * omega;
	return wave_normal;
}

// Vertex -> Fragment:
varying float 	vertex_height;									// Height of the water surface
varying vec3 	vertex_normal;									// Vertex normal -> Needed for refraction calculation
varying mat4 	inv_mvp; 										// Inverse ModelViewProjection matrix -> Needed for caustic projection

void vertex()
{	
	vec4	vertex			 = vec4(VERTEX, 1.0);
	vec3	vertex_position  = (INV_VIEW_MATRIX * vertex).xyz;
	vec3 tang = vec3(0.0, 0.0, 0.0);
	vec3 bin = vec3(0.0, 0.0, 0.0);
	
	vertex 			+= vec4(gerstner_wave_position(vertex_position.xz, TIME, directionWave1, amplitudeWave1, steepnessWave1, frequencyWave1, speedWave1), 1.0);
	vertex_normal  += gerstner_wave_normal(vertex.xyz, TIME, directionWave1, amplitudeWave1, steepnessWave1, frequencyWave1, speedWave1);
	vertex 			+= vec4(gerstner_wave_position(vertex_position.xz, TIME, directionWave2, amplitudeWave2, steepnessWave2, frequencyWave2, speedWave2), 1.0);
	vertex_normal  += gerstner_wave_normal(vertex.xyz, TIME, directionWave2, amplitudeWave2, steepnessWave2, frequencyWave2, speedWave2);
	// vertex 			+= wave(wave_c, vertex_position.xz, time, tang, bin);

	vertex_position  = vertex.xyz;

	vertex_height	 = (PROJECTION_MATRIX * MODELVIEW_MATRIX * vertex).z;

	NORMAL			 = vertex_normal;

	UV				 = vertex.xz * sampler_scale;

	VERTEX			 = vertex.xyz;
	
	inv_mvp = inverse(PROJECTION_MATRIX * MODELVIEW_MATRIX);
}

void fragment() {
	// Calculation of the UV with the UV motion sampler
	vec2	uv_offset 					 = sampler_direction * TIME;
	vec2 	uv_sampler_uv 				 = UV * uv_sampler_scale + uv_offset;
	vec2	uv_sampler_uv_offset 		 = uv_sampler_strength * texture(uv_sampler, uv_sampler_uv).rg * 2.0 - 1.0;
	vec2 	uv 							 = UV + uv_sampler_uv_offset;
	
	// Normalmap:
	vec3 	normalmap					 = texture(normalmap_a_sampler, uv - uv_offset*2.0).rgb * 0.75;		// 75 % sampler A
			normalmap 					+= texture(normalmap_b_sampler, uv + uv_offset).rgb * 0.25;			// 25 % sampler B
	
	// Refraction UV:
	vec3	ref_normalmap				 = normalmap * 2.0 - 1.0;
	//		ref_normalmap				 = normalize(vertex_tangent*ref_normalmap.x + vertex_binormal*ref_normalmap.y + vertex_normal*ref_normalmap.z);
	vec2 	ref_uv						 = SCREEN_UV + (ref_normalmap.xy * refraction) / vertex_height;
	
	// Ground depth:
	float 	depth_raw					 = texture(DEPTH_TEXTURE, ref_uv).r;
	float	depth						 = PROJECTION_MATRIX[3][2] / (depth_raw + PROJECTION_MATRIX[2][2]);
			
	float 	depth_blend 				 = exp((depth+VERTEX.z + depth_offset) * -beers_law);
			depth_blend 				 = clamp(1.0-depth_blend, 0.0, 1.0);	
	float	depth_blend_pow				 = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	// Ground color:
	vec3 	screen_color 				 = textureLod(SCREEN_TEXTURE, ref_uv, depth_blend_pow * 2.5).rgb;
	vec3 	dye_color 					 = mix(color_shallow.rgb, color_deep.rgb, depth_blend_pow);
	//vec3	color 						 = mix(screen_color*dye_color, dye_color*0.25, depth_blend_pow*0.5);
	vec3 color = color_shallow.rgb;
	
	// Caustic screen projection
	vec4 	caustic_screenPos 			 = vec4(ref_uv*2.0-1.0, depth_raw, 1.0);
	vec4 	caustic_localPos 			 = inv_mvp * caustic_screenPos;
			caustic_localPos			 = vec4(caustic_localPos.xyz/caustic_localPos.w, caustic_localPos.w);
	
	vec2 	caustic_Uv 					 = caustic_localPos.xz / vec2(1024.0) + 0.5;
	vec4	caustic_color				 = texture(caustic_sampler, vec3(caustic_Uv*300.0, mod(TIME*14.0, 16.0)));

	//color 						*= 1.0 + pow(caustic_color.r, 1.50) * (1.0-depth_blend) * 6.0;

	// Foam:
	if (depth + VERTEX.z < vertex_height-0.1)
	{
		float foam_noise = clamp(pow(texture(foam_sampler, (uv*4.0) - uv_offset).r, 10.0)*40.0, 0.0, 0.2);
		float foam_mix = clamp(pow((1.0-(depth + VERTEX.z) + foam_noise), 8.0) * foam_noise * 0.4, 0.0, 1.0);
		color = mix(color, vec3(1.0), foam_mix);
	}
	
	// Set all values:
	ALBEDO = color.xyz;
	METALLIC = 0.1;
	ROUGHNESS = 0.2;
	SPECULAR = 0.2 + depth_blend_pow * 0.4;
	NORMAL_MAP = normalmap;
	NORMAL_MAP_DEPTH = 1.25;
}
