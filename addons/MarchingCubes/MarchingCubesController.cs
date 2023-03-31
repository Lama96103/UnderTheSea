using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MarchingCubes
{
    public partial class MarchingCubesController : Node3D
    {
        [Export(PropertyHint.Range, "0,1")] public float SurfaceLevel = 0.5f;
        [Export] private uint chunkSize = 32;
		public uint ChunkSize {get{ return chunkSize-1;}}

		private NoiseGenerator noiseGen = null;
		private PointMeshGenerator meshGen = null;


		[Export] private Vector3 noiseOffset;
		public float NoiseScale = 1;
		public int Octaves = 4;
		public float Persistence = 0.5f;

		private RenderingDevice renderingDevice;

		private Dictionary<Vector3, List<Vector4>> pointData = new Dictionary<Vector3, List<Vector4>>();
		private Dictionary<Vector3, MeshInstance3D> meshData = new Dictionary<Vector3, MeshInstance3D>();

        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {	
			GD.Randomize();
			// noiseOffset = new Vector3(GD.Randf() * 100, GD.Randf() * 100,GD.Randf() * 100);
			noiseOffset = Vector3.Zero;
            this.CallDeferred("Init");
        }

        private void Init()
        {
            renderingDevice = RenderingServer.CreateLocalRenderingDevice();
			
			noiseGen = new NoiseGenerator();
			noiseGen.Init(renderingDevice, chunkSize);

			meshGen = new PointMeshGenerator();
			meshGen.Init(renderingDevice, chunkSize);

            Generate(Vector3.Zero, true);
        }

        

        public void Generate(Vector3 position, bool createNoise)
        {
			GD.Print("Generating chunk " + position);
			List<Vector4> currentData = null;
			if(!pointData.ContainsKey(position)) pointData.Add(position, null);
			if(!meshData.ContainsKey(position)) meshData.Add(position, null);

            if(createNoise)
            {
				Stopwatch watchNoise = Stopwatch.StartNew(); 
                noiseGen.UpdateSettings(position, noiseOffset, NoiseScale, Octaves, Persistence);
                currentData =  noiseGen.Generate();
				pointData[position] = currentData;
				watchNoise.Stop();
				GD.Print("Took " + watchNoise.ElapsedMilliseconds + " ms to generate noise.");
            }
			else
			{
				currentData = pointData[position];
			}

			if(currentData == null)
			{
				GD.PushWarning("No point data was created for " + position);
				return;
			}
           
			Stopwatch watchVertex = Stopwatch.StartNew(); 
            //meshGen.Update(data, SurfaceLevel);
            //List<Vector3> vertiecs = meshGen.GenerateMesh();
            List<Vector3> vertices = GenerateVertices(currentData);

			watchVertex.Stop();
			GD.Print("Took " + watchVertex.ElapsedMilliseconds + " ms to generate verts.");


			Stopwatch watchMesh = Stopwatch.StartNew(); 
            GenerateMesh(position, vertices);
			watchMesh.Stop();
			GD.Print("Took " + watchMesh.ElapsedMilliseconds + " ms to generate mesh.");
        }

        private int IndexFromCoords(int x, int y, int z) 
		{
			return z * (int)chunkSize * (int)chunkSize + y * (int)chunkSize + x;
		}

        private List<Vector3> GenerateVertices(List<Vector4> points)
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int x = 0; x < chunkSize-1; x += 1)
			for (int y = 0; y < chunkSize-1; y += 1)
			for (int z = 0; z < chunkSize-1; z += 1)
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
				if (points[index0].W < SurfaceLevel) cubeIndex |= 1;
				if (points[index1].W < SurfaceLevel) cubeIndex |= 2;
				if (points[index2].W < SurfaceLevel) cubeIndex |= 4;
				if (points[index3].W < SurfaceLevel) cubeIndex |= 8;
				if (points[index4].W < SurfaceLevel) cubeIndex |= 16;
				if (points[index5].W < SurfaceLevel) cubeIndex |= 32;
				if (points[index6].W < SurfaceLevel) cubeIndex |= 64;
				if (points[index7].W < SurfaceLevel) cubeIndex |= 128;

				// Cube is entirely in/out of the surface 
				if (MarchingCubeHelper.EdgeTable[cubeIndex] == 0) continue;


				// Find the vertices where the surface intersects the cube 
				Vector3[] vertList = new Vector3[12];
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1) != 0)
					vertList[0] = VertexInterp(SurfaceLevel, points[index0], points[index1]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2) != 0)
					vertList[1] = VertexInterp(SurfaceLevel, points[index1], points[index2]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 4) != 0)
					vertList[2] = VertexInterp(SurfaceLevel, points[index2], points[index3]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 8) != 0)
					vertList[3] = VertexInterp(SurfaceLevel, points[index3], points[index0]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 16) != 0)
					vertList[4] = VertexInterp(SurfaceLevel, points[index4], points[index5]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 32) != 0)
					vertList[5] = VertexInterp(SurfaceLevel, points[index5], points[index6]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 64) != 0)
					vertList[6] = VertexInterp(SurfaceLevel, points[index6], points[index7]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 128) != 0)
					vertList[7] = VertexInterp(SurfaceLevel, points[index7], points[index4]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 256) != 0)
					vertList[8] = VertexInterp(SurfaceLevel, points[index0], points[index4]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 512) != 0)
					vertList[9] = VertexInterp(SurfaceLevel, points[index1], points[index5]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1024) != 0)
					vertList[10] = VertexInterp(SurfaceLevel, points[index2], points[index6]);
				if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2048) != 0)
					vertList[11] = VertexInterp(SurfaceLevel, points[index3], points[index7]);


				for (int i = 0; MarchingCubeHelper.TriTable[cubeIndex, i] != -1; i += 3)
				{
					foreach (int index in new int[] { 0, 2, 1 })
					{
						Vector3 vertex = vertList[MarchingCubeHelper.TriTable[cubeIndex, i + index]];
						vertices.Add(vertex);
					}
				}
			}
            return vertices;
        }

        private Vector3 VertexInterp(float surfaceLevel, Vector4 p1, Vector4 p2)
        {
            if (Mathf.Abs(surfaceLevel - p1.W) < 0.00001f) return new Vector3(p1.X, p1.Y, p1.Z);
            if (Mathf.Abs(surfaceLevel - p2.W) < 0.00001f) return new Vector3(p2.X, p2.Y, p2.Z);
            if (Mathf.Abs(p1.W - p2.W) < 0.00001f) return new Vector3(p1.X, p1.Y, p1.Z);
            float mu = (surfaceLevel - p1.W) / (p2.W - p1.W);
            Vector3 p = Vector3.Zero;
            p.X = p1.X + mu * (p2.X - p1.X);
            p.Y = p1.Y + mu * (p2.Y - p1.Y);
            p.Z = p1.Z + mu * (p2.Z - p1.Z);
            return p;
        }


        private void GenerateMesh(Vector3 rootPos, List<Vector3> vertices)
		{
			if(meshData[rootPos] != null)
			{
				RemoveChild(meshData[rootPos]);
				meshData[rootPos].QueueFree();
				meshData[rootPos] = null;
			}

			ArrayMesh mesh = new ArrayMesh();
            SurfaceTool tool = new SurfaceTool();
			tool.Begin(Mesh.PrimitiveType.Triangles);
			foreach(Vector3 v in vertices)
			{
				tool.SetColor(new Color(1, 1, 1, 1));
				//tool.SetUV(uv[i]);
				tool.AddVertex(v);
			}

			Stopwatch sp = Stopwatch.StartNew();
            //tool.Index();
            tool.GenerateNormals();
            // tool.GenerateTangents();
            tool.Commit(mesh);
            sp.Stop();
            GD.Print("Took " + sp.Elapsed.TotalMilliseconds + " ms to optimize mesh");

            MeshInstance3D meshInstance3D = new MeshInstance3D();
			meshInstance3D.Name = "Chunk " + rootPos;
            meshInstance3D.Mesh = mesh;
            this.AddChild(meshInstance3D);
			meshInstance3D.Position = rootPos;
			meshData[rootPos] = meshInstance3D;
		}
		

		
    }
}


