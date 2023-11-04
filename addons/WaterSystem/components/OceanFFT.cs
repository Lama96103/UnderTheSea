using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using Godot;
using Godot.NativeInterop;

namespace WaterSystem
{
    
    public partial class OceanFFT : Node3D, OceanSystem
    {
        private const float GoldenRatio = 1.618033989f;
        private const int WorkGroupDim = 32;
        private const int UniformSet = 0;
        private enum Binding{
            Settings = 0, InitialSpectrum = 20, Spectrum = 21, Ping = 25, Pong = 26, Input = 27, Output = 28, Displacement = 30
        }

        #region  Settings
        [Export] private int fftResolution = 512;
        [Export] private int horizontalDimension = 256;
        
        [Export] private Godot.Collections.Array<Vector2> cascadeRanges = new Godot.Collections.Array<Vector2>() {new Vector2(0.0f, 0.03f), new Vector2(0.03f, 0.15f), new Vector2(0.15f, 1.0f)};
        [Export] private Godot.Collections.Array<float> cascadeScales = new Godot.Collections.Array<float>() {GoldenRatio * 2.0f, GoldenRatio, 0.5f};
        [Export] private float choppiness = 1.5f;
        [Export] private float windDirection = 0;
        [Export] private float waveScrollSpeed = 1;
        [Export] private ShaderMaterial material = GD.Load<ShaderMaterial>("res://addons/WaterSystem/Ocean.tres");
        [Export] private float timeScale = 1;
        [Export] private bool isSimulationEnabled = true;
        [Export] private int simulationFrameskip = 0;
        [Export] private int heightmapSyncFrameskip = -1;

        #endregion

        private int frameskip = 0;
        private int heightmapskip = 0;
        private Vector2 waveVector = new Vector2(300.0f, 0.0f);
        private Vector2 windUVOffset = new Vector2();
        private float uvScale = 0.00390625f;
        private float accumulatedDelta = 0;



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

        private Rid phaseShader;
        private Rid phasePipeline;
        private Rid phaseSettingsBuffer;
        private RDUniform phaseSettingsUniform;

        private Godot.Collections.Array<RDUniform> pingUniformCascade = new Godot.Collections.Array<RDUniform>();
        private Godot.Collections.Array<RDUniform> pongUniformCascade = new Godot.Collections.Array<RDUniform>();
        private Godot.Collections.Array<Image> pingImageCascade = new Godot.Collections.Array<Image>();
        private Godot.Collections.Array<Rid> pingTexCascade = new Godot.Collections.Array<Rid>();
        private Godot.Collections.Array<Rid> pongTexCascade = new Godot.Collections.Array<Rid>();

        private Rid spectrumShader;
        private Rid spectrumPipeline;
        private bool isSpectrumChanged = true;
        private Rid spectrumSettingsBuffer;
        private RDUniform spectrumSettingsUniform;
        private Godot.Collections.Array<RDUniform> spectrumUniformCascade = new Godot.Collections.Array<RDUniform>();
        private Godot.Collections.Array<Rid> spectrumTexCascade = new Godot.Collections.Array<Rid>();

        private Rid fftHorizontalShader;
        private Rid fftHorizontalPipeline;
        private Rid fftVerticalShader;
        private Rid fftVerticalPipeline;
        private Rid fftSettingsBuffer;
        private RDUniform fftSettingsUniform;
        private RDUniform subPongUniform;
        private Rid subPongTex;

        private Godot.Collections.Array<Image> wavesImageCascade = new Godot.Collections.Array<Image>();
        private Godot.Collections.Array<Texture2Drd> wavesTextureCascade = new Godot.Collections.Array<Texture2Drd>();
        private bool isPingPhase = true;




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

            GD.Randomize();
            GD.Print("Init Ocean started");
            Init();
            GD.Print("Init Ocean finished");
        }

        public override void _Process(double delta)
        {
            if (isSimulationEnabled)
            {
                accumulatedDelta += (float)delta;

                windUVOffset += new Vector2(Mathf.Cos(windDirection), Mathf.Sin(windDirection)) * waveScrollSpeed * (float)delta;
                material.SetShaderParameter("wind_uv_offset", windUVOffset);

                if(simulationFrameskip > 0)
                {
                    frameskip++;
                    if(frameskip <= simulationFrameskip) return;
                    else frameskip = 0;
                }

                bool syncHeightmap = heightmapSyncFrameskip != -1;
                if(heightmapSyncFrameskip > 0)
                {
                    heightmapskip++;
                    if(heightmapskip <= heightmapSyncFrameskip) syncHeightmap = false;
                    else{
                        syncHeightmap = true;
                        heightmapskip = 0;
                    }
                }

                Simulate(accumulatedDelta, syncHeightmap);
                accumulatedDelta = 0;
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

                // Initialized empty, it will be generated on the first frame
		        initialSpectrumTexCascade[i] = renDevice.TextureCreate(fmtR32f, new RDTextureView(), new Godot.Collections.Array<byte[]>(){initialImageRF.GetData()});
		        initialSpectrumUnifromCascade[i] = new RDUniform();
		        initialSpectrumUnifromCascade[i].UniformType =RenderingDevice.UniformType.Image;
		        initialSpectrumUnifromCascade[i].Binding = (int)Binding.InitialSpectrum;
		        initialSpectrumUnifromCascade[i].AddId(initialSpectrumTexCascade[i]);
            }

            /*#### Compile & Initialize Phase Shader
	        ############################################################################
	        ## Applies time based flow to a crafted random data spectrum. */
	
	        // Compile Shader
            shaderFile = GD.Load<RDShaderFile>("res://addons/WaterSystem/shaders/Phase.glsl");
            phaseShader = renDevice.ShaderCreateFromSpirV(shaderFile.GetSpirV());
            phasePipeline = renDevice.ComputePipelineCreate(phaseShader);

            //Initialize cascade arrays
            pingUniformCascade.Resize(cascadeRanges.Count);
            pongUniformCascade.Resize(cascadeRanges.Count);
            pingImageCascade.Resize(cascadeRanges.Count);
            pingTexCascade.Resize(cascadeRanges.Count);
            pongTexCascade.Resize(cascadeRanges.Count);

	        // Initialize Settings Buffer
	        settingsBytes = PackPhaseSettings(0.0f, 0);
	        phaseSettingsBuffer = renDevice.StorageBufferCreate((uint)settingsBytes.Length, settingsBytes);
            phaseSettingsUniform = new RDUniform();
	        phaseSettingsUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
	        phaseSettingsUniform.Binding = (int)Binding.Settings;
	        phaseSettingsUniform.AddId(phaseSettingsBuffer);

            /*	#### Initialize Ping Pong Buffer Textures
            ############################################################################
            ## These act as the input and output buffers for the Phase shader.
            ##
            ## They are a form of double buffer to work around the fact that due to
            ## asynchronous execution, the shader can't safely read and write from the
            ## same texture in the same execution.
            ##
            ## On even numbered frames (the "ping phase"), we read from the Ping buffer.
            ## The output is written to the Pong buffer.
            ## On odd numbered frames ("pong phase"), we do the opposite, we read from
            ## Pong, and write to Ping.
            ##
            ## On first start up, Ping gets initialized with crafted random data. */

            byte[] pingData = new byte[0];

	        // The Ping buffer must be initialized with this crafted randomized data
            for(int i = 0; i < fftResolution * fftResolution; i++)
            {
                pingData = Combine(pingData, GD.VarToBytes(GD.RandRange(0.0f, 1.0f) * 2.0f * Mathf.Pi));
            }

            for(int i = 0; i < cascadeRanges.Count; i++)
            {
                pingImageCascade[i] = Image.CreateFromData(fftResolution, fftResolution, false, Image.Format.Rf, pingData);
                pingTexCascade[i] = renDevice.TextureCreate(fmtR32f, new RDTextureView(), new Godot.Collections.Array<byte[]>(){pingImageCascade[i].GetData()});
                pingUniformCascade[i] = new RDUniform();
                pingUniformCascade[i].UniformType = RenderingDevice.UniformType.Image;
                pingUniformCascade[i].Binding = (int)Binding.Ping;
                pingUniformCascade[i].AddId(pingTexCascade[i]);

                // The Pong buffer is initialized empty; it will be generated as the output
		        // of the first iteration of the Phase shader based on the Ping input
                pongTexCascade[i] = renDevice.TextureCreate(fmtR32f, new RDTextureView(), new Godot.Collections.Array<byte[]>(){initialImageRF.GetData()});
                pongUniformCascade[i] = new RDUniform();
                pongUniformCascade[i].UniformType = RenderingDevice.UniformType.Image;
                pongUniformCascade[i].Binding = (int)Binding.Pong;
                pongUniformCascade[i].AddId(pongTexCascade[i]);

            }

            /*
            #### Compile & Initialize Spectrum Shader
	        ############################################################################
            ## Merges the weather parameters calculated in the Initial Spectrum with the
            ## crafted time varying randomness calculated in Phase (and stored in the
            ## Ping/Pong textures)
            */
            // Compile Shader
            shaderFile = GD.Load<RDShaderFile>("res://addons/WaterSystem/shaders/Spectrum.glsl");
            spectrumShader = renDevice.ShaderCreateFromSpirV(shaderFile.GetSpirV());
            spectrumPipeline = renDevice.ComputePipelineCreate(spectrumShader);
	
            // Initialize Settings Buffer
            settingsBytes = PackSpectrumSettings(0);
            spectrumSettingsBuffer = renDevice.StorageBufferCreate((uint)settingsBytes.Length, settingsBytes);
            spectrumSettingsUniform = new RDUniform();
            spectrumSettingsUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
            spectrumSettingsUniform.Binding = (int)Binding.Settings;
            spectrumSettingsUniform.AddId(spectrumSettingsBuffer);

            spectrumTexCascade.Resize(cascadeRanges.Count);
            spectrumUniformCascade.Resize(cascadeRanges.Count);
            wavesImageCascade.Resize(cascadeRanges.Count);
            wavesTextureCascade.Resize(cascadeRanges.Count);

            for(int i = 0; i < cascadeRanges.Count; i++)
            {
                // Initialized empty, it will be generated each frame
                spectrumTexCascade[i] = renDevice.TextureCreate(fmtRG32f, new RDTextureView(), new Godot.Collections.Array<byte[]>(){initialImageRGF.GetData()});
                spectrumUniformCascade[i] = new RDUniform();
                spectrumUniformCascade[i].UniformType = RenderingDevice.UniformType.Image;
                spectrumUniformCascade[i].Binding = (int)Binding.Spectrum;
                spectrumUniformCascade[i].AddId(spectrumTexCascade[i]);

                // Bind the displacement map cascade texture to the visual shader
                wavesImageCascade[i] = Image.Create(fftResolution, fftResolution, false, Image.Format.Rgf);
                wavesTextureCascade[i] = new Texture2Drd();
                wavesTextureCascade[i].TextureRdRid = spectrumTexCascade[i];
            }

            material.SetShaderParameter("cascade_displacements", wavesTextureCascade);
            material.SetShaderParameter("cascade_uv_scales", cascadeScales);
            material.SetShaderParameter("uv_scale", uvScale);

            /*
            #### Compile & Initialize FFT Shaders
            ############################################################################
            ## Converts the result of the Spectrum shader into a usable displacement map.
            ##
            ## Uses the Spectrum texture and SubPong texture as ping pong buffers. The
            ## resulting displacement map will be stored in the Specturm texture.
            */

            // Compile Shaders
            shaderFile = GD.Load<RDShaderFile>("res://addons/WaterSystem/shaders/FFTHorizontal.glsl");
            fftHorizontalShader= renDevice.ShaderCreateFromSpirV(shaderFile.GetSpirV());
            fftHorizontalPipeline = renDevice.ComputePipelineCreate(fftHorizontalShader);

            shaderFile = GD.Load<RDShaderFile>("res://addons/WaterSystem/shaders/FFTVertical.glsl");
            fftVerticalShader= renDevice.ShaderCreateFromSpirV(shaderFile.GetSpirV());
            fftVerticalPipeline = renDevice.ComputePipelineCreate(fftVerticalShader);

            // Initialize Settings Buffer
            settingsBytes = PackFFTSettings(0);
            fftSettingsBuffer = renDevice.StorageBufferCreate((uint)settingsBytes.Length, settingsBytes);
            fftSettingsUniform = new RDUniform();
            fftSettingsUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
            fftSettingsUniform.Binding = (int)Binding.Settings;
            fftSettingsUniform.AddId(fftSettingsBuffer);

            // Initialize empty, will be calculated based on the Spectrum
            subPongTex = renDevice.TextureCreate(fmtRG32f, new RDTextureView(), new Godot.Collections.Array<byte[]>(){initialImageRGF.GetData()});
            subPongUniform =new RDUniform();
            subPongUniform.UniformType = RenderingDevice.UniformType.Image;
            subPongUniform.AddId(subPongTex);
        }

        private void Simulate(float deltaTime, bool syncHeightmap)
        {
            Rid uniformSet;
            long computeList;
            byte[] settingsByte;

            	
	        // ## Iterate & Execute Cascades
	        // ##########################################################################
            for(int cascade = 0; cascade < cascadeRanges.Count; cascade++)
            {
                // #### Update Initial Spectrum
                // ########################################################################
                // ## Only executed on first frame, or if Wind, FFT Resolution, or
                // ## Horizontal Dimension inputs are changed, as the output is constant
                // ## for a given set of inputs. The Initial Spectrum is cached in VRAM. It
                // ## is not returned to CPU RAM.
                if(isInitialSpectrumChanged)
                {
                    // Update Settings Buffer
                    settingsByte = PackInitialSpectrumSettings(cascade);
                    if(renDevice.BufferUpdate(initialSpectrumSettingsBufferCascade[cascade], 0, (uint)settingsByte.Length, settingsByte) != Error.Ok)
                        GD.Print("error updating initial spectrum settings buffer");

                    // Build Uniform Set
                    uniformSet = renDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform>(){
                        initialSpectrumSettingsUniformCascade[cascade], initialSpectrumUnifromCascade[cascade]
                    }, initialSpectrumShader, UniformSet);

                    // Create Compute List
                    computeList = renDevice.ComputeListBegin();
                    renDevice.ComputeListBindComputePipeline(computeList, initialSpectrumPipeline);
                    renDevice.ComputeListBindUniformSet(computeList, uniformSet, UniformSet);
                    renDevice.ComputeListDispatch(computeList, (uint)(fftResolution / WorkGroupDim), (uint)(fftResolution / WorkGroupDim), 1);
                    renDevice.ComputeListEnd();

                    renDevice.Barrier(RenderingDevice.BarrierMask.Compute);
                    renDevice.FreeRid(uniformSet);
                }
                
                // Prevent this from running again until the Wind, FFT Resolution, or
                // Horizontal Dimension inputs are changed. The condition ensures it
                // runs for all cascades.
                if (cascade == cascadeRanges.Count - 1)
                    isInitialSpectrumChanged = false;

                // ## Execute Phase Shader; Updates Ping Pong Buffers
                // ######################################################################
                // Leave the textures in place in VRAM, and just switch the binding points.
                if(isPingPhase)
                {
                    pingUniformCascade[cascade].Binding = (int)Binding.Ping;
                    pongUniformCascade[cascade].Binding = (int)Binding.Pong;
                }
                else
                {
                    pingUniformCascade[cascade].Binding = (int)Binding.Pong;
                    pongUniformCascade[cascade].Binding = (int)Binding.Ping;
                }

                // Update Settings Buffer
                settingsByte = PackPhaseSettings(deltaTime * timeScale, cascade);
                if(renDevice.BufferUpdate(phaseSettingsBuffer, 0, (uint)settingsByte.Length, settingsByte) != Error.Ok)
                    GD.Print("error updating phase settings buffer");

                // Build Uniform Set
                uniformSet = renDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform>(){
                    phaseSettingsUniform, pingUniformCascade[cascade], pongUniformCascade[cascade]
                }, phaseShader, UniformSet);

                // Create Compute List
                computeList = renDevice.ComputeListBegin();
                renDevice.ComputeListBindComputePipeline(computeList, phasePipeline);
                renDevice.ComputeListBindUniformSet(computeList, uniformSet, UniformSet);
                renDevice.ComputeListDispatch(computeList, (uint)(fftResolution / WorkGroupDim), (uint)(fftResolution / WorkGroupDim), 1);
                renDevice.ComputeListEnd();

                renDevice.Barrier(RenderingDevice.BarrierMask.Compute);

                renDevice.FreeRid(uniformSet);

                // ## Execute Spectrum Shader Cascades
		        // ######################################################################
                // Update Settings Buffer
                settingsByte = PackSpectrumSettings(cascade);
                if(renDevice.BufferUpdate(spectrumSettingsBuffer, 0, (uint)settingsByte.Length, settingsByte) != Error.Ok)
                    GD.Print("error updating spectrum settings buffer");

                // Ensure the Spectrum texture binding is correct from previous frames.
		        // It gets changed later on in _simulate().
                spectrumUniformCascade[cascade].Binding = (int)Binding.Spectrum;

                // Build Uniform Set
                uniformSet = renDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform>(){
                    spectrumSettingsUniform, initialSpectrumSettingsUniformCascade[cascade], spectrumUniformCascade[cascade],
                    pingUniformCascade[cascade], pongUniformCascade[cascade]
                }, spectrumShader, UniformSet);

                // Create Compute List
                computeList = renDevice.ComputeListBegin();
                renDevice.ComputeListBindComputePipeline(computeList, spectrumPipeline);
                renDevice.ComputeListBindUniformSet(computeList, uniformSet, UniformSet);
                renDevice.ComputeListDispatch(computeList, (uint)(fftResolution / WorkGroupDim), (uint)(fftResolution / WorkGroupDim), 1);
                renDevice.ComputeListEnd();

                renDevice.Barrier(RenderingDevice.BarrierMask.Compute);

                renDevice.FreeRid(uniformSet);


                // ## Execute Horizontal FFT Shader Cascades
                // ######################################################################
                bool isSubPingPhase = true;
                int p = 1;

                while(p < fftResolution)
                {
                    // Leave the textures in place in VRAM, and just switch the binding
			        // points.

                    if(isSubPingPhase)
                    {
                        spectrumUniformCascade[cascade].Binding = (int)Binding.Input;
                        subPongUniform.Binding = (int)Binding.Output;
                    }
                    else
                    {
                        spectrumUniformCascade[cascade].Binding = (int)Binding.Output;
                        subPongUniform.Binding = (int)Binding.Input;
                    }

                    // Update Settings Buffer
                    settingsByte = PackFFTSettings(p);
                    if(renDevice.BufferUpdate(fftSettingsBuffer, 0, (uint)settingsByte.Length, settingsByte) != Error.Ok)
                        GD.Print("error updating horizontal FFT settings buffer");

                    // Build Uniform Set
                    uniformSet = renDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform>(){
                        fftSettingsUniform, subPongUniform, spectrumUniformCascade[cascade]
                    }, fftHorizontalShader, UniformSet);

                     // Create Compute List
                    computeList = renDevice.ComputeListBegin();
                    renDevice.ComputeListBindComputePipeline(computeList, fftHorizontalPipeline);
                    renDevice.ComputeListBindUniformSet(computeList, uniformSet, UniformSet);
                    renDevice.ComputeListDispatch(computeList, (uint)fftResolution, 1, 1);
                    renDevice.ComputeListEnd();

                    renDevice.Barrier(RenderingDevice.BarrierMask.Compute);

                    renDevice.FreeRid(uniformSet);

                    p = p << 1;
                    isSubPingPhase = !isSubPingPhase;
                }

                // ## Execute Vertical FFT Shader Cascades
		        // ######################################################################
                p = 1;
                while(p < fftResolution)
                {
                    // Leave the textures in place in VRAM, and just switch the binding
			        // points.

                    if(isSubPingPhase)
                    {
                        spectrumUniformCascade[cascade].Binding = (int)Binding.Input;
                        subPongUniform.Binding = (int)Binding.Output;
                    }
                    else
                    {
                        spectrumUniformCascade[cascade].Binding = (int)Binding.Output;
                        subPongUniform.Binding = (int)Binding.Input;
                    }

                    // Update Settings Buffer
                    settingsByte = PackFFTSettings(p);
                    if(renDevice.BufferUpdate(fftSettingsBuffer, 0, (uint)settingsByte.Length, settingsByte) != Error.Ok)
                        GD.Print("error updating vertical FFT settings buffer");

                    // Build Uniform Set
                    uniformSet = renDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform>(){
                        fftSettingsUniform, subPongUniform, spectrumUniformCascade[cascade]
                    }, fftVerticalShader, UniformSet);

                    // Create Compute List
                    computeList = renDevice.ComputeListBegin();
                    renDevice.ComputeListBindComputePipeline(computeList, fftVerticalPipeline);
                    renDevice.ComputeListBindUniformSet(computeList, uniformSet, UniformSet);
                    renDevice.ComputeListDispatch(computeList, (uint)fftResolution, 1, 1);
                    renDevice.ComputeListEnd();

                    renDevice.Barrier(RenderingDevice.BarrierMask.Compute);

                    renDevice.FreeRid(uniformSet);

                    p = p << 1;
                    isSubPingPhase = !isSubPingPhase;
                }

                // Retrieve the displacement map from the Spectrum texture, and store it
		        // CPU side for use by buoyancy and wave interaction systems.
                if(syncHeightmap)
                {
                    wavesImageCascade[cascade].SetData(fftResolution, fftResolution, false, Image.Format.Rgf, renDevice.TextureGetData(spectrumTexCascade[cascade], 0));
                }
            }

            // This needs to get updated outside the cascade iteration loop
            isPingPhase = !isPingPhase;
        }


        #region Pack Shader Settings
        private byte[] PackFFTSettings(int subSeqCount)
        {
            return GD.VarToBytes(new int[]{fftResolution, subSeqCount});
        }

        private byte[] PackSpectrumSettings(int cascade)
        {
            byte[] settingsByte = GD.VarToBytes(new int[]{(int)(horizontalDimension * cascadeScales[cascade])});
            settingsByte = Combine(settingsByte, GD.VarToBytes(new float[]{choppiness, fftResolution}));
            return settingsByte;
        }

        private byte[] PackPhaseSettings(float deltaTime, int cascade)
        {
            byte[] settingsByte = GD.VarToBytes(new int[]{fftResolution, (int)(horizontalDimension * cascadeScales[cascade])});
            settingsByte = Combine(settingsByte, GD.VarToBytes(new float[]{deltaTime}));
            return settingsByte;
        }

        private byte[] PackInitialSpectrumSettings(int cascade)
        {
            byte[] settingsByte = GD.VarToBytes(new int[]{fftResolution, (int)(horizontalDimension * cascadeScales[cascade])});
            settingsByte = Combine(settingsByte, GD.VarToBytes(new float[]{cascadeRanges[cascade].X, cascadeRanges[cascade].Y}));
            settingsByte = Combine(settingsByte, GD.VarToBytes(new Vector2[]{waveVector}));
            return settingsByte;
        }
        #endregion
        
        public float GetWaveHeight(Vector3 position)
        {
            return 0;
        }
    }
}
