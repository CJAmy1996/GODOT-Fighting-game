using Godot;

namespace ModularFighter.Demo;

/// <summary>
/// Temporary editor-visible hit spark. Replace these scenes with real VFX later.
/// </summary>
public partial class HitSparkPlaceholder : Node2D
{
	[Export] public bool Heavy { get; set; }
	[Export] public int LifetimeFrames { get; set; } = 8;

	private int _framesLeft;

	public override void _Ready()
	{
		_framesLeft = LifetimeFrames;
		QueueRedraw();
	}

	public override void _Process(double delta)
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
		float life = Mathf.Clamp(_framesLeft / Mathf.Max(1f, LifetimeFrames), 0f, 1f);
		float radius = Heavy ? 38f * life : 24f * life;
		Color core = Heavy ? new Color(0.82f, 0.98f, 1f, life) : new Color(1f, 0.95f, 0.25f, life);
		Color edge = Heavy ? new Color(0.1f, 0.45f, 1f, life * 0.85f) : new Color(1f, 0.42f, 0.06f, life * 0.8f);
		Color white = new(1f, 1f, 1f, life);

		DrawCircle(Vector2.Zero, Heavy ? 8f * life : 5f * life, white);
		DrawCircle(Vector2.Zero, Heavy ? 5f * life : 3f * life, core);

		int rays = Heavy ? 12 : 8;
		for (int i = 0; i < rays; i++)
		{
			float angle = Mathf.Tau * i / rays + (Heavy ? 0.2f : 0f);
			Vector2 dir = Vector2.Right.Rotated(angle);
			float length = radius * (i % 2 == 0 ? 1f : 0.65f);
			DrawLine(-dir * radius * 0.22f, dir * length, i % 2 == 0 ? core : edge, Heavy ? 4f : 2.5f, true);
		}

		if (Heavy)
		{
			DrawArc(Vector2.Zero, radius * 0.72f, 0f, Mathf.Tau, 18, edge, 2f, true);
			DrawLine(new Vector2(-radius * 0.7f, -radius * 0.25f), new Vector2(radius * 0.25f, radius * 0.1f), white, 2f, true);
			DrawLine(new Vector2(-radius * 0.15f, radius * 0.4f), new Vector2(radius * 0.75f, -radius * 0.2f), edge, 2f, true);
		}
	}
}
