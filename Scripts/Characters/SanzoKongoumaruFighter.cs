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
	public const string ParryName = "SANZOU PARRY";
	public const string SuperReflectorName = "SUPER REFLECTOR";
	public const string SpdName = "SANZOU SPD";
	public const string SuperSpdName = "SANZOU SUPER SPD";
	public const string StompName = "STOMP SPECIAL";
	public const string CommandRunLightName = "COMMAND RUN LIGHT";
	public const string CommandRunHeavyName = "COMMAND RUN HEAVY";
	public const string CommandRunHopName = "COMMAND RUN HOP";
	public const string CommandRunPunchName = "COMMAND RUN PUNCH";
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

	protected override bool AllowsCloneCall => false;
	[ExportGroup("Sanzou SPD")]
	[Export] public float SpdRiseSpeed { get; set; } = 1450f;
	[Export] public int SpdSlamKnockdownFrames { get; set; } = 90;
	[Export] public int SpdLandingRecoveryFrames { get; set; } = 18;
	[Export] public float SuperSpdRiseSpeed { get; set; } = 3600f;
	[Export] public float SuperSpdDescentSpeed { get; set; } = 4200f;
	[Export] public int SuperSpdSlamKnockdownFrames { get; set; } = 150;
	[Export] public int SuperSpdLandingRecoveryFrames { get; set; } = 30;
	[ExportGroup("Selected Move Presentation")]
	[Export(PropertyHint.Range, "0.1,1.0,0.01")]
	public float SweepAndSpdVisualScale { get; set; } = 1f;

	protected override bool IsCharacterGrabAttack(string attackName) =>
		attackName == SpdName || attackName == SuperSpdName;
	protected override bool IsCharacterSuperGrabAttack(string attackName) => attackName == SuperSpdName;
	protected override bool IsCharacterSpecialAttack(string attackName) => attackName is
		ParryName or StompName or CommandRunLightName or CommandRunHeavyName or CommandRunHopName or CommandRunPunchName;
	protected override bool IsCharacterProjectileAttack(string attackName) => attackName == SuperReflectorName;
	protected override bool IsCharacterSuperAttack(string attackName) =>
		attackName == SuperReflectorName || attackName == SuperSpdName;
	protected override bool IsCharacterRunFollowup(string currentAttack, string nextAttack) =>
		(currentAttack == CommandRunLightName || currentAttack == CommandRunHeavyName) &&
		(nextAttack == CommandRunHopName || nextAttack == CommandRunPunchName);
	protected override bool CharacterSelfLaunchUsesFacing(string attackName) => attackName == CommandRunHopName;
	protected override float CharacterGrabRiseSpeed(bool super) => super ? SuperSpdRiseSpeed : SpdRiseSpeed;
	protected override float CharacterGrabDescentSpeed(bool super) => super ? SuperSpdDescentSpeed : 0f;
	protected override int CharacterGrabKnockdownFrames(bool super) =>
		super ? SuperSpdSlamKnockdownFrames : SpdSlamKnockdownFrames;
	protected override int CharacterGrabLandingRecoveryFrames(bool super) =>
		super ? SuperSpdLandingRecoveryFrames : SpdLandingRecoveryFrames;
	protected override int CharacterGrabConnectedRecoveryFrames(bool super) => super ? 360 : 180;
	protected override string CharacterGrabAirAnimationName => "spd_air_grab";
	protected override float CharacterSelectedVisualScale => SweepAndSpdVisualScale;
	protected override bool UsesCharacterSelectedVisualScale(string attackName, StringName animation) =>
		attackName == CrouchingHeavyKickName || attackName == SpdName || attackName == SuperSpdName ||
		animation == "crouching_heavy_kick" || animation == "spd_grab" || animation == "spd_air_grab";

	protected override bool TrySyncCharacterAttackDrawing(StringName animation)
	{
		if (!CharacterGrabConnected || animation != "spd_air_grab") return false;
		int drawings = CharacterSprite.SpriteFrames.GetFrameCount(animation);
		int flightTick = Mathf.Max(0, CurrentAttackFrame - CurrentAttackStartupFrames);
		CharacterSprite.SetFrameAndProgress((flightTick / 4) % Mathf.Max(1, drawings), 0f);
		return true;
	}

	protected override float CharacterReactionVerticalOffset(StringName animation) =>
		animation == "knockdown" || animation == "ground_bounce" ? 1f : 0f;
	protected override float CharacterLandingShakeMultiplier => CurrentAttackName == StompName ? 1.8f : 1f;
	protected override int CharacterLandingShakeExtraFrames => CurrentAttackName == StompName ? 3 : 0;
	protected override int CharacterLandingShakeMinimumFrames => CurrentAttackName == StompName ? 10 : 0;
	protected override bool UsesNeutralJumpAtLowHorizontalSpeed => true;

	protected override string ResolveCharacterSpecificAttack(FighterInput input)
	{
		if (input.Special2Pressed && WasGrounded &&
			Definition?.SpecialMoves?.FindMove(ParryName, false, false)?.Parry == true)
			return ParryName;
		if (input.Special1Pressed && WasGrounded &&
			Definition?.SpecialMoves?.FindMove(SpdName, false, false) != null)
			return SpdName;
		if (IsAttacking && (CurrentAttackName == CommandRunLightName || CurrentAttackName == CommandRunHeavyName))
		{
			if (CurrentInput.HeavyPunchPressed) return CommandRunPunchName;
			if (CurrentInput.LightPunchPressed) return CommandRunHopName;
		}
		if (HasChargedBackForwardCommand && WasGrounded)
		{
			if (input.HeavyPunchPressed) return CommandRunHeavyName;
			if (input.LightPunchPressed) return CommandRunLightName;
		}
		if (HasChargedDownUpCommand && (input.LightKickPressed || input.HeavyKickPressed)) return StompName;
		bool punchSuperChord = input.LightPunchPressed && input.HeavyPunchPressed;
		bool kickSuperChord = input.LightKickPressed && input.HeavyKickPressed;
		if (HasQuarterCircleForwardCommand && punchSuperChord && IsOnFloor() && FindSuperMove(SuperSpdName) != null)
			return SuperSpdName;
		if (HasQuarterCircleForwardCommand && kickSuperChord && FindSuperMove(SuperReflectorName) != null)
			return SuperReflectorName;
		return "";
	}

	protected override void OnCharacterAttackStarted(string attackName)
	{
		if (attackName == StompName) ConsumeChargedDownUpCommand();
		if (attackName == CommandRunLightName || attackName == CommandRunHeavyName)
			ConsumeChargedBackForwardCommand();
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
		bool reflectorActivationFreeze = CurrentAttackName == SuperReflectorName &&
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
