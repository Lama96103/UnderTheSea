using System;
using Godot;

namespace WaterSystem
{
    [Tool]
    public partial class OceanFFT : Node3D, OceanSystem
    {



        public override void _Ready()
        {
            if(!Engine.IsEditorHint())
            {
                OceanSystem.Instance = this;
            }
        }
        
        public float GetWaveHeight(Vector3 position)
        {
            return 0;
        }
    }
}