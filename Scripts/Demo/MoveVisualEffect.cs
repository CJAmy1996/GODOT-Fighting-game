using Godot;

namespace ModularFighter.Core;

/// <summary>One-shot authored visual spawned from a move timeline.</summary>
public partial class MoveVisualEffect : Node2D
{
	private static Shader _blackKeyAdditiveShader;
	private AnimatedSprite2D _sprite;
	private Vector2 _velocity;
	private float _horizontalDecelerationPerSecond;
	private int _facing = 1;
	private int _ageFrames;
	private int _fadeStartFrame = -1;
	private float _opacityLossPerSecond;
	private Vector2 _sourceStartScale = Vector2.One;
	private Vector2 _sourceEndScale = Vector2.One;
	private int _scaleStartFrame = -1;
	private int _scaleEndFrame = -1;
	private bool _scaleFromFacingBackEdge;
	private Vector2 _sourceStartPosition;
	private float _sourceTextureWidth;

	public void Initialize(SpriteFrames frames, string animationName, int facing, Vector2 scale,
		Vector2 visualOffset = default, bool additiveBlend = false, bool blackKey = false)
	{
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Animation = animationName,
			Position = new Vector2(visualOffset.X * (facing >= 0 ? 1 : -1), visualOffset.Y),
			Scale = new Vector2(scale.X * (facing >= 0 ? 1 : -1), scale.Y),
			Centered = true
		};
		if (blackKey)
		{
			_blackKeyAdditiveShader ??= CreateBlackKeyAdditiveShader();
			_sprite.Material = new ShaderMaterial { Shader = _blackKeyAdditiveShader };
		}
		else if (additiveBlend)
			_sprite.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		AddChild(_sprite);
		_sprite.AnimationFinished += QueueFree;
		_sprite.Play();
	}

	public void ConfigureSourceMotion(Vector2 velocity, float horizontalDecelerationPerFrame,
		int fadeStartFrame, float opacityLossPerFrame, Vector2 endScale,
		int scaleStartFrame, int scaleEndFrame, bool scaleFromFacingBackEdge, int facing)
	{
		_facing = facing >= 0 ? 1 : -1;
		_velocity = new Vector2(velocity.X * _facing, velocity.Y);
		_horizontalDecelerationPerSecond = horizontalDecelerationPerFrame * 60f;
		_fadeStartFrame = fadeStartFrame;
		_opacityLossPerSecond = opacityLossPerFrame * 60f / 255f;
		_sourceStartScale = _sprite?.Scale ?? Vector2.One;
		_sourceEndScale = new Vector2(endScale.X * _facing, endScale.Y);
		_scaleStartFrame = scaleStartFrame;
		_scaleEndFrame = Mathf.Max(scaleStartFrame, scaleEndFrame);
		_scaleFromFacingBackEdge = scaleFromFacingBackEdge;
		_sourceStartPosition = _sprite?.Position ?? Vector2.Zero;
		_sourceTextureWidth = _sprite?.SpriteFrames?.GetFrameTexture(_sprite.Animation, 0)?.GetWidth() ?? 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;
		Position += _velocity * step;
		if (_horizontalDecelerationPerSecond > 0f && !Mathf.IsZeroApprox(_velocity.X))
		{
			float speed = Mathf.Max(0f, Mathf.Abs(_velocity.X) - _horizontalDecelerationPerSecond * step);
			_velocity.X = speed * _facing;
		}
		_ageFrames++;
		if (_sprite != null && _fadeStartFrame >= 0 && _ageFrames >= _fadeStartFrame && _opacityLossPerSecond > 0f)
		{
			Color modulate = _sprite.Modulate;
			modulate.A = Mathf.Max(0f, modulate.A - _opacityLossPerSecond * step);
			_sprite.Modulate = modulate;
		}
		if (_sprite != null && _scaleStartFrame >= 0 && _ageFrames >= _scaleStartFrame)
		{
			float progress = _scaleEndFrame <= _scaleStartFrame
				? 1f
				: Mathf.Clamp((_ageFrames - _scaleStartFrame) / (float)(_scaleEndFrame - _scaleStartFrame), 0f, 1f);
			Vector2 currentScale = _sourceStartScale.Lerp(_sourceEndScale, progress);
			_sprite.Scale = currentScale;
			if (_scaleFromFacingBackEdge && _sourceTextureWidth > 0f)
			{
				float centerShift = _facing * _sourceTextureWidth *
					(Mathf.Abs(currentScale.X) - Mathf.Abs(_sourceStartScale.X)) * 0.5f;
				_sprite.Position = _sourceStartPosition + Vector2.Right * centerShift;
			}
		}
	}

	private static Shader CreateBlackKeyAdditiveShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;
			render_mode blend_add;

			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float energy = max(texel.r, max(texel.g, texel.b));
				float keyed_alpha = smoothstep(0.035, 0.12, energy) * texel.a;
				COLOR = vec4(texel.rgb, keyed_alpha);
			}
			""";
		return shader;
	}
}
