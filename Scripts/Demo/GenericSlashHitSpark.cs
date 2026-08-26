using Godot;

namespace ModularFighter.Demo;

/// <summary>
/// Shared roster slash contact, built from Revolve common actions 43-45
/// (斬撃ヒット_弱/中/強_横), drawing 274 / extracted common PNG 347.
/// </summary>
public partial class GenericSlashHitSpark : Node2D
{
	private static Shader _greenKeyAdditiveShader;
	[Export] public Texture2D SlashTexture { get; set; }
	[Export] public int Facing { get; set; } = 1;
	[Export] public bool Heavy { get; set; }

	private Sprite2D _sprite;
	private int _age;

	public override void _Ready()
	{
		_greenKeyAdditiveShader ??= CreateGreenKeyAdditiveShader();
		_sprite = new Sprite2D
		{
			Texture = SlashTexture,
			Centered = true,
			// Source horizontal slash: X 100%, Y 700% (medium) or 1000% (strong),
			// rotated 100 degrees. Mirroring follows the attacking fighter.
			Scale = new Vector2(Facing >= 0 ? 1f : -1f, Heavy ? 10f : 7f),
			RotationDegrees = 100f * (Facing >= 0 ? 1f : -1f),
			Material = new ShaderMaterial { Shader = _greenKeyAdditiveShader }
		};
		AddChild(_sprite);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_sprite == null) return;
		_age++;

		// The source 大 command collapses both axes rapidly while its 30-tick
		// drawing hold fades. Preserve that sharp cut instead of a static beam.
		float collapse = Mathf.Clamp(_age / 8f, 0f, 1f);
		float width = Mathf.Lerp(1f, 0.82f, collapse);
		float length = Mathf.Lerp(Heavy ? 10f : 7f, 0.35f, collapse);
		_sprite.Scale = new Vector2((Facing >= 0 ? 1f : -1f) * width, length);

		if (_age >= 5)
		{
			Color color = _sprite.Modulate;
			color.A = Mathf.Clamp(1f - (_age - 5) / 13f, 0f, 1f);
			_sprite.Modulate = color;
		}

		if (_age >= 18) QueueFree();
	}

	private static Shader CreateGreenKeyAdditiveShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;
			render_mode blend_add;

			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float green_key = smoothstep(0.04, 0.18,
					distance(texel.rgb, vec3(0.0, 1.0, 0.0)));
				float alpha = texel.a * green_key;
				COLOR = vec4(texel.rgb, alpha) * COLOR;
			}
			""";
		return shader;
	}
}
