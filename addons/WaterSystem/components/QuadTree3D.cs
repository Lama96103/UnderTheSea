using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using Godot;
using Godot.NativeInterop;
namespace WaterSystem;

public partial class QuadTree3D : Node3D
{
    // Specifies the LOD level of the current quad. There will be X - 1 subquad
    // levels nested below this quad.
    [Export(PropertyHint.Range, "0,1000000,1")] private int lodLevel = 2;

    // Horizontal size of the current quad.
    [Export(PropertyHint.Range, "1.0,65535.0")] private float quadSize = 1024.0f;

    // Morph range for CDLOD geomorph between LOD levels.
    [Export(PropertyHint.Range, "0.0,1.0, 0.001")] private float morphRange = 1024.0f;

    // Vertex resolution of each of the quads in this tree.
    [Export(PropertyHint.Range, "0,32000,1")] private int meshVertexResolution = 256;

    // Ranges for each LOD level. Accessed as ranges[lod_level].
    [Export] private Godot.Collections.Array<float> ranges = new Godot.Collections.Array<float>() {512, 1024, 2048};

    // The visual shader to apply to the surface geometry.
    [Export] private ShaderMaterial material;

    // Will hold this resource loaded so as to instantiate subquads
    // This can't currently be preloaded due to an engine bug
    // https://github.com/godotengine/godot/issues/70985

    private PackedScene quad;

    // Whether the current quad is the root quad in the tree. Initializes all nested
    // subquads on ready.
    bool isRootQuad = true;
    // If this is true, the LOD system will be paused in its current state.
    private bool pauseCull = false;
    // The cull box that encloses this quad.
    private Aabb cullBox;

    // Meshes for each LOD level.
    // TODO: Why am I storing so many meshes?
    private Godot.Collections.Array<PlaneMesh> lodMeshes = new Godot.Collections.Array<PlaneMesh>();

    // This quads mesh instance.
    private MeshInstance3D meshInstance;
    private VisibleOnScreenNotifier3D visibilityDetector;
    private Godot.Collections.Array<QuadTree3D> subQuads = new Godot.Collections.Array<QuadTree3D>();

    public override void _Ready()
    {
        Node3D subQuadNode;

        if(isRootQuad)
        {
            // Load self to instantiate subquads with
		    // This can't currently be preloaded due to an engine bug
		    // https://github.com/godotengine/godot/issues/70985
            quad = GD.Load<PackedScene>("res://addons/WaterSystem/components/QuadTree3D.tscn");

	
            // Set max view distance and fade range start
            Camera3D camera = GetViewport().GetCamera3D();
            material.SetShaderParameter("view_distance_max", camera.Far);
            material.SetShaderParameter("vertex_resolution", meshVertexResolution);

            // Initialize LOD meshes for each level
            float currentSize = quadSize;

            for(int i = 0; i < lodLevel + 1; i++)
            {
                PlaneMesh mesh = new PlaneMesh();

                mesh.Size = Vector2.One * currentSize;
                mesh.SubdivideDepth = meshVertexResolution - 1;
                mesh.SubdivideWidth = meshVertexResolution - 1;

                lodMeshes.Insert(0, mesh);
                currentSize *= 0.5f;
            }

            meshInstance = new MeshInstance3D();
            subQuadNode = new Node3D();
            visibilityDetector = new VisibleOnScreenNotifier3D();

            AddChild(subQuadNode);
            AddChild(meshInstance);
            AddChild(visibilityDetector);

        }
        else
        {
            meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
            subQuadNode = GetNode<Node3D>("SubQuads");
            visibilityDetector = GetNode<VisibleOnScreenNotifier3D>("VisibleOnScreenNotifier3D");
        }

		float offsetLength = quadSize * 0.25f;
		meshInstance.Visible = false;
		meshInstance.Mesh = lodMeshes[lodLevel];
		meshInstance.MaterialOverride = material;
		meshInstance.SetInstanceShaderParameter("patch_size", quadSize);
		meshInstance.SetInstanceShaderParameter("min_lod_morph_distance", ranges[lodLevel] * 2 * (1 - morphRange));
		meshInstance.SetInstanceShaderParameter("max_lod_morph_distance", ranges[lodLevel] * 2);

		visibilityDetector.Aabb = new Aabb(new Vector3(-quadSize * 0.75f, -quadSize * 0.5f, -quadSize * 0.75f),
											new Vector3(quadSize * 1.5f, quadSize, quadSize * 1.5f));
		meshInstance.CustomAabb = visibilityDetector.Aabb;

		cullBox = new Aabb(GlobalPosition + new Vector3(-quadSize * 0.5f, -10, -quadSize * 0.5f),
											new Vector3(quadSize, 20, quadSize));

		// If this is not the most detailed LOD level, initialize more detailed children.
		if(lodLevel > 0)
		{
			foreach(Vector3 offset in new Vector3[]{new Vector3(1, 0, 1),new Vector3(-1, 0, 1),new Vector3(1, 0, -1),new Vector3(-1, 0, -1)})
			{
				QuadTree3D newQuad = quad.Instantiate<QuadTree3D>();
				newQuad.lodLevel = lodLevel - 1;
				newQuad.quadSize = quadSize * 0.5f;
				newQuad.ranges = ranges;
				newQuad.ProcessMode = ProcessModeEnum.Disabled;
				newQuad.Position = offset * offsetLength;
				newQuad.morphRange = morphRange;
				newQuad.quad = quad;
				newQuad.lodMeshes = lodMeshes;
				newQuad.isRootQuad = false;
				newQuad.material = material;

				subQuadNode.AddChild(newQuad);
				subQuads.Add(newQuad); 

			}
		}

		



    }

	// Process mode is set to PROCESS_MODE_DISABLED for subquads, so only the root
	// quad will run _process().
    public override void _Process(double delta)
    {
		if(!pauseCull && Engine.GetFramesDrawn() % 2 == 0)
		{
			Camera3D camera = GetViewport().GetCamera3D();
			LodSelect(camera.GlobalPosition);
		}
    }

	// Select which meshes will be displayed at which LOD level. A return value of
	// true marks the node as handled, and a value of false indicates the parent
	// node must handle it.
	// cam_pos is the camera/player position in global coordinates.
	private bool LodSelect(Vector3 cameraPos)
	{
		// Beginning at the root node of lowest LOD, and working towards the most
		// detailed LOD 0.

		if(!IsInsideSphere(cameraPos, ranges[lodLevel]))
		{
			// This quad is not within range of the selected LOD level, the parent
			// will need to display this at a lower detailed LOD. Return false to
			// mark the area as not handled.
			ResetVisibility();
			return false;
		}

		if(!visibilityDetector.IsOnScreen())
		{
			// This quad is not on screen. Do not make it visible, and return true
			// to mark the area as handled.
			meshInstance.Visible = false;
			return true;
		}

		if(lodLevel == 0)
		{
			// Within range of selected LOD level, and at highest detailed LOD,
			// there are no more detailed children to render this. Make this quad
			// visible. Return true to mark the area handled.
			meshInstance.Visible = true;
			return true;
		}
		else
		{
			// Within range of selected LOD level, but there are more detailed
			// children that may be able to display this. Check if any are within
			// their LOD range.
			if(!IsInsideSphere(cameraPos, ranges[lodLevel - 1]))
			{
				ResetVisibility();
				// No children are within range of their LOD levels, make this quad
				// visible to handle the area.
				foreach(var subQuad in subQuads) subQuad.meshInstance.Visible = true;
			}
			else
			{
				// At least one more detailed children is within LOD range. Recurse
				// through them and select them if appropriate.
				foreach(var subQuad in subQuads)
				{
					if(!subQuad.LodSelect(cameraPos)) subQuad.meshInstance.Visible = true;
				}
				
			}

			// The area has been handled.
			return true;
		}
	}




	// Reset this quad and all subquads to invisible.
	private void ResetVisibility()
	{
		if(meshInstance.Visible) meshInstance.Visible = false;
		else
			foreach(var subQuad in subQuads) subQuad.ResetVisibility();
	}



	// Returns true if this quads cull_box AABB intersects with a sphere with the
	// specified radius and center point.
	private bool IsInsideSphere(Vector3 center, float radius)
	{
		float radiusSquared = radius * radius;
		float dMin = 0.0f;

		for(int i = 0; i < 3; i++)
		{
			if(center[i] < cullBox.Position[i])
				dMin += Mathf.Pow(center[i] - cullBox.Position[i], 2.0f);
			else if(center[i] > cullBox.Position[i])
				dMin += Mathf.Pow(center[i] - cullBox.End[i], 2.0f);
		}
		return dMin <= radiusSquared;
	}

}
