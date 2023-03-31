#[compute]
#version 450
#include "Includes/march_table.glsl"

layout (constant_id = 0) const int CHUNK_SIZE = 4;

struct Triangle
{
  vec3 vert1;
  vec3 vert2;
  vec3 vert3;
};


layout(set = 0, binding = 0, std430) restrict buffer InputData {
    vec4 points[];
}
input_data_buffer;

layout(set = 0, binding = 1, std430) restrict buffer SettingData {
    float isolevel;
    int triangleCount;
}
input_setting_buffer;




layout(set = 1, binding = 0, std430) restrict buffer OutputData {
    Triangle triangles[];
}
output_data_buffer;



int indexFromCoord(int x, int y, int z) 
{
    return z * CHUNK_SIZE * CHUNK_SIZE + y * CHUNK_SIZE + x;
}


vec3 VertexInterp(float surfaceLevel, vec4 p1, vec4 p2)
{
    if (abs(surfaceLevel - p1.w) < 0.00001f) return p1.xyz;
    if (abs(surfaceLevel - p2.w) < 0.00001f) return p2.xyz;
    if (abs(p1.w - p2.w) < 0.00001f) return p1.xyz;

    float mu = (surfaceLevel - p1.w) / (p2.w - p1.w);
    vec3 p;
    p.x = p1.x + mu * (p2.x - p1.x);
    p.y = p1.y + mu * (p2.y - p1.y);
    p.z = p1.z + mu * (p2.z - p1.z);
    return p;
}


layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;
void main() {

    int x = int(gl_GlobalInvocationID.x);
    int y = int(gl_GlobalInvocationID.y);
    int z = int(gl_GlobalInvocationID.z);

    int index0 = indexFromCoord(x    , y    , z);
    int index1 = indexFromCoord(x + 1, y    , z);
    int index2 = indexFromCoord(x + 1, y + 1, z);
    int index3 = indexFromCoord(x    , y + 1, z);
    int index4 = indexFromCoord(x    , y    , z + 1);
    int index5 = indexFromCoord(x + 1, y    , z + 1);
    int index6 = indexFromCoord(x + 1, y + 1, z + 1);
    int index7 = indexFromCoord(x    , y + 1, z + 1);

    float isolevel = input_setting_buffer.isolevel;
    int cubeIndex = 0;
    if (input_data_buffer.points[index0].w < isolevel) cubeIndex |= 1;
    if (input_data_buffer.points[index1].w < isolevel) cubeIndex |= 2;
    if (input_data_buffer.points[index2].w < isolevel) cubeIndex |= 4;
    if (input_data_buffer.points[index3].w < isolevel) cubeIndex |= 8;
    if (input_data_buffer.points[index4].w < isolevel) cubeIndex |= 16;
    if (input_data_buffer.points[index5].w < isolevel) cubeIndex |= 32;
    if (input_data_buffer.points[index6].w < isolevel) cubeIndex |= 64;
    if (input_data_buffer.points[index7].w < isolevel) cubeIndex |= 128;

    // Cube is entirely in/out of the surface 
	if (edgeTable[cubeIndex] == 0) return;

    // Find the vertices where the surface intersects the cube 
    vec3 vertList[12];
    if ((edgeTable[cubeIndex] & 1) != 0)
        vertList[0] = VertexInterp(isolevel, input_data_buffer.points[index0], input_data_buffer.points[index1]);
    if ((edgeTable[cubeIndex] & 2) != 0)
        vertList[1] = VertexInterp(isolevel, input_data_buffer.points[index1], input_data_buffer.points[index2]);
    if ((edgeTable[cubeIndex] & 4) != 0)
        vertList[2] = VertexInterp(isolevel, input_data_buffer.points[index2], input_data_buffer.points[index3]);
    if ((edgeTable[cubeIndex] & 8) != 0)
        vertList[3] = VertexInterp(isolevel, input_data_buffer.points[index3], input_data_buffer.points[index0]);
    if ((edgeTable[cubeIndex] & 16) != 0)
        vertList[4] = VertexInterp(isolevel, input_data_buffer.points[index4], input_data_buffer.points[index5]);
    if ((edgeTable[cubeIndex] & 32) != 0)
        vertList[5] = VertexInterp(isolevel, input_data_buffer.points[index5], input_data_buffer.points[index6]);
    if ((edgeTable[cubeIndex] & 64) != 0)
        vertList[6] = VertexInterp(isolevel, input_data_buffer.points[index6], input_data_buffer.points[index7]);
    if ((edgeTable[cubeIndex] & 128) != 0)
        vertList[7] = VertexInterp(isolevel, input_data_buffer.points[index7], input_data_buffer.points[index4]);
    if ((edgeTable[cubeIndex] & 256) != 0)
        vertList[8] = VertexInterp(isolevel, input_data_buffer.points[index0], input_data_buffer.points[index4]);
    if ((edgeTable[cubeIndex] & 512) != 0)
        vertList[9] = VertexInterp(isolevel, input_data_buffer.points[index1], input_data_buffer.points[index5]);
    if ((edgeTable[cubeIndex] & 1024) != 0)
        vertList[10] = VertexInterp(isolevel, input_data_buffer.points[index2], input_data_buffer.points[index6]);
    if ((edgeTable[cubeIndex] & 2048) != 0)
        vertList[11] = VertexInterp(isolevel, input_data_buffer.points[index3], input_data_buffer.points[index7]);

    for (int i = 0; triTable[cubeIndex][i] != -1; i += 3)
    {
        Triangle tri;
        tri.vert1 = vertList[triTable[cubeIndex][i]];
        tri.vert2 = vertList[triTable[cubeIndex][i + 2]];
        tri.vert3 = vertList[triTable[cubeIndex][i + 1]];
        int index = atomicAdd(input_setting_buffer.triangleCount, 1);
        output_data_buffer.triangles[index] = tri;
    }
}