#[compute]
#version 450

#include "Includes/noise.glsl"

layout(set = 0, binding = 0, std430) restrict buffer InputBufferData {
    vec3 rootPos;
    float noiseScale;
    int octaves;
    float persistence;
    float lacunarity;
    int chunkSize;
}
noise_input_data;

layout(set = 0, binding = 1, std430) restrict buffer NoiseBufferData {
    vec4 point[];
}
noise_buffer_data;


int indexFromCoord(int x, int y, int z) 
{
    return z * noise_input_data.chunkSize * noise_input_data.chunkSize + y * noise_input_data.chunkSize + x;
}


layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;
void main() {

    int index = indexFromCoord(int(gl_GlobalInvocationID.x), int(gl_GlobalInvocationID.y), int(gl_GlobalInvocationID.z));

    
    vec3 pos = noise_input_data.rootPos + gl_GlobalInvocationID.xyz;
    
    //float finalNoise = perlin(pos);

    
    float frequency = noise_input_data.noiseScale / 100;
    float amplitude = 1;
    
    float weight = 1;
    float weightMultiplier = 1;

    float noise = 0;
    for(int i = 0; i < int(noise_input_data.octaves); i++)
    {
        float n = perlin(pos * frequency);
        float v = 1-abs(n);
        v = v * v;
        v *= weight;
        weight = max(min(v*weightMultiplier,1),0);

        noise += v * amplitude;

        amplitude *= noise_input_data.persistence;
        frequency *= noise_input_data.lacunarity;

    }

    


    noise_buffer_data.point[index] = vec4(pos, noise);
}