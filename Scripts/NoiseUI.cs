using Godot;
using System;

public partial class NoiseUI : VBoxContainer
{
	private MarchingCubes.MarchingCubesGenerater generator;

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
		
	}

	

	public void OnUpdateNoiseScale(float val)
	{
		GetNode<Label>("NoiseScaleText").Text = "Noise Scale : " + val.ToString("0.00");
		generator.NoiseScale = val;
		OnGenerateNoise();
	}

	public void OnUpdateOctaves(float val)
	{
		GetNode<Label>("OctavesText").Text = "Octaves : " + val.ToString("0.00");
		generator.Octaves = val;
		OnGenerateNoise();
	}
	public void OnUpdatePersistence(float val)
	{
		GetNode<Label>("PersistenceText").Text = "Persistence : " + val.ToString("0.00");
		generator.Persistence = val;
		OnGenerateNoise();
	}
	public void OnUpdateLacunarity(float val)
	{
		GetNode<Label>("LacunarityText").Text = "Lacunarity : " + val.ToString("0.00");
		generator.Lacunarity = val;
		OnGenerateNoise();
	}

	public void OnUpdateSurfaceLevel(float val)
	{
		GetNode<Label>("SurfaceText").Text = "Surface Level : " + val.ToString("0.00");
		generator.SurfaceLevel = val;
		OnGenerateMap();
	}

	public void OnGenerateNoise()
	{
		generator.UpdateNoise(Vector3.Zero);
		generator.UpdateMesh(Vector3.Zero);
	}

	public void OnGenerateMap()
	{
		generator.UpdateMesh(Vector3.Zero);
	}
}
