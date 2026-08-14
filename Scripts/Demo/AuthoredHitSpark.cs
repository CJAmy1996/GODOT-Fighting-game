using Godot;

namespace ModularFighter.Demo;

public partial class AuthoredHitSpark : AnimatedSprite2D
{
	public override void _Ready()
	{
		AnimationFinished += QueueFree;
		Play();
	}
}
