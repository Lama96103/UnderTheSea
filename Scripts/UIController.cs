using Godot;
using System;

public partial class UIController : Control
{

	private TextureRect textureRect;
	private WaterSystem.OceanSystem ocean = null;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		textureRect = GetChild<TextureRect>(0);
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(ocean == null) ocean = WaterSystem.OceanSystem.Instance;

		textureRect.Texture = ((WaterSystem.OceanFFT)ocean).GetWavesTexture(0);
	}
}
