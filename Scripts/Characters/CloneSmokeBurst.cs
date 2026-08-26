using Godot;

namespace ModularFighter.Characters;

/// <summary>Short procedural smoke puff used for clone entrances and exits.</summary>
public partial class CloneSmokeBurst : Node2D
{
	[Export] public int LifetimeFrames { get; set; } = 24;
	private int _framesLeft;

	public override void _Ready()
	{
		ZIndex = 8;
		_framesLeft = Mathf.Max(1, LifetimeFrames);
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		_framesLeft--;
		if (_framesLeft <= 0)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		float progress = 1f - _framesLeft / (float)Mathf.Max(1, LifetimeFrames);
		float alpha = Mathf.Clamp(1f - progress, 0f, 1f) * 0.82f;
		float spread = Mathf.Lerp(8f, 54f, progress);
		float radius = Mathf.Lerp(18f, 31f, progress);
		Color outer = new(0.72f, 0.78f, 0.86f, alpha * 0.55f);
		Color inner = new(0.94f, 0.97f, 1f, alpha);
		Vector2[] offsets =
		{
			new(-spread, 4f), new(-spread * 0.5f, -15f), new(0f, 2f),
			new(spread * 0.5f, -12f), new(spread, 6f)
		};
		for (int index = 0; index < offsets.Length; index++)
		{
			float puffRadius = radius * (index == 2 ? 1.15f : 0.82f);
			DrawCircle(offsets[index], puffRadius, outer);
			DrawCircle(offsets[index] + new Vector2(-3f, -4f), puffRadius * 0.62f, inner);
		}
	}
}
