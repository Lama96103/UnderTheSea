#[compute]
#version 450

const int size = 20;

// Invocations in the (x, y, z) dimension
layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;


layout(set = 0, binding = 0, std430) restrict buffer NoiseBufferData {
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


    
    float noiseScale = 0.5;
    float octaves = 4;
    float persistence = 0.5;
    float lacunarity = 2;
    vec3 rootPos = vec3(0,0,0);

    vec3 pos = gl_GlobalInvocationID.xyz + rootPos;

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