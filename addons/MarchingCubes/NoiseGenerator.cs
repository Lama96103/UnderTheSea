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
            shader = rd.ShaderCreateFromSpirV(shaderBytecode);


            InputNoiseBufferData data = new InputNoiseBufferData();
            inputBuffer = rd.StorageBufferCreate(InputNoiseBufferData.GetSize(), data.GetBytes());
            RDUniform inputBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0,
            };
            inputBufferUniform.AddId(inputBuffer);

            uint resultBufferSize = (sizeof(float) * 4) * (uint)(ChunkSize * ChunkSize * ChunkSize);
            currentDataBuffer = new PointsDataBuffer(rd, resultBufferSize);


            inputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { inputBufferUniform }, shader, 0);
            outputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { currentDataBuffer.Uniform }, shader, 1);
            GD.Print("Input uniform " + inputUniformSet.IsValid);
            GD.Print("Output uniform " + outputUniformSet.IsValid);
            

            // Create a compute pipeline
            computePipeline = rd.ComputePipelineCreate(shader);
            GD.Print("Pipeline is valied = " + computePipeline.IsValid);
        }

        public void UpdateSettings(Vector3 rootPos, float noiseScale, int octaves, float persistence, float lacunarity)
        {
            noiseSettings = new InputNoiseBufferData
            {
                rootPos = rootPos,
                noiseScale = noiseScale,
                octaves = octaves,
                persistence = persistence,
                lacunarity = lacunarity,
                chunkSize = (int)ChunkSize
            };
        }


        public PointsDataBuffer Generate()
        {
            long computeList = rd.ComputeListBegin();

            Stopwatch sp = Stopwatch.StartNew();
            //Error err = rd.BufferUpdate(inputBuffer, 0, InputNoiseBufferData.GetSize(), noiseSettings.GetBytes());
            //GD.Print("Updated input buffer " + err + " -> took " + sp.ElapsedMilliseconds + " ms");
            sp.Restart();

            rd.ComputeListBindComputePipeline(computeList, computePipeline);
            rd.ComputeListBindUniformSet(computeList, inputUniformSet, 0);
            rd.ComputeListBindUniformSet(computeList, outputUniformSet, 1);
            rd.ComputeListDispatch(computeList, xGroups: (uint)ChunkSize, yGroups: (uint)ChunkSize, zGroups: (uint)ChunkSize);
            rd.ComputeListEnd();
            

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();


            GD.Print("Executed compute shader in " + sp.ElapsedMilliseconds + " ms");
            sp.Restart();
            
            /*
            // Read back the data from the buffers
            byte[] outputBytes = rd.BufferGetData(currentDataBuffer.Buffer);
            GD.Print("Got data in  " + sp.ElapsedMilliseconds);
           
            
            float[] output = new float[ChunkSize * ChunkSize * ChunkSize * 4];
            Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

            float minVal = 0.5f;
            float maxVal = 0.5f;
            List<Vector4> points = new List<Vector4>((int)(ChunkSize * ChunkSize * ChunkSize));
            for(int i = 0; i < output.Length ; i+=4)
            {
                Vector4 point = new Vector4(output[i+0], output[i+1], output[i+2], output[i+3]);
                if(point.W > maxVal) maxVal = point.W;
                if(point.W < minVal) minVal = point.W;
                points.Add(point);
            }

            GD.Print("Min " + minVal);
            GD.Print("Max " + maxVal);
            */

            return currentDataBuffer;
        }

        private class InputNoiseBufferData
        {
            public Vector3 rootPos = Vector3.Zero;
            public float noiseScale = 0.5f;
            public int octaves = 4;
            public float persistence = 0.5f;
            public float lacunarity = 2f;
            public int chunkSize = 16;

            public byte[] GetBytes()
            {
                float[] data = new float[] {rootPos.X, rootPos.Y, rootPos.Z, noiseScale, (float)octaves, persistence, lacunarity, (float)chunkSize};
                byte[] dataBytes = new byte[data.Length * sizeof(float)];
                Buffer.BlockCopy(data, 0, dataBytes, 0, dataBytes.Length);
                return dataBytes;
            }

            public static uint GetSize()
            {
                return (sizeof(float) * 8);
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