using Godot;
using System;
using System.Collections.Generic;

public partial class WaterController : MeshInstance3D
{
	[Export] private int Size = 100;
	[Export] private int PointsPerAxis = 400;

	// Called when the node enters the scene tree for the first time.

	private int GetIndex(int x, int z)
	{
		return x * PointsPerAxis + z;
	}
	public override void _Ready()
	{
		List<Vector3> vertices = new List<Vector3>((PointsPerAxis * PointsPerAxis));
		List<Vector3> normals = new List<Vector3>((PointsPerAxis * PointsPerAxis));
		List<int> indices = new List<int>((PointsPerAxis * PointsPerAxis) * 6);

		//List<Vector2> uvs = new List<Vector2>((PointsPerAxis * PointsPerAxis) + 6);

		float triangleSize = Size / PointsPerAxis;
		int indexX = 0;
		for(float x = 0; x < Size; x += triangleSize)
		{
			int indexZ = 0;
			for(float z = 0; z < Size; z += triangleSize)
			{
				vertices.Add(new Vector3(x, 0, z));
				normals.Add(Vector3.Up);

				if(z < Size -1  && x < Size-1)
				{
					indices.Add(GetIndex(indexX, indexZ));
					indices.Add(GetIndex(indexX + 1, indexZ));
					indices.Add(GetIndex(indexX + 1, indexZ + 1));
					
					indices.Add(GetIndex(indexX + 1, indexZ + 1));
					indices.Add(GetIndex(indexX, indexZ + 1));
					indices.Add(GetIndex(indexX, indexZ));
				}
				indexZ++;
			}
			indexX++;
		}
		

		ArrayMesh mesh = new ArrayMesh();
	
		Godot.Collections.Array surfaceArray = new Godot.Collections.Array();
		surfaceArray.Resize((int)Mesh.ArrayType.Max);

		surfaceArray[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		surfaceArray[(int)Mesh.ArrayType.Normal] = normals.ToArray();
		surfaceArray[(int)Mesh.ArrayType.Index] = indices.ToArray();


		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);


		this.Mesh = mesh;
	}

	private void GenerateSurfaceTool()
	{
		ArrayMesh mesh = new ArrayMesh();
		SurfaceTool tool = new SurfaceTool();
		tool.Begin(Mesh.PrimitiveType.Triangles);
		tool.SetNormal(Vector3.Up);
		tool.SetUV(Vector2.Zero);

		float triangleSize = Size / PointsPerAxis;
		int vertexCount = 0;
		for(float x = 0; x < Size; x += triangleSize)
		for(float z = 0; z < Size; z += triangleSize)
		{
			tool.AddVertex(new Vector3(x, 0, z));
			tool.AddVertex(new Vector3(x + 1, 0, z));
			tool.AddVertex(new Vector3(x + 1, 0, z + 1));

			
			tool.AddVertex(new Vector3(x + 1, 0, z + 1));
			tool.AddVertex(new Vector3(x, 0, z + 1));
			tool.AddVertex(new Vector3(x, 0, z));
			vertexCount += 6;
		}


		// tool.Index();
    	tool.GenerateNormals();
    	tool.GenerateTangents();
        tool.Commit(mesh);

		GD.Print("Generated " + vertexCount );

		this.Mesh = mesh;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
