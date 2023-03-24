#[compute]
#version 450

const int size = 50;

// Invocations in the (x, y, z) dimension
layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;


layout(set = 0, binding = 0, std430) restrict buffer InputBufferData {
    vec3 rootPos;
    float noiseScale;
    float octaves;
    float persistence;
    float lacunarity;
}
noise_input_data;

layout(set = 1, binding = 0, std430) restrict buffer NoiseBufferData {
    vec4 point[];
}
noise_buffer_data;



//#include "Includes/classic3d.glsl"
#include "Includes/simplex3d.glsl"

int indexFromCoord(int x, int y, int z) 
{
    return z * size * size + y * size + x;
}


// The code we want to execute in each invocation
void main() {
    // gl_GlobalInvocationID.x uniquely identifies this invocation across all work groups
    int index = indexFromCoord(int(gl_GlobalInvocationID.x), int(gl_GlobalInvocationID.y), int(gl_GlobalInvocationID.z));

    
    vec3 rootPos = noise_input_data.rootPos;
    vec3 pos = rootPos + gl_GlobalInvocationID.xyz;
    float noiseScale = noise_input_data.noiseScale;
    
    int octaves = int(noise_input_data.octaves);
    float persistence = noise_input_data.persistence;
    float lacunarity = noise_input_data.lacunarity;
    float weightMultiplier = 1;


    float noise = 0;

    float amplitude = 1;
    float frequency = noiseScale / 100;
    
    float weight = 1;

    for(int i = 0; i < octaves; i++)
    {
        float perlinValue = snoise(pos * frequency);

        noise += perlinValue * amplitude;

        amplitude *= persistence;
        frequency *= lacunarity;

    }

    float finalNoise = noise;
    


    noise_buffer_data.point[index] = vec4(pos, finalNoise);
}