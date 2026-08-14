using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Characters;

/// <summary>
/// Sanzou's dedicated fighter node. Character-specific behavior belongs here;
/// the shared presentation/controller remains framework code only.
/// </summary>
public partial class SanzoKongoumaruFighter : SpriteTestFighter
{
	private const string ParryGuardFlashPath = "res://Assets/TestFighter/Sanzo/Effects/parry_guard_flash.png";
	private static readonly string[] ParrySparkPaths =
	{
		"res://Assets/TestFighter/Sanzo/Effects/parry_spark_0.png",
		"res://Assets/TestFighter/Sanzo/Effects/parry_spark_1.png",
		"res://Assets/TestFighter/Sanzo/Effects/parry_spark_2.png",
		"res://Assets/TestFighter/Sanzo/Effects/parry_spark_3.png"
	};
	private Sprite2D _parryGuardFlash;
	private Material _baseCharacterMaterial;
	private ShaderMaterial _parryWindowFlashMaterial;
	private static readonly StringName ParryWhiteAmount = "white_flash_amount";
	private int _reflectorFreezeDrawingTick;
	private bool _reflectorActivationFreezePlaying;

	public override void _Ready()
	{
		base._Ready();
		_baseCharacterMaterial = CharacterSprite?.Material;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateParryWindowWhiteFlash();
		if (_parryGuardFlash == null) return;
		_parryGuardFlash.Visible = IsParrySuccessPresentationActive;
		_parryGuardFlash.Position = new Vector2(Facing * 64f, -76f);
		_parryGuardFlash.FlipH = Facing < 0;
	}

	private void UpdateParryWindowWhiteFlash()
	{
		if (CharacterSprite == null) return;
		if (!IsParryWindowActive)
		{
			if (CharacterSprite.Material == _parryWindowFlashMaterial)
				CharacterSprite.Material = _baseCharacterMaterial;
			return;
		}

		EnsureParryWindowFlashMaterial();
		CharacterSprite.Material = _parryWindowFlashMaterial;
		// Alternate a pure-white silhouette with a pale flash every two 60 Hz
		// frames so the complete 30-frame parry window is visually readable.
		float whiteAmount = ((Mathf.Max(0, CurrentAttackFrame) / 2) & 1) == 0 ? 1f : 0.35f;
		_parryWindowFlashMaterial.SetShaderParameter(ParryWhiteAmount, whiteAmount);
	}

	protected override int ResolveAttackDrawing(StringName animation)
	{
		// The activation freeze is real hitstop, so gameplay's attack frame does
		// not advance. Give only Sanzou's reflector a separate presentation clock
		// that ping-pongs authored ticks 3→4→5→4 throughout that freeze.
		bool reflectorActivationFreeze = CurrentAttackName == SanzoSuperReflectorName &&
			CurrentAttackFrame <= 0 && IsInHitstop;
		if (!reflectorActivationFreeze)
		{
			_reflectorActivationFreezePlaying = false;
			_reflectorFreezeDrawingTick = 0;
			return base.ResolveAttackDrawing(animation);
		}

		if (!_reflectorActivationFreezePlaying)
		{
			_reflectorActivationFreezePlaying = true;
			_reflectorFreezeDrawingTick = 0;
		}
		int[] sourceCycle = { 3, 4, 5, 4 };
		int sourceTick = sourceCycle[_reflectorFreezeDrawingTick++ % sourceCycle.Length];
		return AttackDrawingTimeline.ResolveSourceTick(CharacterSprite.SpriteFrames, animation, sourceTick);
	}

	private void EnsureParryWindowFlashMaterial()
	{
		if (GodotObject.IsInstanceValid(_parryWindowFlashMaterial)) return;
		var shader = new Shader
		{
			Code = @"shader_type canvas_item;
render_mode unshaded;
uniform float white_flash_amount : hint_range(0.0, 1.0) = 0.0;
void fragment() {
	vec4 source = texture(TEXTURE, UV);
	vec4 tint = COLOR;
	vec3 flashed = mix(source.rgb, vec3(1.0), white_flash_amount);
	COLOR = vec4(flashed, source.a) * tint;
}"
		};
		_parryWindowFlashMaterial = new ShaderMaterial { Shader = shader };
	}

	protected override void OnParrySuccessVisual(Vector2 hitPoint)
	{
		EnsureParryGuardFlash();
		_parryGuardFlash.Visible = true;
		SpawnParrySpark(hitPoint);
	}

	private void EnsureParryGuardFlash()
	{
		if (GodotObject.IsInstanceValid(_parryGuardFlash)) return;
		_parryGuardFlash = new Sprite2D
		{
			Name = "ParryGuardFlash320",
			Texture = ResourceLoader.Load<Texture2D>(ParryGuardFlashPath),
			Position = new Vector2(Facing * 64f, -76f),
			Scale = Vector2.One * 0.72f,
			ZIndex = 32,
			Visible = false
		};
		AddChild(_parryGuardFlash);
	}

	private void SpawnParrySpark(Vector2 hitPoint)
	{
		var frames = new SpriteFrames();
		StringName animation = "parry";
		frames.AddAnimation(animation);
		frames.SetAnimationLoop(animation, false);
		frames.SetAnimationSpeed(animation, 60f);
		foreach (string path in ParrySparkPaths)
			frames.AddFrame(animation, ResourceLoader.Load<Texture2D>(path));

		var spark = new AnimatedSprite2D
		{
			Name = "SanzouParrySpark330To333",
			SpriteFrames = frames,
			Animation = animation,
			TopLevel = true,
			ZAsRelative = false,
			ZIndex = 4096
		};
		Node parent = GetParent();
		if (parent == null) return;
		parent.AddChild(spark);
		spark.GlobalPosition = hitPoint;
		spark.AnimationFinished += () => spark.QueueFree();
		spark.Play(animation);
	}
}
