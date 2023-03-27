using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Godot;

namespace MarchingCubes
{
    class MarchingCubesNoiseGen
    {
        private FastNoiseLite noise = null;
        private Godot.Collections.Dictionary<Vector3, float> cachedValues = new Godot.Collections.Dictionary<Vector3, float>();
        
        private RenderingDevice rd;
        private Rid shader;
        private Rid inputBuffer;
        private Rid resultBuffer;
        private RDUniform resultBufferUniform;

        private Rid mainUniformSet;
        private Rid computePipeline;



        private InputNoiseBufferData noiseSettings = new InputNoiseBufferData();
        private int ChunkSize;

        public void Init(int chunkSize, RenderingDevice rd)
        {
            this.ChunkSize = chunkSize;
            this.rd = rd;
            GD.Print("Chunksize is " + chunkSize);

            // Init cpu noise
            noise = new FastNoiseLite();
            noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
            noise.Seed = (int)GD.Randi();
            noise.FractalOctaves = 4;
            noise.Frequency = 1.0f / 20.0f;
            InitComputeShader();
        }

        private void InitComputeShader()
        {
            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://MarchingCubes/Shader/marching_noise.glsl");
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
            resultBuffer = rd.StorageBufferCreate(resultBufferSize);
            resultBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1
            };
            resultBufferUniform.AddId(resultBuffer);


            mainUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { inputBufferUniform,resultBufferUniform }, shader, 0);

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
                chunkSize = ChunkSize
            };
        }

        public byte[] GenerateNoise(Vector3 rootPos)
        {
            return GenerateGPU(rootPos);
        }

        private float GetValue(Vector3 pos)
        {
            if (cachedValues.ContainsKey(pos)) return cachedValues[pos];
            float value = (noise.GetNoise3D(pos.X, pos.Y, pos.Z) + 1) / 2;
            if (pos.Y < -(ChunkSize / 2) + 1) value = 1;
            cachedValues.Add(pos, value);
            return value;
        }

        private List<DataPoint> GenerateCPU(Vector3 rootPos)
        {
            float size = ChunkSize / 2;
            List<DataPoint> cubes = new List<DataPoint>(ChunkSize * ChunkSize * ChunkSize);

            for (int x = 0; x < ChunkSize; x += 1)
                for (int y = 0; y < ChunkSize; y += 1)
                    for (int z = 0; z < ChunkSize; z += 1)
                    {
                        DataPoint point = new DataPoint();
                        point.Position = new Vector3(x, y, z);
                        point.Value = GetValue(point.Position + rootPos);
                        cubes.Add(point);

                        /*
                        MarchingCube grid = new MarchingCube();
                        grid.Points = new Vector3[8];
                        grid.Values = new float[8];
                        grid.Points[0] = rootPos + new Vector3(x, y, z);
                        grid.Values[0] = GetValue(grid.Points[0]);
                        grid.Points[1] = new Vector3(x + CubeSize, y, z);
                        grid.Values[1] = GetValue(grid.Points[1]);

                        grid.Points[2] = new Vector3(x + CubeSize, y + CubeSize, z);
                        grid.Values[2] = GetValue(grid.Points[2]);
                        grid.Points[3] = new Vector3(x, y + CubeSize, z);
                        grid.Values[3] = GetValue(grid.Points[3]);

                        grid.Points[4] = new Vector3(x, y, z + CubeSize);
                        grid.Values[4] = GetValue(grid.Points[4]);
                        grid.Points[5] = new Vector3(x + CubeSize, y, z + CubeSize);
                        grid.Values[5] = GetValue(grid.Points[5]);

                        grid.Points[6] = new Vector3(x + CubeSize, y + CubeSize, z + CubeSize);
                        grid.Values[6] = GetValue(grid.Points[6]);
                        grid.Points[7] = new Vector3(x, y + CubeSize, z + CubeSize);
                        grid.Values[7] = GetValue(grid.Points[7]);
                        cubes.Add(grid);
                        */
                    }

            return cubes;
        }

        private byte[] GenerateGPU(Vector3 rootPos)
        {
            Stopwatch sp = Stopwatch.StartNew();
            Error err = rd.BufferUpdate(inputBuffer, 0, InputNoiseBufferData.GetSize(), noiseSettings.GetBytes());
            GD.Print("Updated input buffer " + err + " -> took " + sp.ElapsedMilliseconds + " ms");
            sp.Restart();

            long computeList = rd.ComputeListBegin();
            rd.ComputeListBindComputePipeline(computeList, computePipeline);
            rd.ComputeListBindUniformSet(computeList, mainUniformSet, 0);
            rd.ComputeListDispatch(computeList, xGroups: (uint)ChunkSize, yGroups: (uint)ChunkSize, zGroups: (uint)ChunkSize);
            rd.ComputeListEnd();
            

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();


            GD.Print("Executed compute shader in " + sp.ElapsedMilliseconds + " ms");
            sp.Restart();

            // Read back the data from the buffers
            byte[] outputBytes = rd.BufferGetData(resultBuffer);
            GD.Print("Got data in  " + sp.ElapsedMilliseconds);
           
            
            float[] output = new float[ChunkSize * ChunkSize * ChunkSize * 4];
            Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

            float minVal = 0.5f;
            float maxVal = 0.5f;
            List<DataPoint> cubes = new List<DataPoint>(ChunkSize * ChunkSize * ChunkSize);
            for(int i = 0; i < output.Length ; i+=4)
            {
                DataPoint point = new DataPoint();
                point.Position = new Vector3(output[i+0], output[i+1], output[i+2]);
                point.Value = output[i+3];
                if(point.Value > maxVal) maxVal = point.Value;
                if(point.Value < minVal) minVal = point.Value;
                cubes.Add(point);
            }

            GD.Print("Min " + minVal);
            GD.Print("Max " + maxVal);

            return outputBytes;
        }
    }

    public class InputNoiseBufferData
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

    public struct DataPoint
    {
        public Vector3 Position;
        public float Value;
    }
}