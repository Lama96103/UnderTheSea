shader_type spatial;

uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;


uniform vec4 	color_deep : source_color;						// Color for deep places in the water, medium to dark blue
uniform vec4 	color_shallow : source_color;						// Color for lower places in the water, bright blue - green
uniform float 	beers_law		 = 2.0;							// Beers law value, regulates the blending size to the deep water level
uniform float 	depth_offset	 = -0.75;						// Offset for the blending

uniform float metallic : hint_range(0.0, 1.0) = 0;
uniform float roughness : hint_range(0.0, 1.0) = 0.02;

uniform sampler2D texture_normal1;
uniform sampler2D texture_normal2;


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

group_uniforms WaveSettings;
uniform int gerstner_waves_length;
uniform vec2 waveDirection[9];
uniform float waveSpeed[9];
uniform float waveFrequency[9];
uniform float waveAmplitude[9];
uniform float waveSteepness[9];



vec3 gerstner_wave_normal(vec3 position, float time) {
    vec3 wave_normal = vec3(0.0, 1.0, 0.0);
    for (int i = 0; i < gerstner_waves_length; ++i) {
        float proj = dot(position.xz, waveDirection[i]),
              phase = time * waveSpeed[i],
              psi = proj * waveFrequency[i] + phase,
              Af = waveAmplitude[i] *
                   waveFrequency[i],
              alpha = Af * sin(psi);

        wave_normal.y -= waveSteepness[i] * alpha;

        float x = waveDirection[i].x,
              y = waveDirection[i].y,
              omega = Af * cos(psi);

        wave_normal.x -= x * omega;
        wave_normal.z -= y * omega;
    } return wave_normal;
}

vec3 gerstner_wave_position(vec2 position, float time, out vec3 vertPos) {
    vec3 wave_position = vec3(0, 0, 0);
	vertPos = vec3(position.x, 0, position.y);
    for (int i = 0; i < gerstner_waves_length; ++i) {
        float proj = dot(position, waveDirection[i]),
              phase = time * waveSpeed[i],
              theta = proj * waveFrequency[i] + phase,
              height = waveAmplitude[i] * sin(theta);

        wave_position.y += height;

        float maximum_width = waveSteepness[i] *
                              waveAmplitude[i],
              width = maximum_width * cos(theta),
              x = waveDirection[i].x,
              y = waveDirection[i].y;

        wave_position.x += x * width;
        wave_position.z += y * width;
    } 
	vertPos + wave_position;
	return wave_position;
}

vec3 gerstner_wave(vec2 position, float time, inout vec3 normal) {
	vec3 vertPos = vec3(0,0,0);
    vec3 wave_position = gerstner_wave_position(position, time, vertPos);
    normal = gerstner_wave_normal(vertPos, time);
    return wave_position; // Accumulated Gerstner Wave.
}

float fresnel(float amount, vec3 normal, vec3 view)
{
	return pow((1.0 - clamp(dot(normalize(normal), normalize(view)), 0.0, 1.0 )), amount);
}

varying float height;
varying vec3 world_pos;

void vertex()
{	
	vec3 vertex = VERTEX;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 vertex_normal = vec3(0, 1, 0);

	vertex += gerstner_wave(world_pos.xz, TIME, vertex_normal);
	
	NORMAL			 = vertex_normal;
	VERTEX			 = vertex.xyz;
	height = vertex.y;
}

void fragment() {
	
	float depth_texture = texture(DEPTH_TEXTURE, SCREEN_UV).r * 2.0 - 1.0;
	float depth = PROJECTION_MATRIX[3][2] / (depth_texture + PROJECTION_MATRIX[2][2]);
	float depth_blend = exp((depth+VERTEX.z + depth_offset) * -beers_law);
	depth_blend = clamp(1.0 - depth_blend, 0.0, 1.0);	
	float depth_blend_power = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	vec3 screen_color = textureLod(SCREEN_TEXTURE, SCREEN_UV, depth_blend_power * 2.5).rgb;
	vec3 depth_color = mix(color_shallow.rgb, color_deep.rgb, depth_blend_power);
	vec3 color = mix(screen_color * depth_color, depth_color * 0.25, depth_blend_power * 0.5);
	
	vec3 normal_blend = mix(texture(texture_normal1,UV + (directionWave1 * TIME * 0.01)).rgb, texture(texture_normal2,UV + (directionWave2 * TIME * 0.01)).rgb, 0.5);
	
	float fresnel = fresnel(5.0, NORMAL, VIEW);
	vec3 surface_color = mix(color_shallow, vec4(1,1,1,1), fresnel).rgb; // Interpolate albedo values by frensel
	
	//ALBEDO = mix(surface_color, color, 0.75);
	ALBEDO = color;
	METALLIC = metallic;
	ROUGHNESS = roughness;
	NORMAL_MAP = normal_blend;
}
