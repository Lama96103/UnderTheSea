using Godot;
using System;
using MonoCustomResourceRegistry;


namespace WaterSystem
{
    [RegisteredType(nameof(WaveSetting), "", nameof(Resource))]
    public partial class WaveSetting : Resource
    {
        [Export] public Vector2 Direction = new Vector2();
        [Export] public float Speed = 1;
        [Export] public float Amplitude = 0.5f;
        [Export] public float Frequency = 1;
        [Export] public float Steepness = 1;

    }
}


   
