using Godot;

namespace ModularFighter.Demo;

/// <summary>Hosts the source horizontal slash and blood burst as one authored contact spark.</summary>
public partial class CompositeSwordHitSpark : Node2D
{
	private int _framesLeft = 60;
	public override void _PhysicsProcess(double delta)
	{
		if (--_framesLeft <= 0) QueueFree();
	}
}
