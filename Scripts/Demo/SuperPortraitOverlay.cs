using Godot;

namespace ModularFighter.Demo;

/// <summary>Short screen-space character cut-in that exists only during super activation freeze.</summary>
public partial class SuperPortraitOverlay : Node2D
{
	public Texture2D Portrait { get; set; }
	public StageCamera FightCamera { get; set; }
	public Vector2 FocusPosition { get; set; }
	public bool EntersFromLeft { get; set; }
	public int LifetimeFrames { get; set; } = 1;
	public float DriftDistance { get; set; } = 16f;

	private Sprite2D _sprite;
	private int _framesElapsed;
	private Vector2 _startPosition;
	private Vector2 _targetPosition;
	private Rect2 _cameraRect;
	private float _baseScale;
	private SuperActivationRings _rings;

	public override void _Ready()
	{
		if (Portrait == null)
		{
			QueueFree();
			return;
		}

		ZAsRelative = false;
		ZIndex = -40; // Blackout is below the portrait and gameplay sprites.
		_cameraRect = FightCamera?.CurrentFightBox ?? new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
		Vector2 portraitSize = Portrait.GetSize();
		float halfCameraWidth = _cameraRect.Size.X * 0.5f;
		_baseScale = halfCameraWidth / Mathf.Max(1f, portraitSize.X);
		float targetX = EntersFromLeft
			? _cameraRect.Position.X + halfCameraWidth * 0.5f
			: _cameraRect.End.X - halfCameraWidth * 0.5f;
		float offscreenX = EntersFromLeft
			? _cameraRect.Position.X - halfCameraWidth * 0.5f
			: _cameraRect.End.X + halfCameraWidth * 0.5f;
		float y = _cameraRect.GetCenter().Y;
		_startPosition = new Vector2(offscreenX, y);
		_targetPosition = new Vector2(targetX, y);
		GlobalPosition = Vector2.Zero;

		_sprite = new Sprite2D
		{
			Texture = Portrait,
			Position = _startPosition,
			Scale = Vector2.One * _baseScale,
			FlipH = !EntersFromLeft,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Modulate = Colors.White
		};
		float panelRadiusWorld = Mathf.Max(_cameraRect.Size.Y * 0.72f, _cameraRect.Size.X * 0.48f);
		var panelMaskShader = new Shader
		{
			Code = @"shader_type canvas_item;
uniform vec2 circle_center_uv = vec2(0.0, 0.5);
uniform float circle_radius_pixels = 256.0;
void fragment() {
	vec4 portrait = texture(TEXTURE, UV);
	vec2 texture_size = vec2(textureSize(TEXTURE, 0));
	vec2 from_circle_center = (UV - circle_center_uv) * texture_size;
	float edge = 1.0 - smoothstep(circle_radius_pixels - 1.5, circle_radius_pixels + 1.5, length(from_circle_center));
	COLOR = vec4(portrait.rgb, portrait.a * edge);
}"
		};
		var panelMaskMaterial = new ShaderMaterial { Shader = panelMaskShader };
		panelMaskMaterial.SetShaderParameter("circle_center_uv", new Vector2(EntersFromLeft ? 0f : 1f, 0.5f));
		panelMaskMaterial.SetShaderParameter("circle_radius_pixels", panelRadiusWorld / Mathf.Max(0.001f, _baseScale));
		_sprite.Material = panelMaskMaterial;
		_sprite.ZAsRelative = false;
		_sprite.ZIndex = -25; // Above blackout, below foreground effects and fighters.
		AddChild(_sprite);

		_rings = new SuperActivationRings
		{
			Name = "ForegroundActivationRings",
			PortraitSprite = _sprite,
			FocusPosition = FocusPosition,
			CameraRect = _cameraRect,
			PortraitEntersFromLeft = EntersFromLeft,
			LifetimeFrames = LifetimeFrames,
			ZAsRelative = false,
			ZIndex = -10 // Above the portrait, below gameplay sprites.
		};
		AddChild(_rings);
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_sprite == null) return;
		_framesElapsed++;
		float progress = Mathf.Clamp(_framesElapsed / (float)Mathf.Max(1, LifetimeFrames), 0f, 1f);
		float entranceProgress = Mathf.Clamp(progress / 0.34f, 0f, 1f);
		float eased = 1f - Mathf.Pow(1f - entranceProgress, 3f);
		float direction = EntersFromLeft ? 1f : -1f;
		_sprite.Position = _startPosition.Lerp(_targetPosition, eased) + new Vector2(direction * DriftDistance * progress, 0f);
		// Keep the portrait and its clipping circle at exactly the same radius.
		_sprite.Scale = Vector2.One * _baseScale;
		_rings.FramesElapsed = _framesElapsed;
		QueueRedraw();

		if (_framesElapsed >= LifetimeFrames) QueueFree();
	}

	public override void _Draw()
	{
		// Hide the stage, but leave the portrait (-25) and gameplay sprites (0) visible.
		// A two-frame white ignition flash gives way to pure black for the rest
		// of the activation freeze.
		DrawRect(_cameraRect, _framesElapsed <= 2 ? Colors.White : Colors.Black, true);

	}
}
