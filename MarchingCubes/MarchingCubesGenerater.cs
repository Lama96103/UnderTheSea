using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MarchingCubes
{
    public partial class MarchingCubesGenerater : Node3D
    {
        [Export(PropertyHint.Range, "-1,1")] private float SurfaceLevel = 0.5f;
        [Export] private int ChunkSize = 20;


        private FastNoiseLite noise = new FastNoiseLite();

        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            GD.Randomize();
			
			MarchingCubesNoiseGen noiseGen = new MarchingCubesNoiseGen();
			noiseGen.Init(ChunkSize);

			Stopwatch sp = Stopwatch.StartNew();
			//var result = noiseGen.GenerateNoise(Vector3.Zero, false);
            sp.Stop();
			GD.Print("Took " + sp.Elapsed.TotalMilliseconds + " ms to generate [CPU]");
			//GD.Print(result.Count + " entries");

			sp.Restart();
			var result = noiseGen.GenerateNoise(Vector3.Zero, false);
            sp.Stop();
			GD.Print("Took " + sp.Elapsed.TotalMilliseconds + " ms to generate [GPU]");
			GD.Print(result.Count + " entries");


			GenerateChunk(Vector3.Zero, result);
            //PerformanceTest();

            //GenerateChunk(Vector3.Zero);
            //GenerateChunk(Vector3.Forward * ChunkSize);
        }

     

        private void GenerateChunkComputeShader(Vector3 position)
        {
            // Create a local rendering device.
            RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();

            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://MarchingCubes/Shader/marching_noise.glsl");
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            Rid shader = rd.ShaderCreateFromSpirV(shaderBytecode);




            // Prepare our data. We use floats in the shader, so we need 32 bit.
            //float[] input = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            //byte[] inputBytes = new byte[input.Length * sizeof(float)];
            //Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);

            // Create a storage buffer that can hold our float values.
            // Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
            //Rid buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);
            Rid outputBuffer = rd.StorageBufferCreate((uint)(ChunkSize * ChunkSize * ChunkSize) * sizeof(float));

            // Create a uniform to assign the buffer to the rendering device
            /*RDUniform uniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0
            };
            uniform.AddId(buffer);
            */
            RDUniform uniformOutput = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0
            };
            uniformOutput.AddId(outputBuffer);

            Rid uniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { uniformOutput }, shader, 0);

            // Create a compute pipeline
            Rid pipeline = rd.ComputePipelineCreate(shader);
            long computeList = rd.ComputeListBegin();
            rd.ComputeListBindComputePipeline(computeList, pipeline);
            rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
            rd.ComputeListDispatch(computeList, xGroups: 5, yGroups: 1, zGroups: 1);
            rd.ComputeListEnd();

            // Submit to GPU and wait for sync
            rd.Submit();
            rd.Sync();

            // Read back the data from the buffers
            byte[] outputBytes = rd.BufferGetData(outputBuffer);
            float[] output = new float[ChunkSize * ChunkSize * ChunkSize];
            Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

            GD.Print("Output: ", output.Join());
        }
		
		private int IndexFromCoords(int x, int y, int z) 
		{
			return z * ChunkSize * ChunkSize + y * ChunkSize + x;
		}
				
		private void GenerateChunk(Vector3 position, List<DataPoint> points)
		{
			Stopwatch chunkGenSp = Stopwatch.StartNew();
			ArrayMesh mesh = new ArrayMesh();
            SurfaceTool tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);

            int triangles = 0;
			for (int x = 0; x < ChunkSize-1; x += 1)
			for (int y = 0; y < ChunkSize-1; y += 1)
			for (int z = 0; z < ChunkSize-1; z += 1)
			{
				int index0 = IndexFromCoords(x    , y    , z);
				int index1 = IndexFromCoords(x + 1, y    , z);
				int index2 = IndexFromCoords(x + 1, y + 1, z);
				int index3 = IndexFromCoords(x    , y + 1, z);
				int index4 = IndexFromCoords(x    , y    , z + 1);
				int index5 = IndexFromCoords(x + 1, y    , z + 1);
				int index6 = IndexFromCoords(x + 1, y + 1, z + 1);
				int index7 = IndexFromCoords(x    , y + 1, z + 1);

				int cubeIndex = 0;
				if (points[index0].Value < SurfaceLevel) cubeIndex |= 1;
				if (points[index1].Value < SurfaceLevel) cubeIndex |= 2;
				if (points[index2].Value < SurfaceLevel) cubeIndex |= 4;
				if (points[index3].Value < SurfaceLevel) cubeIndex |= 8;
				if (points[index4].Value < SurfaceLevel) cubeIndex |= 16;
				if (points[index5].Value < SurfaceLevel) cubeIndex |= 32;
				if (points[index6].Value < SurfaceLevel) cubeIndex |= 64;
				if (points[index7].Value < SurfaceLevel) cubeIndex |= 128;

				// Cube is entirely in/out of the surface 
				if (MarchingCubeHelper.EdgeTable[cubeIndex] == 0) continue;


				// Find the vertices where the surface intersects the cube 
				Vector3[] vertList = new Vector3[12];
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1) != 0)
					vertList[0] = VertexInterp(SurfaceLevel, points[index0].Position, points[index1].Position,
					 points[index0].Value, points[index1].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2) != 0)
					vertList[1] = VertexInterp(SurfaceLevel, points[index1].Position, points[index2].Position,
					 points[index1].Value, points[index2].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 4) != 0)
					vertList[2] = VertexInterp(SurfaceLevel, points[index2].Position, points[index3].Position,
					 points[index2].Value, points[index3].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 8) != 0)
					vertList[3] = VertexInterp(SurfaceLevel, points[index3].Position, points[index0].Position,
					 points[index3].Value, points[index0].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 16) != 0)
					vertList[4] = VertexInterp(SurfaceLevel, points[index4].Position, points[index5].Position,
					 points[index4].Value, points[index5].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 32) != 0)
					vertList[5] = VertexInterp(SurfaceLevel, points[index5].Position, points[index6].Position,
					 points[index5].Value, points[index6].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 64) != 0)
					vertList[6] = VertexInterp(SurfaceLevel, points[index6].Position, points[index7].Position,
					 points[index6].Value, points[index7].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 128) != 0)
					vertList[7] = VertexInterp(SurfaceLevel, points[index7].Position, points[index4].Position,
					 points[index7].Value, points[index4].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 256) != 0)
					vertList[8] = VertexInterp(SurfaceLevel, points[index0].Position, points[index4].Position,
					 points[index0].Value, points[index4].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 512) != 0)
					vertList[9] = VertexInterp(SurfaceLevel, points[index1].Position, points[index5].Position,
					 points[index1].Value, points[index5].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1024) != 0)
					vertList[10] = VertexInterp(SurfaceLevel, points[index2].Position, points[index6].Position,
					 points[index2].Value, points[index6].Value);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2048) != 0)
					vertList[11] = VertexInterp(SurfaceLevel, points[index3].Position, points[index7].Position,
					 points[index3].Value, points[index7].Value);

				Godot.Collections.Array<Vector3> vertices = new Godot.Collections.Array<Vector3>();
				Godot.Collections.Array<Vector2> uv = new Godot.Collections.Array<Vector2>();
				for (int i = 0; MarchingCubeHelper.TriTable[cubeIndex, i] != -1; i += 3)
				{
					foreach (int index in new int[] { 0, 2, 1 })
					{
						Vector3 vertex = vertList[MarchingCubeHelper.TriTable[cubeIndex, i + index]];
						vertices.Add(vertex);
						uv.Add(new Vector2(vertex.X, vertex.Z));
					}
					triangles++;
				}

				for (int i = 0; i < vertices.Count; i++)
				{
					tool.SetColor(new Color(1, 1, 1, 1));
					tool.SetUV(uv[i]);
					tool.AddVertex(vertices[i]);
				}
			}

			Stopwatch sp = Stopwatch.StartNew();
            //tool.Index();
            tool.GenerateNormals();
            // tool.GenerateTangents();
            tool.Commit(mesh);
            sp.Stop();
            GD.Print("Took " + sp.Elapsed.TotalSeconds + " s to generate mesh");

            MeshInstance3D meshInstance3D = new MeshInstance3D();
            meshInstance3D.Mesh = mesh;
            this.AddChild(meshInstance3D);

            chunkGenSp.Stop();
            GD.Print("Took " + chunkGenSp.Elapsed.TotalSeconds + " s to generate chunk");
            GD.Print("Generated " + triangles + " triangles");
		}
 

		/*
        private void GenerateChunk(Vector3 position, System.Collections.Generic.List<MarchingCube> cubes)
        {
            Stopwatch chunkGenSp = Stopwatch.StartNew();
            float size = ChunkSize / 2;
            float height = ChunkSize / 2;
            ArrayMesh mesh = new ArrayMesh();
            SurfaceTool tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            int triangles = 0;

			foreach(MarchingCube cube in cubes)
			{
				int cubeIndex = 0;
				Stopwatch s = Stopwatch.StartNew();

				if (cube.Values[0] < SurfaceLevel) cubeIndex |= 1;
				if (cube.Values[1] < SurfaceLevel) cubeIndex |= 2;
				if (cube.Values[2] < SurfaceLevel) cubeIndex |= 4;
				if (cube.Values[3] < SurfaceLevel) cubeIndex |= 8;
				if (cube.Values[4] < SurfaceLevel) cubeIndex |= 16;
				if (cube.Values[5] < SurfaceLevel) cubeIndex |= 32;
				if (cube.Values[6] < SurfaceLevel) cubeIndex |= 64;
				if (cube.Values[7] < SurfaceLevel) cubeIndex |= 128;

				// Cube is entirely in/out of the surface 
				if (MarchingCubeHelper.EdgeTable[cubeIndex] == 0) continue;


				// Find the vertices where the surface intersects the cube 
				Vector3[] vertList = new Vector3[12];
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1) != 0)
					vertList[0] = VertexInterp(SurfaceLevel, cube.Points[0], cube.Points[1], cube.Values[0], cube.Values[1]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2) != 0)
					vertList[1] = VertexInterp(SurfaceLevel, cube.Points[1], cube.Points[2], cube.Values[1], cube.Values[2]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 4) != 0)
					vertList[2] = VertexInterp(SurfaceLevel, cube.Points[2], cube.Points[3], cube.Values[2], cube.Values[3]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 8) != 0)
					vertList[3] = VertexInterp(SurfaceLevel, cube.Points[3], cube.Points[0], cube.Values[3], cube.Values[0]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 16) != 0)
					vertList[4] = VertexInterp(SurfaceLevel, cube.Points[4], cube.Points[5], cube.Values[4], cube.Values[5]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 32) != 0)
					vertList[5] = VertexInterp(SurfaceLevel, cube.Points[5], cube.Points[6], cube.Values[5], cube.Values[6]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 64) != 0)
					vertList[6] = VertexInterp(SurfaceLevel, cube.Points[6], cube.Points[7], cube.Values[6], cube.Values[7]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 128) != 0)
					vertList[7] = VertexInterp(SurfaceLevel, cube.Points[7], cube.Points[4], cube.Values[7], cube.Values[4]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 256) != 0)
					vertList[8] = VertexInterp(SurfaceLevel, cube.Points[0], cube.Points[4], cube.Values[0], cube.Values[4]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 512) != 0)
					vertList[9] = VertexInterp(SurfaceLevel, cube.Points[1], cube.Points[5], cube.Values[1], cube.Values[5]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1024) != 0)
					vertList[10] = VertexInterp(SurfaceLevel, cube.Points[2], cube.Points[6], cube.Values[2], cube.Values[6]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2048) != 0)
					vertList[11] = VertexInterp(SurfaceLevel, cube.Points[3], cube.Points[7], cube.Values[3], cube.Values[7]);




				Godot.Collections.Array<Vector3> vertices = new Godot.Collections.Array<Vector3>();
				Godot.Collections.Array<Vector2> uv = new Godot.Collections.Array<Vector2>();
				for (int i = 0; MarchingCubeHelper.TriTable[cubeIndex, i] != -1; i += 3)
				{
					foreach (int index in new int[] { 0, 2, 1 })
					{
						Vector3 vertex = vertList[MarchingCubeHelper.TriTable[cubeIndex, i + index]];
						vertices.Add(vertex);
						uv.Add(new Vector2(vertex.X, vertex.Z));
					}
					triangles++;
				}

				for (int i = 0; i < vertices.Count; i++)
				{
					tool.SetColor(new Color(1, 1, 1, 1));
					tool.SetUV(uv[i]);
					tool.AddVertex(vertices[i]);
				}

				s.Stop();
				GD.Print("Took " + s.Elapsed.TotalMilliseconds + " s to generate cube mesh");
			}

            Stopwatch sp = Stopwatch.StartNew();
            //tool.Index();
            tool.GenerateNormals();
            // tool.GenerateTangents();
            tool.Commit(mesh);
            sp.Stop();
            GD.Print("Took " + sp.Elapsed.TotalSeconds + " s to generate mesh");

            MeshInstance3D meshInstance3D = new MeshInstance3D();
            meshInstance3D.Mesh = mesh;
            this.AddChild(meshInstance3D);

            chunkGenSp.Stop();
            GD.Print("Took " + chunkGenSp.Elapsed.TotalSeconds + " s to generate chunk");
            GD.Print("Generated " + triangles + " triangles");
        }
*/
        private Vector3 VertexInterp(float surfaceLevel, Vector3 p1, Vector3 p2, float val1, float val2)
        {
            if (Mathf.Abs(surfaceLevel - val1) < 0.00001f) return p1;
            if (Mathf.Abs(surfaceLevel - val2) < 0.00001f) return p2;
            if (Mathf.Abs(val1 - val2) < 0.00001f) return p1;
            float mu = (surfaceLevel - val1) / (val2 - val1);
            Vector3 p = Vector3.Zero;
            p.X = p1.X + mu * (p2.X - p1.X);
            p.Y = p1.Y + mu * (p2.Y - p1.Y);
            p.Z = p1.Z + mu * (p2.Z - p1.Z);
            return p;
        }
		
    }
}


