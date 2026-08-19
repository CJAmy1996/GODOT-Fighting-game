using System;
using Godot;

namespace ModularFighter.Demo;

/// <summary>
/// Reconstructs the BBB Special Impact controller as three simultaneous 60 Hz
/// layers: numbered lightning 000-007, lightning 008-015, and impact 017-036.
/// </summary>
public partial class BigBangSuperCancelEffect : Node2D
{
	private const string AssetRoot = "res://Assets/Effects/BigBangCommon";
	private const int LightningDrawingCount = 8;
	private const int CoreDrawingCount = 20;
	private const int LightningHoldTicks = 2;
	private const int LifetimeTicks = 20;
	private static Shader _greenKeyShader;

	private readonly Texture2D[] _innerTextures = new Texture2D[LightningDrawingCount];
	private readonly Texture2D[] _outerTextures = new Texture2D[LightningDrawingCount];
	private readonly Texture2D[] _coreTextures = new Texture2D[CoreDrawingCount];
	private Sprite2D _inner;
	private Sprite2D _innerOpposite;
	private Sprite2D _outer;
	private Sprite2D _outerQuarterTurn;
	private Sprite2D _core;
	private int _age;

	public int CurrentTick => _age;
	public int CurrentInnerFrame => Mathf.Min(7, _age / LightningHoldTicks);
	public int CurrentOuterFrame => 8 + Mathf.Min(7, _age / LightningHoldTicks);
	public int CurrentCoreFrame => 17 + Mathf.Min(19, _age);
	public bool LightningVisible => _age < LightningDrawingCount * LightningHoldTicks;
	public int TotalTicks => LifetimeTicks;

	public override void _Ready()
	{
		for (int index = 0; index < LightningDrawingCount; index++)
		{
			_innerTextures[index] = GD.Load<Texture2D>($"{AssetRoot}/{index:D3}.png");
			_outerTextures[index] = GD.Load<Texture2D>($"{AssetRoot}/{index + 8:D3}.png");
		}
		for (int index = 0; index < CoreDrawingCount; index++)
			_coreTextures[index] = GD.Load<Texture2D>($"{AssetRoot}/{index + 17:D3}.png");

		_greenKeyShader ??= CreateGreenKeyShader();
		_inner = CreateLayer("InnerLightning", zIndex: 0);
		_innerOpposite = CreateLayer("InnerLightningOpposite", zIndex: 0);
		_innerOpposite.Rotation = Mathf.Pi;
		_outer = CreateLayer("OuterLightning", zIndex: 1, scale: 0.5f);
		_outerQuarterTurn = CreateLayer("OuterLightningQuarterTurn", zIndex: 1, scale: 0.5f);
		_outerQuarterTurn.Rotation = Mathf.Pi * 0.5f;
		_core = CreateLayer("ImpactRing", zIndex: 2);
		ApplyTick();
	}

	public override void _PhysicsProcess(double delta)
	{
		AdvanceOneTick();
	}

	public void AdvanceOneTick()
	{
		_age++;
		if (_age >= LifetimeTicks)
		{
			QueueFree();
			return;
		}
		ApplyTick();
	}

	private Sprite2D CreateLayer(string name, int zIndex, float scale = 1f)
	{
		var layer = new Sprite2D
		{
			Name = name,
			Centered = true,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ZIndex = zIndex,
			Scale = Vector2.One * scale,
			Material = new ShaderMaterial { Shader = _greenKeyShader }
		};
		AddChild(layer);
		return layer;
	}

	private void ApplyTick()
	{
		int lightningIndex = Mathf.Min(7, _age / LightningHoldTicks);
		_inner.Texture = _innerTextures[lightningIndex];
		_innerOpposite.Texture = _innerTextures[lightningIndex];
		_outer.Texture = _outerTextures[lightningIndex];
		_outerQuarterTurn.Texture = _outerTextures[lightningIndex];
		_core.Texture = _coreTextures[Mathf.Min(19, _age)];
		bool showLightning = LightningVisible;
		_inner.Visible = showLightning;
		_innerOpposite.Visible = showLightning;
		_outer.Visible = showLightning;
		_outerQuarterTurn.Visible = showLightning;
	}

	private static Shader CreateGreenKeyShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;

			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float dominant_green = texel.g - max(texel.r, texel.b);
				float keyed = step(0.45, texel.g) * step(0.22, dominant_green);
				COLOR = vec4(texel.rgb, texel.a * (1.0 - keyed));
			}
			""";
		return shader;
	}
}
