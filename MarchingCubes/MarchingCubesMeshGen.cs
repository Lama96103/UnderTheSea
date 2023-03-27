using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Godot;

namespace MarchingCubes
{
    class MarchingCubesMeshGen
    {
        
        private RenderingDevice rd;
        private Rid shader;

        private Rid inputBuffer;
        private RDUniform inputBufferUniform;
        private Rid settingsBuffer;
        private RDUniform settingsBufferUniform;
        private Rid outputBuffer;
        private RDUniform ouputBufferUniform;

        private Rid inputUniformSet;
        private Rid ouputUniformSet;
        private Rid computePipeline;

        private uint ChunkSize;

        public void Init(int chunkSize, RenderingDevice rd)
        {
            ChunkSize = (uint)chunkSize;
            this.rd = rd;
            InitComputeShader();
        }

        private void InitComputeShader()
        {
            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://MarchingCubes/Shader/marching_cube.glsl");
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            shader = rd.ShaderCreateFromSpirV(shaderBytecode);

            // Create a compute pipeline
            computePipeline = rd.ComputePipelineCreate(shader);
        }


        public List<Vector3> GenerateMesh(byte[] points, float isolevel)
        {
            CreateBuffers(points, isolevel);

            long computeList = rd.ComputeListBegin();
            rd.ComputeListBindComputePipeline(computeList, computePipeline);
            rd.ComputeListBindUniformSet(computeList, inputUniformSet, 0);
            rd.ComputeListBindUniformSet(computeList, ouputUniformSet, 1);
            rd.ComputeListDispatch(computeList, xGroups: 1, yGroups: 1, zGroups: 1);
            rd.ComputeListEnd();

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();

            byte[] outputBytes = rd.BufferGetData(settingsBuffer);
            int[] triangleCoutArray = new int[3];
            Buffer.BlockCopy(outputBytes, 0, triangleCoutArray, 0, outputBytes.Length);

            int triangleCount = triangleCoutArray[1];
            GD.Print("Generated " + triangleCount + " triangles in shader");
            GD.Print("Vertex should be "  + (triangleCount * 3 ));


            outputBytes = rd.BufferGetData(outputBuffer);
            float[] verticesData = new float[triangleCount * 3 * 3];
            Buffer.BlockCopy(outputBytes, 0, verticesData, 0, triangleCount * 3 * sizeof(float));


            List<Vector3> vertices = new List<Vector3>();
            for(int i = 0; i < verticesData.Length-1; i+=3)
            {
                Vector3 vertex = new Vector3(verticesData[i+1], verticesData[i], verticesData[i+2]);   
                vertices.Add(vertex);
            }

            GD.Print("Vertices count " + vertices.Count);

            //for(int i = 0; i < 3; i++) GD.Print(vertices[i]);

            return vertices;
        }

        private void CreateBuffers(byte[] points, float isolevel)
        {

            inputBuffer = rd.StorageBufferCreate((uint)points.Length, points);
            inputBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0,
            };
            inputBufferUniform.AddId(inputBuffer);


            float[] input = new float[] {isolevel, 0.0f, (float)ChunkSize};
            byte[] inputBytes = new byte[input.Length * sizeof(float)];
            Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);
            settingsBuffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);
            settingsBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1,
            };
            settingsBufferUniform.AddId(settingsBuffer);
            inputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { inputBufferUniform, settingsBufferUniform }, shader, 0);


            uint maxTriangleCount = (ChunkSize * ChunkSize * ChunkSize) * 5;
            outputBuffer = rd.StorageBufferCreate((sizeof(float) * 3) * maxTriangleCount);
            ouputBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0
            };
            ouputBufferUniform.AddId(outputBuffer);
            ouputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { ouputBufferUniform }, shader, 1);
        }
    }
}