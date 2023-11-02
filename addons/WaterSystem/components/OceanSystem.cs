using Godot;

namespace WaterSystem
{
    public interface OceanSystem 
    {
        public static OceanSystem Instance = null;
        public abstract float GetWaveHeight(Vector3 position);

    
    }

}