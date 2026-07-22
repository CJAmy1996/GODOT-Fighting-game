using Godot;

namespace ModularFighter.Demo;

public partial class SuperBackdrop : Node2D
{
	[Export] public int LifetimeFrames { get; set; } = 90;
	[Export] public float Width { get; set; } = 1400f;
	[Export] public float Height { get; set; } = 900f;
	[Export] public int ParticleCount { get; set; } = 90;

	private readonly Vector2[] _particles = new Vector2[128];
	private readonly float[] _speeds = new float[128];
	private readonly float[] _radii = new float[128];
	private static readonly Color[] ParticleColors =
	{
		new(1f, 0.1f, 0.08f, 1f),
		new(0.1f, 0.45f, 1f, 1f),
		new(1f, 0.9f, 0.08f, 1f),
		new(0.15f, 1f, 0.25f, 1f)
	};
	private int _ageFrames;

	public override void _Ready()
	{
		for (int i = 0; i < _particles.Length; i++)
		{
			_particles[i] = new Vector2(Mathf.PosMod(i * 97f, Width), Mathf.PosMod(i * 53f, Height));
			_speeds[i] = 280f + Mathf.PosMod(i * 41f, 520f);
			_radii[i] = 2.5f + Mathf.PosMod(i * 19f, 8f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		_ageFrames++;
		for (int i = 0; i < Mathf.Min(ParticleCount, _particles.Length); i++)
		{
			_particles[i].Y += _speeds[i] * (float)delta;
			if (_particles[i].Y > Height) _particles[i].Y -= Height + 40f;
		}
		if (_ageFrames >= LifetimeFrames) QueueFree();
		QueueRedraw();
	}

	public override void _Draw()
	{
		float fadeIn = Mathf.Clamp((_ageFrames + 1f) / 3f, 0f, 1f);
		float fadeOut = Mathf.Clamp((LifetimeFrames - _ageFrames) / 18f, 0f, 1f);
		float alpha = Mathf.Min(fadeIn, fadeOut);
		DrawRect(new Rect2(0f, 0f, Width, Height), new Color(0.015f, 0.015f, 0.09f, 0.92f * alpha), true);
		int count = Mathf.Min(ParticleCount, _particles.Length);
		for (int i = 0; i < count; i++)
		{
			float pulse = 0.5f + 0.5f * Mathf.Sin((_ageFrames + i * 7f) * 0.16f);
			Color baseColor = ParticleColors[i % ParticleColors.Length];
			Color color = baseColor.Lightened(0.18f + pulse * 0.18f);
			color.A = (0.25f + pulse * 0.45f) * alpha;
			DrawCircle(_particles[i], _radii[i], color);
			DrawLine(_particles[i] - new Vector2(0f, _radii[i] * 4f), _particles[i] + new Vector2(0f, _radii[i] * 7f), color, Mathf.Max(1.5f, _radii[i] * 0.6f), true);
		}
	}
}
