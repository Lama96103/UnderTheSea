using System;
using Godot;

namespace WaterSystem
{
    [Tool]
    public partial class Ocean : Node3D
    {
        // [Export] private System WaveSystem = System.Gerstner;
        [Export] private float ChunkSize = 50;
        [Export] private float[] Lod = new float[]{2,1,0.5f};
        private ShaderMaterial oceanMaterial;
        [Export] private Shader oceanShader;
        [Export] private bool generate = false;
        [Export] private bool updateShader = false;

        [ExportGroup("Settings")]
        [Export] private Color deepColor;
        [Export] private Color shallowColor;
        [Export] private float beersLaw = 2;
        [Export] private float depthOffset = -0.75f;
        [Export] private float metallic = 0;
        [Export] private float roughness = 0.02f;

        [ExportGroup("Normal Maps" ,"noise_")]
        [Export] private Texture2D noise_Normal1;
        [Export] private Vector2 noise_Direction1;
        [Export] private Texture2D noise_Normal2;
        [Export] private Vector2 noise_Direction2;


        #region Waves
        [ExportGroup("Waves")]
        [ExportSubgroup("Wave 1", "wave1_")]
        [Export] private bool wave1_enabled = false;
        [Export] private Vector2 wave1_direction = new Vector2(1,0);
        [Export] private float wave1_speed = 1;
        [Export] private float wave1_frequency = 1;
        [Export] private float wave1_amplitude = 0.5f;
        [Export] private float wave1_steepness = 0.5f;


        [ExportSubgroup("Wave 2", "wave2_")]
        [Export] private bool wave2_enabled = false;
        [Export] private Vector2 wave2_direction = new Vector2(1,0);
        [Export] private float wave2_speed = 1;
        [Export] private float wave2_frequency = 1;
        [Export] private float wave2_amplitude = 0.5f;
        [Export] private float wave2_steepness = 0.5f;

        
        [ExportSubgroup("Wave 3", "wave3_")]
        [Export] private bool wave3_enabled = false;
        [Export] private Vector2 wave3_direction = new Vector2(1,0);
        [Export] private float wave3_speed = 1;
        [Export] private float wave3_frequency = 1;
        [Export] private float wave3_amplitude = 0.5f;
        [Export] private float wave3_steepness = 0.5f;

        [ExportSubgroup("Wave 4", "wave4_")]
        [Export] private bool wave4_enabled = false;
        [Export] private Vector2 wave4_direction = new Vector2(1,0);
        [Export] private float wave4_speed = 1;
        [Export] private float wave4_frequency = 1;
        [Export] private float wave4_amplitude = 0.5f;
        [Export] private float wave4_steepness = 0.5f;

        [ExportSubgroup("Wave 5", "wave5_")]
        [Export] private bool wave5_enabled = false;
        [Export] private Vector2 wave5_direction = new Vector2(1,0);
        [Export] private float wave5_speed = 1;
        [Export] private float wave5_frequency = 1;
        [Export] private float wave5_amplitude = 0.5f;
        [Export] private float wave5_steepness = 0.5f;
        #endregion

       

        private Godot.Collections.Dictionary<Vector3, MeshInstance3D> generatedMeshes = new Godot.Collections.Dictionary<Vector3, MeshInstance3D>();

        public override void _Process(double delta)
        {
            if(generate)
            {
                generate = false;
                Cleanup();
                GenerateMesh();
            }

            if(updateShader)
            {
                updateShader = false;
                SetShader();
            }
        }

        private void Cleanup()
        {
            foreach(Node child in GetChildren())
            {
                this.RemoveChild(child);
                child.QueueFree();
            }
            generatedMeshes.Clear();
        }

        private void GenerateMesh()
        {        
            SetShader();
            int numberOfLodLevels = Lod.Length-1;
            for(float x = -numberOfLodLevels; x <= numberOfLodLevels; x++)
            for(float z = -numberOfLodLevels; z <= numberOfLodLevels; z++)
            {
                MeshInstance3D meshInstance = new MeshInstance3D();

                PlaneMesh mesh = new PlaneMesh();
                mesh.Size = new Vector2(ChunkSize, ChunkSize);
            


                float level = 0;
                if(Mathf.Abs(x) > level) level = Mathf.Abs(x);
                if(Mathf.Abs(z) > level) level = Mathf.Abs(z);
                


                mesh.SubdivideDepth = (int)(ChunkSize * Lod[(int)level]);
                mesh.SubdivideWidth = (int)(ChunkSize * Lod[(int)level]);

                mesh.Material = oceanMaterial;

                meshInstance.Mesh = mesh;
                // meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

                this.AddChild(meshInstance);
                meshInstance.Owner = this.Owner;
                meshInstance.Position = new Vector3(ChunkSize * x, 0, ChunkSize * z);

                meshInstance.Name = meshInstance.Position.ToString();

                generatedMeshes.Add(meshInstance.Position, meshInstance);
            }
        }

        private void SetShader()
        {
            if(oceanMaterial == null)
            {
                oceanMaterial = new ShaderMaterial();
                oceanMaterial.Shader = oceanShader;
            }

            oceanMaterial.SetShaderParameter("color_deep", deepColor);
            oceanMaterial.SetShaderParameter("color_shallow", shallowColor);
            oceanMaterial.SetShaderParameter("beers_law", beersLaw);
            oceanMaterial.SetShaderParameter("depth_offset", depthOffset);
            oceanMaterial.SetShaderParameter("metallic", metallic);
            oceanMaterial.SetShaderParameter("roughness", roughness);

            oceanMaterial.SetShaderParameter("texture_normal1", noise_Normal1);
            oceanMaterial.SetShaderParameter("texture_normal1_dir", noise_Direction1);
            oceanMaterial.SetShaderParameter("texture_normal2", noise_Normal2);
            oceanMaterial.SetShaderParameter("texture_normal2_dir", noise_Direction2);


            int waveCount = (wave1_enabled ? 1 : 0) + (wave2_enabled ? 1: 0)+ (wave3_enabled ? 1: 0)+ (wave4_enabled ? 1: 0)+ (wave5_enabled ? 1: 0);
            oceanMaterial.SetShaderParameter("gerstner_waves_length", waveCount);

            oceanMaterial.SetShaderParameter("waveDirection", new Godot.Collections.Array{wave1_direction.Normalized(), wave2_direction.Normalized(),wave3_direction.Normalized(),wave4_direction.Normalized(),wave5_direction.Normalized()});
            oceanMaterial.SetShaderParameter("waveSpeed", new Godot.Collections.Array{   wave1_speed, wave2_speed, wave3_speed, wave4_speed, wave5_speed});
            oceanMaterial.SetShaderParameter("waveFrequency", new Godot.Collections.Array{wave1_frequency, wave2_frequency, wave3_frequency, wave4_frequency, wave5_frequency});
            oceanMaterial.SetShaderParameter("waveAmplitude", new Godot.Collections.Array{wave1_amplitude, wave2_amplitude, wave3_amplitude, wave4_amplitude, wave5_amplitude});
            oceanMaterial.SetShaderParameter("waveSteepness", new Godot.Collections.Array{wave1_steepness, wave2_steepness, wave3_steepness, wave4_steepness, wave5_steepness});
        }
        public enum System
        {
            Gerstner ,
            FFT
        }

    
    }
}