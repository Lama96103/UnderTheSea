using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Godot;

namespace MarchingCubes
{
    class PointMeshGenerator
    {
        
        private RenderingDevice rd;
        private Rid shader;


        private Rid settingsBuffer;
        private RDUniform settingsBufferUniform;
        private Rid outputBuffer;
        private RDUniform ouputBufferUniform;

        private Rid inputUniformSet;
        private Rid ouputUniformSet;
        private Rid computePipeline;

        private uint ChunkSize;

        public void Init(RenderingDevice rd, uint chunkSize)
        {
            ChunkSize = (uint)chunkSize;
            this.rd = rd;
            InitComputeShader();
        }

        private void InitComputeShader()
        {
            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://addons/MarchingCubes/Shader/marching_cube.glsl");
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            shader = rd.ShaderCreateFromSpirV(shaderBytecode);

            RDPipelineSpecializationConstant chunkSizeConstant = new RDPipelineSpecializationConstant();
            chunkSizeConstant.ConstantId = 0;
            chunkSizeConstant.Value = (int)ChunkSize;

            // Create a compute pipeline
            computePipeline = rd.ComputePipelineCreate(shader, new Godot.Collections.Array<RDPipelineSpecializationConstant> {chunkSizeConstant});

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

        public void Update(PointsDataBuffer inputBuffer, float isolevel)
        {
            float[] input = new float[] {isolevel, 0.0f};
            byte[] inputBytes = new byte[input.Length * sizeof(float)];
            Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);
            settingsBuffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);
            settingsBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1,
            };
            settingsBufferUniform.AddId(settingsBuffer);
            inputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> {  inputBuffer.Uniform, settingsBufferUniform,  }, shader, 0);
        }

        public List<Vector3> GenerateMesh()
        {
            long computeList = rd.ComputeListBegin();
            rd.DrawCommandBeginLabel("GeneratePointMesh", new Color(1,0,0,1));
            rd.ComputeListBindComputePipeline(computeList, computePipeline);
            rd.ComputeListBindUniformSet(computeList, inputUniformSet, 0);
            rd.ComputeListBindUniformSet(computeList, ouputUniformSet, 1);
            rd.ComputeListDispatch(computeList, xGroups: 1, yGroups: 1, zGroups: 1);
            rd.ComputeListEnd();

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();

            rd.DrawCommandEndLabel();

            byte[] outputBytes = rd.BufferGetData(settingsBuffer);
            int[] triangleCoutArray = new int[2];
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

       
    }
}