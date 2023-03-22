using Godot;
using System;

public partial class MarchingCubes : Node3D
{
	private FastNoiseLite noise = new FastNoiseLite();
	private PackedScene pointScene;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Randomize();

		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
    	noise.Seed = (int)GD.Randi();
    	noise.FractalOctaves = 4;
    	noise.Frequency = 1.0f / 20.0f;

		pointScene = GD.Load<PackedScene>("res://Prefabs/Point.tscn");

		GenerateChunk();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private float GetValue(Vector3 position)
	{
		return noise.GetNoise3D(position.X, position.Y, position.Z);

	}

	private void GenerateChunk()
	{
		float size = 50;
		for(float x = -size; x < size; x++)
		for(float y = -size; y < size; y++)
		for(float z = -size; z < size; z++)
		{
			float val = GetValue(new Vector3(x, y, z));
			if(val > 0.4f)
			{
				var mesh = pointScene.Instantiate<MeshInstance3D>();
				mesh.Position = new Vector3(x,y ,z);

				this.AddChild(mesh);
			}
		}
	}
}
