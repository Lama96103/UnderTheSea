using System;
using Godot;

namespace WaterSystem
{
    public partial class BuoyancyBody : RigidBody3D
    {
        [Export] private float bouyancyPower = 1;
        [Export] private float bouyancyMultiplier = 1;
        [Export] private float submergedDragLinear = 0.05f;
        [Export] private float submergedDragAngular = 0.1f;
        private bool isSubmerged = false;

        private Godot.Collections.Array<BuoyancyProbe> probes;
        public override void _Ready()
        {
            probes = new Godot.Collections.Array<BuoyancyProbe>();

            foreach(Node child in GetChildren())
            {
                if(child.GetType() == typeof(BuoyancyProbe))
                    probes.Add((BuoyancyProbe)child);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            Vector3 gravity = new Vector3(0, -9.87f, 0);
            int submerged_probes = 0;
	        isSubmerged = false;
            foreach(BuoyancyProbe probe in probes)
            {
                float depth = (float)Mathf.Clamp((probe.GlobalPosition.Y - Ocean.Instance.GetWaveHeight(probe.GlobalPosition)), -10000.0, 0.0);
                float buoyancy = Mathf.Pow(Mathf.Abs(depth), bouyancyPower);
                
                if (depth < 0.0)
                {
                    isSubmerged = true;
                    submerged_probes++;
                    float multiplier = bouyancyMultiplier * probe.bouyancyMultiplier;
                    Vector3 force = -gravity * buoyancy * multiplier * (float)delta;
                    this.ApplyForce(force, probe.GlobalPosition - this.GlobalPosition);
                }
                    
            }    
        }

        public override void _IntegrateForces(PhysicsDirectBodyState3D state)
        {
            if(isSubmerged)
            {
                state.LinearVelocity *= 1.0f - submergedDragLinear;
                state.AngularVelocity *= 1.0f - submergedDragAngular;
            }
        }
    }
}