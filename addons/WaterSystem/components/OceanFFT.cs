using System;
using Godot;

namespace WaterSystem
{
    [Tool]
    public partial class OceanFFT : Node3D, OceanSystem
    {
        #region  Settings
        [Export]private int fftResolution = 512;

        #endregion

        private RDTextureFormat fmtR32f = new RDTextureFormat();
        private RDTextureFormat fmtRG32f = new RDTextureFormat();
        private RDTextureFormat fmtRGBA32f = new RDTextureFormat();


        public override void _Ready()
        {
            if(!Engine.IsEditorHint())
            {
                OceanSystem.Instance = this;
            }
        }

        private void Init()
        {
            Resource shaderFile;
            PackedDataContainer settingsBytes;
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


        }
        
        public float GetWaveHeight(Vector3 position)
        {
            return 0;
        }
    }
}
