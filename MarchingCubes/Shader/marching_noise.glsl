#[compute]
#version 450

const int size = 20;

// Invocations in the (x, y, z) dimension
layout(local_size_x = 10, local_size_y = 10, local_size_z = 10) in;


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



#include "Includes/simplex3d.glsl"
//#include "Includes/periodic3d.glsl"

int indexFromCoord(int x, int y, int z) 
{
    return z * size * size + y * size + x;
}


// The code we want to execute in each invocation
void main() {
    // gl_GlobalInvocationID.x uniquely identifies this invocation across all work groups
    int index = indexFromCoord(int(gl_GlobalInvocationID.x), int(gl_GlobalInvocationID.y), int(gl_GlobalInvocationID.z));


    
    float noiseScale = noise_input_data.noiseScale;
    float octaves = noise_input_data.octaves;
    float persistence = noise_input_data.persistence;
    float lacunarity = noise_input_data.lacunarity;
    vec3 rootPos = noise_input_data.rootPos;

    vec3 pos = rootPos + gl_GlobalInvocationID.xyz;

    float noise = 0;

    float frequency = noiseScale / 100;
    float amplitude = 1;
    float weight = 1;

    for(int j = 0; j < octaves; j++)
    {
        float n = snoise(pos * frequency);
        float v = 1 - abs(n);
        v = v * v;
        v *= weight;
        noise = max(min(v * weight, 1), 0);
        noise += v * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    float finalNoise = noise * weight;


    //float noiseValue = pnoise(vec3(0.5, 0.5, 0.5),gl_GlobalInvocationID.xyz);
    noise_buffer_data.point[index] = vec4(pos, finalNoise);
}