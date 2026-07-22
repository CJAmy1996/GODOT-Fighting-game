using Godot;

namespace ModularFighter.Demo;

/// <summary>
/// Temporary editor-visible hit spark. Replace these scenes with real VFX later.
/// </summary>
public partial class HitSparkPlaceholder : Node2D
{
	[Export] public bool Heavy { get; set; }
	[Export] public int LifetimeFrames { get; set; } = 8;
	[Export] public float LightEnergy { get; set; } = 1.8f;
	[Export] public float LightTextureScale { get; set; } = 0.55f;

	private int _framesLeft;
	private float _rotationOffset;
	private float _sizeMultiplier;
	private PointLight2D _light;

	public override void _Ready()
	{
		TopLevel = true;
		ZAsRelative = false;
		ZIndex = 4096;
		_framesLeft = LifetimeFrames;
		_rotationOffset = (float)GD.RandRange(0.0, Mathf.Tau);
		_sizeMultiplier = (float)GD.RandRange(0.85, 1.25);
		_light = new PointLight2D
		{
			Name = "HitSparkLight2D",
			Color = Heavy ? new Color(0.35f, 0.7f, 1f, 1f) : new Color(1f, 0.72f, 0.12f, 1f),
			Energy = Heavy ? LightEnergy * 1.35f : LightEnergy,
			TextureScale = Heavy ? LightTextureScale * 1.35f : LightTextureScale,
			ShadowEnabled = false,
			BlendMode = Light2D.BlendModeEnum.Add
		};
		_light.Texture = CreateLightTexture();
		AddChild(_light);
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
		if (_light != null)
		{
			float life = Mathf.Clamp(_framesLeft / Mathf.Max(1f, LifetimeFrames), 0f, 1f);
			_light.Energy = (Heavy ? LightEnergy * 1.35f : LightEnergy) * life;
			_light.TextureScale = (Heavy ? LightTextureScale * 1.35f : LightTextureScale) * (0.75f + 0.45f * life);
		}
		QueueRedraw();
	}

	private static Texture2D CreateLightTexture()
	{
		const int size = 64;
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Vector2 center = new(size * 0.5f, size * 0.5f);
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float distance = new Vector2(x, y).DistanceTo(center) / (size * 0.5f);
			float alpha = Mathf.Pow(Mathf.Clamp(1f - distance, 0f, 1f), 2.2f);
			image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
		}
		return ImageTexture.CreateFromImage(image);
	}

	public override void _Draw()
	{
		float life = Mathf.Clamp(_framesLeft / Mathf.Max(1f, LifetimeFrames), 0f, 1f);
		float radius = (Heavy ? 62f : 45f) * life * _sizeMultiplier;
		float alpha = Mathf.Clamp(life * 1.55f, 0f, 1f);
		Color core = Heavy ? new Color(0.9f, 1f, 1f, alpha) : new Color(1f, 1f, 0.28f, alpha);
		Color edge = Heavy ? new Color(0.2f, 0.6f, 1f, alpha) : new Color(1f, 0.3f, 0.04f, alpha);
		Color white = new(1f, 1f, 1f, alpha);

		DrawStar(Vector2.Zero, radius, radius * 0.34f, Heavy ? 8 : 6, edge, _rotationOffset - life * 1.2f);
		DrawStar(Vector2.Zero, radius * 0.62f, radius * 0.2f, Heavy ? 8 : 6, core, _rotationOffset + life * 1.8f);
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

	private void DrawStar(Vector2 center, float outerRadius, float innerRadius, int points, Color color, float rotation)
	{
		Vector2[] vertices = new Vector2[points * 2];
		for (int i = 0; i < vertices.Length; i++)
		{
			float radius = i % 2 == 0 ? outerRadius : innerRadius;
			float angle = rotation + i * Mathf.Pi / points;
			vertices[i] = center + Vector2.Right.Rotated(angle) * radius;
		}
		DrawColoredPolygon(vertices, color);
	}
}
