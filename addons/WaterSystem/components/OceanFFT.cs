using System;
using System.ComponentModel;
using System.Linq;
using Godot;
using Godot.NativeInterop;

namespace WaterSystem
{
    [Tool]
    public partial class OceanFFT : Node3D, OceanSystem
    {
        private const float GoldenRatio = 1.618033989f;
        private const int WorkGroupDim = 32;
        private const int uniformSet = 0;
        private enum Binding{
            Settings = 0, InitialSpectrum = 20, Spectrum = 21, Ping = 25, Pong = 26, Input = 27, Output = 28, Displacement = 30
        }

        #region  Settings
        [Export] private int fftResolution = 512;
        [Export] private int horizontalDimension = 256;
        
        [Export] private Godot.Collections.Array<Vector2> cascadeRanges = new Godot.Collections.Array<Vector2>() {new Vector2(0.0f, 0.03f), new Vector2(0.03f, 0.15f), new Vector2(0.15f, 1.0f)};
        [Export] private Godot.Collections.Array<float> cascadeScales = new Godot.Collections.Array<float>() {GoldenRatio * 2.0f, GoldenRatio, 0.5f};

        #endregion

        private RenderingDevice renDevice = RenderingServer.GetRenderingDevice();
        private RDTextureFormat fmtR32f = new RDTextureFormat();
        private RDTextureFormat fmtRG32f = new RDTextureFormat();
        private RDTextureFormat fmtRGBA32f = new RDTextureFormat();


        private Rid initialSpectrumShader;
        private Rid initialSpectrumPipeline;
        private bool isInitialSpectrumChanged = true;
        private Godot.Collections.Array<Rid> initialSpectrumSettingsBufferCascade = new Godot.Collections.Array<Rid>();
        private Godot.Collections.Array<RDUniform> initialSpectrumSettingsUniformCascade = new Godot.Collections.Array<RDUniform>();
        private Godot.Collections.Array<RDUniform> initialSpectrumUnifromCascade = new Godot.Collections.Array<RDUniform>();
        private Godot.Collections.Array<Rid> initialSpectrumTexCascade = new Godot.Collections.Array<Rid>();
        private Vector2 waveVector = new Vector2(300.0f, 0.0f);

        public static byte[] Combine(params byte[][] arrays)
        {
            byte[] ret = new byte[arrays.Sum(x => x.Length)];
            int offset = 0;
            foreach (byte[] data in arrays)
            {
                Buffer.BlockCopy(data, 0, ret, offset, data.Length);
                offset += data.Length;
            }
            return ret;
        }


        public override void _Ready()
        {
            if(!Engine.IsEditorHint())
            {
                OceanSystem.Instance = this;
            }
        }

        private void Init()
        {
            RDShaderFile shaderFile;
            byte[] settingsBytes;
            Image initialImageRF = Image.Create(fftResolution, fftResolution, false, Image.Format.Rf);
            Image initialImageRGF = Image.Create(fftResolution, fftResolution, false, Image.Format.Rgf);


            // Initialize RDTextureFormats
            //##########################################################################
            // These are initialized once and reused as needed.
            fmtR32f.Width = (uint)fftResolution;
            fmtR32f.Height = (uint)fftResolution;
            fmtR32f.Format = RenderingDevice.DataFormat.R32Sfloat;
            fmtR32f.UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanUpdateBit | 
            RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit;

            fmtRG32f.Width = (uint)fftResolution;
            fmtRG32f.Height = (uint)fftResolution;
            fmtRG32f.Format = RenderingDevice.DataFormat.R32G32Sfloat;
            fmtRG32f.UsageBits = fmtR32f.UsageBits;

            fmtRGBA32f.Width = (uint)fftResolution;
            fmtRGBA32f.Height = (uint)fftResolution;
            fmtRGBA32f.Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
            fmtRGBA32f.UsageBits = fmtR32f.UsageBits;

            /* #### Compile & Initialize Initial Spectrum Shader
            ############################################################################
            ## The Initial Spectrum texture is initialized empty. It will be generated
            ## by the Initial Spectrum shader based on the wind, FFT resolution, and
            ## horizontal dimension inputs. It will be static and constant for a given
            ## set of inputs, and doesn't need to be recalculated per frame, only when
            ## inputs change. */
            
            // Compile Shader
            shaderFile = GD.Load<RDShaderFile>("res://addons/WaterSystem/shaders/InitialSpectrum.glsl");
            initialSpectrumShader = renDevice.ShaderCreateFromSpirV(shaderFile.GetSpirV());
            initialSpectrumPipeline = renDevice.ComputePipelineCreate(initialSpectrumShader);

            // Initialize cascaded FFTs
            initialSpectrumSettingsBufferCascade.Resize(cascadeRanges.Count);
            initialSpectrumSettingsUniformCascade.Resize(cascadeRanges.Count);
            initialSpectrumTexCascade.Resize(cascadeRanges.Count);
            initialSpectrumUnifromCascade.Resize(cascadeRanges.Count);

            for(int i = 0; i < cascadeRanges.Count; i++)
            {
                // Initialize Settings Buffer
                settingsBytes = PackInitialSpectrumSettings(i);
                initialSpectrumSettingsBufferCascade[i] = renDevice.StorageBufferCreate((uint)settingsBytes.Length, settingsBytes);
                initialSpectrumSettingsUniformCascade[i] = new RDUniform();
                initialSpectrumSettingsUniformCascade[i].UniformType = RenderingDevice.UniformType.Image;
                initialSpectrumSettingsUniformCascade[i].Binding = (int)Binding.InitialSpectrum;
                initialSpectrumSettingsUniformCascade[i].AddId(initialSpectrumSettingsBufferCascade[i]);
            }
        }

        private byte[] PackInitialSpectrumSettings(int cascade)
        {
            byte[] settingsByte = GD.VarToBytes(new int[]{fftResolution, (int)(horizontalDimension * cascadeScales[cascade])});
            settingsByte = Combine(settingsByte, GD.VarToBytes(new float[]{cascadeRanges[cascade].X, cascadeRanges[cascade].Y}));
            settingsByte = Combine(settingsByte, GD.VarToBytes(new Vector2[]{waveVector}));
            return settingsByte;
        }
        
        public float GetWaveHeight(Vector3 position)
        {
            return 0;
        }
    }
}
