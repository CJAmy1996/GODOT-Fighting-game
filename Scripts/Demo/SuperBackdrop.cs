using Godot;

namespace ModularFighter.Demo;

public partial class SuperBackdrop : Node2D
{
	[Export] public int LifetimeFrames { get; set; } = 90;
	[Export] public float Width { get; set; } = 1400f;
	[Export] public float Height { get; set; } = 900f;
	[Export] public int ParticleCount { get; set; } = 90;
	[Export(PropertyHint.Range, "0.1,40.0,0.1")] public float AnimationSpeedMultiplier { get; set; } = 4f;
	[Export(PropertyHint.Range, "1.0,60.0,1.0")] public float MaxAnimationTextureUpdatesPerSecond { get; set; } = 30f;
	[Export(PropertyHint.File, "*.gif")] public string AnimatedBackgroundPath { get; set; } = "";
	public Camera2D FollowCamera { get; set; }
	public bool AnimatedBackgroundReady => _animatedBackground != null;
	public int AnimatedBackgroundFrameCount => _animatedBackground?.FrameCount ?? 0;
	public int AnimatedBackgroundFrame => _animatedBackground?.CurrentFrame ?? -1;

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
	private AnimatedGifFrameSource _animatedBackground;
	private double _animationUpdateElapsedSeconds;

	public override void _Ready()
	{
		ProcessPhysicsPriority = 60;
		TextureFilter = TextureFilterEnum.Nearest;
		SyncToCameraView();
		if (!string.IsNullOrEmpty(AnimatedBackgroundPath) &&
			!AnimatedGifFrameSource.TryOpen(AnimatedBackgroundPath, out _animatedBackground, out string error))
			GD.PushWarning($"Hyper-combo background '{AnimatedBackgroundPath}' could not be opened: {error}");
		for (int i = 0; i < _particles.Length; i++)
		{
			_particles[i] = new Vector2(Mathf.PosMod(i * 97f, Width), Mathf.PosMod(i * 53f, Height));
			_speeds[i] = 280f + Mathf.PosMod(i * 41f, 520f);
			_radii[i] = 2.5f + Mathf.PosMod(i * 19f, 8f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		SyncToCameraView();
		_animationUpdateElapsedSeconds += delta;
		double updateInterval = 1.0 / Mathf.Max(1f, MaxAnimationTextureUpdatesPerSecond);
		if (_animatedBackground != null && _animationUpdateElapsedSeconds >= updateInterval)
		{
			// Playback time remains exact while expensive texture decoding is capped.
			// At 4x, skipped source drawings are intentionally never uploaded.
			_animatedBackground.Advance(_animationUpdateElapsedSeconds * Mathf.Max(0.1f, AnimationSpeedMultiplier));
			_animationUpdateElapsedSeconds = 0.0;
		}
		_ageFrames++;
		for (int i = 0; i < Mathf.Min(ParticleCount, _particles.Length); i++)
		{
			_particles[i].Y += _speeds[i] * (float)delta;
			if (_particles[i].Y > Height) _particles[i].Y -= Height + 40f;
		}
		if (_ageFrames >= LifetimeFrames) QueueFree();
		QueueRedraw();
	}

	public override void _ExitTree()
	{
		_animatedBackground?.Dispose();
		_animatedBackground = null;
	}

	public override void _Draw()
	{
		float fadeIn = Mathf.Clamp((_ageFrames + 1f) / 3f, 0f, 1f);
		float fadeOut = Mathf.Clamp((LifetimeFrames - _ageFrames) / 18f, 0f, 1f);
		float alpha = Mathf.Min(fadeIn, fadeOut);
		Rect2 screenRect = new(0f, 0f, Width, Height);
		DrawRect(screenRect, new Color(0.005f, 0.005f, 0.02f, 0.98f * alpha), true);
		if (_animatedBackground?.Texture != null)
		{
			DrawTextureRect(_animatedBackground.Texture, screenRect, false, new Color(1f, 1f, 1f, alpha));
			return;
		}
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

	private void SyncToCameraView()
	{
		if (!GodotObject.IsInstanceValid(FollowCamera)) return;
		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 zoom = FollowCamera.Zoom;
		float zoomX = Mathf.Max(0.001f, Mathf.Abs(zoom.X));
		float zoomY = Mathf.Max(0.001f, Mathf.Abs(zoom.Y));
		Width = viewportSize.X / zoomX;
		Height = viewportSize.Y / zoomY;
		Vector2 center = FollowCamera.GetScreenCenterPosition();
		GlobalPosition = center - new Vector2(Width, Height) * 0.5f;
	}
}
