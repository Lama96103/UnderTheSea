using Godot;
using System;

public partial class Boid : RigidBody3D
{
	const int numViewDirections = 300;
	private static Vector3[] rayDirections = new Vector3[0];

	private Vector3 position;
	private Vector3 forward = Vector3.Up;
	private Vector3 velocity;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if(Boid.rayDirections.Length == 0) InitDirections();
	}

	private static void InitDirections()
	{
		rayDirections = new Vector3[Boid.numViewDirections];

        float goldenRatio = (1 + Mathf.Sqrt (5)) / 2;
        float angleIncrement = Mathf.Pi * 2 * goldenRatio;

        for (int i = 0; i < numViewDirections; i++) {
            float t = (float) i / numViewDirections;
            float inclination = Mathf.Acos (1 - 2 * t);
            float azimuth = angleIncrement * i;

            float x = Mathf.Sin (inclination) * Mathf.Cos (azimuth);
            float y = Mathf.Sin (inclination) * Mathf.Sin (azimuth);
            float z = Mathf.Cos (inclination);
            rayDirections[i] = new Vector3 (x, y, z);
        }
	}

	public override void _PhysicsProcess(double delta)
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		
		if(IsHeadingForCollision(spaceState))
		{
			forward = ObstacleRays(spaceState);
			GD.Print("Heading for collision " + forward);
		}
		

		this.LinearVelocity = forward;
		this.position = this.Position;
	}

	private bool IsHeadingForCollision (PhysicsDirectSpaceState3D spaceState) 
	{
		var query = PhysicsRayQueryParameters3D.Create(this.position, this.position + forward * 0.5f);
		Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

		if(result.Count > 0) return true;
		return false;
    }

    private Vector3 ObstacleRays (PhysicsDirectSpaceState3D spaceState) 
	{
        for (int i = 0; i < rayDirections.Length; i++) {

			var query = PhysicsRayQueryParameters3D.Create(position, position + rayDirections[i] * 0.5f);
			Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

			if(result.Count == 0) return rayDirections[i];
        }

        return forward;
    }


	

}
