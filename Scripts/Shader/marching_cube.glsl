#[compute]
#version 450
#include "res://Scripts/Shader/Includes/march_table.glsl"

// Invocations in the (x, y, z) dimension
layout(local_size_x = 2, local_size_y = 1, local_size_z = 1) in;

// A binding to the buffer we create in our script
layout(set = 0, binding = 0, std430) restrict buffer MyDataBuffer {
    float data[];
}
my_data_buffer;

layout(set = 0, binding = 1, std430) restrict buffer MyOuputBuffer {
    float data[];
}
my_output_buffer;

const float surface_level = 0.5f;

// The code we want to execute in each invocation
void main() {
    // gl_GlobalInvocationID.x uniquely identifies this invocation across all work groups
    my_output_buffer.data[gl_GlobalInvocationID.x] = my_data_buffer.data[gl_GlobalInvocationID.x] * triTable[0][0] +1;
}