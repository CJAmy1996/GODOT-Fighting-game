using System.Collections.Generic;
using Godot;
using ModularFighter.Movement;

namespace ModularFighter.Core;

public enum FighterHitState
{
	None,
	Hitstun,
	CounterHit,
	Tumble,
	Knockdown,
	GroundedKnockdown,
	WallBounce,
	GroundBounce,
	Crumple
}

/// <summary>
/// Shared deterministic-ish movement loop. Attacks, hitstun and rollback can feed this class
/// command input without changing any movement ability.
/// </summary>
public partial class FighterController : CharacterBody2D
{
	[Export] public FighterDefinition Definition { get; set; }
	[Export] public bool ReadLocalInput { get; set; } = true;
	[Export] public bool FaceWithMovement { get; set; } = true;
	[ExportGroup("Collision")]
	[Export] public Rect2 PushboxLocal { get; set; } = new(-28f, -50f, 56f, 100f);
	[Export] public Rect2 AirbornePushboxLocal { get; set; } = new(-20f, -42f, 40f, 78f);
	[Export] public Rect2 HurtboxLocal { get; set; } = new(-32f, -92f, 64f, 142f);
	[Export] public Rect2 HitboxLocal { get; set; } = new(22f, -68f, 54f, 44f);
	[Export] public Rect2 PositionBoxLocal { get; set; } = new(-4f, -46f, 8f, 8f);
	[Export] public bool DebugDrawCombatBoxes { get; set; }
	[ExportGroup("Basic Attacks")]
	[Export] public int BasicAttackStartupFrames { get; set; } = 4;
	[Export] public int BasicAttackActiveFrames { get; set; } = 5;
	[Export] public int BasicAttackRecoveryFrames { get; set; } = 10;
	[Export] public int BasicAttackHitstunFrames { get; set; } = 20;
	[Export] public float BasicAttackPushback { get; set; } = 220f;
	[Export] public float BasicAttackFriction { get; set; } = 9000f;
	[Export] public float RunningAttackFriction { get; set; } = 2600f;
	[Export] public float RunStopSlideFriction { get; set; } = 4200f;
	[Export] public int RunStopSlideFrames { get; set; } = 8;
	[Export] public int RunCrouchSlideFrames { get; set; } = 12;
	[Export] public int LightAttackStartupFrames { get; set; } = 5;
	[Export] public int GroundLightAttackStartupFrames { get; set; } = 4;
	[Export] public int LightPunchActiveFrames { get; set; } = 3;
	[Export] public int LightKickActiveFrames { get; set; } = 4;
	[Export] public int LightAttackRecoveryFrames { get; set; } = 8;
	[Export] public int GroundLightAttackRecoveryFrames { get; set; } = 6;
	[Export] public int LightChainEarliestActiveFramesLeft { get; set; } = 3;
	[Export] public int HeavyPunchActiveFrames { get; set; } = 4;
	[Export] public int HeavyKickActiveFrames { get; set; } = 5;
	[Export] public int HeavyAttackRecoveryFrames { get; set; } = 12;
	[Export] public int SpecialAttackActiveFrames { get; set; } = 6;
	[Export] public int SpecialAttackRecoveryFrames { get; set; } = 13;
	[Export] public int LightAttackHitstunFrames { get; set; } = 10;
	[Export] public int HeavyAttackHitstunFrames { get; set; } = 14;
	[Export] public int SpecialAttackHitstunFrames { get; set; } = 20;
	[Export] public float LightAttackPushback { get; set; } = 520f;
	[Export] public float HeavyAttackPushback { get; set; } = 1240f;
	[Export] public float SpecialAttackPushback { get; set; } = 560f;
	[Export] public float AirAttackPushbackMultiplier { get; set; } = 0.25f;
	[Export] public float GroundToAirPushbackMultiplier { get; set; } = 0.55f;
	[Export] public int LightAttackHitstopFrames { get; set; } = 5;
	[Export] public int HeavyAttackHitstopFrames { get; set; } = 11;
	[Export] public int SpecialAttackHitstopFrames { get; set; } = 6;
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float NormalAttackHitstopMultiplier { get; set; } = 0.5f;
	[Export] public int GlobalHitstopBonusFrames { get; set; } = 6;
	[Export] public int GroundedAttackHitstopBonusFrames { get; set; } = 4;
	[Export] public int AirAttackHitstopBonusFrames { get; set; } = 2;
	[Export] public int LightAirToGroundAttackerHitstopFrames { get; set; } = 4;
	[Export] public int HeavyAirToGroundAttackerHitstopFrames { get; set; } = 7;
	[Export] public int SpecialAirToGroundAttackerHitstopFrames { get; set; } = 5;
	[Export] public float LightAttackShakeStrength { get; set; } = 2.5f;
	[Export] public float HeavyAttackShakeStrength { get; set; } = 9f;
	[Export] public float SpecialAttackShakeStrength { get; set; } = 5f;
	[Export] public int ComboDisplayFrames { get; set; } = 90;
	[Export] public int AirDashAttackCancelDelayFrames { get; set; } = 2;
	[Export] public float LightAirAttackMomentumMultiplier { get; set; } = 1.08f;
	[Export] public float HeavyAirAttackMomentumMultiplier { get; set; } = 0.68f;
	[Export] public float SpecialAirAttackMomentumMultiplier { get; set; } = 0.9f;
	[Export] public float LightAirAttackMomentumBoost { get; set; } = 35f;
	[Export] public int AirChainEarliestActiveFramesLeft { get; set; } = 4;
	[Export] public float AirHitPopUpSpeed { get; set; } = 620f;
	[Export] public float HeavyAirAttackSpikeSpeed { get; set; } = 980f;
	[Export] public int AirToAirHitstunBonusFrames { get; set; } = 8;
	[Export] public int AirLightHitJumpCancelWindowFrames { get; set; } = 20;
	[Export] public float ComboGravityScalePerHit { get; set; } = 0.12f;
	[Export] public float MaxComboGravityScale { get; set; } = 2.2f;
	[Export] public int CounterHitExtraHitstunFrames { get; set; } = 4;
	[Export] public float GroundBounceSpeed { get; set; } = 900f;
	[Export] public float WallBounceHorizontalSpeed { get; set; } = 850f;
	[ExportGroup("Move Rules")]
	[Export] public float DefaultLauncherSpeed { get; set; } = 1820f;
	[Export] public float DefaultLauncherPushback { get; set; } = 180f;
	[Export] public int DefaultLauncherHitstunFrames { get; set; } = 30;
	[Export] public int DefaultJumpCancelWindowFrames { get; set; } = 30;
	[Export] public float DefaultLauncherChaseJumpSpeed { get; set; } = 1820f;
	[Export] public float DefaultLauncherChaseForwardSpeed { get; set; } = 360f;
	// Air dash / double-jump height gate leniency. Raise this to allow air actions earlier before jump peak.
	[Export] public float AirActionPeakVelocityLeniency { get; set; } = 140f;
	[ExportGroup("Attack Hitboxes")]
	[Export] public Rect2 LightPunchHitboxLocal { get; set; } = new(24f, -58f, 42f, 28f);
	[Export] public Rect2 LightKickHitboxLocal { get; set; } = new(18f, -24f, 58f, 24f);
	[Export] public Rect2 HeavyPunchHitboxLocal { get; set; } = new(26f, -66f, 68f, 36f);
	[Export] public Rect2 HeavyKickHitboxLocal { get; set; } = new(18f, -32f, 84f, 30f);
	[Export] public Rect2 CrouchingHeavyKickHitboxLocal { get; set; } = new(18f, 10f, 96f, 28f);
	[Export] public Rect2 Special1HitboxLocal { get; set; } = new(20f, -70f, 76f, 52f);
	[Export] public Rect2 Special2HitboxLocal { get; set; } = new(18f, -50f, 88f, 42f);
	[Export] public Rect2 ElectricWindGodFistHitboxLocal { get; set; } = new(24f, -66f, 72f, 46f);
	[ExportGroup("Projectile Specials")]
	[Export] public float LightProjectileSpeed { get; set; } = 720f;
	[Export] public float HeavyProjectileSpeed { get; set; } = 1040f;
	[Export] public Vector2 ProjectileSpawnOffset { get; set; } = new(70f, -42f);
	[ExportGroup("Visual Smoothing")]
	[Export] public float VisualCorrectionSlideSpeed { get; set; } = 900f;
	[Export] public float MaxVisualCorrectionOffset { get; set; } = 80f;

	public FighterInput CurrentInput { get; private set; }
	public FighterInput ActionInput { get; private set; }
	public int Facing { get; private set; } = 1;
	public bool WasGrounded { get; private set; }
	public bool JustLanded { get; private set; }
	public int AirTimeFrames { get; private set; }
	public int CoyoteFramesLeft { get; private set; }
	public int JumpBufferFramesLeft { get; private set; }
	public int DashBufferFramesLeft { get; private set; }
	public int AttackBufferFramesLeft { get; private set; }
	public float BufferedJumpHorizontal { get; private set; }
	public int BufferedJumpFacing { get; private set; } = 1;
	public float JumpInputHorizontal => CurrentInput.JumpPressed ? CurrentInput.Horizontal : BufferedJumpHorizontal;
	public int JumpInputFacing => CurrentInput.JumpPressed ? Facing : BufferedJumpFacing;
	public int DashInputDirection => _motionInputBuffer.DashCommandDirection != 0 ? _motionInputBuffer.DashCommandDirection : Facing;
	public MovementAbility ActiveAbility { get; private set; }
	/// <summary>Pushbox in world space. It follows this fighter's origin exactly.</summary>
	public Rect2 ActivePushboxLocal => !IsOnFloor() && SuppressesGroundedPushWhileAirborne ? AirbornePushboxLocal : PushboxLocal;
	public Rect2 WorldPushbox => new(GlobalPosition + ActivePushboxLocal.Position, ActivePushboxLocal.Size);
	public Rect2 WorldHurtbox => GetFirstActiveWorldBox(FighterBoxKind.Hurtbox, HurtboxLocal, false);
	public Rect2 CurrentHitboxLocal => GetFirstActiveLocalBox(FighterBoxKind.Hitbox, IsAttacking ? _currentAttackHitboxLocal : HitboxLocal);
	public Rect2 WorldHitbox => GetFirstActiveWorldBox(FighterBoxKind.Hitbox, CurrentHitboxLocal, true);
	/// <summary>Small torso/axis tracker used for side/crossup decisions. It does not push anything.</summary>
	public Rect2 WorldPositionBox => new(GlobalPosition + PositionBoxLocal.Position, PositionBoxLocal.Size);
	public Vector2 PreviousGlobalPosition { get; private set; }
	public Rect2 PreviousWorldPositionBox => new(PreviousGlobalPosition + PositionBoxLocal.Position, PositionBoxLocal.Size);
	/// <summary>Current air-state rule; persists after jump startup ends, until landing.</summary>
	public bool SuppressesGroundedPushWhileAirborne { get; private set; }
	public bool EnablesAirControlWhileAirborne { get; private set; }
	public float AirDecelerationMultiplierWhileAirborne { get; private set; } = 1f;
	public int AirActionsUsed { get; private set; }
	public int LandingLagFramesLeft { get; private set; }
	public bool AirActionsRequirePeakThisJump { get; private set; }
	public bool AirJumpsDisabledThisJump { get; private set; }
	public bool ShortHopInteractsWithGroundedPushbox { get; private set; }
	public bool ShortHopPushesGroundedOpponent { get; private set; }
	public bool JumpInteractsWithGroundedPushbox { get; private set; }
	public float JumpGroundedPushStrength { get; private set; }
	public bool IsInDoubleJumpState { get; private set; }
	public bool IsInSuperJumpRoute { get; private set; }
	public bool IsAttacking => _attackStartupFramesLeft > 0 || _attackActiveFramesLeft > 0 || _attackRecoveryFramesLeft > 0;
	public bool IsAttackActive => _attackActiveFramesLeft > 0;
	public bool IsCrouchAttackLocked => IsAttacking && _currentAttackStartedCrouching;
	public bool CurrentAttackStartedAirborne => _currentAttackStartedAirborne;
	public int CurrentAirToGroundAttackerHitstopFrames => GetAirToGroundAttackerHitstopFrames(CurrentAttackName);
	public bool IsInHitstun => HitstunFramesLeft > 0;
	public bool IsKnockedDown => (HitState == FighterHitState.Knockdown || HitState == FighterHitState.GroundedKnockdown ||
		HitState == FighterHitState.WallBounce || HitState == FighterHitState.GroundBounce || HitState == FighterHitState.Crumple) && HitstunFramesLeft > 0;
	public bool IsGroundedKnockdown => HitState == FighterHitState.GroundedKnockdown && HitstunFramesLeft > 0;
	public FighterHitState HitState { get; private set; } = FighterHitState.None;
	public KnockdownType CurrentKnockdownType { get; private set; } = KnockdownType.None;
	public bool IsInHitstop => HitstopFramesLeft > 0;
	public int HitstunFramesLeft { get; private set; }
	public int HitstopFramesLeft { get; private set; }
	public int ComboCount { get; private set; }
	public int ComboDisplayFramesLeft { get; private set; }
	public string CurrentAttackName { get; private set; } = "";
	public int CurrentAttackFrame { get; private set; }
	public int CurrentAttackDamage => _currentAttackDamage;
	public int CurrentAttackBlockstunFrames => _currentAttackBlockstunFrames;
	public bool CurrentAttackKnocksDown => _currentAttackKnocksDown;
	public int CurrentAttackKnockdownFrames => _currentAttackKnockdownFrames;
	public KnockdownType CurrentAttackKnockdownType => _currentAttackKnockdownType;
	public bool CurrentAttackCanHitGroundedKnockdown => _currentAttackCanHitGroundedKnockdown;
	public Vector2 VisualCorrectionOffset { get; private set; }
	public readonly Dictionary<string, AbilityRuntime> Runtime = new();
	private readonly MotionInputBuffer _motionInputBuffer = new();
	private readonly Dictionary<string, int> _airJumpUses = new();
	private readonly Dictionary<string, int> _normalUsesThisChain = new();
	private bool _groundedLastFrame;
	private int _pendingLandingLagFrames;
	private int _lightPunchBufferFramesLeft;
	private int _lightKickBufferFramesLeft;
	private int _heavyPunchBufferFramesLeft;
	private int _heavyKickBufferFramesLeft;
	private int _special1BufferFramesLeft;
	private int _special2BufferFramesLeft;
	private int _runCrouchSlideFramesLeft;
	private int _runStopSlideFramesLeft;
	private bool _doubleJumpAirDashAvailable;
	private int _attackStartupFramesLeft;
	private int _attackActiveFramesLeft;
	private int _attackRecoveryFramesLeft;
	private bool _attackHasHit;
	private bool _projectileSpawnedThisAttack;
	private bool _currentAttackStartedAirborne;
	private bool _currentAttackStartedFromAirDash;
	private bool _currentAttackStartedFromRun;
	private bool _currentAttackStartedCrouching;
	private int _currentAttackStartupFrames;
	private int _currentAttackActiveFrames;
	private int _currentAttackRecoveryFrames;
	private int _currentAttackHitstunFrames;
	private float _currentAttackPushback;
	private int _currentAttackHitstopFrames;
	private float _currentAttackShakeStrength;
	private int _currentAttackDamage;
	private int _currentAttackBlockstunFrames;
	private bool _currentAttackKnocksDown;
	private int _currentAttackKnockdownFrames;
	private KnockdownType _currentAttackKnockdownType;
	private bool _currentAttackCanHitGroundedKnockdown;
	private HitReactionKind _currentAttackHitReaction;
	private Rect2 _currentAttackHitboxLocal;
	private int _launcherJumpCancelFramesLeft;
	private int _airLightJumpCancelFramesLeft;
	private NormalMoveRule _currentMoveRule;
	private const int DoubleTapDashWindowFrames = 12;
	private const int QuarterCircleForwardWindowFrames = 9;
	private const int QuarterCircleForwardLatchFrames = 9;
	private const int BackDashInputLockoutWindowFrames = 18;
	private const string ElectricWindGodFistName = "ELECTRIC WIND GOD FIST";
	private const string LightProjectileName = "LIGHT PROJECTILE";
	private const string HeavyProjectileName = "HEAVY PROJECTILE";

	private readonly struct NormalMoveRule
	{
		public static readonly NormalMoveRule None = new();
		public bool Launches { get; init; }
		public float LaunchSpeed { get; init; }
		public float LaunchPushback { get; init; }
		public int LaunchHitstunFrames { get; init; }
		public int JumpCancelWindowFrames { get; init; }
		public float ChaseJumpSpeed { get; init; }
		public float ChaseForwardSpeed { get; init; }
		public bool CanChainToLight { get; init; }
		public bool CanChainToHeavy { get; init; }
		public bool CanChainToSpecial { get; init; }
		public string[] AllowedChainTargets { get; init; }
		public int MaxUsesPerCombo { get; init; }
		public bool ChainRequiresContact { get; init; }
		public int ChainEarliestActiveFramesLeft { get; init; }
		public int CancelWindowStartFrame { get; init; }
		public int CancelWindowEndFrame { get; init; }
		public int Damage { get; init; }
		public int HitstunFramesOverride { get; init; }
		public int BlockstunFrames { get; init; }
		public int HitstopFramesOverride { get; init; }
		public float PushbackOverride { get; init; }
		public HitReactionKind HitReaction { get; init; }
		public KnockdownType KnockdownType { get; init; }
		public bool KnocksDown { get; init; }
		public int KnockdownFrames { get; init; }
		public bool CanHitGroundedKnockdown { get; init; }
		public FighterBoxFrame[] BoxTimeline { get; init; }

		public static NormalMoveRule FromData(NormalMoveData data) => data == null
			? None
			: new NormalMoveRule
			{
				Launches = data.Launches,
				LaunchSpeed = data.LaunchSpeed,
				LaunchPushback = data.LaunchPushback,
				LaunchHitstunFrames = data.LaunchHitstunFrames,
				JumpCancelWindowFrames = data.JumpCancelWindowFrames,
				ChaseJumpSpeed = data.ChaseJumpSpeed,
				ChaseForwardSpeed = data.ChaseForwardSpeed,
				CanChainToLight = data.CanChainToLight,
				CanChainToHeavy = data.CanChainToHeavy,
				CanChainToSpecial = data.CanChainToSpecial,
				AllowedChainTargets = data.AllowedChainTargets,
				MaxUsesPerCombo = data.MaxUsesPerCombo,
				ChainRequiresContact = data.ChainRequiresContact,
				ChainEarliestActiveFramesLeft = data.ChainEarliestActiveFramesLeft,
				CancelWindowStartFrame = data.CancelWindowStartFrame,
				CancelWindowEndFrame = data.CancelWindowEndFrame,
				Damage = data.Damage,
				HitstunFramesOverride = data.HitstunFrames,
				BlockstunFrames = data.BlockstunFrames,
				HitstopFramesOverride = data.HitstopFrames,
				PushbackOverride = data.Pushback,
				HitReaction = data.HitReaction,
				KnockdownType = data.KnockdownType,
				KnocksDown = data.KnocksDown,
				KnockdownFrames = data.KnockdownFrames,
				CanHitGroundedKnockdown = data.CanHitGroundedKnockdown,
				BoxTimeline = data.BoxTimeline
			};

		public bool AllowsChainTo(string nextAttackName, bool nextStartedCrouching, bool nextStartedAirborne)
		{
			if (AllowedChainTargets != null && AllowedChainTargets.Length > 0)
			{
				foreach (string target in AllowedChainTargets)
					if (NormalMoveData.MatchesAttackToken(target, nextAttackName, nextStartedCrouching, nextStartedAirborne)) return true;
				return false;
			}

			return (nextAttackName.StartsWith("LIGHT") && CanChainToLight) ||
				(nextAttackName.StartsWith("HEAVY") && CanChainToHeavy) ||
				(nextAttackName.StartsWith("SPECIAL") && CanChainToSpecial);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Definition?.Tuning is null) return;
		Simulate(ReadLocalInput ? FighterInput.ReadLocal() : CurrentInput, (float)delta);
	}

	/// <summary>
	/// Command detection is event-latched, so down and up may occur arbitrarily quickly
	/// between two 60 Hz simulation frames without losing the sequence.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!ReadLocalInput) return;
		if (@event.IsActionPressed("move_down")) _motionInputBuffer.PressDown();
		if (@event.IsActionPressed("move_left")) HandleHorizontalTap(-1);
		if (@event.IsActionPressed("move_right")) HandleHorizontalTap(1);
		if (@event.IsActionPressed("jump")) _motionInputBuffer.PressJump(Definition?.Tuning?.InputBufferFrames ?? 3);
	}

	public void SetExternalInput(FighterInput input) => CurrentInput = input;
	public void SetFacing(int direction) => Facing = direction >= 0 ? 1 : -1;

	public void Simulate(FighterInput input, float delta)
	{
		JustLanded = false;
		PreviousGlobalPosition = GlobalPosition;
		CurrentInput = input;
		WasGrounded = IsOnFloor();
		if (HitstopFramesLeft > 0)
		{
			_motionInputBuffer.TickQuarterCircleForwardCommand();
			UpdateInputBuffer(input, true);
			HitstopFramesLeft--;
			return;
		}
		_motionInputBuffer.Tick();
		if (WasGrounded && LandingLagFramesLeft > 0) LandingLagFramesLeft--;
		if (FaceWithMovement && input.Horizontal != 0) Facing = input.Horizontal > 0 ? 1 : -1;
		UpdateInputBuffer(input, false);

		if (WasGrounded)
		{
			AirTimeFrames = 0;
			CoyoteFramesLeft = Definition.Tuning.CoyoteFrames;
			if (!_groundedLastFrame) ResetAirResources();
		}
		else
		{
			AirTimeFrames++;
			if (CoyoteFramesLeft > 0) CoyoteFramesLeft--;
		}

		if (HitstunFramesLeft > 0)
		{
			HitstunFramesLeft--;
			ClearAttackState();
			if (HitstunFramesLeft == 0)
			{
				if (ShouldPersistAirReactionUntilLanding())
					HitstunFramesLeft = 1;
				else
				{
					HitState = FighterHitState.None;
					CurrentKnockdownType = KnockdownType.None;
				}
			}
		}
		else
		{
			if (!TryStartLauncherChaseJump() && !TryStartAirLightHitJumpCancel() && !TryStartNormalJumpCancel() &&
				!TryStartNormalAirDashCancel() && !TryCrouchCancelCurrentNormal() && !TryStartDoubleJumpStateAirDashCancel())
			{
				CancelGroundMovementForCrouchNormal();
				TryStartBasicAttack();
				if (!IsAttacking) TryStartAbility();
			}
			TickBasicAttack();
			if (!IsAttacking && ActiveAbility != null && !ActiveAbility.Tick(this, GetRuntime(ActiveAbility), delta))
				StopActiveAbility();
		}

		ApplyBaseMotion(delta);
		TickComboDisplay();
		MoveAndSlide();
		if (!WasGrounded && HitstunFramesLeft > 0 && HitState == FighterHitState.Tumble && Velocity.Y >= 0f)
			RecoverFromComboHitstun();
		JustLanded = !WasGrounded && IsOnFloor();
		if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.GroundBounce)
			ResolveGroundBounceLanding();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.Knockdown)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState != FighterHitState.GroundedKnockdown)
			RecoverFromComboHitstun();
		if (JustLanded && IsAttacking && _currentAttackStartedAirborne)
			ClearAttackState();
		if (JustLanded && _pendingLandingLagFrames > 0)
		{
			LandingLagFramesLeft = _pendingLandingLagFrames;
			_pendingLandingLagFrames = 0;
			ConsumeJumpBuffer();
		}
		_groundedLastFrame = WasGrounded;
		TrySpawnProjectileForCurrentAttack();
		if (_launcherJumpCancelFramesLeft > 0) _launcherJumpCancelFramesLeft--;
		if (_airLightJumpCancelFramesLeft > 0) _airLightJumpCancelFramesLeft--;
	}

	public AbilityRuntime GetRuntime(MovementAbility ability)
	{
		if (!Runtime.TryGetValue(ability.Id, out var runtime))
			Runtime[ability.Id] = runtime = new AbilityRuntime();
		return runtime;
	}

	public bool StartAbility(MovementAbility ability)
	{
		if (ActiveAbility != null && !ActiveAbility.CanBeInterruptedBy(ability)) return false;
		if (ActiveAbility != null) StopActiveAbility();
		ActiveAbility = ability;
		SuppressesGroundedPushWhileAirborne = ability.SuppressesGroundedPushWhileAirborne;
		EnablesAirControlWhileAirborne = ability.EnablesAirControlWhileAirborne;
		AirDecelerationMultiplierWhileAirborne = ability.AirDecelerationMultiplierWhileAirborne;
		ability.Start(this, GetRuntime(ability));
		return true;
	}

	public void ConsumeJumpBuffer()
	{
		JumpBufferFramesLeft = 0;
		BufferedJumpHorizontal = 0;
	}
	public void AddVisualCorrection(Vector2 worldCorrection)
	{
		if (worldCorrection.IsZeroApprox()) return;
		VisualCorrectionOffset -= worldCorrection;
		if (VisualCorrectionOffset.Length() > MaxVisualCorrectionOffset)
			VisualCorrectionOffset = VisualCorrectionOffset.Normalized() * MaxVisualCorrectionOffset;
	}
	public void ConsumeDashBuffer() => DashBufferFramesLeft = 0;
	public void ConsumeDashCommand()
	{
		DashBufferFramesLeft = 0;
		_motionInputBuffer.ConsumeDashCommand();
	}
	public void ConsumeQuarterCircleForwardCommand() => _motionInputBuffer.ConsumeQuarterCircleForwardCommand();
	public bool IsDownThenUpCommand(int windowFrames) =>
		_motionInputBuffer.IsDownThenUpCommand(windowFrames);
	public void ConsumeDownThenUpCommand() => _motionInputBuffer.ConsumeDownThenUpCommand();

	public bool CanUseAirJump(string resourceId, int maximumUses) =>
		!AirJumpsDisabledThisJump &&
		CanUseAirAction() &&
		(!_airJumpUses.TryGetValue(resourceId, out int uses) || uses < maximumUses);

	public void ConsumeAirJump(string resourceId)
	{
		_airJumpUses.TryGetValue(resourceId, out int uses);
		_airJumpUses[resourceId] = uses + 1;
		IsInDoubleJumpState = true;
		_doubleJumpAirDashAvailable = IsInSuperJumpRoute;
		ConsumeAirAction();
	}

	public bool CanUseAirAction() =>
		AirActionsUsed < Definition.Tuning.MaxAirActions && IsAirActionHeightReady();
	public void ConsumeAirAction() => AirActionsUsed++;
	public bool CanUseAirDashAction() =>
		CanUseAirAction() || (IsInDoubleJumpState && _doubleJumpAirDashAvailable && IsAirActionHeightReady());
	public void ConsumeAirDashAction()
	{
		if (IsInDoubleJumpState && _doubleJumpAirDashAvailable && AirActionsUsed >= Definition.Tuning.MaxAirActions)
		{
			_doubleJumpAirDashAvailable = false;
			return;
		}
		ConsumeAirAction();
		if (IsInDoubleJumpState) _doubleJumpAirDashAvailable = false;
	}
	public void SetAirActionsRequirePeakThisJump(bool requirePeak) => AirActionsRequirePeakThisJump = requirePeak;
	public void DisableAirJumpsThisJump() => AirJumpsDisabledThisJump = true;
	public void RefreshAirJumpResourcesForSuperJump()
	{
		AirActionsUsed = 0;
		AirActionsRequirePeakThisJump = false;
		AirJumpsDisabledThisJump = false;
		IsInSuperJumpRoute = true;
		IsInDoubleJumpState = false;
		_doubleJumpAirDashAvailable = false;
		_airJumpUses.Clear();
	}
	public void SetShortHopPushboxRules(bool interactsWithGroundedPushbox, bool pushesGroundedOpponent)
	{
		ShortHopInteractsWithGroundedPushbox = interactsWithGroundedPushbox;
		ShortHopPushesGroundedOpponent = pushesGroundedOpponent;
	}
	public void SetJumpGroundedPushboxRules(bool interactsWithGroundedPushbox, float groundedPushStrength)
	{
		JumpInteractsWithGroundedPushbox = interactsWithGroundedPushbox;
		JumpGroundedPushStrength = Mathf.Clamp(groundedPushStrength, 0f, 1f);
	}
	public void QueueLandingLag(int frames)
	{
		if (frames > _pendingLandingLagFrames) _pendingLandingLagFrames = frames;
	}
	public void BeginRunCrouchSlide() => _runCrouchSlideFramesLeft = Mathf.Max(_runCrouchSlideFramesLeft, RunCrouchSlideFrames);
	public void BeginRunStopSlide() => _runStopSlideFramesLeft = Mathf.Max(_runStopSlideFramesLeft, RunStopSlideFrames);
	public bool TryApplyBasicAttackHit(FighterController defender, out int hitstopFrames, out float shakeStrength, out float hitPushback, out Vector2 hitPoint, out bool heavySpark)
	{
		hitstopFrames = 0;
		shakeStrength = 0f;
		hitPushback = 0f;
		hitPoint = Vector2.Zero;
		heavySpark = false;
		if (!IsAttackActive || _attackHasHit || IsProjectileAttackName(CurrentAttackName) || defender == null || defender == this) return false;
		if (!TryFindBoxContact(GetActiveWorldBoxInstances(FighterBoxKind.Hitbox), defender.GetActiveWorldBoxInstances(FighterBoxKind.Hurtbox),
			out hitPoint, out ActiveFighterBox hitbox, out _)) return false;
		FighterBoxFrame hitboxData = hitbox.Source;
		if (defender.IsGroundedKnockdown && !CanCurrentHitboxHitGroundedKnockdown(hitboxData)) return false;
		_attackHasHit = true;
		heavySpark = UsesHeavyHitSpark(CurrentAttackName);
		bool isLauncher = _currentMoveRule.Launches || hitboxData?.Launches == true;
		int baseHitstun = ResolveIntOverride(hitboxData?.HitstunFrames, _currentAttackHitstunFrames);
		float basePushback = ResolveFloatOverride(hitboxData?.Pushback, _currentAttackPushback);
		HitReactionKind hitReaction = hitboxData?.HitReaction ?? _currentAttackHitReaction;
		float appliedPushback = isLauncher
			? ResolveFloatOverride(hitboxData?.LaunchPushback, _currentMoveRule.LaunchPushback)
			: basePushback * (_currentAttackStartedAirborne ? AirAttackPushbackMultiplier : 1f);
		if (!_currentAttackStartedAirborne && !defender.WasGrounded)
			appliedPushback *= GroundToAirPushbackMultiplier;
		bool counterHit = defender.IsAttacking;
		int appliedHitstun = baseHitstun + (counterHit ? CounterHitExtraHitstunFrames : 0);
		if (_currentAttackStartedAirborne && !defender.WasGrounded)
			appliedHitstun += AirToAirHitstunBonusFrames;
		if (isLauncher)
		{
			int launchHitstun = ResolveIntOverride(hitboxData?.LaunchHitstunFrames, _currentMoveRule.LaunchHitstunFrames) + (counterHit ? CounterHitExtraHitstunFrames : 0);
			if (_currentAttackStartedAirborne && !defender.WasGrounded)
				launchHitstun += AirToAirHitstunBonusFrames;
			float launchSpeed = ResolveFloatOverride(hitboxData?.LaunchSpeed, _currentMoveRule.LaunchSpeed);
			defender.ApplyLaunchHitstun(launchHitstun, Facing * appliedPushback, launchSpeed, counterHit);
			_launcherJumpCancelFramesLeft = ResolveIntOverride(hitboxData?.JumpCancelWindowFrames, _currentMoveRule.JumpCancelWindowFrames);
		}
		else if (CurrentHitboxRequestsKnockdown(hitboxData))
		{
			int overrideKnockdownFrames = ResolveIntOverride(hitboxData?.KnockdownFrames, _currentAttackKnockdownFrames);
			int knockdownFrames = overrideKnockdownFrames > 0 ? overrideKnockdownFrames : appliedHitstun;
			KnockdownType knockdownType = ResolveCurrentAttackKnockdownType(defender, hitboxData);
			float downwardSpeed = !defender.WasGrounded ? HeavyAirAttackSpikeSpeed : 0f;
			defender.ApplyKnockdown(knockdownFrames, Facing * appliedPushback, downwardSpeed, knockdownType, counterHit);
		}
		else
		{
			if (!defender.WasGrounded)
			{
				if (_currentAttackStartedAirborne && CurrentAttackName.StartsWith("HEAVY"))
					defender.ApplyKnockdown(appliedHitstun, Facing * appliedPushback, HeavyAirAttackSpikeSpeed, KnockdownType.AirKnockdown, counterHit);
				else
					defender.ApplyAirPopHitstun(appliedHitstun, Facing * appliedPushback, AirHitPopUpSpeed, counterHit || hitReaction == HitReactionKind.Tumble);
			}
			else
				defender.ApplyHitstun(appliedHitstun, Facing * appliedPushback, counterHit);
			if (_currentAttackStartedAirborne && CurrentAttackName.StartsWith("LIGHT"))
				_airLightJumpCancelFramesLeft = AirLightHitJumpCancelWindowFrames;
		}
		hitstopFrames = ResolveIntOverride(hitboxData?.HitstopFrames, _currentAttackHitstopFrames) +
			GlobalHitstopBonusFrames + (_currentAttackStartedAirborne ? AirAttackHitstopBonusFrames : GroundedAttackHitstopBonusFrames);
		shakeStrength = ResolveFloatOverride(hitboxData?.ShakeStrength, _currentAttackShakeStrength);
		hitPushback = appliedPushback;
		return true;
	}

	public bool TryApplyProjectileHit(FighterController defender, Rect2 projectileHitbox, int hitstunFrames, float pushback, int hitstopFrames,
		float shakeStrength, out int appliedHitstopFrames, out float appliedShakeStrength, out float hitPushback, out Vector2 hitPoint, out bool heavySpark)
	{
		appliedHitstopFrames = 0;
		appliedShakeStrength = 0f;
		hitPushback = 0f;
		hitPoint = Vector2.Zero;
		heavySpark = false;
		if (defender == null || defender == this) return false;
		if (defender.IsGroundedKnockdown) return false;
		if (!TryFindBoxContact(new[] { new ActiveFighterBox(projectileHitbox) }, defender.GetActiveWorldBoxInstances(FighterBoxKind.Hurtbox),
			out hitPoint, out _, out _)) return false;

		float appliedPushback = Facing * pushback;
		if (defender.WasGrounded)
			defender.ApplyHitstun(hitstunFrames, appliedPushback);
		else
			defender.ApplyAirPopHitstun(hitstunFrames, appliedPushback, AirHitPopUpSpeed);
		appliedHitstopFrames = hitstopFrames;
		appliedShakeStrength = shakeStrength;
		hitPushback = pushback;
		heavySpark = true;
		return true;
	}
	public void RequestHitstop(int frames)
	{
		if (frames > HitstopFramesLeft) HitstopFramesLeft = frames;
	}

	public void StopActiveAbility()
	{
		if (ActiveAbility == null) return;
		ActiveAbility.Stop(this, GetRuntime(ActiveAbility));
		ActiveAbility = null;
	}

	private void ApplyHitstun(int frames, float horizontalPushback, bool counterHit = false)
	{
		ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Hitstun);
		Velocity = new Vector2(horizontalPushback, Velocity.Y);
	}

	private void ApplyLaunchHitstun(int frames, float horizontalPushback, float verticalLaunchSpeed, bool counterHit = false)
	{
		ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Tumble);
		Velocity = new Vector2(horizontalPushback, -verticalLaunchSpeed);
	}

	private void ApplyAirPopHitstun(int frames, float horizontalPushback, float popUpSpeed, bool tumble = false)
	{
		ApplyHitReaction(frames, tumble ? FighterHitState.Tumble : FighterHitState.Hitstun);
		Velocity = new Vector2(horizontalPushback, Mathf.Min(Velocity.Y, -popUpSpeed));
	}

	private void ApplyAirSpikeHitstun(int frames, float horizontalPushback, float spikeSpeed, bool counterHit = false)
	{
		ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Tumble);
		Velocity = new Vector2(horizontalPushback, Mathf.Max(Velocity.Y, spikeSpeed));
	}

	private void ApplyKnockdown(int frames, float horizontalPushback, float downwardSpeed, KnockdownType knockdownType, bool counterHit = false)
	{
		CurrentKnockdownType = knockdownType == KnockdownType.None ? KnockdownType.AirKnockdown : knockdownType;
		FighterHitState state = GetInitialKnockdownState(CurrentKnockdownType);
		ApplyHitReaction(frames, state);
		if (CurrentKnockdownType == KnockdownType.WallBounce)
		{
			float direction = Mathf.Abs(horizontalPushback) > 1f ? -Mathf.Sign(horizontalPushback) : -Facing;
			Velocity = new Vector2(direction * WallBounceHorizontalSpeed, Mathf.Min(Velocity.Y, -GroundBounceSpeed * 0.35f));
			return;
		}

		if (CurrentKnockdownType == KnockdownType.GroundBounce)
		{
			float verticalBounce = IsOnFloor() ? -GroundBounceSpeed : Mathf.Max(Velocity.Y, downwardSpeed);
			Velocity = new Vector2(horizontalPushback, verticalBounce);
			return;
		}

		float vertical = downwardSpeed > 0f ? Mathf.Max(Velocity.Y, downwardSpeed) : Velocity.Y;
		Velocity = new Vector2(horizontalPushback, vertical);
	}

	private void ApplyHitReaction(int frames, FighterHitState state)
	{
		ComboCount = HitstunFramesLeft > 0 ? ComboCount + 1 : 1;
		ComboDisplayFramesLeft = ComboDisplayFrames;
		HitstunFramesLeft = frames;
		HitState = state;
		if (state != FighterHitState.Knockdown && state != FighterHitState.GroundedKnockdown &&
			state != FighterHitState.WallBounce && state != FighterHitState.GroundBounce && state != FighterHitState.Crumple)
			CurrentKnockdownType = KnockdownType.None;
		StopActiveAbility();
		ClearAttackState();
	}

	private void RecoverFromComboHitstun()
	{
		HitstunFramesLeft = 0;
		HitState = FighterHitState.None;
		CurrentKnockdownType = KnockdownType.None;
		ComboDisplayFramesLeft = ComboDisplayFrames;
		Velocity = new Vector2(Velocity.X, 0f);
	}

	private void EnterGroundedKnockdown()
	{
		HitState = FighterHitState.GroundedKnockdown;
		if (CurrentKnockdownType == KnockdownType.None) CurrentKnockdownType = KnockdownType.AirKnockdown;
		Velocity = new Vector2(Velocity.X, 0f);
	}

	private void ResolveGroundBounceLanding()
	{
		HitState = FighterHitState.Tumble;
		CurrentKnockdownType = KnockdownType.None;
		Velocity = new Vector2(Velocity.X, -GroundBounceSpeed);
	}

	private bool ShouldPersistAirReactionUntilLanding() =>
		!WasGrounded && (HitState == FighterHitState.Knockdown || HitState == FighterHitState.GroundBounce);

	private FighterHitState GetInitialKnockdownState(KnockdownType knockdownType)
	{
		return knockdownType switch
		{
			KnockdownType.Sweep => FighterHitState.GroundedKnockdown,
			KnockdownType.WallBounce => FighterHitState.WallBounce,
			KnockdownType.GroundBounce => FighterHitState.GroundBounce,
			KnockdownType.Crumple => FighterHitState.Crumple,
			_ => IsOnFloor() ? FighterHitState.GroundedKnockdown : FighterHitState.Knockdown
		};
	}

	private bool CurrentMoveRequestsKnockdown() =>
		_currentAttackKnocksDown ||
		_currentAttackHitReaction == HitReactionKind.Knockdown ||
		_currentAttackHitReaction == HitReactionKind.WallBounce ||
		_currentAttackHitReaction == HitReactionKind.GroundBounce ||
		_currentAttackHitReaction == HitReactionKind.Crumple ||
		_currentAttackKnockdownType != KnockdownType.None;

	private bool CurrentHitboxRequestsKnockdown(FighterBoxFrame hitboxData)
	{
		if (hitboxData == null) return CurrentMoveRequestsKnockdown();
		return CurrentMoveRequestsKnockdown() ||
			hitboxData.KnocksDown ||
			hitboxData.HitReaction == HitReactionKind.Knockdown ||
			hitboxData.HitReaction == HitReactionKind.WallBounce ||
			hitboxData.HitReaction == HitReactionKind.GroundBounce ||
			hitboxData.HitReaction == HitReactionKind.Crumple ||
			hitboxData.KnockdownType != KnockdownType.None;
	}

	private bool CanCurrentHitboxHitGroundedKnockdown(FighterBoxFrame hitboxData) =>
		_currentAttackCanHitGroundedKnockdown || hitboxData?.CanHitGroundedKnockdown == true;

	private KnockdownType ResolveCurrentAttackKnockdownType(FighterController defender, FighterBoxFrame hitboxData)
	{
		if (hitboxData?.KnockdownType != null && hitboxData.KnockdownType != KnockdownType.None) return hitboxData.KnockdownType;
		if (_currentAttackKnockdownType != KnockdownType.None) return _currentAttackKnockdownType;
		HitReactionKind hitReaction = hitboxData?.HitReaction ?? _currentAttackHitReaction;
		return hitReaction switch
		{
			HitReactionKind.WallBounce => KnockdownType.WallBounce,
			HitReactionKind.GroundBounce => KnockdownType.GroundBounce,
			HitReactionKind.Crumple => KnockdownType.Crumple,
			_ => defender.WasGrounded ? KnockdownType.Sweep : KnockdownType.AirKnockdown
		};
	}

	private static int ResolveIntOverride(int? overrideValue, int fallback) =>
		overrideValue.HasValue && overrideValue.Value >= 0 ? overrideValue.Value : fallback;

	private static float ResolveFloatOverride(float? overrideValue, float fallback) =>
		overrideValue.HasValue && overrideValue.Value >= 0f ? overrideValue.Value : fallback;

	private void TickComboDisplay()
	{
		if (HitstunFramesLeft > 0) return;
		if (ComboDisplayFramesLeft > 0) ComboDisplayFramesLeft--;
		if (ComboDisplayFramesLeft <= 0)
		{
			ComboCount = 0;
			HitState = FighterHitState.None;
		}
	}

	private void TryStartAbility()
	{
		MovementAbility candidate = null;
		foreach (var ability in Definition.Abilities)
			if (ability != null && ability != ActiveAbility && ability.CanStart(this, GetRuntime(ability)) &&
				(candidate == null || ability.Priority > candidate.Priority)) candidate = ability;
		if (candidate != null) StartAbility(candidate);
	}

	private void CancelGroundMovementForCrouchNormal()
	{
		if (!WasGrounded || ActionInput.Vertical <= 0.5f || !HasPressedBasicAttack(ActionInput)) return;
		if (ActiveAbility is RunAbility)
			StopActiveAbility();
	}

	private void UpdateInputBuffer(FighterInput input, bool freezeBufferDecay)
	{
		if (ActiveAbility?.SuspendsInputBufferWhileActive == true)
		{
			JumpBufferFramesLeft = 0;
			DashBufferFramesLeft = 0;
			ClearAttackInputBuffers();
		}
		else
		{
			if (input.JumpPressed)
			{
				BufferedJumpHorizontal = input.Horizontal;
				BufferedJumpFacing = Facing;
			}
			JumpBufferFramesLeft = AdvanceBuffer(JumpBufferFramesLeft, input.JumpPressed, freezeBufferDecay);
			DashBufferFramesLeft = AdvanceBuffer(DashBufferFramesLeft, input.DashPressed || _motionInputBuffer.HasDashCommand, freezeBufferDecay);
			_lightPunchBufferFramesLeft = AdvanceBuffer(_lightPunchBufferFramesLeft, input.LightPunchPressed, freezeBufferDecay);
			_lightKickBufferFramesLeft = AdvanceBuffer(_lightKickBufferFramesLeft, input.LightKickPressed, freezeBufferDecay);
			_heavyPunchBufferFramesLeft = AdvanceBuffer(_heavyPunchBufferFramesLeft, input.HeavyPunchPressed, freezeBufferDecay);
			_heavyKickBufferFramesLeft = AdvanceBuffer(_heavyKickBufferFramesLeft, input.HeavyKickPressed, freezeBufferDecay);
			_special1BufferFramesLeft = AdvanceBuffer(_special1BufferFramesLeft, input.Special1Pressed, freezeBufferDecay);
			_special2BufferFramesLeft = AdvanceBuffer(_special2BufferFramesLeft, input.Special2Pressed, freezeBufferDecay);
		}
		if (LandingLagFramesLeft > 0) JumpBufferFramesLeft = 0;
		AttackBufferFramesLeft = Mathf.Max(Mathf.Max(Mathf.Max(_lightPunchBufferFramesLeft, _lightKickBufferFramesLeft),
			Mathf.Max(_heavyPunchBufferFramesLeft, _heavyKickBufferFramesLeft)), Mathf.Max(_special1BufferFramesLeft, _special2BufferFramesLeft));
		ActionInput = new FighterInput(input.Horizontal, input.Vertical,
			LandingLagFramesLeft <= 0 && (input.JumpPressed || JumpBufferFramesLeft > 0), input.JumpHeld,
			input.DashPressed || DashBufferFramesLeft > 0, input.FlightHeld,
			input.LightPunchPressed || _lightPunchBufferFramesLeft > 0, input.LightPunchHeld,
			input.LightKickPressed || _lightKickBufferFramesLeft > 0, input.LightKickHeld,
			input.HeavyPunchPressed || _heavyPunchBufferFramesLeft > 0, input.HeavyPunchHeld,
			input.HeavyKickPressed || _heavyKickBufferFramesLeft > 0, input.HeavyKickHeld,
			input.Special1Pressed || _special1BufferFramesLeft > 0, input.Special1Held,
			input.Special2Pressed || _special2BufferFramesLeft > 0, input.Special2Held);
	}

	private void TryStartBasicAttack()
	{
		string attackName = GetPressedBasicAttackName(ActionInput, CurrentInput);
		if (attackName == "") return;
		bool attackStartedFromAirDash = false;
		bool attackStartedFromRun = false;
		if (IsAttacking)
		{
			if (!CanChainBasicAttack(attackName)) return;
			ClearAttackState();
		}
		else
		{
			_normalUsesThisChain.Clear();
		}
		if (ActiveAbility != null)
		{
			if (ActiveAbility is DashAbility dash && dash.AirOnly)
			{
				var dashRuntime = GetRuntime(dash);
				int elapsedDashFrames = dash.ActiveFrames - dashRuntime.FramesRemaining;
				if (elapsedDashFrames < AirDashAttackCancelDelayFrames) return;
				attackStartedFromAirDash = true;
			}
			else if (ActiveAbility is JumpAbility && IsInDoubleJumpState)
			{
				// Double-jump attacks should be immediate; the jump launch has already happened.
			}
			else if (ActiveAbility is RunAbility)
			{
				attackStartedFromRun = WasGrounded;
			}
			else return;
			StopActiveAbility();
		}
		CurrentAttackName = attackName;
		_currentAttackStartedAirborne = !WasGrounded;
		_currentAttackStartedFromAirDash = attackStartedFromAirDash;
		_currentAttackStartedFromRun = attackStartedFromRun;
		_currentAttackStartedCrouching = WasGrounded && ActionInput.Vertical > 0.5f;
		_currentMoveRule = GetNormalMoveRule(attackName, _currentAttackStartedCrouching, _currentAttackStartedAirborne);
		RegisterNormalUse(attackName);
		_currentAttackStartupFrames = GetBasicAttackStartupFrames(attackName);
		_currentAttackActiveFrames = GetBasicAttackActiveFrames(attackName);
		_currentAttackRecoveryFrames = GetBasicAttackRecoveryFrames(attackName);
		_currentAttackHitstunFrames = GetBasicAttackHitstunFrames(attackName);
		_currentAttackPushback = GetBasicAttackPushback(attackName);
		_currentAttackHitstopFrames = GetBasicAttackHitstopFrames(attackName);
		_currentAttackShakeStrength = GetBasicAttackShakeStrength(attackName);
		ApplyMoveDataCombatOverrides();
		_currentAttackHitboxLocal = GetBasicAttackHitboxLocal(attackName);
		CurrentAttackFrame = 0;
		_attackStartupFramesLeft = _currentAttackStartupFrames;
		_attackActiveFramesLeft = 0;
		_attackRecoveryFramesLeft = 0;
		_attackHasHit = false;
		_projectileSpawnedThisAttack = false;
		_launcherJumpCancelFramesLeft = 0;
		if (attackName == ElectricWindGodFistName || IsProjectileAttackName(attackName)) ConsumeQuarterCircleForwardCommand();
		ConsumeDashBuffer();
		ClearAttackInputBuffers();
		ApplyAirAttackMomentum(attackName);
	}

	private void TrySpawnProjectileForCurrentAttack()
	{
		if (_projectileSpawnedThisAttack || !IsProjectileAttackName(CurrentAttackName) || _attackStartupFramesLeft > 0) return;
		_projectileSpawnedThisAttack = true;

		bool heavy = CurrentAttackName == HeavyProjectileName;
		var projectile = new BasicProjectile { Name = heavy ? "HeavyProjectile" : "LightProjectile" };
		Vector2 offset = new(ProjectileSpawnOffset.X * Facing, ProjectileSpawnOffset.Y);
		projectile.GlobalPosition = GlobalPosition + offset;
		projectile.Initialize(this, Facing,
			heavy ? HeavyProjectileSpeed : LightProjectileSpeed,
			heavy ? HeavyAttackHitstunFrames : LightAttackHitstunFrames,
			heavy ? HeavyAttackPushback * 0.55f : LightAttackPushback * 0.75f,
			heavy ? HeavyAttackHitstopFrames : LightAttackHitstopFrames,
			heavy ? HeavyAttackShakeStrength * 0.65f : LightAttackShakeStrength,
			heavy);
		GetParent()?.AddChild(projectile);
	}

	private bool TryStartLauncherChaseJump()
	{
		if (_launcherJumpCancelFramesLeft <= 0 || !WantsJumpCancel()) return false;
		NormalMoveRule launcherRule = _currentMoveRule;
		_launcherJumpCancelFramesLeft = 0;
		_airLightJumpCancelFramesLeft = 0;
		ClearAttackState();
		StopActiveAbility();
		ConsumeJumpBuffer();
		ConsumeDashBuffer();
		SuppressesGroundedPushWhileAirborne = true;
		EnablesAirControlWhileAirborne = true;
		AirDecelerationMultiplierWhileAirborne = 0.08f;
		RefreshAirJumpResourcesForSuperJump();
		Velocity = new Vector2(Facing * launcherRule.ChaseForwardSpeed, -launcherRule.ChaseJumpSpeed);
		return true;
	}

	private bool TryStartAirLightHitJumpCancel()
	{
		if (_airLightJumpCancelFramesLeft <= 0 || !WantsJumpCancel()) return false;
		var savedCurrentInput = CurrentInput;
		var savedActionInput = ActionInput;
		bool savedAirActionsRequirePeak = AirActionsRequirePeakThisJump;
		var forcedJumpInput = WithForcedJumpPressed(savedCurrentInput);
		CurrentInput = forcedJumpInput;
		ActionInput = WithForcedJumpPressed(savedActionInput);
		AirActionsRequirePeakThisJump = false;
		BufferedJumpHorizontal = CurrentInput.Horizontal;
		BufferedJumpFacing = Facing;

		MovementAbility candidate = null;
		foreach (var ability in Definition.Abilities)
			if (ability is JumpAbility && ability.CanStart(this, GetRuntime(ability)) &&
				(candidate == null || ability.Priority > candidate.Priority)) candidate = ability;

		if (candidate == null)
		{
			AirActionsRequirePeakThisJump = savedAirActionsRequirePeak;
			CurrentInput = savedCurrentInput;
			ActionInput = savedActionInput;
			return false;
		}

		_airLightJumpCancelFramesLeft = 0;
		_launcherJumpCancelFramesLeft = 0;
		ClearAttackState();
		StopActiveAbility();
		StartAbility(candidate);
		CurrentInput = savedCurrentInput;
		ActionInput = savedActionInput;
		return true;
	}

	private bool TryStartNormalJumpCancel()
	{
		if (!IsAttacking || !WantsJumpCancel()) return false;
		if (!CanCancelCurrentMove(CancelKind.Jump, "JUMP")) return false;

		var savedCurrentInput = CurrentInput;
		var savedActionInput = ActionInput;
		var forcedJumpInput = WithForcedJumpPressed(savedCurrentInput);
		CurrentInput = forcedJumpInput;
		ActionInput = WithForcedJumpPressed(savedActionInput);
		BufferedJumpHorizontal = CurrentInput.Horizontal;
		BufferedJumpFacing = Facing;

		MovementAbility candidate = null;
		foreach (var ability in Definition.Abilities)
			if (ability is JumpAbility && ability.CanStart(this, GetRuntime(ability)) &&
				(candidate == null || ability.Priority > candidate.Priority)) candidate = ability;
		if (candidate == null)
		{
			CurrentInput = savedCurrentInput;
			ActionInput = savedActionInput;
			return false;
		}

		ClearAttackState();
		StopActiveAbility();
		ConsumeJumpBuffer();
		StartAbility(candidate);
		CurrentInput = savedCurrentInput;
		ActionInput = savedActionInput;
		return true;
	}

	private bool TryStartNormalAirDashCancel()
	{
		if (!IsAttacking || !ActionInput.DashPressed) return false;
		if (!CanCancelCurrentMove(CancelKind.AirDash, "AIR DASH")) return false;

		MovementAbility candidate = null;
		foreach (var ability in Definition.Abilities)
			if (ability is DashAbility { AirOnly: true } && ability.CanStart(this, GetRuntime(ability)) &&
				(candidate == null || ability.Priority > candidate.Priority)) candidate = ability;
		if (candidate == null) return false;

		ClearAttackState();
		StartAbility(candidate);
		return true;
	}

	private bool TryCrouchCancelCurrentNormal()
	{
		if (!IsAttacking || !WasGrounded || CurrentInput.Vertical <= 0.5f) return false;
		if (!CanCancelCurrentMove(CancelKind.Crouch, "CROUCH")) return false;

		ClearAttackState();
		Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, BasicAttackFriction / 60f), Velocity.Y);
		return true;
	}

	private bool TryStartDoubleJumpStateAirDashCancel()
	{
		if (!IsInDoubleJumpState || !_doubleJumpAirDashAvailable) return false;
		if (!IsAttacking || !_currentAttackStartedAirborne || !_attackHasHit) return false;
		if (!ActionInput.DashPressed) return false;

		MovementAbility candidate = null;
		foreach (var ability in Definition.Abilities)
			if (ability is DashAbility { AirOnly: true } && ability.CanStart(this, GetRuntime(ability)) &&
				(candidate == null || ability.Priority > candidate.Priority)) candidate = ability;
		if (candidate == null) return false;

		ClearAttackState();
		StartAbility(candidate);
		return true;
	}

	private bool WantsJumpCancel() => ActionInput.JumpPressed || CurrentInput.JumpHeld || ActionInput.JumpHeld;

	private static FighterInput WithForcedJumpPressed(FighterInput input) => new(input.Horizontal, input.Vertical,
		true, true, input.DashPressed, input.FlightHeld,
		input.LightPunchPressed, input.LightPunchHeld,
		input.LightKickPressed, input.LightKickHeld,
		input.HeavyPunchPressed, input.HeavyPunchHeld,
		input.HeavyKickPressed, input.HeavyKickHeld,
		input.Special1Pressed, input.Special1Held,
		input.Special2Pressed, input.Special2Held);

	private bool CanChainBasicAttack(string nextAttackName)
	{
		bool nextStartedCrouching = WasGrounded && ActionInput.Vertical > 0.5f;
		bool nextStartedAirborne = !WasGrounded;
		NormalMoveRule nextRule = GetNormalMoveRule(nextAttackName, nextStartedCrouching, nextStartedAirborne);
		if (nextRule.MaxUsesPerCombo > 0 && GetNormalUseCount(nextAttackName) >= nextRule.MaxUsesPerCombo) return false;
		if (IsSpecialAttackName(nextAttackName))
		{
			return CanCancelCurrentMove(CancelKind.Special, nextAttackName);
		}

		if (_currentMoveRule.ChainRequiresContact && !_attackHasHit) return false;
		if (!IsWithinCurrentMoveCancelWindow(_currentMoveRule.CancelWindowStartFrame,
			_currentMoveRule.CancelWindowEndFrame, _currentMoveRule.ChainEarliestActiveFramesLeft)) return false;
		return _currentMoveRule.AllowsChainTo(nextAttackName, nextStartedCrouching, nextStartedAirborne);
	}

	private bool CanCancelCurrentMove(CancelKind kind, string targetMove)
	{
		if (!IsAttacking || Definition?.CancelRules == null) return false;
		int totalFrames = _currentAttackStartupFrames + _currentAttackActiveFrames + _currentAttackRecoveryFrames;
		int remainingFrames = _attackStartupFramesLeft + _attackActiveFramesLeft + _attackRecoveryFramesLeft;
		int elapsedFrames = totalFrames - remainingFrames;
		bool currentMoveIsNormal = IsNormalAttackName(CurrentAttackName);

		foreach (CancelRule rule in Definition.CancelRules)
			if (rule != null && rule.Allows(CurrentAttackName, targetMove, kind, currentMoveIsNormal, _attackHasHit,
				elapsedFrames, _attackStartupFramesLeft, _attackActiveFramesLeft)) return true;
		return false;
	}

	private void RegisterNormalUse(string attackName)
	{
		string key = attackName.ToUpperInvariant();
		_normalUsesThisChain.TryGetValue(key, out int uses);
		_normalUsesThisChain[key] = uses + 1;
	}

	private int GetNormalUseCount(string attackName)
	{
		_normalUsesThisChain.TryGetValue(attackName.ToUpperInvariant(), out int exactUses);
		return exactUses;
	}

	private void ApplyMoveDataCombatOverrides()
	{
		_currentAttackDamage = _currentMoveRule.Damage;
		_currentAttackBlockstunFrames = _currentMoveRule.BlockstunFrames;
		_currentAttackKnocksDown = _currentMoveRule.KnocksDown;
		_currentAttackKnockdownFrames = _currentMoveRule.KnockdownFrames;
		_currentAttackKnockdownType = _currentMoveRule.KnockdownType;
		_currentAttackCanHitGroundedKnockdown = _currentMoveRule.CanHitGroundedKnockdown;
		_currentAttackHitReaction = _currentMoveRule.HitReaction;
		if (_currentMoveRule.HitstunFramesOverride > 0) _currentAttackHitstunFrames = _currentMoveRule.HitstunFramesOverride;
		if (_currentMoveRule.HitstopFramesOverride > 0) _currentAttackHitstopFrames = _currentMoveRule.HitstopFramesOverride;
		if (_currentMoveRule.PushbackOverride > 0f) _currentAttackPushback = _currentMoveRule.PushbackOverride;
	}

	private bool IsWithinCurrentMoveCancelWindow(int windowStartFrame, int windowEndFrame, int earliestActiveFramesLeft)
	{
		if (windowStartFrame >= 0 || windowEndFrame >= 0)
		{
			int totalFrames = _currentAttackStartupFrames + _currentAttackActiveFrames + _currentAttackRecoveryFrames;
			int remainingFrames = _attackStartupFramesLeft + _attackActiveFramesLeft + _attackRecoveryFramesLeft;
			int elapsedFrames = totalFrames - remainingFrames;
			if (windowStartFrame >= 0 && elapsedFrames < windowStartFrame) return false;
			// Negative end frames mean the cancel remains available until this move fully ends.
			if (windowEndFrame >= 0 && elapsedFrames > windowEndFrame) return false;
			return true;
		}

		if (_attackStartupFramesLeft > 0) return false;
		if (_attackActiveFramesLeft > earliestActiveFramesLeft) return false;
		return true;
	}

	private NormalMoveRule GetNormalMoveRule(string attackName, bool startedCrouching, bool startedAirborne)
	{
		NormalMoveData moveData = Definition?.NormalMoves?.FindRule(attackName, startedCrouching, startedAirborne);
		if (moveData != null) return NormalMoveRule.FromData(moveData);

		// Backwards-compatible default rules for characters that do not yet have a NormalMoveSet.
		if (startedAirborne)
		{
			return new NormalMoveRule
			{
				CanChainToLight = true,
				CanChainToHeavy = true,
				CanChainToSpecial = true,
				ChainRequiresContact = true,
				ChainEarliestActiveFramesLeft = AirChainEarliestActiveFramesLeft,
				CancelWindowStartFrame = -1,
				CancelWindowEndFrame = -1
			};
		}

		if (attackName.StartsWith("LIGHT"))
		{
			return new NormalMoveRule
			{
				CanChainToLight = true,
				CanChainToHeavy = true,
				ChainRequiresContact = true,
				ChainEarliestActiveFramesLeft = LightChainEarliestActiveFramesLeft,
				CancelWindowStartFrame = -1,
				CancelWindowEndFrame = -1
			};
		}

		if (attackName == "HEAVY PUNCH" && startedCrouching && !startedAirborne)
		{
			return new NormalMoveRule
			{
				Launches = true,
				LaunchSpeed = DefaultLauncherSpeed,
				LaunchPushback = DefaultLauncherPushback,
				LaunchHitstunFrames = DefaultLauncherHitstunFrames,
				JumpCancelWindowFrames = DefaultJumpCancelWindowFrames,
				ChaseJumpSpeed = DefaultLauncherChaseJumpSpeed,
				ChaseForwardSpeed = DefaultLauncherChaseForwardSpeed,
				CancelWindowStartFrame = -1,
				CancelWindowEndFrame = -1
			};
		}

		return NormalMoveRule.None;
	}

	private void TickBasicAttack()
	{
		if (!IsAttacking) return;
		if (_attackStartupFramesLeft > 0)
		{
			_attackStartupFramesLeft--;
			if (_attackStartupFramesLeft == 0) _attackActiveFramesLeft = _currentAttackActiveFrames;
		}
		else if (_attackActiveFramesLeft > 0)
		{
			_attackActiveFramesLeft--;
			if (_attackActiveFramesLeft == 0) _attackRecoveryFramesLeft = _currentAttackRecoveryFrames;
		}
		else if (_attackRecoveryFramesLeft > 0)
		{
			_attackRecoveryFramesLeft--;
			if (_attackRecoveryFramesLeft == 0) ClearAttackState();
		}
		if (IsAttacking) CurrentAttackFrame++;
	}

	private void ClearAttackState()
	{
		_attackStartupFramesLeft = 0;
		_attackActiveFramesLeft = 0;
		_attackRecoveryFramesLeft = 0;
		CurrentAttackFrame = 0;
		_attackHasHit = false;
		_projectileSpawnedThisAttack = false;
		_currentAttackStartedAirborne = false;
		_currentAttackStartedFromAirDash = false;
		_currentAttackStartedFromRun = false;
		_currentAttackStartedCrouching = false;
		CurrentAttackName = "";
		_currentAttackStartupFrames = 0;
		_currentAttackActiveFrames = 0;
		_currentAttackRecoveryFrames = 0;
		_currentAttackHitstunFrames = 0;
		_currentAttackPushback = 0f;
		_currentAttackHitstopFrames = 0;
		_currentAttackShakeStrength = 0f;
		_currentAttackDamage = 0;
		_currentAttackBlockstunFrames = 0;
		_currentAttackKnocksDown = false;
		_currentAttackKnockdownFrames = 0;
		_currentAttackKnockdownType = KnockdownType.None;
		_currentAttackCanHitGroundedKnockdown = false;
		_currentAttackHitReaction = HitReactionKind.Normal;
		_currentAttackHitboxLocal = HitboxLocal;
		_launcherJumpCancelFramesLeft = 0;
		_airLightJumpCancelFramesLeft = 0;
		_currentMoveRule = NormalMoveRule.None;
	}

	private string GetPressedBasicAttackName(FighterInput input, FighterInput rawInput)
	{
		if (rawInput.LightPunchPressed && _motionInputBuffer.HasQuarterCircleForwardCommand) return LightProjectileName;
		if (rawInput.HeavyPunchPressed && _motionInputBuffer.HasQuarterCircleForwardCommand) return HeavyProjectileName;
		if (input.LightPunchPressed) return "LIGHT PUNCH";
		if (input.LightKickPressed) return "LIGHT KICK";
		if (input.HeavyPunchPressed) return "HEAVY PUNCH";
		if (input.HeavyKickPressed) return "HEAVY KICK";
		if (rawInput.Special1Pressed && _motionInputBuffer.HasQuarterCircleForwardCommand) return ElectricWindGodFistName;
		if (input.Special1Pressed) return "SPECIAL 1";
		if (input.Special2Pressed) return "SPECIAL 2";
		return "";
	}

	private static bool IsSpecialAttackName(string attackName) =>
		attackName.StartsWith("SPECIAL") || attackName == ElectricWindGodFistName || IsProjectileAttackName(attackName);

	private static bool IsProjectileAttackName(string attackName) =>
		attackName == LightProjectileName || attackName == HeavyProjectileName;

	private static bool IsNormalAttackName(string attackName) =>
		attackName == "LIGHT PUNCH" || attackName == "LIGHT KICK" ||
		attackName == "HEAVY PUNCH" || attackName == "HEAVY KICK";

	private static bool HasPressedBasicAttack(FighterInput input) =>
		input.LightPunchPressed || input.LightKickPressed || input.HeavyPunchPressed || input.HeavyKickPressed ||
		input.Special1Pressed || input.Special2Pressed;

	private int GetBasicAttackRecoveryFrames(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return 20;
		if (IsProjectileAttackName(attackName)) return SpecialAttackRecoveryFrames;
		if (attackName.StartsWith("LIGHT")) return _currentAttackStartedAirborne ? LightAttackRecoveryFrames : GroundLightAttackRecoveryFrames;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackRecoveryFrames;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackRecoveryFrames;
		return BasicAttackRecoveryFrames;
	}

	private int GetBasicAttackActiveFrames(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return 4;
		if (IsProjectileAttackName(attackName)) return 2;
		if (attackName == "LIGHT PUNCH") return LightPunchActiveFrames;
		if (attackName == "LIGHT KICK") return LightKickActiveFrames;
		if (attackName == "HEAVY PUNCH") return HeavyPunchActiveFrames;
		if (attackName == "HEAVY KICK") return HeavyKickActiveFrames;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackActiveFrames;
		return BasicAttackActiveFrames;
	}

	private int GetBasicAttackStartupFrames(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return 5;
		if (IsProjectileAttackName(attackName)) return BasicAttackStartupFrames;
		if (attackName.StartsWith("LIGHT")) return _currentAttackStartedAirborne ? LightAttackStartupFrames : GroundLightAttackStartupFrames;
		return BasicAttackStartupFrames;
	}

	private int GetBasicAttackHitstunFrames(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return HeavyAttackHitstunFrames;
		if (attackName == LightProjectileName) return LightAttackHitstunFrames;
		if (attackName == HeavyProjectileName) return HeavyAttackHitstunFrames;
		if (attackName.StartsWith("LIGHT")) return LightAttackHitstunFrames;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackHitstunFrames;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackHitstunFrames;
		return BasicAttackHitstunFrames;
	}

	private float GetBasicAttackPushback(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return 360f;
		if (attackName == LightProjectileName) return LightAttackPushback;
		if (attackName == HeavyProjectileName) return HeavyAttackPushback;
		if (attackName.StartsWith("LIGHT")) return LightAttackPushback;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackPushback;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackPushback;
		return BasicAttackPushback;
	}

	private int GetBasicAttackHitstopFrames(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return HeavyAttackHitstopFrames;
		if (attackName == LightProjectileName) return LightAttackHitstopFrames;
		if (attackName == HeavyProjectileName) return HeavyAttackHitstopFrames;
		if (attackName.StartsWith("LIGHT")) return _currentAttackStartedAirborne ? LightAttackHitstopFrames : ScaleNormalHitstop(LightAttackHitstopFrames);
		if (attackName.StartsWith("HEAVY")) return _currentAttackStartedAirborne ? HeavyAttackHitstopFrames : ScaleNormalHitstop(HeavyAttackHitstopFrames);
		return SpecialAttackHitstopFrames;
	}

	private int ScaleNormalHitstop(int frames) => Mathf.Max(1, Mathf.RoundToInt(frames * NormalAttackHitstopMultiplier));

	private int GetAirToGroundAttackerHitstopFrames(string attackName)
	{
		if (attackName.StartsWith("LIGHT")) return LightAirToGroundAttackerHitstopFrames;
		if (attackName.StartsWith("HEAVY")) return HeavyAirToGroundAttackerHitstopFrames;
		return SpecialAirToGroundAttackerHitstopFrames;
	}

	private float GetBasicAttackShakeStrength(string attackName)
	{
		if (attackName == ElectricWindGodFistName) return HeavyAttackShakeStrength;
		if (attackName == LightProjectileName) return LightAttackShakeStrength;
		if (attackName == HeavyProjectileName) return HeavyAttackShakeStrength;
		if (attackName.StartsWith("LIGHT")) return LightAttackShakeStrength;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackShakeStrength;
		return SpecialAttackShakeStrength;
	}

	private Rect2 GetBasicAttackHitboxLocal(string attackName)
	{
		if (_currentAttackStartedCrouching && attackName == "HEAVY KICK") return CrouchingHeavyKickHitboxLocal;
		return attackName switch
		{
			"LIGHT PUNCH" => LightPunchHitboxLocal,
			"LIGHT KICK" => LightKickHitboxLocal,
			"HEAVY PUNCH" => HeavyPunchHitboxLocal,
			"HEAVY KICK" => HeavyKickHitboxLocal,
			ElectricWindGodFistName => ElectricWindGodFistHitboxLocal,
			"SPECIAL 1" => Special1HitboxLocal,
			"SPECIAL 2" => Special2HitboxLocal,
			_ => HitboxLocal
		};
	}

	private void ApplyAirAttackMomentum(string attackName)
	{
		if (WasGrounded || !_currentAttackStartedFromAirDash) return;
		if (attackName.StartsWith("LIGHT"))
		{
			float driftDirection = Mathf.Abs(Velocity.X) > 1f ? Mathf.Sign(Velocity.X) : Facing;
			float boostedX = Velocity.X * LightAirAttackMomentumMultiplier;
			if (Mathf.Abs(boostedX) < Mathf.Abs(Velocity.X) + LightAirAttackMomentumBoost)
				boostedX += driftDirection * LightAirAttackMomentumBoost;
			Velocity = new Vector2(boostedX, Velocity.Y);
		}
		else if (attackName.StartsWith("HEAVY"))
		{
			Velocity = new Vector2(Velocity.X * HeavyAirAttackMomentumMultiplier, Velocity.Y);
		}
		else if (attackName.StartsWith("SPECIAL"))
		{
			Velocity = new Vector2(Velocity.X * SpecialAirAttackMomentumMultiplier, Velocity.Y);
		}
	}

	private void HandleHorizontalTap(int direction)
	{
		_motionInputBuffer.PressHorizontalTap(direction, Facing, Definition?.Tuning?.InputBufferFrames ?? 3,
			DoubleTapDashWindowFrames, QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
	}

	private static bool UsesHeavyHitSpark(string attackName) =>
		attackName.StartsWith("HEAVY") || attackName.StartsWith("SPECIAL") || attackName == ElectricWindGodFistName;

	private int AdvanceBuffer(int framesLeft, bool pressed, bool freezeDecay = false)
	{
		if (pressed)
		{
			// The press frame counts as one of the configured buffer frames.
			return Definition.Tuning.InputBufferFrames;
		}
		if (freezeDecay) return framesLeft;
		return framesLeft > 0 ? framesLeft - 1 : 0;
	}

	private void ClearAttackInputBuffers()
	{
		_lightPunchBufferFramesLeft = 0;
		_lightKickBufferFramesLeft = 0;
		_heavyPunchBufferFramesLeft = 0;
		_heavyKickBufferFramesLeft = 0;
		_special1BufferFramesLeft = 0;
		_special2BufferFramesLeft = 0;
		AttackBufferFramesLeft = 0;
	}

	private void ApplyBaseMotion(float delta)
	{
		// An active ability can opt out of any of these rules by setting its flags.
		bool ownsHorizontal = ActiveAbility?.OwnsHorizontalVelocity ?? false;
		bool ownsGravity = ActiveAbility?.OwnsGravity ?? false;
		// Standard fighters commit to their jump trajectory. Exotic movement (super jumps,
		// flight, air walks) can opt in per character or take ownership in an ability.
		if (!ownsHorizontal && (WasGrounded || Definition.Tuning.AllowAirControl || EnablesAirControlWhileAirborne))
		{
			if (IsAttacking && WasGrounded)
			{
				float attackFriction = _currentAttackStartedFromRun ? RunningAttackFriction : BasicAttackFriction;
				Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, attackFriction * delta), Velocity.Y);
				return;
			}
			bool crouching = WasGrounded && CurrentInput.Vertical > 0.5f;
			if (crouching && _runCrouchSlideFramesLeft > 0)
			{
				Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, RunningAttackFriction * delta), Velocity.Y);
				_runCrouchSlideFramesLeft--;
				return;
			}
			if (!crouching) _runCrouchSlideFramesLeft = 0;
			float movementHorizontal = crouching ? 0f : CurrentInput.Horizontal;
			if (movementHorizontal == 0f && _runStopSlideFramesLeft > 0)
			{
				Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, RunStopSlideFriction * delta), Velocity.Y);
				_runStopSlideFramesLeft--;
				return;
			}
			if (movementHorizontal != 0f) _runStopSlideFramesLeft = 0;
			bool walkingBackward = WasGrounded && movementHorizontal * Facing < 0;
			float groundSpeed = walkingBackward
				? Definition.Tuning.WalkSpeed * Definition.Tuning.BackWalkSpeedMultiplier
				: Definition.Tuning.WalkSpeed;
			float target = movementHorizontal * (WasGrounded ? groundSpeed : Definition.Tuning.AirSpeed);
			bool reversingOnGround = WasGrounded && movementHorizontal != 0f && Velocity.X != 0f && Mathf.Sign(Velocity.X) != Mathf.Sign(target);
			float rate = movementHorizontal == 0
				? (WasGrounded ? Mathf.Max(Definition.Tuning.GroundDeceleration, Definition.Tuning.GroundFriction) : Definition.Tuning.AirDeceleration * AirDecelerationMultiplierWhileAirborne)
				: (WasGrounded ? (reversingOnGround ? Definition.Tuning.GroundTurnAcceleration : Definition.Tuning.GroundAcceleration) : Definition.Tuning.AirAcceleration);
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, target, rate * delta), Velocity.Y);
		}
		if (!WasGrounded && !ownsGravity)
		{
			float gravityScale = HitstunFramesLeft > 0
				? Mathf.Min(MaxComboGravityScale, 1f + Mathf.Max(0, ComboCount - 1) * ComboGravityScalePerHit)
				: 1f;
			Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y + Definition.Tuning.Gravity * gravityScale * delta, Definition.Tuning.TerminalFallSpeed));
		}
		if (!VisualCorrectionOffset.IsZeroApprox())
			VisualCorrectionOffset = VisualCorrectionOffset.MoveToward(Vector2.Zero, VisualCorrectionSlideSpeed * delta);
	}

	private void ResetAirResources()
	{
		SuppressesGroundedPushWhileAirborne = false;
		EnablesAirControlWhileAirborne = false;
		AirDecelerationMultiplierWhileAirborne = 1f;
		AirActionsUsed = 0;
		AirActionsRequirePeakThisJump = false;
		AirJumpsDisabledThisJump = false;
		IsInSuperJumpRoute = false;
		IsInDoubleJumpState = false;
		_doubleJumpAirDashAvailable = false;
		ShortHopInteractsWithGroundedPushbox = false;
		ShortHopPushesGroundedOpponent = false;
		JumpInteractsWithGroundedPushbox = false;
		JumpGroundedPushStrength = 0f;
		_pendingLandingLagFrames = 0;
		_airJumpUses.Clear();
		foreach (var runtime in Runtime.Values)
		{
			runtime.UsesThisAirTime = 0;
			runtime.IntValue = 0;
			runtime.FloatValue = 0;
		}
	}

	public IEnumerable<Rect2> GetActiveLocalBoxes(FighterBoxKind kind)
	{
		foreach (ActiveFighterBox box in GetActiveLocalBoxInstances(kind))
			yield return box.Rect;
	}

	public IEnumerable<ActiveFighterBox> GetActiveLocalBoxInstances(FighterBoxKind kind)
	{
		bool hasTimelineBoxes = false;
		if (IsAttacking && _currentMoveRule.BoxTimeline != null)
		{
			foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			{
				if (box == null || box.Kind != kind) continue;
				hasTimelineBoxes = true;
				if (box.IsActiveOnFrame(CurrentAttackFrame))
					yield return new ActiveFighterBox(GetFacingLocalBox(box.LocalRect, box.MirrorWithFacing), box);
			}
		}

		if (hasTimelineBoxes) yield break;
		if (kind == FighterBoxKind.Hurtbox)
		{
			yield return new ActiveFighterBox(HurtboxLocal);
			yield break;
		}
		if (kind == FighterBoxKind.Pushbox)
		{
			yield return new ActiveFighterBox(ActivePushboxLocal);
			yield break;
		}
		if (kind == FighterBoxKind.Hitbox && IsAttackActive)
			yield return new ActiveFighterBox(GetFacingLocalBox(_currentAttackHitboxLocal, true));
	}

	public IEnumerable<Rect2> GetActiveWorldBoxes(FighterBoxKind kind)
	{
		foreach (ActiveFighterBox box in GetActiveWorldBoxInstances(kind))
			yield return box.Rect;
	}

	public IEnumerable<ActiveFighterBox> GetActiveWorldBoxInstances(FighterBoxKind kind)
	{
		bool hasTimelineBoxes = false;
		if (IsAttacking && _currentMoveRule.BoxTimeline != null)
		{
			foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			{
				if (box == null || box.Kind != kind) continue;
				hasTimelineBoxes = true;
				if (box.IsActiveOnFrame(CurrentAttackFrame))
					yield return new ActiveFighterBox(GetWorldFacingBox(box.LocalRect, box.MirrorWithFacing), box);
			}
		}

		if (hasTimelineBoxes) yield break;
		if (kind == FighterBoxKind.Hurtbox)
		{
			yield return new ActiveFighterBox(GetWorldFacingBox(HurtboxLocal, false));
			yield break;
		}
		if (kind == FighterBoxKind.Pushbox)
		{
			yield return new ActiveFighterBox(new Rect2(GlobalPosition + ActivePushboxLocal.Position, ActivePushboxLocal.Size));
			yield break;
		}
		if (kind == FighterBoxKind.Hitbox && IsAttackActive)
			yield return new ActiveFighterBox(GetWorldFacingBox(_currentAttackHitboxLocal, true));
	}

	private Rect2 GetFirstActiveLocalBox(FighterBoxKind kind, Rect2 fallback)
	{
		foreach (Rect2 box in GetActiveLocalBoxes(kind))
			return box;
		return GetFacingLocalBox(fallback, kind == FighterBoxKind.Hitbox);
	}

	private Rect2 GetFirstActiveWorldBox(FighterBoxKind kind, Rect2 fallback, bool mirrorFallback)
	{
		foreach (Rect2 box in GetActiveWorldBoxes(kind))
			return box;
		return GetWorldFacingBox(fallback, mirrorFallback);
	}

	private static bool TryFindBoxContact(IEnumerable<ActiveFighterBox> attackerBoxes, IEnumerable<ActiveFighterBox> defenderBoxes,
		out Vector2 hitPoint, out ActiveFighterBox attackerBox, out ActiveFighterBox defenderBox)
	{
		foreach (ActiveFighterBox attackBox in attackerBoxes)
		{
			foreach (ActiveFighterBox hurtBox in defenderBoxes)
			{
				if (!attackBox.Rect.Intersects(hurtBox.Rect)) continue;
				hitPoint = (attackBox.Rect.GetCenter() + hurtBox.Rect.GetCenter()) * 0.5f;
				attackerBox = attackBox;
				defenderBox = hurtBox;
				return true;
			}
		}

		hitPoint = Vector2.Zero;
		attackerBox = default;
		defenderBox = default;
		return false;
	}

	private Rect2 GetFacingLocalBox(Rect2 localBox, bool mirrorWithFacing)
	{
		if (!mirrorWithFacing || Facing >= 0) return localBox;
		return new Rect2(new Vector2(-localBox.Position.X - localBox.Size.X, localBox.Position.Y), localBox.Size);
	}

	private Rect2 GetWorldFacingBox(Rect2 localBox, bool mirrorWithFacing)
	{
		Rect2 facingBox = GetFacingLocalBox(localBox, mirrorWithFacing);
		return new Rect2(GlobalPosition + facingBox.Position, facingBox.Size);
	}

	private bool IsAirActionHeightReady()
	{
		if (WasGrounded || !AirActionsRequirePeakThisJump) return true;
		return Velocity.Y >= Definition.Tuning.NormalJumpAirActionPeakVelocity - AirActionPeakVelocityLeniency;
	}
}
