using System;
using Godot;

namespace WaterSystem
{
    [Tool]
    public partial class Ocean : Node3D
    {
        [Export] private System WaveSystem = System.Gerstner;
        [Export] private WaveSetting[] Waves = new WaveSetting[0];




        public override void _Ready()
        {
            
        }



        public enum System
        {
            Gerstner ,
            FFT
        }

    
    }
}