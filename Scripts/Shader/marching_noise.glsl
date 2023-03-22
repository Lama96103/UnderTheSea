#[compute]
#version 450

const int size = 20;

// Invocations in the (x, y, z) dimension
layout(local_size_x = size, local_size_y = size, local_size_z = size) in;


layout(set = 0, binding = 0, std430) restrict buffer NoiseBuffer {
    float data[];
}
noise_buffer;

#include "res://Scripts/Shader/Includes/simplex3d.glsl"

int indexFromCoord(int x, int y, int z) 
{
    return z * size * size + y * size + x;
}


// The code we want to execute in each invocation
void main() {
    // gl_GlobalInvocationID.x uniquely identifies this invocation across all work groups
    int index = indexFromCoord(int(gl_GlobalInvocationID.x), int(gl_GlobalInvocationID.y), int(gl_GlobalInvocationID.z));
    noise_buffer.data[index] = snoise(gl_GlobalInvocationID.xyz);
    //noise_buffer.data[index] = 1;

}