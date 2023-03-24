using System;
using System.Collections.Generic;
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
        private Rid inputBuffer;
        private Rid resultBuffer;

        private Rid inputUniformSet;
        private Rid resultUniformSet;
        private Rid computePipeline;



        private InputBufferData noiseSettings = new InputBufferData();
        private int ChunkSize;

        public void Init(int chunkSize)
        {
            this.ChunkSize = chunkSize;

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
            // Create a local rendering device.
            rd = RenderingServer.CreateLocalRenderingDevice();

            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://MarchingCubes/Shader/marching_noise.glsl");
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            Rid shader = rd.ShaderCreateFromSpirV(shaderBytecode);


            InputBufferData data = new InputBufferData();
            inputBuffer = rd.StorageBufferCreate(InputBufferData.GetSize(), data.GetBytes());
            RDUniform inputBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0,
            };
            inputBufferUniform.AddId(inputBuffer);
            inputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { inputBufferUniform }, shader, 0);

            


            uint resultBufferSize = (sizeof(float) * 4) * (uint)(ChunkSize * ChunkSize * ChunkSize);
            resultBuffer = rd.StorageBufferCreate(resultBufferSize);
            RDUniform resultBufferUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0
            };
            resultBufferUniform.AddId(resultBuffer);
            resultUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { resultBufferUniform }, shader, 0);

            // Create a compute pipeline
            computePipeline = rd.ComputePipelineCreate(shader);
        }

        public void UpdateSettings(Vector3 rootPos, float noiseScale, float octaves, float persistence, float lacunarity)
        {
            noiseSettings = new InputBufferData
            {
                rootPos = rootPos,
                noiseScale = noiseScale,
                octaves = octaves,
                persistence = persistence,
                lacunarity = lacunarity
            };
        }

        public List<DataPoint> GenerateNoise(Vector3 rootPos, bool runOnGPU)
        {
            if(runOnGPU) return GenerateGPU(rootPos);
            return GenerateCPU(rootPos);
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

        private List<DataPoint> GenerateGPU(Vector3 rootPos)
        {
            Error err = rd.BufferUpdate(inputBuffer, 0, InputBufferData.GetSize(), noiseSettings.GetBytes());
            GD.Print("Updated input buffer " + err);
            long computeList = rd.ComputeListBegin();
            rd.ComputeListBindComputePipeline(computeList, computePipeline);
            rd.ComputeListBindUniformSet(computeList, inputUniformSet, 0);
            rd.ComputeListBindUniformSet(computeList, resultUniformSet, 1);
            rd.ComputeListDispatch(computeList, xGroups: 2, yGroups: 2, zGroups: 2);
            rd.ComputeListEnd();

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();

            // Read back the data from the buffers
            byte[] outputBytes = rd.BufferGetData(resultBuffer);
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

            return cubes;
        }
    }

    public class InputBufferData
    {
        public Vector3 rootPos = Vector3.Zero;
        public float noiseScale = 1;
        public float octaves = 4f;
        public float persistence = 0.5f;
        public float lacunarity = 2f;

        public byte[] GetBytes()
        {
            float[] data = new float[] {rootPos.X, rootPos.Y, rootPos.Z, noiseScale, octaves, persistence, lacunarity};
            byte[] dataBytes = new byte[data.Length * sizeof(float)];
            Buffer.BlockCopy(data, 0, dataBytes, 0, dataBytes.Length);
            return dataBytes;
        }

        public static uint GetSize()
        {
            return (sizeof(float) * 7);
        }
    }

    public struct DataPoint
    {
        public Vector3 Position;
        public float Value;
    }
}