using Godot;
using System;

public partial class DebugCamera : Camera3D
{
	[Export] private float Speed = 6;
	[Export] private float Sensitivity = 1f;
	[Export] private bool lightEnabled = false;

	private float totalPitch = 0.0f;
	private Vector2 mousePosition = Vector2.Zero;

	private bool isActive = false;

	public override void _Ready()
	{
		foreach(Light3D light in GetChildren())
		{
			if(lightEnabled) light.Show();
			else light.Hide();
		}
	}

	public override void _Process(double delta)
	{
		if(Input.IsActionJustPressed("debug_camera_activate"))
		{
			isActive = !isActive;
			if(isActive)
				Input.MouseMode = Input.MouseModeEnum.Captured;
			else
				Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		if(isActive) 
		{
			CalculateCamera((float)delta);
			if(Input.IsActionJustPressed("debug_action"))
			{
				lightEnabled = !lightEnabled;
				foreach(Light3D light in GetChildren())
				{
					if(lightEnabled) light.Show();
					else light.Hide();
				}
			}

		}

	}	

	private void CalculateCamera(float delta)
	{
		Vector3 move = Vector3.Zero;
		if(Input.IsActionPressed("debug_camera_forward")) move += -this.Transform.Basis.Z;
		else if(Input.IsActionPressed("debug_camera_backward")) move += this.Transform.Basis.Z;

		if(Input.IsActionPressed("debug_camera_right")) move += this.Transform.Basis.X;
		else if(Input.IsActionPressed("debug_camera_left")) move += -this.Transform.Basis.X;


		this.Position += move * Speed * delta;

		UpdateMouse(mousePosition * delta);
		this.mousePosition = Vector2.Zero;


		if(Input.IsActionJustPressed("ui_cancel")) 
		{
			GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
			GetTree().Quit();
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion eventMouseMotion)
			mousePosition = eventMouseMotion.Relative;
	}

	private void UpdateMouse(Vector2 mousePosition)
	{
		mousePosition *= Sensitivity;
		float yaw = mousePosition.X;
		float pitch = mousePosition.Y;
		

		pitch = Mathf.Clamp(pitch, -90- totalPitch, 90 - totalPitch);
		totalPitch += pitch;

		RotateY(Mathf.DegToRad(-yaw));
		RotateObjectLocal(new Vector3(1,0,0), Mathf.DegToRad(-pitch));

	}
}
