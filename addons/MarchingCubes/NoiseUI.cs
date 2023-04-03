using Godot;
using System;

public partial class NoiseUI : VBoxContainer
{
	private MarchingCubes.MarchingCubesController generator;
	private bool didInitialied = false;

	public override void _Ready()
	{
		generator = GetParent().GetParent().GetParent().GetParent<MarchingCubes.MarchingCubesController>();

		GetNode<Slider>("NoiseScaleSlider").Value = generator.NoiseScale;
		OnUpdateNoiseScale(generator.NoiseScale);

		GetNode<Slider>("OctavesSlider").Value = generator.Octaves;
		OnUpdateOctaves(generator.Octaves);

		GetNode<Slider>("PersistenceSlider").Value = generator.Persistence;
		OnUpdatePersistence(generator.Persistence);

		GetNode<Slider>("SurfaceSlider").Value = generator.SurfaceLevel;
		OnUpdateSurfaceLevel(generator.SurfaceLevel);	
		
		didInitialied = true;
	}

	public void OnUpdateNoiseScale(float val)
	{
		GetNode<Label>("NoiseScaleText").Text = "Noise Scale : " + val.ToString("0.00");
		if(!didInitialied) return;
		generator.NoiseScale = val;
		OnGenerateNoise();
	}

	public void OnUpdateOctaves(float val)
	{
		GetNode<Label>("OctavesText").Text = "Octaves : " + val.ToString("0.00");
		if(!didInitialied) return;
		generator.Octaves = (int)val;
		OnGenerateNoise();
	}
	public void OnUpdatePersistence(float val)
	{
		GetNode<Label>("PersistenceText").Text = "Persistence : " + val.ToString("0.00");
		if(!didInitialied) return;
		generator.Persistence = val;
		OnGenerateNoise();
	}


	public void OnUpdateSurfaceLevel(float val)
	{
		GetNode<Label>("SurfaceText").Text = "Surface Level : " + val.ToString("0.00");
		if(!didInitialied) return;
		generator.SurfaceLevel = val;
		OnGenerateMap();
	}

	public void OnGenerateNoise()
	{
		if(!didInitialied) return;
		generator.Generate(Vector3.Zero, true);

		generator.Generate(Vector3.Zero + new Vector3(generator.ChunkSize, 0 , 0 ), true);
		generator.Generate(Vector3.Zero + new Vector3(0, 0 , generator.ChunkSize ), true);
		generator.Generate(Vector3.Zero + new Vector3(generator.ChunkSize, 0 , generator.ChunkSize ), true);
	}

	public void OnGenerateMap()
	{
		if(!didInitialied) return;
		generator.Generate(Vector3.Zero, false);

		generator.Generate(Vector3.Zero + new Vector3(generator.ChunkSize, 0 , 0 ), false);
		generator.Generate(Vector3.Zero + new Vector3(0, 0 , generator.ChunkSize ), false);
		generator.Generate(Vector3.Zero + new Vector3(generator.ChunkSize, 0 , generator.ChunkSize ), false);

		
	}
}
