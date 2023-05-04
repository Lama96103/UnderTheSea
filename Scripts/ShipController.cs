using System;
using Godot;

public partial class ShipController : WaterSystem.BuoyancyBody
{
    [Export] private float Thrust = 10;

    protected override void OnIntegrateForces(PhysicsDirectBodyState3D state)
    {
        Vector3 move = Vector3.Zero;
		if(Input.IsActionPressed("debug_camera_forward")) move += -this.Transform.Basis.Z;
		else if(Input.IsActionPressed("debug_camera_backward")) move += this.Transform.Basis.Z;

		if(Input.IsActionPressed("debug_camera_right")) move += this.Transform.Basis.X;
		else if(Input.IsActionPressed("debug_camera_left")) move += -this.Transform.Basis.X;

        if(move != Vector3.Zero)
        {
            Vector3 force = move * Thrust * (float)state.Step;
            state.ApplyForce(force);
            GD.Print("Applying force " + force);
        }

    }
}