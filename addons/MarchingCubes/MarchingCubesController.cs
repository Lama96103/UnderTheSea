using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MarchingCubes
{
    public partial class MarchingCubesController : Node3D
    {
        [Export(PropertyHint.Range, "-1,1")] public float SurfaceLevel = 0.5f;
        private uint ChunkSize = 16;

		private NoiseGenerator noiseGen = null;
		private PointMeshGenerator meshGen = null;


		public float NoiseScale = 0.5f;
		public int Octaves = 4;
		public float Persistence = 0.5f;
		public float Lacunarity = 2f;

		private MeshInstance3D meshInstance3D = null;
		private byte[] noiseMap = null;

		private RenderingDevice renderingDevice;


        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {	
            this.CallDeferred("Init");
        }

        private void Init()
        {
            renderingDevice = RenderingServer.CreateLocalRenderingDevice();
			
			noiseGen = new NoiseGenerator();
			noiseGen.Init(renderingDevice, ChunkSize);

			//meshGen = new PointMeshGenerator();
			//meshGen.Init(renderingDevice, ChunkSize);

            Generate(Vector3.Zero, true);
        }

        public void Generate(Vector3 position, bool createNoise)
        {
            noiseGen.UpdateSettings(position, NoiseScale, Octaves, Persistence, Lacunarity);
            PointsDataBuffer data =  noiseGen.Generate();
        }
		

		
    }
}


