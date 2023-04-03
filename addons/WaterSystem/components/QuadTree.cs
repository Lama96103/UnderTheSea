using System;
using Godot;

namespace WaterSystem
{
    public partial class QuadTree : Node3D
    {
        // Specifies the LOD level of the current quad. There will be X - 1 subquad
        // levels nested below this quad.
        [Export(PropertyHint.Range, "0, 1000000, 1")] private int lodLevel = 2;

        // Horizontal size of the current quad.
        [Export(PropertyHint.Range, "1.0, 65535.0")] private float quadSize = 1024f;

        //Morph range for CDLOD geomorph between LOD levels.
        [Export(PropertyHint.Range, "0.0, 1.0, 0.001")] private float morphRange = 0.15f;

        // Vertex resolution of each of the quads in this tree.
        [Export(PropertyHint.Range, "0, 32000, 1")] private int meshVertexResolution = 256;

        // Ranges for each LOD level. Accessed as ranges[lod_level].
        [Export] private Godot.Collections.Array<float> ranges = new Godot.Collections.Array<float>{512,1024,2048};

        // The visual shader to apply to the surface geometry.
        [Export] ShaderMaterial material;
    }
}