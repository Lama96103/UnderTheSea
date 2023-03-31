using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Godot;

namespace MarchingCubes
{
    class NoiseGenerator
    {
        
        private RenderingDevice rd;
        private Rid shader;
        private Rid inputBuffer;

        private Rid inputUniformSet;
        private Rid outputUniformSet;
        private Rid computePipeline;

        private PointsDataBuffer currentDataBuffer;



        private InputNoiseBufferData noiseSettings = new InputNoiseBufferData();
        private uint ChunkSize;

        public void Init(RenderingDevice rd, uint chunkSize)
        {
            this.ChunkSize = chunkSize;
            this.rd = rd;
            GD.Print("Chunksize is " + chunkSize);

            InitComputeShader();
        }

        private void InitComputeShader()
        {
            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://addons/MarchingCubes/Shader/marching_noise.glsl");
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            shader = rd.ShaderCreateFromSpirV(shaderBytecode, "marching_noise");


            inputBuffer = rd.StorageBufferCreate(InputNoiseBufferData.GetSize());
            RDUniform inputBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0,
            };
            inputBufferUniform.AddId(inputBuffer);

            uint resultBufferSize = (sizeof(float) * 4) * ChunkSize * ChunkSize * ChunkSize;
            currentDataBuffer = new PointsDataBuffer(rd, resultBufferSize);


            inputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { inputBufferUniform }, shader, 0);
            outputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { currentDataBuffer.Uniform }, shader, 1);

            RDPipelineSpecializationConstant chunkSizeConstant = new RDPipelineSpecializationConstant();
            chunkSizeConstant.ConstantId = 0;
            chunkSizeConstant.Value = (int)ChunkSize;
            

            // Create a compute pipeline
            computePipeline = rd.ComputePipelineCreate(shader, new Godot.Collections.Array<RDPipelineSpecializationConstant> {chunkSizeConstant});
        }

        public void UpdateSettings(Vector3 rootPos, Vector3 offset, float noiseScale, int octaves, float persistence)
        {
            noiseSettings = new InputNoiseBufferData
            {
                rootPos = rootPos,
                noiseScale = noiseScale,
                octaves = octaves,
                persistence = persistence
            };
        }


        public List<Vector4> Generate()
        {

            byte[] data = noiseSettings.GetBytes();
            Error err = rd.BufferUpdate(inputBuffer, 0, (uint)data.Length, data);
            GD.Print("Update buffer " + data.Length + " bytes: " + err);

            long computeList = rd.ComputeListBegin();
            rd.ComputeListBindComputePipeline(computeList, computePipeline);
            rd.ComputeListBindUniformSet(computeList, inputUniformSet, 0);
            rd.ComputeListBindUniformSet(computeList, outputUniformSet, 1);
            rd.ComputeListDispatch(computeList, xGroups: (uint)ChunkSize, yGroups: (uint)ChunkSize, zGroups: (uint)ChunkSize);
            rd.ComputeListEnd();

            
            Stopwatch sp = Stopwatch.StartNew();

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();

            GD.Print("Executed compute shader in " + sp.ElapsedMilliseconds + " ms");
            sp.Restart();
            
            
            // Read back the data from the buffers
            byte[] outputBytes = rd.BufferGetData(currentDataBuffer.Buffer);
            GD.Print("Got data in  " + sp.ElapsedMilliseconds);
           
            
            float[] output = new float[ChunkSize * ChunkSize * ChunkSize * 4];
            Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

            List<Vector4> points = new List<Vector4>((int)(ChunkSize * ChunkSize * ChunkSize));
            for(int i = 0; i < output.Length ; i+=4)
            {
                Vector4 point = new Vector4(output[i+0], output[i+1], output[i+2], output[i+3]);
                points.Add(point);
            }
            
            

            return points;
        }

        [Serializable]
        private class InputNoiseBufferData
        {
            public Vector3 rootPos = Vector3.Zero;

            public float noiseScale = 0.5f;
            public float persistence = 0.5f;
            public int octaves = 4;




            public byte[] GetBytes()
            {
                float[] dataFloat = new float[] {rootPos.X, rootPos.Y, rootPos.Z, noiseScale, persistence};
                int[] dataInt = new int[] {octaves};

                int sizeFloatByte = dataFloat.Length * sizeof(float);
                int sizeIntByte = dataInt.Length * sizeof(int);

                byte[] dataBytes = new byte[sizeFloatByte + sizeIntByte];

                Buffer.BlockCopy(dataFloat, 0, dataBytes, 0, sizeFloatByte);
                Buffer.BlockCopy(dataInt, 0, dataBytes, sizeFloatByte, sizeIntByte);
                return dataBytes;
            }

            public static uint GetSize()
            {
                int countInts = 1;
                int countFloats = 2;
                int countVec3 = 1;
                return (uint)((sizeof(float) * 3 * countVec3) + (sizeof(float) * countFloats) + (sizeof(int) * countInts));
            }
        }
    }

    

    public class PointsDataBuffer
    {
        public Rid Buffer { get; private set; }
        public RDUniform Uniform { get; private set; }

        public uint Size { get; private set; }

        public PointsDataBuffer(RenderingDevice rd, uint size)
        {
            this.Size = size;
            Buffer = rd.StorageBufferCreate(size);
            Uniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0
            };
            Uniform.AddId(Buffer);
        }

    }
}