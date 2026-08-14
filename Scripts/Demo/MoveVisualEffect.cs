using Godot;

namespace ModularFighter.Core;

/// <summary>One-shot authored visual spawned from a move timeline.</summary>
public partial class MoveVisualEffect : Node2D
{
	private static Shader _blackKeyAdditiveShader;
	private AnimatedSprite2D _sprite;

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
