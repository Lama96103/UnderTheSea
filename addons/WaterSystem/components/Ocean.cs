using System;
using Godot;

namespace WaterSystem
{
    public partial class Ocean : Node3D
    {
        [Export] private System WaveSystem = System.Gerstner;


        [Flags]
        public enum System
        {
            None,
            Gerstner ,
            FFT
        }
    }
}