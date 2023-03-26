using Godot;
using System;

public partial class NoiseUI : VBoxContainer
{
	private MarchingCubes.MarchingCubesGenerater generator;
	private bool didInitialied = false;

	public override void _Ready()
	{
		generator = GetNode<MarchingCubes.MarchingCubesGenerater>("%MarchingCubes");

		GetNode<Slider>("NoiseScaleSlider").Value = generator.NoiseScale;
		OnUpdateNoiseScale(generator.NoiseScale);

		GetNode<Slider>("OctavesSlider").Value = generator.Octaves;
		OnUpdateOctaves(generator.Octaves);

		GetNode<Slider>("PersistenceSlider").Value = generator.Persistence;
		OnUpdatePersistence(generator.Persistence);

		GetNode<Slider>("LacunaritySlider").Value = generator.Lacunarity;
		OnUpdateLacunarity(generator.Lacunarity);	

		GetNode<Slider>("SurfaceSlider").Value = generator.SurfaceLevel;
		OnUpdateLacunarity(generator.SurfaceLevel);	
		
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
	public void OnUpdateLacunarity(float val)
	{
		GetNode<Label>("LacunarityText").Text = "Lacunarity : " + val.ToString("0.00");
		if(!didInitialied) return;
		generator.Lacunarity = val;
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
		generator.UpdateNoise(Vector3.Zero);
		generator.UpdateMesh(Vector3.Zero);
	}

	public void OnGenerateMap()
	{
		if(!didInitialied) return;
		generator.UpdateMesh(Vector3.Zero);
	}
}
