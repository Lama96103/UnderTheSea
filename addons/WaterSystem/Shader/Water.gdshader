shader_type spatial;
render_mode 	cull_back,diffuse_burley,specular_schlick_ggx, blend_mix;

uniform vec4 color_shallow : source_color;	
uniform float wave_speed = 0.5;


uniform sampler2D normalmap_a_sampler : hint_normal;
uniform sampler2D normalmap_b_sampler : hint_normal;

uniform vec2 	sampler_scale 	 = vec2(0.25, 0.25); 			// Scale for the sampler
uniform vec2	sampler_direction= vec2(0.05, 0.04); 			// Direction and speed for the sampler offset

uniform sampler2D uv_sampler : hint_anisotropy; 						// UV motion sampler for shifting the normalmap
uniform vec2 	uv_sampler_scale = vec2(0.25, 0.25); 			// UV sampler scale
uniform float 	uv_sampler_strength = 0.04; 					// UV shifting strength


// Vertex -> Fragment:
varying float 	vertex_height;									// Height of the water surface
varying vec3 	vertex_normal;									// Vertex normal -> Needed for refraction calculation
varying vec3 	vertex_binormal;								// Vertex binormal -> Needed for refraction calculation
varying vec3 	vertex_tangent;									// Vertex tangent -> Needed for refraction calculation

varying mat4 	inv_mvp; 										// Inverse ModelViewProjection matrix -> Needed for caustic projection

// Wave function:
vec4 wave(vec4 parameter, vec2 position, float time, inout vec3 tangent, inout vec3 binormal)
{
	float	wave_steepness	 = parameter.z;
	float	wave_length		 = parameter.w;

	float	k				 = 2.0 * 3.14159265359 / wave_length;
	float 	c 				 = sqrt(9.8 / k);
	vec2	d				 = normalize(parameter.xy);
	float 	f 				 = k * (dot(d, position) - c * time);
	float 	a				 = wave_steepness / k;
	
	tangent			+= normalize(vec3(1.0-d.x * d.x * (wave_steepness * sin(f)), d.x * (wave_steepness * cos(f)), -d.x * d.y * (wave_steepness * sin(f))));
	binormal		+= normalize(vec3(-d.x * d.y * (wave_steepness * sin(f)), d.y * (wave_steepness * cos(f)), 1.0-d.y * d.y * (wave_steepness * sin(f))));

	return vec4(d.x * (a * cos(f)), a * sin(f) * 0.25, d.y * (a * cos(f)), 0.0);
}


void vertex()
{
	float	time			 = TIME * wave_speed;
	
	vec4	vertex			 = vec4(VERTEX, 1.0);
	vec3	vertex_position  = (INV_VIEW_MATRIX * vertex).xyz;

	vec3 tang = vec3(0.0, 0.0, 0.0);
	vec3 bin = vec3(0.0, 0.0, 0.0);
	
	vertex 			+= wave(vec4(0.1, 0.1, 0.1, 1), vertex_position.xz, time, tang, bin);
	vertex 			+= wave(vec4(-0.1, 0.1, 0.1, 1), vertex_position.xz, time, tang, bin);
	vertex 			+= wave(vec4(0.1, 0.4, 0.1, 1), vertex_position.xz, time, tang, bin);
	vertex_tangent 	 = tang;
	vertex_binormal  = bin;

	vertex_position  = vertex.xyz;

	vertex_height	 = (PROJECTION_MATRIX * MODELVIEW_MATRIX * vertex).z;

	TANGENT			 = vertex_tangent;
	BINORMAL		 = vertex_binormal;
	vertex_normal	 = normalize(cross(vertex_binormal, vertex_tangent));
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
			normalmap 					+= texture(normalmap_b_sampler, uv + uv_offset).rgb * 0.25;		


	// Set all values:
	ALBEDO = color_shallow.rgb;
	METALLIC = 0.1;
	ROUGHNESS = 0.2;
	SPECULAR = 0.2;
	NORMAL_MAP = normalmap;
	NORMAL_MAP_DEPTH = 1.25;
}
