using System;
using Godot;

namespace WaterSystem
{
    [Tool]
    public partial class Ocean : Node3D
    {
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

        [ExportGroup("Environment")]
        [Export] private Godot.Environment underWaterEnvironment;
        [Export] private Godot.Environment aboveWaterEnvironment;

        [ExportGroup("Normal Maps" ,"noise_")]
        [Export] private Texture2D noise_Normal1;
        [Export] private Vector2 noise_Direction1;
        [Export] private Texture2D noise_Normal2;
        [Export] private Vector2 noise_Direction2;

        private float currentTime = 0;
        private bool isAboveWater = true;




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


        int curWaveCount = 0;
        Godot.Collections.Array<Vector2> curWaveDirection = null;
        Godot.Collections.Array<float> curWaveSpeed;
        Godot.Collections.Array<float> curWaveFrequency;
        Godot.Collections.Array<float> curWaveAmplitude;
        Godot.Collections.Array<float> curWaveSteepness;

        public static Vector3 Normalized3(Vector2 vec)
        {
            Vector2 work = vec.Normalized();
            return new Vector3(work.X, 0, work.Y);
        }

        private Godot.Collections.Dictionary<Vector3, MeshInstance3D> generatedMeshes = new Godot.Collections.Dictionary<Vector3, MeshInstance3D>();

        public override void _Ready()
        {
            if(!Engine.IsEditorHint())
            {
                Cleanup();
                GenerateMesh();
            }
        }

        public override void _Process(double delta)
        {
            currentTime += (float)(delta);

            if(oceanMaterial != null) oceanMaterial.SetShaderParameter("shaderTime", currentTime);
            

            if(Engine.IsEditorHint())
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
                return;
            }

            Camera3D camera = GetViewport().GetCamera3D();

            if(camera != null)
            {
                this.Position = new Vector3(camera.GlobalPosition.X, 0, camera.GlobalPosition.Z);

                float height = CalculateHeight(camera.GlobalPosition);

                float cameraDepth = camera.GlobalPosition.Y - height;

                if(cameraDepth < 0 && isAboveWater)
                {
                    GD.Print("Camera entered under water");
                    // oceanMaterial.SetShaderParameter("normal_factor", -1);
                    isAboveWater = false;

                    GetParent<WorldEnvironment>().Environment = underWaterEnvironment;
                }
                else if(cameraDepth > 0 && !isAboveWater)
                {
                    GD.Print("Camera entered above water");
                    // oceanMaterial.SetShaderParameter("normal_factor", 1);
                    isAboveWater = true;
                    GetParent<WorldEnvironment>().Environment = aboveWaterEnvironment;
                }

                if(!isAboveWater) UpdateUnderwaterScene(cameraDepth);
            }

            
        }

        private void UpdateUnderwaterScene(float depth)
        {
            DirectionalLight3D sunLight = GetParent().GetNode<DirectionalLight3D>("Sun_Light");

            float lightStrength = Mathf.Lerp(1, 0, depth / -175);
            sunLight.LightEnergy = lightStrength;


            GD.Print(depth + " m");


        }

        public float CalculateHeight(Vector3 position)
        {
            Vector3 wave_position = new Vector3(0, 0, 0);
            Vector3 vertPos = new Vector3(position.X, 0, position.Y);
            for (int i = 0; i < curWaveCount; ++i) {
                float proj = position.Dot(Normalized3(curWaveDirection[i])),
                    phase = currentTime * curWaveSpeed[i],
                    theta = proj * curWaveFrequency[i] + phase,
                    height = curWaveAmplitude[i] * Mathf.Sin(theta);

                wave_position.Y += height;

                /*
                float maximum_width = curWaveSteepness[i] *
                                    curWaveAmplitude[i],
                    width = maximum_width * Mathf.Cos(theta),
                    x = curWaveDirection[i].X,
                    y = curWaveDirection[i].Y;

                wave_position.X += x * width;
                wave_position.Z += y * width;
                */
            } 
            vertPos += wave_position;

            return vertPos.Y;
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
            oceanMaterial.SetShaderParameter("shaderTime", currentTime);

            oceanMaterial.SetShaderParameter("texture_normal1", noise_Normal1);
            oceanMaterial.SetShaderParameter("texture_normal1_dir", noise_Direction1);
            oceanMaterial.SetShaderParameter("texture_normal2", noise_Normal2);
            oceanMaterial.SetShaderParameter("texture_normal2_dir", noise_Direction2);

            curWaveCount = (wave1_enabled ? 1 : 0) + (wave2_enabled ? 1: 0)+ (wave3_enabled ? 1: 0)+ (wave4_enabled ? 1: 0)+ (wave5_enabled ? 1: 0);
            curWaveDirection = new Godot.Collections.Array<Vector2>{wave1_direction.Normalized(), wave2_direction.Normalized(), wave3_direction.Normalized(),wave4_direction.Normalized(),wave5_direction.Normalized()};
            curWaveSpeed = new Godot.Collections.Array<float> {   wave1_speed, wave2_speed, wave3_speed, wave4_speed, wave5_speed};
            curWaveFrequency = new Godot.Collections.Array<float>{wave1_frequency, wave2_frequency, wave3_frequency, wave4_frequency, wave5_frequency};
            curWaveAmplitude = new Godot.Collections.Array<float>{wave1_amplitude, wave2_amplitude, wave3_amplitude, wave4_amplitude, wave5_amplitude};
            curWaveSteepness = new Godot.Collections.Array<float>{wave1_steepness, wave2_steepness, wave3_steepness, wave4_steepness, wave5_steepness};


            oceanMaterial.SetShaderParameter("gerstner_waves_length", curWaveCount);
            oceanMaterial.SetShaderParameter("waveDirection", curWaveDirection);
            oceanMaterial.SetShaderParameter("waveSpeed", curWaveSpeed);
            oceanMaterial.SetShaderParameter("waveFrequency", curWaveFrequency);
            oceanMaterial.SetShaderParameter("waveAmplitude", curWaveAmplitude);
            oceanMaterial.SetShaderParameter("waveSteepness", curWaveSteepness);
        }

    
    }
}