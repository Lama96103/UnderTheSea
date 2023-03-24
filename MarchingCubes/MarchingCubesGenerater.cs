using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MarchingCubes
{
    public partial class MarchingCubesGenerater : Node3D
    {
        [Export(PropertyHint.Range, "-1,1")] public float SurfaceLevel = 0.5f;
        [Export] private int ChunkSize = 50;

		private MarchingCubesNoiseGen noiseGen = null;


		public float NoiseScale = 0.5f;
		public float Octaves = 4;
		public float Persistence = 0.5f;
		public float Lacunarity = 2f;

		private MeshInstance3D meshInstance3D = null;
		private List<DataPoint> noiseMap = null;


        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            GD.Randomize();
			
			noiseGen = new MarchingCubesNoiseGen();
			noiseGen.Init(ChunkSize);


        }

		

		public void UpdateNoise(Vector3 rootPos)
		{
			Stopwatch sp = Stopwatch.StartNew();
			noiseGen.UpdateSettings(rootPos, NoiseScale, Octaves, Persistence, Lacunarity);
			noiseMap = noiseGen.GenerateNoise(Vector3.Zero, true);
            sp.Stop();
			GD.Print("Took " + sp.Elapsed.TotalMilliseconds + " ms to generate noise");
		}

		public void UpdateMesh(Vector3 rootPos)
		{
			Stopwatch sp = Stopwatch.StartNew();
			GenerateChunk(Vector3.Zero, noiseMap);
			GD.Print("Took " + sp.Elapsed.TotalMilliseconds + " ms to generate mesh");
		}

     

 
		
		private int IndexFromCoords(int x, int y, int z) 
		{
			return z * ChunkSize * ChunkSize + y * ChunkSize + x;
		}
				
		private void GenerateChunk(Vector3 position, List<DataPoint> points)
		{
			if(meshInstance3D != null)
			{
				RemoveChild(meshInstance3D);
				meshInstance3D.QueueFree();
			}


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

            meshInstance3D = new MeshInstance3D();
            meshInstance3D.Mesh = mesh;
            this.AddChild(meshInstance3D);

            chunkGenSp.Stop();
            GD.Print("Took " + chunkGenSp.Elapsed.TotalSeconds + " s to generate chunk");
            GD.Print("Generated " + triangles + " triangles");
		}
 
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


