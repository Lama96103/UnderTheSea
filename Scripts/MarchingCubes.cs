using Godot;
using System;
using System.Diagnostics;

public partial class MarchingCubes : Node3D
{
	[Export(PropertyHint.Range,"-1,1")] private float SurfaceLevel = 0.5f;
	[Export] private float CubeSize = 1;
	[Export] private int ChunkSize = 20;


	private FastNoiseLite noise = new FastNoiseLite();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Randomize();

		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
    	noise.Seed = (int)GD.Randi();
    	noise.FractalOctaves = 4;
    	noise.Frequency = 1.0f / 20.0f;

		PerformanceTest();
		
		//GenerateChunk(Vector3.Zero);
		//GenerateChunk(Vector3.Forward * ChunkSize);
	}

	Godot.Collections.Dictionary<Vector3, float> cachedValues = new Godot.Collections.Dictionary<Vector3, float>();
	private float GetValue(Vector3 pos)
	{
		if(cachedValues.ContainsKey(pos)) return cachedValues[pos];
		float value = (noise.GetNoise3D(pos.X, pos.Y, pos.Z) + 1) / 2;
		if(pos.Y < -(ChunkSize/2)+1) value = 1;
		cachedValues.Add(pos, value);
		return value;
	}

	private void GenerateChunkComputeShader(Vector3 position)
	{
		// Create a local rendering device.
		RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();

		// Load GLSL shader
		RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://Scripts/Shader/marching_noise.glsl");
		RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
		Rid shader = rd.ShaderCreateFromSpirV(shaderBytecode);


		int size = 20;


		// Prepare our data. We use floats in the shader, so we need 32 bit.
		//float[] input = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
		//byte[] inputBytes = new byte[input.Length * sizeof(float)];
		//Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);

		// Create a storage buffer that can hold our float values.
		// Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
		//Rid buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);
		Rid outputBuffer = rd.StorageBufferCreate((uint)(size*size*size) * sizeof(float));

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
		float[] output = new float[size*size*size];
		Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

		GD.Print("Output: ", output.Join());
	}

	private void PerformanceTest()
	{
		Vector3 position = Vector3.Zero;

		Stopwatch s = Stopwatch.StartNew();
		GenerateChunkComputeShader(Vector3.Zero);
		s.Stop();
		GD.Print("Took " + s.Elapsed.TotalMilliseconds + " s to generate");

		s.Restart();
		GenerateGridData(position);
		s.Stop();
		GD.Print("Took " + s.Elapsed.TotalMilliseconds + " s to generate");

	}

	private void GenerateChunk(Vector3 position)
	{
		Stopwatch chunkGenSp = Stopwatch.StartNew();
		float size = ChunkSize / 2;
		float height = ChunkSize / 2;
		ArrayMesh mesh = new ArrayMesh();
		SurfaceTool tool = new SurfaceTool();
		tool.Begin(Mesh.PrimitiveType.Triangles);
		int triangles = 0;
		
		//tool.SetMaterial()
		for(float x = -size; x < size; x+=CubeSize)
		for(float y = -height; y < height; y+=CubeSize)
		for(float z = -size; z < size; z+=CubeSize)
		{
			int cubeIndex = 0;
			Stopwatch s = Stopwatch.StartNew();
			Grid grid = GetGrid(position.X + x, position.Y + y, position.Z + z);
			s.Stop();
			GD.Print("Took " + s.Elapsed.TotalMilliseconds + " s to generate cube");
			s.Restart();

			if(grid.val[0] < SurfaceLevel) cubeIndex |= 1;
			if(grid.val[1] < SurfaceLevel) cubeIndex |= 2;
			if(grid.val[2] < SurfaceLevel) cubeIndex |= 4;
			if(grid.val[3] < SurfaceLevel) cubeIndex |= 8;
			if(grid.val[4] < SurfaceLevel) cubeIndex |= 16;
			if(grid.val[5] < SurfaceLevel) cubeIndex |= 32;
			if(grid.val[6] < SurfaceLevel) cubeIndex |= 64;
			if(grid.val[7] < SurfaceLevel) cubeIndex |= 128;

			/* Cube is entirely in/out of the surface */
			if(MarchingCubeHelper.EdgeTable[cubeIndex] == 0) continue;


			/* Find the vertices where the surface intersects the cube */
			Vector3[] vertList = new Vector3[12];
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1) != 0)
				vertList[0] = VertexInterp(SurfaceLevel, grid.p[0],grid.p[1],grid.val[0],grid.val[1]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2) != 0)
				vertList[1] = VertexInterp(SurfaceLevel, grid.p[1],grid.p[2],grid.val[1],grid.val[2]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 4) != 0)
				vertList[2] = VertexInterp(SurfaceLevel, grid.p[2],grid.p[3],grid.val[2],grid.val[3]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 8) != 0)
				vertList[3] = VertexInterp(SurfaceLevel, grid.p[3],grid.p[0],grid.val[3],grid.val[0]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 16) != 0)
				vertList[4] = VertexInterp(SurfaceLevel, grid.p[4],grid.p[5],grid.val[4],grid.val[5]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 32) != 0)
				vertList[5] = VertexInterp(SurfaceLevel, grid.p[5],grid.p[6],grid.val[5],grid.val[6]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 64) != 0)
				vertList[6] = VertexInterp(SurfaceLevel, grid.p[6],grid.p[7],grid.val[6],grid.val[7]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 128) != 0)
				vertList[7] = VertexInterp(SurfaceLevel, grid.p[7],grid.p[4],grid.val[7],grid.val[4]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 256) != 0)
				vertList[8] = VertexInterp(SurfaceLevel, grid.p[0],grid.p[4],grid.val[0],grid.val[4]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 512) != 0)
				vertList[9] = VertexInterp(SurfaceLevel, grid.p[1],grid.p[5],grid.val[1],grid.val[5]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 1024) != 0)
				vertList[10] = VertexInterp(SurfaceLevel, grid.p[2],grid.p[6],grid.val[2],grid.val[6]);
			if ((MarchingCubeHelper.EdgeTable[cubeIndex] & 2048) != 0)
				vertList[11] = VertexInterp(SurfaceLevel,grid.p[3],grid.p[7],grid.val[3],grid.val[7]);




			Godot.Collections.Array<Vector3> vertices = new Godot.Collections.Array<Vector3>();
			Godot.Collections.Array<Vector2> uv = new Godot.Collections.Array<Vector2>();
			for(int i = 0; MarchingCubeHelper.TriTable[cubeIndex, i] != -1; i+=3)
			{
				foreach(int index in new int[]{0,2,1})
				{
					Vector3 vertex =vertList[MarchingCubeHelper.TriTable[cubeIndex,i + index]];
					vertices.Add(vertex);
					uv.Add(new Vector2(vertex.X, vertex.Z));
				}
				triangles++;
			}

			for(int i = 0; i < vertices.Count; i++)
			{
				tool.SetColor(new Color(1,1,1,1));
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

	private Vector3 VertexInterp(float surfaceLevel, Vector3 p1, Vector3 p2, float val1, float val2)
	{
		if(Mathf.Abs(surfaceLevel - val1) < 0.00001f) return p1;
		if(Mathf.Abs(surfaceLevel - val2) < 0.00001f) return p2;
		if(Mathf.Abs(val1 - val2) < 0.00001f) return p1;
		float mu = (surfaceLevel - val1) / (val2 - val1);
		Vector3 p = Vector3.Zero;
		p.X = p1.X + mu * (p2.X - p1.X);
   		p.Y = p1.Y + mu * (p2.Y - p1.Y);
   		p.Z = p1.Z + mu * (p2.Z - p1.Z);
		return p;
	}

	private Grid GetGrid(float x, float y, float z)
	{
		float spacing = CubeSize;
		Grid grid = new Grid();
		grid.p[0] = new Vector3(x, y, z);
		grid.val[0] = GetValue(grid.p[0]);
		grid.p[1] = new Vector3(x + spacing, y, z);
		grid.val[1] = GetValue(grid.p[1]);

		grid.p[2] = new Vector3(x + spacing, y + spacing, z);
		grid.val[2] = GetValue(grid.p[2]);
		grid.p[3] = new Vector3(x, y + spacing, z);
		grid.val[3] = GetValue(grid.p[3]);

		grid.p[4] = new Vector3(x, y, z + spacing);
		grid.val[4] = GetValue(grid.p[4]);
		grid.p[5] = new Vector3(x + spacing, y, z + spacing);
		grid.val[5] = GetValue(grid.p[5]);

		grid.p[6] = new Vector3(x + spacing, y + spacing, z + spacing);
		grid.val[6] = GetValue(grid.p[6]);
		grid.p[7] = new Vector3(x , y + spacing, z + spacing);
		grid.val[7] = GetValue(grid.p[7]);
		return grid;
	}

	class Grid
	{
		public Vector3[] p = new Vector3[8];
		public float[] val = new float[8];
	}

	class CubeSample
	{
		public Vector3[] Points = new Vector3[8];
		public float[] Values = new float[8];
	}


	private System.Collections.Generic.List<CubeSample> GenerateGridData(Vector3 rootPos)
	{
		float size = ChunkSize / 2;


		System.Collections.Generic.List<CubeSample> cubes = new System.Collections.Generic.List<CubeSample>();

		for(float x = -size; x < size; x += CubeSize)
		for(float y = -size; y < size; y += CubeSize)
		for(float z = -size; z < size; z += CubeSize)
		{
			CubeSample grid = new CubeSample();
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
			grid.Points[7] = new Vector3(x , y + CubeSize, z + CubeSize);
			grid.Values[7] = GetValue(grid.Points[7]);
			cubes.Add(grid);
		}

		return cubes;
	}
}
