#[compute]
#version 450

#include "Includes/noise3d.glsl"

layout (constant_id = 0) const int CHUNK_SIZE = 4;

layout(set = 0, binding = 0, std430) restrict buffer InputBufferData {
    vec3 rootPos;
    float scale;
    float persistence;
    int octaves;
}
noise_input_data;

layout(set = 1, binding = 0, std430) restrict buffer NoiseBufferData {
    vec4 point[];
}
noise_buffer_data;


int indexFromCoord(int x, int y, int z) 
{
    return z * CHUNK_SIZE * CHUNK_SIZE + y * CHUNK_SIZE + x;
}


float CaculateNoise(vec3 pos, float persistence, float scale, int octaves)
{
    // Calc
    float height = pos.y / float(CHUNK_SIZE); 

    float maxAmp = 0;
    float amp = 1;
    float freq = scale / 100;
    float noise = 0;

    for(int i = 0; i < octaves; i++)
    {
        noise += snoise(pos * freq) * amp ;
        maxAmp += amp;
        amp *= persistence;
        freq *= 2;
    }

    noise /= maxAmp;

    //noise *= height;

    float high = 1;
    float low = 0;
    
    noise = noise * (high - low) / 2 + (high + low) / 2;
    return noise;
}


layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;
void main() {

    int index = indexFromCoord(int(gl_GlobalInvocationID.x), int(gl_GlobalInvocationID.y), int(gl_GlobalInvocationID.z));

    
    vec3 pos = noise_input_data.rootPos + gl_GlobalInvocationID.xyz;


    float noise = CaculateNoise(pos, noise_input_data.persistence, noise_input_data.scale, noise_input_data.octaves);

    float seaLevel = -100;
    float seaLevelWeigth = 1;
    float floorLevel = -200;
    float floorWeight = 0;

    if(pos.y >= seaLevel)
    {
        seaLevelWeigth = 0;
    }
    else if(pos.y >= seaLevel - 5)
    {
        float x = (abs(pos.y - seaLevel) / 10);
        seaLevelWeigth = mix(0,1, x);
    }

    if(pos.y <= floorLevel)
    {
        floorWeight = 1;
    }


    float finalNoise = noise * seaLevelWeigth + floorWeight;

    noise_buffer_data.point[index] = vec4(gl_GlobalInvocationID.xyz, finalNoise);
}