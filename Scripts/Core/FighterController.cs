using System;
using System.Collections.Generic;
using Godot;
using ModularFighter.Movement;

namespace ModularFighter.Core;

public enum FighterHitState
{
	None,
	Hitstun,
	Blockstun,
	CounterHit,
	Juggle,
	WallSplat,
	Tumble,
	Knockdown,
	GroundedKnockdown,
	WallBounce,
	GroundBounce,
	Crumple,
	Stumble,
	HitFall
}

/// <summary>
/// Shared deterministic-ish movement loop. Attacks, hitstun and rollback can feed this class
/// command input without changing any movement ability.
/// </summary>
public partial class FighterController : CharacterBody2D
{
	[Export] public FighterDefinition Definition { get; set; }
	[Export] public bool ReadLocalInput { get; set; } = true;
	[Export(PropertyHint.Range, "0,3,1")] public int LocalPlayerIndex { get; set; }
	[Export] public bool FaceWithMovement { get; set; } = true;
	[ExportGroup("Match Identity")]
	[Export] public int TeamId { get; set; }
	public bool ParticipatesInPointCollision { get; private set; } = true;
	[ExportGroup("Training Guard")]
	[Export] public bool TrainingAutoBlock { get; set; }
	[Export] public bool TrainingAirBlock { get; set; }
	[Export] public bool InstantBlockEnabled { get; set; }
	[Export(PropertyHint.Range, "1,12,1")] public int InstantBlockWindowFrames { get; set; } = 6;
	[ExportGroup("Universal Trait System")]
	[Export] public bool BlueRecoveryCancelEnabled { get; set; } = true;
	[Export(PropertyHint.Range, "1,12,1")] public int BlueRecoveryCancelWindowFrames { get; set; } = 3;
	[ExportGroup("Collision")]
	[Export] public Rect2 PushboxLocal { get; set; } = new(-28f, -50f, 56f, 100f);
	[Export] public Rect2 AirbornePushboxLocal { get; set; } = new(-20f, -42f, 40f, 78f);
	[Export] public Rect2 HurtboxLocal { get; set; } = new(-32f, -92f, 64f, 142f);
	[Export] public Rect2 HitboxLocal { get; set; } = new(22f, -68f, 54f, 44f);
	[Export] public Rect2 PositionBoxLocal { get; set; } = new(-4f, -46f, 8f, 8f);
	[Export] public bool DebugDrawCombatBoxes { get; set; }
	[Export] public int MaxHitLogEntries { get; set; } = 24;
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
	[Export] public bool ReverseAttackRecoveryToNeutral { get; set; }
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
	[Export] public int ProjectileAttackStartupFrames { get; set; } = 4;
	[Export] public int LightAttackHitstunFrames { get; set; } = 10;
	[Export] public int HeavyAttackHitstunFrames { get; set; } = 14;
	[Export] public int SpecialAttackHitstunFrames { get; set; } = 20;
	[Export] public float LightAttackPushback { get; set; } = 520f;
	[Export] public float HeavyAttackPushback { get; set; } = 1240f;
	[Export] public float SpecialAttackPushback { get; set; } = 560f;
	[Export] public float AirAttackPushbackMultiplier { get; set; } = 0.25f;
	[Export] public float AirLightAttackPushback { get; set; } = 50f;
	[Export] public float GroundToAirPushbackMultiplier { get; set; } = 0.55f;
	[Export] public int LightAttackHitstopFrames { get; set; } = 5;
	[Export] public int HeavyAttackHitstopFrames { get; set; } = 13;
	[Export] public int SpecialAttackHitstopFrames { get; set; } = 6;
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float NormalAttackHitstopMultiplier { get; set; } = 0.5f;
	[Export] public int GlobalHitstopBonusFrames { get; set; } = 6;
	[Export] public int BlockHitstopBonusFrames { get; set; } = 2;
	[Export(PropertyHint.Range, "0.0,2.0,0.01")] public float BlockPushbackMultiplier { get; set; } = 1.2f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float HeavyNormalBlockPushbackScale { get; set; } = 1f;
	[Export] public float BlockShakeStrength { get; set; } = 1.25f;
	[Export] public int GroundedAttackHitstopBonusFrames { get; set; } = 4;
	[Export] public int AirAttackHitstopBonusFrames { get; set; } = 3;
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float AirAttackHitstopMultiplier { get; set; } = 0.5f;
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float AirLightHitstopMultiplier { get; set; } = 1f;
	[Export(PropertyHint.Range, "1,30,1")] public int AirNormalHitstopFrames { get; set; } = 10;
	[Export] public int JumpInInitialFullFreezeFrames { get; set; } = 5;
	[Export] public int JumpInHitstopBonusFrames { get; set; } = 1;
	[ExportGroup("Jumping Heavy vs Grounded")]
	[Export] public int JumpingHeavyGroundedHitstunFrames { get; set; } = 12;
	[Export] public int JumpingHeavyAttackerHitlagFrames { get; set; } = 15;
	[Export] public int JumpingHeavyDefenderHitstopFrames { get; set; } = 12;
	[Export] public bool HeldJumpRepeatsOnLanding { get; set; } = true;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float AirToGroundHitstopMomentumScale { get; set; } = 0.85f;
	[Export] public float AirToGroundShakeMultiplier { get; set; } = 1.25f;
	[Export] public float LightAttackShakeStrength { get; set; } = 2.5f;
	[Export] public float HeavyAttackShakeStrength { get; set; } = 9f;
	[Export] public float SpecialAttackShakeStrength { get; set; } = 5f;
	[Export] public int ComboDisplayFrames { get; set; } = 90;
	[Export] public int AirDashAttackCancelDelayFrames { get; set; } = 2;
	[Export] public float LightAirAttackMomentumMultiplier { get; set; } = 0.85f;
	[Export] public float HeavyAirAttackMomentumMultiplier { get; set; } = 0.68f;
	[Export] public float SpecialAirAttackMomentumMultiplier { get; set; } = 0.9f;
	[Export] public float LightAirAttackMomentumBoost { get; set; }
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float AirLightHitMomentumScale { get; set; } = 0.7f;
	[Export] public int AirChainEarliestActiveFramesLeft { get; set; } = 4;
	[Export] public float AirHitPopUpSpeed { get; set; } = 620f;
	[Export] public float AirLightInitialPopUpSpeed { get; set; } = 140f;
	[Export] public float HeavyAirAttackSpikeSpeed { get; set; } = 980f;
	[Export] public int AirToAirHitstunBonusFrames { get; set; } = 8;
	[Export(PropertyHint.Range, "-20,20,1")] public int AirToAirNormalHitstunAdjustment { get; set; }
	[Export] public int AirLightHitJumpCancelWindowFrames { get; set; } = 20;
	[Export] public float ComboGravityScalePerHit { get; set; } = 0.12f;
	[Export] public float MaxComboGravityScale { get; set; } = 2.2f;
	[Export] public float JuggleGravityScalePerHit { get; set; } = 0.20f;
	[Export] public float MaxJuggleGravityScale { get; set; } = 2.75f;
	[Export] public float JuggleDistanceScalePerHit { get; set; } = 0.09f;
	[Export] public float MaxJuggleDistanceScale { get; set; } = 1.55f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float GroundNormalJugglePushbackMultiplier { get; set; } = 0.65f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float GroundJabJugglePopLossPerHit { get; set; } = 0.22f;
	[Export] public int WallSplatHitstunFrames { get; set; } = 28;
	[Export] public float WallSplatSlideSpeed { get; set; } = 105f;
	[Export] public int CounterHitExtraHitstunFrames { get; set; } = 4;
	[Export] public float GroundBounceSpeed { get; set; } = 900f;
	[Export] public float SweepPopUpSpeed { get; set; } = 220f;
	[Export] public float StumblePopUpSpeed { get; set; } = 720f;
	[Export] public float HitFallSpeed { get; set; } = 620f;
	[Export] public int GroundedKnockdownHoldFrames { get; set; } = 30;
	[Export] public int WakeupFrames { get; set; }
	[Export] public float WeakWallBounceHorizontalSpeed { get; set; } = 560f;
	[Export] public float WallBounceHorizontalSpeed { get; set; } = 850f;
	[ExportGroup("Move Rules")]
	[Export] public float DefaultLauncherSpeed { get; set; } = 1265f;
	[Export] public float DefaultLauncherPushback { get; set; } = 180f;
	[Export] public int DefaultLauncherHitstunFrames { get; set; } = 30;
	[Export] public int DefaultJumpCancelWindowFrames { get; set; } = 30;
	[Export] public float DefaultLauncherChaseJumpSpeed { get; set; } = 1265f;
	[Export] public float DefaultLauncherChaseForwardSpeed { get; set; } = 360f;
	[ExportGroup("Blow Away Reactions")]
	[Export] public float WeakBlowAwaySpeed { get; set; } = 620f;
	[Export] public float MediumBlowAwaySpeed { get; set; } = 820f;
	[Export] public float StrongBlowAwaySpeed { get; set; } = 1040f;
	[Export(PropertyHint.Range, "0.1,1.0,0.01")] public float BlowAwayBounceScale { get; set; } = 0.55f;
	// Air dash / double-jump height gate leniency. Raise this to allow air actions earlier before jump peak.
	[Export] public float AirActionPeakVelocityLeniency { get; set; } = 140f;
	[ExportGroup("Attack Hitboxes")]
	[Export] public Rect2 LightPunchHitboxLocal { get; set; } = new(24f, -58f, 42f, 28f);
	[Export] public Rect2 LightKickHitboxLocal { get; set; } = new(18f, -24f, 58f, 24f);
	[Export] public Rect2 HeavyPunchHitboxLocal { get; set; } = new(26f, -66f, 68f, 36f);
	[Export] public Rect2 HeavyKickHitboxLocal { get; set; } = new(18f, -32f, 84f, 30f);
	[Export] public Rect2 CrouchingHeavyKickHitboxLocal { get; set; } = new(16f, -20f, 112f, 42f);
	[Export] public Rect2 Special1HitboxLocal { get; set; } = new(20f, -70f, 76f, 52f);
	[Export] public Rect2 Special2HitboxLocal { get; set; } = new(18f, -50f, 88f, 42f);
	[Export] public Rect2 ElectricWindGodFistHitboxLocal { get; set; } = new(24f, -66f, 72f, 46f);
	[ExportGroup("Projectile Specials")]
	[Export] public float LightProjectileSpeed { get; set; } = 720f;
	[Export] public float HeavyProjectileSpeed { get; set; } = 1040f;
	[Export] public float SuperProjectileSpeed { get; set; } = 620f;
	[Export] public Vector2 ProjectileSpawnOffset { get; set; } = new(70f, -42f);
	[ExportGroup("Super Test")]
	[Export] public int SuperActivationFreezeFrames { get; set; } = 45;
	[Export] public int SuperProjectileHits { get; set; } = 10;
	[Export] public int SuperProjectileHitCooldownFrames { get; set; } = 5;
	[ExportGroup("Visual Smoothing")]
	[Export] public float VisualCorrectionSlideSpeed { get; set; } = 900f;
	[Export] public float MaxVisualCorrectionOffset { get; set; } = 80f;

	public FighterInput CurrentInput { get; private set; }
	public FighterInput ActionInput { get; private set; }
	public int Facing { get; private set; } = 1;
	public bool WasGrounded { get; private set; }
	public bool JustLanded { get; private set; }
	public int AirTimeFrames { get; private set; }
	public float AirHeightAboveGround => Mathf.Max(0f, _lastGroundedY - GlobalPosition.Y);
	public int CoyoteFramesLeft { get; private set; }
	public int JumpBufferFramesLeft { get; private set; }
	public int DashBufferFramesLeft { get; private set; }
	public int AttackBufferFramesLeft { get; private set; }
	public bool HasBufferedDashCommand => _motionInputBuffer.HasDashCommand;
	public bool HasBufferedQuarterCircleForwardCommand => _motionInputBuffer.HasQuarterCircleForwardCommand;
	public float BufferedJumpHorizontal { get; private set; }
	public int BufferedJumpFacing { get; private set; } = 1;
	public float JumpInputHorizontal => CurrentInput.JumpPressed ? CurrentInput.Horizontal : BufferedJumpHorizontal;
	public int JumpInputFacing => CurrentInput.JumpPressed ? Facing : BufferedJumpFacing;
	public int DashInputDirection => _motionInputBuffer.DashCommandDirection != 0 ? _motionInputBuffer.DashCommandDirection : Facing;
	public MovementAbility ActiveAbility { get; private set; }
	/// <summary>Pushbox in world space. It follows this fighter's origin exactly.</summary>
	public Rect2 ActivePushboxLocal => !IsOnFloor() && SuppressesGroundedPushWhileAirborne ? AirbornePushboxLocal : PushboxLocal;
	public Rect2 WorldPushbox => ParticipatesInPointCollision
		? GetCombinedActiveWorldBox(FighterBoxKind.Pushbox, ActivePushboxLocal, false)
		: new Rect2(GlobalPosition, Vector2.Zero);
	public Rect2 WorldHurtbox => ParticipatesInPointCollision
		? GetFirstActiveWorldBox(FighterBoxKind.Hurtbox, HurtboxLocal, false)
		: new Rect2(GlobalPosition, Vector2.Zero);
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
	/// <summary>State-authored recovery used when an aerial normal touches down.</summary>
	public int AirAttackLandingFramesLeft { get; private set; }
	public bool IsInAirAttackLanding => AirAttackLandingFramesLeft > 0;
	public bool IsInRunStopSlide => _runStopSlideFramesLeft > 0;
	public string CurrentAirAttackLandingAnimationName { get; private set; } = "air_attack_landing";
	public int FlightLandingFramesLeft { get; private set; }
	public bool IsInFlightLanding => FlightLandingFramesLeft > 0;
	public bool IsPostFlightFallNormalLocked => _flightUsedThisAirTime && !WasGrounded && ActiveAbility is not FlightAbility;
	public bool IsInButtonFlight => ActiveAbility is FlightAbility flight && flight.IsButtonActivatedFlight(this);
	public bool UsesSuperJumpAirNormalRules => IsInSuperJumpRoute || ActiveAbility is FlightAbility;
	public bool AirActionsRequirePeakThisJump { get; private set; }
	public bool AirJumpsDisabledThisJump { get; private set; }
	public bool ShortHopInteractsWithGroundedPushbox { get; private set; }
	public bool ShortHopPushesGroundedOpponent { get; private set; }
	public bool IsInShortHopRoute { get; private set; }
	public bool JumpInteractsWithGroundedPushbox { get; private set; }
	public float JumpGroundedPushStrength { get; private set; }
	public bool IsInDoubleJumpState { get; private set; }
	public bool IsInSuperJumpRoute { get; private set; }
	/// <summary>-1 backward, 0 neutral, +1 forward, captured at super-jump takeoff.</summary>
	public int SuperJumpPresentationDirection { get; private set; }
	public bool IsAttacking => _attackStartupFramesLeft > 0 || _attackActiveFramesLeft > 0 || _attackRecoveryFramesLeft > 0;
	public bool IsAttackActive => IsAttacking && _attackStartupFramesLeft <= 0 && _attackActiveFramesLeft > 0;
	public bool IsAttackRecovering => IsAttacking && _attackRecoveryFramesLeft > 0;
	public bool CurrentAttackHasHit => _attackHasHit;
	public bool CurrentAttackHasContact => _attackHasHit;
	public bool CurrentAttackHasUnblockedHit => _attackHasUnblockedHit;
	public bool CurrentAttackIsNormal => IsAttacking && _currentSpecialMove == null && _currentSuperMove == null &&
		IsNormalAttackName(CurrentAttackName);
	public bool CurrentAttackIsLightNormal => CurrentAttackIsNormal && CurrentAttackName.Contains("LIGHT");
	public bool CurrentAttackIsSpecial => IsAttacking && _currentSpecialMove != null && _currentSuperMove == null;
	public bool IsWithinBlueRecoveryCancelWindow => IsAttackRecovering &&
		Mathf.Max(0, _currentAttackRecoveryFrames - _attackRecoveryFramesLeft) <
		Mathf.Max(1, BlueRecoveryCancelWindowFrames);
	public int CurrentAttackHitsRemaining => _currentAttackHitsRemaining;
	public PackedScene CurrentHitSparkScene => _currentContactHitSparkScene ?? _currentMoveData?.HitSparkScene;
	public bool IsCurrentSuperConfirmed => _currentSuperConfirmed;
	public int CurrentSuperConfirmedFrame => _currentSuperConfirmedFrame;
	public bool IsPerformingSuperMove => IsAttacking && _currentSuperMove != null;
	public bool IsPlayingWinAnimation { get; private set; }
	public bool WinAnimationFinished { get; private set; }
	/// <summary>True for real supers and authored special moves that use the universal super presentation.</summary>
	public bool CurrentAttackTriggersHyperComboFinish => IsAttacking &&
		(_currentSuperMove != null || _currentSpecialMove?.TriggersSuperPresentation == true);
	public bool IsPerformingThrow => IsAttacking && (IsRegularThrowAttackName(CurrentAttackName) || IsSpdGrabAttackName(CurrentAttackName));
	public bool IsPerformingSpdGrab => IsAttacking && IsSpdGrabAttackName(CurrentAttackName);
	public bool IsPerformingSuperSpdGrab => IsAttacking && CurrentAttackName == SanzoSuperSpdName;
	public bool SpdGrabConnected => _spdGrabConnected;
	public bool IsCrouchAttackLocked => IsAttacking && _currentAttackStartedCrouching;
	public bool CurrentAttackStartedAirborne => _currentAttackStartedAirborne;
	public bool CurrentAttackIsGroundedNormal => !_currentAttackStartedAirborne &&
		_currentSpecialMove == null && _currentSuperMove == null && IsNormalAttackName(CurrentAttackName);
	public bool IsInHitstun => HitstunFramesLeft > 0;
	public bool IsInBlockstun => HitState == FighterHitState.Blockstun && HitstunFramesLeft > 0;
	public bool IsCrouchBlocking { get; private set; }
	public bool LastContactWasBlocked { get; private set; }
	public bool LastContactWasInstantBlocked { get; private set; }
	public bool LastContactWasParried { get; private set; }
	public int LastContactDefenderHitstopFrames { get; private set; }
	public bool IsParryWindowActive =>
		(_currentSuperMove?.Parry == true || _currentSpecialMove?.Parry == true) &&
		_attackActiveFramesLeft > 0 && IsAttackActive;
	public bool IsParrySuccessPresentationActive => _parrySuccessPresentationFramesLeft > 0;
	public ulong ParrySuccessSerial { get; private set; }
	public bool IsKnockedDown => (HitState == FighterHitState.Knockdown || HitState == FighterHitState.GroundedKnockdown ||
		HitState == FighterHitState.WallBounce || HitState == FighterHitState.GroundBounce || HitState == FighterHitState.Crumple ||
		HitState == FighterHitState.Stumble || HitState == FighterHitState.HitFall) && HitstunFramesLeft > 0;
	public bool IsGroundedKnockdown => HitState == FighterHitState.GroundedKnockdown && HitstunFramesLeft > 0;
	public bool IsWakingUp => _wakeupFramesLeft > 0;
	public bool IsMovementInvulnerable => _movementInvulnerabilityFramesLeft > 0;
	public int WakeupFramesLeft => _wakeupFramesLeft;
	public int CurrentWakeupFrame => IsWakingUp ? Mathf.Max(0, _activeWakeupTotalFrames - _wakeupFramesLeft) : 0;
	public bool IsWallSplatSliding => _pendingWallSplatKnockdown && !WasGrounded;
	public FighterHitState HitState { get; private set; } = FighterHitState.None;
	public int LastHitReactionLevel { get; private set; }
	public bool LastHitCameFromAir { get; private set; }
	public bool HitReactionStartedCrouching { get; private set; }
	public ulong HitReactionSerial { get; private set; }
	public ulong BlockReactionSerial { get; private set; }
	public ulong BlueRecoveryCancelSerial { get; private set; }
	public GuardReactionStrength CurrentGuardReactionStrength { get; private set; } = GuardReactionStrength.None;
	public string CurrentStandingGuardAnimationName => CurrentGuardReactionStrength switch
	{
		GuardReactionStrength.Weak => "stand_block_weak",
		GuardReactionStrength.Strong => "stand_block_strong",
		GuardReactionStrength.SpecialStrong => "stand_block_special_strong",
		_ => "stand_block_medium"
	};
	private string CurrentStandingGuardStateName => CurrentGuardReactionStrength switch
	{
		GuardReactionStrength.Weak => "STATE [ガード]立ち_弱",
		GuardReactionStrength.Strong => "STATE [ガード]立ち_強",
		GuardReactionStrength.SpecialStrong => "STATE [ガード]立ち_特強",
		_ => "STATE [ガード]立ち_中"
	};
	public string CurrentCrouchingGuardAnimationName => CurrentGuardReactionStrength switch
	{
		GuardReactionStrength.Weak => "crouch_block_weak",
		GuardReactionStrength.Strong => "crouch_block_strong",
		GuardReactionStrength.SpecialStrong => "crouch_block_special_strong",
		_ => "crouch_block_medium"
	};
	private string CurrentCrouchingGuardStateName => CurrentGuardReactionStrength switch
	{
		GuardReactionStrength.Weak => "STATE [ガード]屈_弱",
		GuardReactionStrength.Strong => "STATE [ガード]屈_強",
		GuardReactionStrength.SpecialStrong => "STATE [ガード]屈_特強",
		_ => "STATE [ガード]屈_中"
	};
	public string CurrentAirGuardAnimationName => CurrentGuardReactionStrength switch
	{
		GuardReactionStrength.Weak => "air_block_weak",
		GuardReactionStrength.Strong => "air_block_strong",
		GuardReactionStrength.SpecialStrong => "air_block_special_strong",
		_ => "air_block_medium"
	};
	private string CurrentAirGuardStateName => CurrentGuardReactionStrength switch
	{
		GuardReactionStrength.Weak => "STATE [ガード]空中_弱",
		GuardReactionStrength.Strong => "STATE [ガード]空中_強",
		GuardReactionStrength.SpecialStrong => "STATE [ガード]空中_特強",
		_ => "STATE [ガード]空中_中"
	};
	public SpecialReactionKind CurrentSpecialReaction { get; private set; } = SpecialReactionKind.None;
	public string CurrentSpecialReactionAnimationName => CurrentSpecialReaction switch
	{
		SpecialReactionKind.Stagger => "special_stagger",
		SpecialReactionKind.SlideDownHorizontal => "slide_down_horizontal",
		SpecialReactionKind.SlideDownDiagonal => "slide_down_diagonal",
		SpecialReactionKind.SlideDowned => "slide_downed",
		SpecialReactionKind.DiagonalBounce => "diagonal_bounce",
		SpecialReactionKind.PullbackWeak => "pullback_weak",
		SpecialReactionKind.PullbackStrong => "pullback_strong",
		SpecialReactionKind.GuardPullbackWeak => "guard_pullback_weak",
		SpecialReactionKind.GuardPullbackStrong => "guard_pullback_strong",
		SpecialReactionKind.PullbackAir => "pullback_air",
		SpecialReactionKind.GuardPullbackAir => "guard_pullback_air",
		_ => ""
	};
	private string CurrentSpecialReactionStateName => CurrentSpecialReaction switch
	{
		SpecialReactionKind.Stagger => "STATE [特殊やられ]よろめき",
		SpecialReactionKind.SlideDownHorizontal => "STATE [特殊やられ]スライドダウン_横",
		SpecialReactionKind.SlideDownDiagonal => "STATE [特殊やられ]スライドダウン_斜め下",
		SpecialReactionKind.SlideDowned => "STATE [特殊やられ]ダウン(スライド)",
		SpecialReactionKind.DiagonalBounce => "STATE [やられ]斜めバウンド",
		SpecialReactionKind.PullbackWeak => "STATE [特殊やられ]引き戻し_弱",
		SpecialReactionKind.PullbackStrong => "STATE [特殊やられ]引き戻し_強",
		SpecialReactionKind.GuardPullbackWeak => "STATE [特殊ガード]引き戻し_弱",
		SpecialReactionKind.GuardPullbackStrong => "STATE [特殊ガード]引き戻し_強",
		SpecialReactionKind.PullbackAir => "STATE [特殊やられ]引き戻し_空中",
		SpecialReactionKind.GuardPullbackAir => "STATE [特殊ガード]引き戻し_空中",
		_ => ""
	};
	public int JuggleHitCount { get; private set; }
	public int GroundNormalJuggleHitCount { get; private set; }
	public KnockdownType CurrentKnockdownType { get; private set; } = KnockdownType.None;
	public BlowAwayDirection CurrentBlowAwayDirection { get; private set; } = BlowAwayDirection.None;
	public BlowAwayStrength CurrentBlowAwayStrength { get; private set; } = BlowAwayStrength.None;
	public bool CurrentBlowAwayNoBounce { get; private set; }
	public string CurrentBlowAwayAnimationName => ResolveBlowAwayAnimationName(
		CurrentBlowAwayDirection, CurrentBlowAwayStrength, CurrentBlowAwayNoBounce);
	public WallBounceReactionStrength CurrentWallBounceStrength { get; private set; } = WallBounceReactionStrength.None;
	public string CurrentWallBounceAnimationName => CurrentWallBounceStrength == WallBounceReactionStrength.Weak
		? "wall_bounce_weak"
		: "wall_bounce_strong";
	public GroundBounceReactionStrength CurrentGroundBounceStrength { get; private set; } = GroundBounceReactionStrength.None;
	public string CurrentGroundBounceAnimationName => CurrentGroundBounceStrength switch
	{
		GroundBounceReactionStrength.Weak => "ground_bounce_weak",
		GroundBounceReactionStrength.Strong => "ground_bounce_strong",
		_ => "ground_bounce_medium"
	};
	private string CurrentWallBounceStateName => CurrentWallBounceStrength == WallBounceReactionStrength.Weak
		? "STATE [やられ]壁バウンド_弱"
		: "STATE [やられ]壁バウンド_強";
	private string CurrentGroundBounceStateName => CurrentGroundBounceStrength switch
	{
		GroundBounceReactionStrength.Weak => "STATE [やられ]垂直バウンド弱",
		GroundBounceReactionStrength.Strong => "STATE [やられ]垂直バウンド強",
		_ => "STATE [やられ]垂直バウンド中"
	};
	public bool IsInHitstop => HitstopFramesLeft > 0;
	public int HitstunFramesLeft { get; private set; }
	public int HitstopFramesLeft { get; private set; }
	public int ComboCount { get; private set; }
	public int ComboDisplayFramesLeft { get; private set; }
	public float PlaceholderLife { get; private set; }
	public float PlaceholderSpecialMeter { get; private set; }
	private int _placeholderSpecialMeterRecoveryDelayFramesLeft;
	public string CurrentAttackName { get; private set; } = "";
	public string CurrentAttackAnimationName { get; private set; } = "";
	public int CurrentAttackFrame { get; private set; }
	public int CurrentAttackStartupFrames => _currentAttackStartupFrames;
	public int CurrentAttackActiveFrames => _currentAttackActiveFrames;
	public int CurrentAttackRecoveryFrames => _currentAttackRecoveryFrames;
	public float CurrentAttackCharacterVisualScale => _currentMoveData?.CharacterVisualScale ?? 1f;
	public Vector2 CurrentAttackCharacterVisualOffset => _currentMoveData?.CharacterVisualOffset ?? Vector2.Zero;
	public Vector2[] CurrentAttackAnimationDrawingOffsets =>
		_currentMoveData?.AnimationDrawingOffsets ?? Array.Empty<Vector2>();
	public int[] CurrentAttackAnimationSourceTimeline => _currentMoveData?.AnimationSourceTimeline ?? Array.Empty<int>();
	public string CurrentAttackAnimationTailName => IsRegularThrowAttackName(CurrentAttackName) && !_attackHasHit
		? ""
		: _currentMoveData?.AnimationTailName ?? "";
	public string CurrentAttackActiveLoopAnimationName => _currentMoveData?.ActiveLoopAnimationName ?? "";
	public int CurrentAttackAnimationTailStartFrame => IsRegularThrowAttackName(CurrentAttackName) && !_attackHasHit
		? -1
		: _currentMoveData?.AnimationTailStartFrame ?? -1;
	public int CurrentAttackForceDownwardStartFrame => _currentSpecialMove?.ForceDownwardStartFrame ?? -1;
	public int[] CurrentAttackRiseAnimationSourceCycle => _currentSpecialMove?.RiseAnimationSourceCycle ?? Array.Empty<int>();
	public int CurrentAttackRiseAnimationTicksPerSource => _currentSpecialMove?.RiseAnimationTicksPerSource ?? 1;
	public int[] CurrentAttackDescentAnimationSourceCycle => _currentSpecialMove?.DescentAnimationSourceCycle ?? Array.Empty<int>();
	public int CurrentAttackDescentAnimationTicksPerSource => _currentSpecialMove?.DescentAnimationTicksPerSource ?? 1;
	public int[] CurrentAttackLandingAnimationSourceTimeline => _currentSpecialMove?.LandingAnimationSourceTimeline ?? Array.Empty<int>();
	public string CurrentAttackLandingAnimationName => _currentSpecialMove?.LandingAnimationName ?? "";
	public bool IsCurrentSpecialLandingRecovery => _currentSpecialLandingRecovery;
	public int CurrentSpecialLandingRecoveryFrame => _currentSpecialLandingRecoveryFrame;
	public bool SuperActivationFreezeRequested { get; private set; }
	public int SuperActivationFreezeFramesRequested { get; private set; }
	public int SuperBackdropFramesRequested { get; private set; }
	private bool _superBackdropCancelRequested;
	private bool _stateImpactPending;
	private FighterHitState _stateImpactState;
	private Vector2 _stateImpactPosition;
	private int _stateImpactDirection;
	private bool _stateImpactIsFollowup;
	private bool _jumpStartEffectPending;
	private Vector2 _jumpStartEffectGroundPosition;
	private int _jumpStartEffectFacing;
	private bool _jumpStartEffectIsSuperJump;
	private bool _runDustEffectPending;
	private Vector2 _runDustEffectGroundPosition;
	private int _runDustEffectFacing;
	private bool _pendingWallSplatKnockdown;
	private bool _airNormalPerformedSinceTakeoff;
	private int _wallSplatDirection;
	public IReadOnlyList<FighterHitLogEntry> HitLog => _hitLog;
	public int CurrentAttackDamage => _currentAttackDamage;
	public int CurrentAttackHitstunFrames => _currentAttackHitstunFrames;
	public int CurrentAttackBlockstunFrames => _currentAttackBlockstunFrames;
	public bool CurrentAttackKnocksDown => _currentAttackKnocksDown;
	public int CurrentAttackKnockdownFrames => _currentAttackKnockdownFrames;
	public KnockdownType CurrentAttackKnockdownType => _currentAttackKnockdownType;
	public bool CurrentAttackCanHitGroundedKnockdown => _currentAttackCanHitGroundedKnockdown;
	public Vector2 VisualCorrectionOffset { get; private set; }
	public readonly Dictionary<string, AbilityRuntime> Runtime = new();
	private readonly MotionInputBuffer _motionInputBuffer = new();
	private MotionInputDefinition _pendingReusableMotion;
	private long _pendingReusableMotionCompletion = -1;
	private string _pendingReusableMotionAttackName = "";
	private bool _pendingReusableMotionConsumes = true;
	private string _currentBoxStateName = "";
	private int _currentBoxStateFrame;
	public string CurrentBoxStateName => _currentBoxStateName;
	public int CurrentBoxStateFrame => _currentBoxStateFrame;

	public void ResetPlaceholderGauges()
	{
		FighterGaugeData gauges = Definition?.Gauges;
		PlaceholderLife = gauges?.StartingLife ?? 0f;
		PlaceholderSpecialMeter = gauges?.StartingSpecialMeter ?? 0f;
		_placeholderSpecialMeterRecoveryDelayFramesLeft = 0;
		IsPlayingWinAnimation = false;
		WinAnimationFinished = false;
	}

	public void BeginWinAnimation()
	{
		StopActiveAbility();
		ClearAttackState();
		Velocity = Vector2.Zero;
		IsPlayingWinAnimation = true;
		WinAnimationFinished = false;
		OnWinAnimationRequested();
	}

	protected virtual void OnWinAnimationRequested() => MarkWinAnimationFinished();
	protected void MarkWinAnimationFinished() => WinAnimationFinished = true;

	public void BeginDefeatedKoState()
	{
		StopActiveAbility();
		ClearAttackState();
		// The lethal hit has already authored the correct knockdown reaction.
		// Official KO only locks that result; it must not start a second knockdown.
		Velocity = Vector2.Zero;
		OnDefeatedKoRequested();
	}

	protected virtual void OnDefeatedKoRequested() { }

	public void ApplyPlaceholderLifeDrain(float amount, bool allowEmpty = true)
	{
		FighterGaugeData gauges = Definition?.Gauges;
		if (gauges == null || amount <= 0f) return;
		float minimumLife = allowEmpty ? 0f : Mathf.Min(1f, gauges.MaxLife);
		PlaceholderLife = Mathf.Clamp(PlaceholderLife - amount, minimumLife, gauges.MaxLife);
	}

	public void RecoverPlaceholderLife(float amount)
	{
		FighterGaugeData gauges = Definition?.Gauges;
		if (gauges == null || amount <= 0f) return;
		PlaceholderLife = Mathf.MoveToward(PlaceholderLife, gauges.MaxLife, amount);
	}

	public bool HasPlaceholderSpecialMeter(float amount) =>
		amount <= 0f || PlaceholderSpecialMeter + 0.001f >= amount;

	public bool TrySpendPlaceholderSpecialMeter(float amount)
	{
		if (amount <= 0f) return true;
		if (!HasPlaceholderSpecialMeter(amount)) return false;
		PlaceholderSpecialMeter = Mathf.Max(0f, PlaceholderSpecialMeter - amount);
		_placeholderSpecialMeterRecoveryDelayFramesLeft = Mathf.Max(
			_placeholderSpecialMeterRecoveryDelayFramesLeft,
			Definition?.Gauges?.SpecialMeterRecoveryDelayFrames ?? 0);
		return true;
	}

	private void TickPlaceholderSpecialMeterRecovery(float delta)
	{
		FighterGaugeData gauges = Definition?.Gauges;
		if (gauges == null || gauges.SpecialMeterRecoveryPerSecond <= 0f ||
			PlaceholderSpecialMeter >= gauges.MaxSpecialMeter) return;
		if (_placeholderSpecialMeterRecoveryDelayFramesLeft > 0)
		{
			_placeholderSpecialMeterRecoveryDelayFramesLeft--;
			return;
		}
		PlaceholderSpecialMeter = Mathf.MoveToward(PlaceholderSpecialMeter,
			gauges.MaxSpecialMeter, gauges.SpecialMeterRecoveryPerSecond * delta);
	}
	private readonly List<FighterHitLogEntry> _hitLog = new();
	private readonly Dictionary<string, int> _airJumpUses = new();
	private readonly Dictionary<string, int> _normalUsesThisChain = new();
	private readonly Dictionary<string, int> _normalUsesThisAirTime = new();
	private bool _groundedLastFrame;
	private int _pendingLandingLagFrames;
	private bool _continueVerticalPhysicsDuringHitstop;
	private bool _flightUsedThisAirTime;
	private int _verticalHitstopFreezeFramesLeft;
	private float _lastGroundedY;
	private int _lightPunchBufferFramesLeft;
	private int _lightKickBufferFramesLeft;
	private int _heavyPunchBufferFramesLeft;
	private int _heavyKickBufferFramesLeft;
	private int _special1BufferFramesLeft;
	private int _special2BufferFramesLeft;
	private int _runCrouchSlideFramesLeft;
	private int _runStopSlideFramesLeft;
	private int _previousSampledHorizontalDirection;
	private int _heldHorizontalDirection;
	private int _horizontalDirectionHeldFrames;
	private bool _previousSampledDown;
	private bool _doubleJumpAirDashAvailable;
	private int _attackStartupFramesLeft;
	private int _attackActiveFramesLeft;
	private int _attackRecoveryFramesLeft;
	private bool _attackHasHit;
	private bool _attackHasUnblockedHit;
	private readonly HashSet<int> _attackHitGroups = new();
	private bool _projectileSpawnedThisAttack;
	private int _projectilesSpawnedThisAttack;
	private bool _moveVisualEffectSpawned;
	private int _currentAttackChargeFrames;
	private bool _currentAttackFullyCharged;
	private int _sustainMashGraceFramesLeft;
	private int _sustainMashHitIntervalFramesLeft;
	private bool _startingBlockReflector;
	private int _parrySuccessPresentationFramesLeft;
	private int _currentAttackHitsRemaining;
	private int _currentAttackHitCooldownFramesLeft;
	private bool _currentSuperConfirmed;
	private int _currentSuperConfirmedFrame = -1;
	private FighterController _currentSuperLockedDefender;
	private FighterController _capturedThrowVictim;
	private FighterController _throwCaptor;
	private bool _spdGrabConnected;
	private bool _spdHasLeftGround;
	private bool _spdSlamImpactPending;
	private FighterController _spdSlamVictim;
	private Vector2 _spdSlamImpactPosition;
	private int _spdSlamDamage;
	private bool _spdSlamImpactWasSuper;
	private Vector2 _currentSuperLockedDefenderPosition;
	private Vector2 _currentSuperLockedAttackerOffset;
	private bool _currentAttackStartedAirborne;
	private bool _currentAttackStartedFromAirDash;
	private bool _currentAttackStartedFromRun;
	private bool _currentAttackStartedCrouching;
	private bool _currentSpecialLandingRecovery;
	private bool _currentSpecialSelfLaunchApplied;
	private int _currentSpecialLandingRecoveryFrame;
	private int _currentAttackStartupFrames;
	private int _currentAttackActiveFrames;
	private int _currentAttackRecoveryFrames;
	private int _wakeupFramesLeft;
	private int _activeWakeupTotalFrames;
	private int _movementInvulnerabilityFramesLeft;
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
	private BlowAwayDirection _currentAttackBlowAwayDirection;
	private BlowAwayStrength _currentAttackBlowAwayStrength;
	private bool _currentAttackBlowAwayNoBounce;
	private WallBounceReactionStrength _currentAttackWallBounceStrength;
	private GroundBounceReactionStrength _currentAttackGroundBounceStrength;
	private GuardReactionStrength _currentAttackGuardReactionStrength;
	private SpecialReactionKind _currentAttackSpecialReaction;
	private Rect2 _currentAttackHitboxLocal;
	private SuperMoveData _currentSuperMove;
	private NormalMoveData _currentMoveData;
	private PackedScene _currentContactHitSparkScene;
	private SpecialMoveData _currentSpecialMove;
	private float _pendingGroundBounceSpeed = -1f;
	private bool _pendingGroundBounceIntoJuggle;
	private int _launcherJumpCancelFramesLeft;
	private int _airLightJumpCancelFramesLeft;
	private NormalMoveRule _currentMoveRule;
	private const int DoubleTapDashWindowFrames = 12;
	// SFII-like timing: the motion must be completed deliberately, and the attack
	// button must follow soon after forward rather than recalling an old motion.
	private const int QuarterCircleForwardWindowFrames = 16;
	private const int QuarterCircleForwardLatchFrames = 10;
	private const int SuperChordGraceFrames = 2;
	private const int UpInputMotionSpecialStrictWindowFrames = 4;
	private const int BackDashInputLockoutWindowFrames = 18;
	private const string ElectricWindGodFistName = "ELECTRIC WIND GOD FIST";
	private const string LightProjectileName = "LIGHT PROJECTILE";
	public const string CrouchingMediumJabName = "CROUCHING MEDIUM";
	public const string DownForwardHeavyPunchName = "HEAVY PUNCH DOWN FORWARD";
	public const string ThrowAttackName = "THROW";
	public const string BackThrowAttackName = "BACK THROW";
	public const string ForwardHeavyPunchName = "HEAVY PUNCH FORWARD";
	public const string ForwardLightKickName = "LIGHT KICK FORWARD";
	public const string ForwardHeavyKickName = "HEAVY KICK FORWARD";
	public const string CrouchingHeavyKickName = "HEAVY KICK CROUCHING";
	public const string CrouchingHeavyPunchName = "HEAVY PUNCH CROUCHING";
	public const string AirHeavyPunchName = "HEAVY PUNCH AIR";
	public const string BackLightPunchName = "MEDIUM PUNCH BACK";
	public const string CrouchingMediumKickName = "CROUCHING MEDIUM KICK";
	public const string AirBackLightPunchName = "MEDIUM PUNCH AIR BACK";
	public const string AirBackLightKickName = "MEDIUM KICK AIR BACK";
	public const string QcfPowerPunchRekkaName = "QCF POWER PUNCH REKKA";
	public const string QcfPowerPunchLightName = "QCF POWER PUNCH LIGHT";
	public const string QcfPowerPunchHeavyName = "QCF POWER PUNCH HEAVY";
	public const string BlockReflectorName = "BLOCK REFLECTOR";
	public const string SanzoParryName = "SANZOU PARRY";
	public const string SanzoSuperReflectorName = "SUPER REFLECTOR";
	public const string SanzoSpdName = "SANZOU SPD";
	public const string SanzoSuperSpdName = "SANZOU SUPER SPD";
	public const string StompSpecialName = "STOMP SPECIAL";
	public const string CommandRunLightName = "COMMAND RUN LIGHT";
	public const string CommandRunHeavyName = "COMMAND RUN HEAVY";
	public const string CommandRunHopName = "COMMAND RUN HOP";
	public const string CommandRunPunchName = "COMMAND RUN PUNCH";
	private const int ChargeButtonLenienceFrames = 6; // Set before the same-frame tick: five full follow-up frames remain.
	private const string HeavyProjectileName = "HEAVY PROJECTILE";
	private const string SuperFireballName = "SUPER FIREBALL";
	private const string SuperRushName = "SUPER RUSH";
	[Export] public float DirectionalThrowRange { get; set; } = 90f;
	[Export] public float ThrowLaunchSpeed { get; set; } = 760f;
	[ExportGroup("Sanzou SPD")]
	[Export] public float SpdRiseSpeed { get; set; } = 1450f;
	[Export] public int SpdSlamKnockdownFrames { get; set; } = 90;
	[Export] public int SpdLandingRecoveryFrames { get; set; } = 18;
	[Export] public float SuperSpdRiseSpeed { get; set; } = 3600f;
	[Export] public float SuperSpdDescentSpeed { get; set; } = 4200f;
	[Export] public int SuperSpdSlamKnockdownFrames { get; set; } = 150;
	[Export] public int SuperSpdLandingRecoveryFrames { get; set; } = 30;
	private FighterController _opponent;

	public void SetOpponent(FighterController opponent) => _opponent = opponent;
	public bool IsSameTeam(FighterController other) => other != null && TeamId != 0 && TeamId == other.TeamId;
	/// <summary>
	/// Only the controlled point fighter owns a pushbox and can receive hits. Helpers
	/// remain stage bodies and may finish attacks, but cannot create team collision piles.
	/// </summary>
	public void SetPointCollisionParticipation(bool active) => ParticipatesInPointCollision = active;

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
		public string RepeatLightPunchChainTarget { get; init; }
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
		public bool PreserveAirborneTargetVelocity { get; init; }
		public float ShakeStrengthOverride { get; init; }
		public HitReactionKind HitReaction { get; init; }
		public KnockdownType KnockdownType { get; init; }
		public bool KnocksDown { get; init; }
		public int KnockdownFrames { get; init; }
		public bool CanHitGroundedKnockdown { get; init; }
		public BlowAwayDirection BlowAwayDirection { get; init; }
		public BlowAwayStrength BlowAwayStrength { get; init; }
		public float BlowAwaySpeed { get; init; }
		public bool BlowAwayNoBounce { get; init; }
		public WallBounceReactionStrength WallBounceStrength { get; init; }
		public GroundBounceReactionStrength GroundBounceStrength { get; init; }
		public GuardReactionStrength GuardReactionStrength { get; init; }
		public SpecialReactionKind SpecialReaction { get; init; }
		public bool SuppressFallbackHitbox { get; init; }
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
				RepeatLightPunchChainTarget = data.RepeatLightPunchChainTarget,
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
				PreserveAirborneTargetVelocity = data.PreserveAirborneTargetVelocity,
				ShakeStrengthOverride = data.ShakeStrength,
				HitReaction = data.HitReaction,
				KnockdownType = data.KnockdownType,
				KnocksDown = data.KnocksDown,
				KnockdownFrames = data.KnockdownFrames,
				CanHitGroundedKnockdown = data.CanHitGroundedKnockdown,
				BlowAwayDirection = data.BlowAwayDirection,
				BlowAwayStrength = data.BlowAwayStrength,
				BlowAwaySpeed = data.BlowAwaySpeed,
				BlowAwayNoBounce = data.BlowAwayNoBounce,
				WallBounceStrength = data.WallBounceStrength,
				GroundBounceStrength = data.GroundBounceStrength,
				GuardReactionStrength = data.GuardReactionStrength,
				SpecialReaction = data.SpecialReaction,
				SuppressFallbackHitbox = data.SuppressFallbackHitbox,
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
		EnsureCollisionPolicy();
		if (Definition?.Tuning is null) return;
		FighterInput input = ReadLocalInput
			? NativeInputRouter.GetGameplayInput((long)Engine.GetPhysicsFrames(), LocalPlayerIndex)
			: CurrentInput;
		Simulate(input, (float)delta);
	}

	public void SetExternalInput(FighterInput input) => CurrentInput = input;
	public void SetExternalInputFrame(NativeInputFrame frame) => CurrentInput = frame.ToFighterInput();

	/// <summary>Explicit non-Godot hook for tools that exercise the legacy command latches.</summary>
	public void InjectMotionAction(StringName action)
	{
		if (action == "move_down") _motionInputBuffer.PressDown();
		else if (action == "move_left") HandleHorizontalTap(-1);
		else if (action == "move_right") HandleHorizontalTap(1);
		else if (action == "jump") _motionInputBuffer.PressJump(Definition?.Tuning?.InputBufferFrames ?? 3);
	}
	public void SetFacing(int direction) => Facing = direction >= 0 ? 1 : -1;
	public bool TryBeginCloneCall()
	{
		if (string.Equals(Definition?.FighterName, "Sanzou Kongoumaru", System.StringComparison.OrdinalIgnoreCase))
			return false;
		if (HitState != FighterHitState.None || !WasGrounded || ActiveAbility != null) return false;
		if (!IsAttacking) return true;
		if (!IsNormalAttackName(CurrentAttackName) || !_attackHasHit ||
			!CanCancelCurrentMove(CancelKind.Special, "CLONE ASSIST")) return false;
		ClearAttackState();
		return true;
	}

	public void Simulate(FighterInput input, float delta)
	{
		EnsureCollisionPolicy();
		JustLanded = false;
		PreviousGlobalPosition = GlobalPosition;
		CurrentInput = input;
		TrackHorizontalHoldDuration(input.Horizontal);
		RecordSampledMotionEdges(input);
		_motionInputBuffer.RecordReusableInput(input, Facing);
		_motionInputBuffer.UpdateDownCharge(input.Vertical > 0.5f);
		_motionInputBuffer.UpdateBackForwardCharge(input.Horizontal, Facing,
			Mathf.Max(ChargeButtonLenienceFrames, Definition?.Tuning?.InputBufferFrames ?? 3));
		if (GodotObject.IsInstanceValid(_throwCaptor))
		{
			Velocity = Vector2.Zero;
			return;
		}
		_throwCaptor = null;
		WasGrounded = IsOnFloor();
		if (_spdGrabConnected && !WasGrounded) _spdHasLeftGround = true;
		UpdateStateBoxTimeline(input);
		if (HitstopFramesLeft > 0)
		{
			// Pause motion-command decay alongside attack buttons. This lets a QCF
			// completed during contact freeze survive long enough to super cancel.
			UpdateInputBuffer(input, true);
			HitstopFramesLeft--;
			if (_continueVerticalPhysicsDuringHitstop && !WasGrounded)
			{
				if (_verticalHitstopFreezeFramesLeft > 0)
					_verticalHitstopFreezeFramesLeft--;
				else
					AdvanceVerticalPhysicsDuringHitstop(delta);
			}
			if (HitstopFramesLeft <= 0)
			{
				_continueVerticalPhysicsDuringHitstop = false;
				_verticalHitstopFreezeFramesLeft = 0;
			}
			return;
		}
		TickPlaceholderSpecialMeterRecovery(delta);
		_motionInputBuffer.Tick();
		if (_parrySuccessPresentationFramesLeft > 0) _parrySuccessPresentationFramesLeft--;
		if (_movementInvulnerabilityFramesLeft > 0) _movementInvulnerabilityFramesLeft--;
		if (WasGrounded && LandingLagFramesLeft > 0) LandingLagFramesLeft--;
		if (FaceWithMovement && input.Horizontal != 0) Facing = input.Horizontal > 0 ? 1 : -1;
		UpdateInputBuffer(input, false);
		if (IsInBlockstun && _motionInputBuffer.HasDragonPunchCommand &&
			(input.LightPunchPressed || input.HeavyPunchPressed))
		{
			HitstunFramesLeft = 0;
			HitState = FighterHitState.None;
			_startingBlockReflector = true;
			TryStartBasicAttack();
			_startingBlockReflector = false;
			_motionInputBuffer.ConsumeDragonPunchCommand();
		}

		if (WasGrounded)
		{
			_lastGroundedY = GlobalPosition.Y;
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
			AirAttackLandingFramesLeft = 0;
			FlightLandingFramesLeft = 0;
			HitstunFramesLeft--;
			ClearAttackState();
			if (HitstunFramesLeft == 0)
			{
				if (ShouldPersistAirReactionUntilLanding())
					HitstunFramesLeft = 1;
				else if (HitState == FighterHitState.GroundedKnockdown && ResolveWakeupDurationFrames() > 0)
				{
					BeginWakeup();
				}
				else
				{
					HitState = FighterHitState.None;
					CurrentKnockdownType = KnockdownType.None;
					ClearBlowAwayState();
					CurrentWallBounceStrength = WallBounceReactionStrength.None;
					CurrentGroundBounceStrength = GroundBounceReactionStrength.None;
					CurrentGuardReactionStrength = GuardReactionStrength.None;
					CurrentSpecialReaction = SpecialReactionKind.None;
					JuggleHitCount = 0;
					GroundNormalJuggleHitCount = 0;
				}
			}
		}
		else if (_wakeupFramesLeft > 0)
		{
			_wakeupFramesLeft--;
			if (_wakeupFramesLeft == 0) _activeWakeupTotalFrames = 0;
			Velocity = new Vector2(0f, Velocity.Y);
			ClearAttackState();
		}
		else if (IsInAirAttackLanding || IsInFlightLanding)
		{
			// This short state is intentionally fully state-driven. It can be
			// replaced or removed from an individual fighter without changing
			// how other characters land from aerial normals.
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, BasicAttackFriction * delta), Velocity.Y);
		}
		else
		{
			if (!TryUniversalBlueRecoveryCancel() && !TryCancelNormalIntoFlightDeactivation() && !TryStartMovementAbilityCancel() && !TryStartLauncherChaseJump() && !TryStartAirLightHitJumpCancel() && !TryStartNormalJumpCancel() &&
				!TryStartNormalAirDashCancel() && !TryCrouchCancelCurrentNormal() && !TryStartDoubleJumpStateAirDashCancel())
			{
				CancelGroundMovementForCrouchNormal();
				TryStartBasicAttack();
				if (!IsAttacking) TryStartAbility();
			}
			TickBasicAttack();
			UpdateCapturedThrowVictim();
			bool tickAbility = !IsAttacking || ActiveAbility is FlightAbility activeFlight &&
				activeFlight.ShouldTickDuringAttack(this);
			if (tickAbility && ActiveAbility != null && !ActiveAbility.Tick(this, GetRuntime(ActiveAbility), delta))
				StopActiveAbility();
		}
		if (CurrentAttackName == SuperRushName && !_currentSuperConfirmed && CurrentAttackFrame >= 18)
		{
			Velocity = new Vector2(0f, Velocity.Y);
			_superBackdropCancelRequested = true;
			ClearAttackState();
		}

		ApplyBaseMotion(delta);
		if (_spdGrabConnected && CurrentAttackName == SanzoSuperSpdName && _spdHasLeftGround && Velocity.Y >= 0f)
			Velocity = new Vector2(Velocity.X, Mathf.Max(Velocity.Y, SuperSpdDescentSpeed));
		ClampForcedDescentSpeed();
		TickComboDisplay();
		MoveAndSlide();
		if (!WasGrounded && HitstunFramesLeft > 0 && HitState == FighterHitState.Tumble &&
			CurrentBlowAwayDirection == BlowAwayDirection.None && Velocity.Y >= 0f)
			RecoverFromComboHitstun();
		JustLanded = !WasGrounded && IsOnFloor();
		if (JustLanded && _spdGrabConnected && _spdHasLeftGround)
			ResolveSpdSlamLanding();
		if (JustLanded && HitstunFramesLeft > 0 && CurrentBlowAwayDirection != BlowAwayDirection.None)
			ResolveBlowAwayLanding();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.GroundBounce)
			ResolveGroundBounceLanding();
		else if (JustLanded && HitstunFramesLeft > 0 && _pendingWallSplatKnockdown)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.Knockdown)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.Stumble)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.HitFall)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.Juggle &&
			CurrentKnockdownType != KnockdownType.None)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState == FighterHitState.WallSplat)
			EnterGroundedKnockdown();
		else if (JustLanded && HitstunFramesLeft > 0 && HitState != FighterHitState.GroundedKnockdown)
			RecoverFromComboHitstun();
		bool beganSpecialLandingRecovery = JustLanded && IsAttacking &&
			_currentSpecialMove is { LandingRecoveryFrames: > 0 };
		if (beganSpecialLandingRecovery)
			BeginCurrentSpecialLandingRecovery();
		if (JustLanded && !beganSpecialLandingRecovery && HitstunFramesLeft <= 0 &&
			_airNormalPerformedSinceTakeoff)
		{
			BeginAirAttackLanding();
			_airNormalPerformedSinceTakeoff = false;
		}
		if (JustLanded && IsAttacking && _currentAttackStartedAirborne && !beganSpecialLandingRecovery)
		{
			ClearAttackState();
		}
		if (JustLanded && _pendingLandingLagFrames > 0)
		{
			LandingLagFramesLeft = ResolveLandingLagFramesForCurrentAirTime(_pendingLandingLagFrames);
			_pendingLandingLagFrames = 0;
			ConsumeJumpBuffer();
		}
		if (AirAttackLandingFramesLeft > 0 && !JustLanded)
		{
			AirAttackLandingFramesLeft--;
			if (AirAttackLandingFramesLeft <= 0) CurrentAirAttackLandingAnimationName = "air_attack_landing";
		}
		if (FlightLandingFramesLeft > 0 && !JustLanded) FlightLandingFramesLeft--;
		_groundedLastFrame = WasGrounded;
		TrySpawnProjectileForCurrentAttack();
		TrySpawnMoveVisualEffect();
		if (_launcherJumpCancelFramesLeft > 0) _launcherJumpCancelFramesLeft--;
		if (_airLightJumpCancelFramesLeft > 0) _airLightJumpCancelFramesLeft--;
	}

	private void RecordSampledMotionEdges(FighterInput input)
	{
		int horizontal = input.Horizontal > 0.5f ? 1 : input.Horizontal < -0.5f ? -1 : 0;
		bool down = input.Vertical > 0.5f;
		bool downPressedThisFrame = down && !_previousSampledDown;
		bool horizontalPressedThisFrame = horizontal != 0 && horizontal != _previousSampledHorizontalDirection;
		// A direct neutral -> diagonal sample is one direction, not two ordered inputs.
		// D -> DF supplies QCF's forward edge. Releasing DF -> F must not supply another
		// forward tap: the horizontal control never returned to neutral, so counting it
		// again turns one quarter circle into a false double-tap dash.
		if (downPressedThisFrame) _motionInputBuffer.PressDown();
		if (horizontalPressedThisFrame && !downPressedThisFrame)
			HandleHorizontalTap(horizontal);
		if (input.JumpPressed)
			_motionInputBuffer.PressJump(Mathf.Max(ChargeButtonLenienceFrames, Definition?.Tuning?.InputBufferFrames ?? 3));
		_previousSampledHorizontalDirection = horizontal;
		_previousSampledDown = down;
	}

	private void ClampForcedDescentSpeed()
	{
		if (_currentSpecialMove is not { ForceDownwardStartFrame: >= 0, ForceDownwardTerminalSpeed: > 0f } descent ||
			CurrentAttackFrame < descent.ForceDownwardStartFrame || WasGrounded || Velocity.Y <= descent.ForceDownwardTerminalSpeed)
			return;
		Velocity = new Vector2(Velocity.X, descent.ForceDownwardTerminalSpeed);
	}

	private void BeginCurrentSpecialLandingRecovery()
	{
		int landingFrames = ResolveLandingLagFramesForCurrentAirTime(_currentSpecialMove.LandingRecoveryFrames);
		_attackStartupFramesLeft = 0;
		_attackActiveFramesLeft = 0;
		_currentAttackActiveFrames = Mathf.Max(0, CurrentAttackFrame - _currentAttackStartupFrames);
		_attackRecoveryFramesLeft = landingFrames;
		_currentAttackRecoveryFrames = landingFrames;
		_currentSpecialLandingRecovery = true;
		_currentSpecialLandingRecoveryFrame = 0;
	}

	private void BeginAirAttackLanding()
	{
		NormalMoveData landing = Definition?.StateBoxes?.FindStateRule("STATE AIR ATTACK LANDING");
		if (landing == null) return;
		bool hasMoveSpecificLanding = _currentMoveData != null &&
			!string.IsNullOrEmpty(_currentMoveData.AirAttackLandingAnimationName);
		CurrentAirAttackLandingAnimationName = hasMoveSpecificLanding
			? _currentMoveData.AirAttackLandingAnimationName
			: landing.AnimationName;
		int authoredLandingFrames = hasMoveSpecificLanding && _currentMoveData.AirAttackLandingFrames > 0
			? _currentMoveData.AirAttackLandingFrames
			: Mathf.Max(1,
			Mathf.Max(0, landing.StartupFrames) + Mathf.Max(0, landing.ActiveFrames) +
			Mathf.Max(0, landing.RecoveryFrames));
		AirAttackLandingFramesLeft = ResolveLandingLagFramesForCurrentAirTime(authoredLandingFrames);
	}

	/// <summary>Returns full authored recovery after flight; ordinary airtime is capped at two frames.</summary>
	public int ResolveLandingLagFramesForCurrentAirTime(int authoredFrames)
	{
		int frames = Mathf.Max(1, authoredFrames);
		if (_flightUsedThisAirTime) return frames;
		float multiplier = Mathf.Clamp(Definition?.Tuning?.NonFlightLandingLagMultiplier ?? 1f, 0.1f, 1f);
		return Mathf.Min(2, Mathf.Max(1, Mathf.CeilToInt(frames * multiplier)));
	}

	public void BeginFlightLanding()
	{
		NormalMoveData landing = Definition?.StateBoxes?.FindStateRule("STATE FLIGHT LANDING");
		if (landing == null) return;
		FlightLandingFramesLeft = Mathf.Max(1,
			Mathf.Max(0, landing.StartupFrames) + Mathf.Max(0, landing.ActiveFrames) +
			Mathf.Max(0, landing.RecoveryFrames));
	}

	/// <summary>
	/// Called by character-authored flight resources while airborne. The mark survives both
	/// ordinary-jump and super-jump state transitions and is cleared only by the next landing.
	/// </summary>
	public void MarkFlightUsedThisAirTime()
	{
		if (!WasGrounded) _flightUsedThisAirTime = true;
	}

	public AbilityRuntime GetRuntime(MovementAbility ability)
	{
		if (!Runtime.TryGetValue(ability.Id, out var runtime))
			Runtime[ability.Id] = runtime = new AbilityRuntime();
		return runtime;
	}

	public void BeginMovementInvulnerability(int frames) =>
		_movementInvulnerabilityFramesLeft = Mathf.Max(_movementInvulnerabilityFramesLeft, frames);

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
	public void SetSuperJumpPresentationDirection(float horizontal) =>
		SuperJumpPresentationDirection = Mathf.Abs(horizontal) < 0.1f ? 0 : Mathf.Sign(horizontal * Facing);
	public void SetShortHopPushboxRules(bool interactsWithGroundedPushbox, bool pushesGroundedOpponent)
	{
		ShortHopInteractsWithGroundedPushbox = interactsWithGroundedPushbox;
		ShortHopPushesGroundedOpponent = pushesGroundedOpponent;
	}
	public void MarkShortHopRoute() => IsInShortHopRoute = true;
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
	public void BeginRunStopSlide(int frames = -1) =>
		_runStopSlideFramesLeft = Mathf.Max(_runStopSlideFramesLeft, frames > 0 ? frames : RunStopSlideFrames);
	public bool TryApplyBasicAttackHit(FighterController defender, out int hitstopFrames, out float shakeStrength, out float hitPushback, out Vector2 hitPoint, out bool heavySpark)
	{
		hitstopFrames = 0;
		shakeStrength = 0f;
		hitPushback = 0f;
		hitPoint = Vector2.Zero;
		heavySpark = false;
		_currentContactHitSparkScene = null;
		LastContactWasBlocked = false;
		LastContactWasInstantBlocked = false;
		LastContactWasParried = false;
		LastContactDefenderHitstopFrames = 0;
		if (!IsAttackActive || IsProjectileAttackName(CurrentAttackName) || defender == null || defender == this || IsSameTeam(defender) || defender.IsWakingUp || defender.IsMovementInvulnerable) return false;
		if (_currentSuperMove != null && (_currentAttackHitsRemaining <= 0 || _currentAttackHitCooldownFramesLeft > 0)) return false;
		if (!TryFindBoxContact(GetActiveWorldBoxInstances(FighterBoxKind.Hitbox), defender.GetActiveWorldBoxInstances(FighterBoxKind.Hurtbox),
			out hitPoint, out ActiveFighterBox hitbox, out ActiveFighterBox hurtbox)) return false;
		bool defenderWasWallSliding = defender._pendingWallSplatKnockdown;
		if (IsRegularThrowAttackName(CurrentAttackName) || IsSpdGrabAttackName(CurrentAttackName))
		{
			if (defender.IsInHitstun || defender.IsKnockedDown ||
				(IsSpdGrabAttackName(CurrentAttackName) && !defender.WasGrounded)) return false;
			_attackHasHit = true;
			_attackHasUnblockedHit = true;
			// Throws have no strike spark. Release the defender during the middle of line 16
			// and guarantee a knockdown instead of resolving this contact as a normal hit.
			heavySpark = false;
			hitstopFrames = 0;
			shakeStrength = HeavyAttackShakeStrength;
			hitPushback = HeavyAttackPushback;
			CaptureThrowVictim(defender);
			return true;
		}
		FighterBoxFrame hitboxData = hitbox.Source;
		_currentContactHitSparkScene = hitboxData?.HitSparkScene;
		if (defender.IsGroundedKnockdown && !CanCurrentHitboxHitGroundedKnockdown(hitboxData)) return false;
		if (_currentSuperMove == null)
		{
			int hitGroup = hitboxData?.HitGroup ?? 0;
			if (hitGroup > 0)
			{
				if (!_attackHitGroups.Add(hitGroup)) return false;
			}
			else if (_attackHasHit) return false;
		}
		if (defender.TryParryIncomingHit(this, hitPoint))
		{
			_attackHasHit = true;
			if (_currentSuperMove != null) _currentAttackHitsRemaining = 0;
			LastContactWasParried = true;
			hitstopFrames = 12;
			LastContactDefenderHitstopFrames = hitstopFrames;
			shakeStrength = 4.5f;
			return true;
		}
		_attackHasHit = true;
		heavySpark = UsesHeavyHitSpark(CurrentAttackName);
		bool superHit = _currentSuperMove != null;
		bool finalSuperHit = superHit && _currentAttackHitsRemaining == 1;
		if (superHit && !_currentSuperConfirmed) ConfirmCurrentSuper(defender);
		bool isLauncher = _currentMoveRule.Launches || hitboxData?.Launches == true;
		bool penultimateSuperRushHit = CurrentAttackName == SuperRushName && _currentAttackHitsRemaining == 2;
		bool jumpingHeavyHitGroundedDefender = _currentAttackStartedAirborne && defender.WasGrounded &&
			CurrentAttackName.StartsWith("HEAVY");
		int authoredBaseHitstun = finalSuperHit
			? _currentSuperMove.FinalHitstunFrames
			: penultimateSuperRushHit ? 100 : ResolveIntOverride(hitboxData?.HitstunFrames, _currentAttackHitstunFrames);
		int baseHitstun = authoredBaseHitstun;
		if (jumpingHeavyHitGroundedDefender)
			baseHitstun = Mathf.Max(1, JumpingHeavyGroundedHitstunFrames);
		float basePushback = finalSuperHit ? _currentSuperMove.FinalPushback : ResolveFloatOverride(hitboxData?.Pushback, _currentAttackPushback);
		HitReactionKind hitReaction = hitboxData?.HitReaction ?? _currentAttackHitReaction;
		BlowAwayDirection blowAwayDirection = hitboxData?.BlowAwayDirection is { } boxDirection && boxDirection != BlowAwayDirection.None
			? boxDirection
			: _currentAttackBlowAwayDirection;
		BlowAwayStrength blowAwayStrength = hitboxData?.BlowAwayStrength is { } boxStrength && boxStrength != BlowAwayStrength.None
			? boxStrength
			: _currentAttackBlowAwayStrength;
		bool blowAwayNoBounce = hitboxData?.BlowAwayNoBounce == true || _currentAttackBlowAwayNoBounce;
		WallBounceReactionStrength wallBounceStrength = hitboxData?.WallBounceStrength is { } boxWallBounceStrength &&
			boxWallBounceStrength != WallBounceReactionStrength.None
			? boxWallBounceStrength
			: _currentAttackWallBounceStrength;
		GroundBounceReactionStrength groundBounceStrength = hitboxData?.GroundBounceStrength is { } boxGroundBounceStrength &&
			boxGroundBounceStrength != GroundBounceReactionStrength.None
			? boxGroundBounceStrength
			: _currentAttackGroundBounceStrength;
		GuardReactionStrength guardReactionStrength = ResolveCurrentGuardReactionStrength(hitboxData);
		bool airborneLightNormal = _currentAttackStartedAirborne && IsCurrentAttackLightNormal();
		float appliedPushback = isLauncher
			? ResolveFloatOverride(hitboxData?.LaunchPushback, _currentMoveRule.LaunchPushback)
			: airborneLightNormal
				? Mathf.Max(0f, AirLightAttackPushback)
				: basePushback * (_currentAttackStartedAirborne ? AirAttackPushbackMultiplier : 1f);
		// Keep light air-chain spacing stable instead of pushing the defender farther
		// away on every juggle hit.
		if (!airborneLightNormal && defender.HitState == FighterHitState.Juggle && !defender.WasGrounded)
			appliedPushback *= Mathf.Min(MaxJuggleDistanceScale,
				1f + Mathf.Max(0, defender.JuggleHitCount) * JuggleDistanceScalePerHit);
		if (!_currentAttackStartedAirborne && !defender.WasGrounded)
			appliedPushback *= GroundToAirPushbackMultiplier;
		bool groundedNormalContinuingJuggle = defender.HitState == FighterHitState.Juggle &&
			!defender.WasGrounded && !_currentAttackStartedAirborne &&
			_currentSpecialMove == null && _currentSuperMove == null && IsNormalAttackName(CurrentAttackName);
		if (groundedNormalContinuingJuggle)
		{
			appliedPushback *= GroundNormalJugglePushbackMultiplier;
			defender.GroundNormalJuggleHitCount++;
		}
		bool counterHit = defender.IsAttacking;
		int appliedHitstun = baseHitstun + (counterHit ? CounterHitExtraHitstunFrames : 0);
		if (_currentAttackStartedAirborne && !defender.WasGrounded)
			appliedHitstun += AirToAirHitstunBonusFrames;
		if (_currentAttackStartedAirborne && !defender.WasGrounded && CurrentAttackIsNormal)
			appliedHitstun = Mathf.Max(1, appliedHitstun + AirToAirNormalHitstunAdjustment);
		SpecialReactionKind specialReaction = ResolveCurrentSpecialReaction(hitboxData);
		bool hasDedicatedReaction = blowAwayDirection != BlowAwayDirection.None || isLauncher ||
			hitReaction != HitReactionKind.Normal || CurrentHitboxRequestsKnockdown(hitboxData);
		if (specialReaction == SpecialReactionKind.None && ShouldUseAutomaticSpecialStagger(counterHit,
			_currentSpecialMove != null || _currentSuperMove != null, hasDedicatedReaction))
			specialReaction = SpecialReactionKind.Stagger;
		FighterAttackLevel attackLevel = ResolveCurrentAttackLevel(hitboxData);
		if (defender.CanBlockStrike(attackLevel, this))
		{
			int authoredBlockstun = ResolveIntOverride(hitboxData?.BlockstunFrames, _currentAttackBlockstunFrames);
			int appliedBlockstun = authoredBlockstun > 0 ? authoredBlockstun : Mathf.Max(1, authoredBaseHitstun - 4);
			bool instantBlock = defender.IsInstantBlockAgainst(this);
			if (instantBlock) appliedBlockstun = Mathf.Max(1, Mathf.CeilToInt(appliedBlockstun * 0.5f));
			float blockPushback = appliedPushback * BlockPushbackMultiplier;
			if (IsCurrentAttackHeavyNormal())
				blockPushback *= Mathf.Clamp(HeavyNormalBlockPushbackScale, 0f, 1f);
			defender.ApplyBlockstun(appliedBlockstun, Facing * blockPushback, guardReactionStrength,
				ResolveGuardSpecialReaction(specialReaction, defender.WasGrounded),
				defender.ResolveCrouchingGuard(attackLevel));
			LastContactWasBlocked = true;
			LastContactWasInstantBlocked = instantBlock;
			hitstopFrames = ResolveIntOverride(hitboxData?.HitstopFrames, _currentAttackHitstopFrames);
			bool useGroundedNormalHitstop = !_currentAttackStartedAirborne || defender.WasGrounded;
			if (_currentSpecialMove?.AddsGlobalHitstopBonus != false)
			{
				hitstopFrames += GlobalHitstopBonusFrames +
					(useGroundedNormalHitstop ? GroundedAttackHitstopBonusFrames : AirAttackHitstopBonusFrames);
				if (_currentAttackStartedAirborne && defender.WasGrounded)
					hitstopFrames += JumpInHitstopBonusFrames;
			}
			hitstopFrames = Mathf.Max(1, hitstopFrames + BlockHitstopBonusFrames);
			if (_currentAttackStartedAirborne)
				hitstopFrames = ScaleAirAttackHitstop(hitstopFrames);
			hitstopFrames = ScaleSpecialMoveHitstop(hitstopFrames);
			LastContactDefenderHitstopFrames = hitstopFrames;
			shakeStrength = BlockShakeStrength;
			hitPushback = blockPushback;
			if (_currentSuperMove != null)
			{
				_currentAttackHitsRemaining = 0;
				ResolveBlockedSuperRush();
			}
			return true;
		}
		_attackHasUnblockedHit = true;
		if (TrySpawnMoveContactEffect(hitPoint))
			defender.OnMoveContactBurnVisual(_currentMoveData.EffectBlackensDefender,
				_currentMoveData.EffectBlackSilhouetteFrames,
				_currentMoveData.EffectDefenderFireSpriteFrames,
				_currentMoveData.EffectDefenderFireAnimationName);
		defender.LastHitReactionLevel = CurrentAttackName.Contains("HEAVY")
			? 2
			: CurrentAttackName.Contains("MEDIUM") ? 1 : 0;
		defender.LastHitCameFromAir = _currentAttackStartedAirborne;
		if (blowAwayDirection != BlowAwayDirection.None)
		{
			float authoredBlowAwaySpeed = ResolveFloatOverride(hitboxData?.BlowAwaySpeed,
				_currentMoveRule.BlowAwaySpeed);
			defender.ApplyBlowAwayHitstun(appliedHitstun, Facing, blowAwayDirection, blowAwayStrength,
				blowAwayNoBounce, authoredBlowAwaySpeed);
		}
		else if (isLauncher)
		{
			int launchHitstun = ResolveIntOverride(hitboxData?.LaunchHitstunFrames, _currentMoveRule.LaunchHitstunFrames) +
				(counterHit ? CounterHitExtraHitstunFrames : 0);
			if (_currentAttackStartedAirborne && !defender.WasGrounded)
				launchHitstun += AirToAirHitstunBonusFrames;
			if (_currentAttackStartedAirborne && !defender.WasGrounded && CurrentAttackIsNormal)
				launchHitstun = Mathf.Max(1, launchHitstun + AirToAirNormalHitstunAdjustment);
			float launchSpeed = ResolveFloatOverride(hitboxData?.LaunchSpeed, _currentMoveRule.LaunchSpeed);
			if (CurrentAttackName == CrouchingMediumJabName)
				defender.ApplyJuggleHitstun(launchHitstun, Facing * appliedPushback, -launchSpeed, true);
			else
				defender.ApplyLaunchHitstun(launchHitstun, Facing * appliedPushback, launchSpeed, counterHit);
			_launcherJumpCancelFramesLeft = ResolveIntOverride(hitboxData?.JumpCancelWindowFrames, _currentMoveRule.JumpCancelWindowFrames);
		}
		else if (specialReaction != SpecialReactionKind.None && !IsGuardSpecialReaction(specialReaction))
		{
			defender.ApplySpecialReactionHitstun(appliedHitstun, Facing * appliedPushback, specialReaction);
		}
		else if (hitReaction == HitReactionKind.Stumble)
		{
			defender.ApplyStumbleHitstun(appliedHitstun, Facing * appliedPushback);
		}
		else if (hitReaction == HitReactionKind.HitFall)
		{
			defender.ApplyHitFallHitstun(appliedHitstun, Facing * appliedPushback);
		}
		else if (CurrentHitboxRequestsKnockdown(hitboxData) || (hitboxData?.AirborneTargetWallSplat == true && !defender.WasGrounded))
		{
			int overrideKnockdownFrames = ResolveIntOverride(hitboxData?.KnockdownFrames, _currentAttackKnockdownFrames);
			int knockdownFrames = overrideKnockdownFrames > 0 ? overrideKnockdownFrames : appliedHitstun;
			KnockdownType knockdownType = hitboxData?.AirborneTargetWallSplat == true && !defender.WasGrounded
				? KnockdownType.WallBounce
				: ResolveCurrentAttackKnockdownType(defender, hitboxData);
			if (knockdownType == KnockdownType.Sweep)
				appliedPushback = 0f;
			float downwardSpeed = !defender.WasGrounded ? HeavyAirAttackSpikeSpeed : 0f;
			float groundBounceSpeed = ResolveFloatOverride(hitboxData?.GroundBounceSpeed, defender.GroundBounceSpeed);
			defender.ApplyKnockdown(knockdownFrames, Facing * appliedPushback, downwardSpeed, knockdownType, counterHit,
				groundBounceSpeed, hitboxData?.GroundBounceIntoJuggle == true, groundBounceStrength, wallBounceStrength);
		}
		else if (finalSuperHit && CurrentAttackName == SuperRushName)
		{
			// Launch into a knockback tumble while retaining a pending hard knockdown;
			// the grounded knockdown state begins only when the defender lands.
			defender.ApplyThrowLaunch(_currentSuperMove.FinalKnockdownFrames,
				Facing * _currentSuperMove.FinalPushback, 820f);
		}
		else if (finalSuperHit && _currentSuperMove.FinalHitKnocksDown)
		{
			int knockdownFrames = _currentSuperMove.FinalKnockdownFrames > 0 ? _currentSuperMove.FinalKnockdownFrames : appliedHitstun;
			float downwardSpeed = !defender.WasGrounded ? HeavyAirAttackSpikeSpeed : 0f;
			defender.ApplyKnockdown(knockdownFrames, Facing * appliedPushback, downwardSpeed, _currentSuperMove.FinalKnockdownType, counterHit);
		}
		else
		{
			if (!defender.WasGrounded)
			{
				if (_currentAttackStartedAirborne && CurrentAttackName.StartsWith("HEAVY"))
					defender.ApplyJuggleHitstun(appliedHitstun, Facing * appliedPushback, HeavyAirAttackSpikeSpeed, true);
				else if (defender.HitState == FighterHitState.Juggle)
				{
					float verticalVelocity = airborneLightNormal || _currentMoveRule.PreserveAirborneTargetVelocity
						? defender.Velocity.Y
						: -AirHitPopUpSpeed;
					if (!airborneLightNormal && !_currentAttackStartedAirborne && CurrentAttackName == "LIGHT PUNCH")
					{
						float popScale = Mathf.Max(0f,
							1f - defender.GroundNormalJuggleHitCount * GroundJabJugglePopLossPerHit);
						verticalVelocity = popScale > 0f
							? -AirHitPopUpSpeed * popScale
							: Mathf.Max(0f, defender.Velocity.Y);
					}
					defender.ApplyJuggleHitstun(appliedHitstun, Facing * appliedPushback, verticalVelocity, true);
				}
				else
					defender.ApplyAirPopHitstun(appliedHitstun, Facing * appliedPushback,
						_currentMoveRule.PreserveAirborneTargetVelocity ? 0f :
							airborneLightNormal ? AirLightInitialPopUpSpeed : AirHitPopUpSpeed,
						counterHit || hitReaction == HitReactionKind.Tumble);
			}
			else
				defender.ApplyHitstun(appliedHitstun, Facing * appliedPushback, counterHit);
			if (_currentAttackStartedAirborne && CurrentAttackName.StartsWith("LIGHT"))
				_airLightJumpCancelFramesLeft = AirLightHitJumpCancelWindowFrames;
		}
		hitstopFrames = finalSuperHit ? _currentSuperMove.FinalHitstopFrames : ResolveIntOverride(hitboxData?.HitstopFrames, _currentAttackHitstopFrames);
		bool addsGlobalHitstopBonus = superHit
			? _currentSuperMove.AddsGlobalHitstopBonus
			: _currentSpecialMove?.AddsGlobalHitstopBonus != false;
		if (addsGlobalHitstopBonus)
		{
			bool useGroundedNormalHitstop = !_currentAttackStartedAirborne || defender.WasGrounded;
			hitstopFrames += GlobalHitstopBonusFrames +
				(useGroundedNormalHitstop ? GroundedAttackHitstopBonusFrames : AirAttackHitstopBonusFrames);
			if (_currentAttackStartedAirborne && defender.WasGrounded)
				hitstopFrames += JumpInHitstopBonusFrames;
		}
		hitstopFrames = ScaleSpecialMoveHitstop(hitstopFrames);
		if (jumpingHeavyHitGroundedDefender)
		{
			hitstopFrames = ScaleAirAttackHitstop(JumpingHeavyAttackerHitlagFrames);
			LastContactDefenderHitstopFrames = ScaleAirAttackHitstop(JumpingHeavyDefenderHitstopFrames);
		}
		else if (_currentAttackStartedAirborne)
		{
			hitstopFrames = ScaleAirAttackHitstop(hitstopFrames);
			LastContactDefenderHitstopFrames = hitstopFrames;
		}
		else
			LastContactDefenderHitstopFrames = hitstopFrames;
		shakeStrength = finalSuperHit ? _currentSuperMove.FinalShakeStrength : ResolveFloatOverride(hitboxData?.ShakeStrength, _currentAttackShakeStrength);
		if (_currentAttackStartedAirborne && defender.WasGrounded && !superHit)
			shakeStrength *= AirToGroundShakeMultiplier;
		hitPushback = appliedPushback;
		if (airborneLightNormal)
			Velocity = new Vector2(Velocity.X * Mathf.Clamp(AirLightHitMomentumScale, 0f, 1f), Velocity.Y);
		if (superHit)
		{
			_currentAttackHitsRemaining--;
			_currentAttackHitCooldownFramesLeft = CurrentAttackName == SuperRushName && _currentAttackHitsRemaining == 1
				? 16
				: _currentSuperMove.HitIntervalFrames;
			MaintainSuperHitLock();
		}
		LogHit(new FighterHitLogEntry
		{
			PhysicsFrame = Engine.GetPhysicsFrames(),
			AttackerName = Name,
			DefenderName = defender.Name,
			MoveName = CurrentAttackName,
			HitboxTag = hitbox.Source?.Tag ?? "",
			HurtboxTag = hurtbox.Source?.Tag ?? "",
			HitPoint = hitPoint,
			HitboxWorldRect = hitbox.Rect,
			HurtboxWorldRect = hurtbox.Rect,
			HitboxAttributes = hitbox.Source?.Attributes ?? FighterBoxAttribute.Strike,
			HurtboxAttributes = hurtbox.Source?.Attributes ?? FighterBoxAttribute.Strike,
			AttackLevel = attackLevel,
			HitboxPriority = hitbox.Source?.Priority ?? 0,
			AttackFrame = CurrentAttackFrame,
			HitstunFrames = appliedHitstun,
			HitstopFrames = hitstopFrames,
			DefenderHitstopFrames = LastContactDefenderHitstopFrames,
			Pushback = appliedPushback,
			CounterHit = counterHit
		});
		if (defenderWasWallSliding)
			defender.QueueStateImpact(FighterHitState.WallSplat, defender._wallSplatDirection, true);
		return true;
	}

	public bool TryApplyProjectileHit(FighterController defender, Rect2 projectileHitbox, int hitstunFrames, float pushback, int hitstopFrames,
		float shakeStrength, bool knocksDown, KnockdownType knockdownType, int knockdownFrames,
		out int appliedHitstopFrames, out float appliedShakeStrength, out float hitPushback, out Vector2 hitPoint, out bool heavySpark)
	{
		appliedHitstopFrames = 0;
		appliedShakeStrength = 0f;
		hitPushback = 0f;
		hitPoint = Vector2.Zero;
		heavySpark = false;
		LastContactWasBlocked = false;
		LastContactWasInstantBlocked = false;
		LastContactWasParried = false;
		LastContactDefenderHitstopFrames = 0;
		if (defender == null || defender == this || IsSameTeam(defender) || defender.IsWakingUp || defender.IsMovementInvulnerable) return false;
		if (defender.IsGroundedKnockdown) return false;
		if (!TryFindBoxContact(new[] { new ActiveFighterBox(projectileHitbox) }, defender.GetActiveWorldBoxInstances(FighterBoxKind.Hurtbox),
			out hitPoint, out ActiveFighterBox hitbox, out ActiveFighterBox hurtbox)) return false;
		bool defenderWasWallSliding = defender._pendingWallSplatKnockdown;
		if (defender.TryParryIncomingHit(this, hitPoint))
		{
			LastContactWasParried = true;
			appliedHitstopFrames = 12;
			LastContactDefenderHitstopFrames = appliedHitstopFrames;
			appliedShakeStrength = 4.5f;
			return true;
		}

		float appliedPushback = Facing * pushback;
		const FighterAttackLevel projectileAttackLevel = FighterAttackLevel.Mid;
		if (defender.CanBlockStrike(projectileAttackLevel, this))
		{
			int blockstunFrames = Mathf.Max(1, hitstunFrames - 4);
			bool instantBlock = defender.IsInstantBlockAgainst(this);
			if (instantBlock) blockstunFrames = Mathf.Max(1, Mathf.CeilToInt(blockstunFrames * 0.5f));
			float blockPushback = appliedPushback * BlockPushbackMultiplier;
			defender.ApplyBlockstun(blockstunFrames, blockPushback, GuardReactionStrength.Medium,
				crouchBlock: defender.ResolveCrouchingGuard(projectileAttackLevel));
			LastContactWasBlocked = true;
			LastContactWasInstantBlocked = instantBlock;
			appliedHitstopFrames = Mathf.Max(1, hitstopFrames + BlockHitstopBonusFrames);
			LastContactDefenderHitstopFrames = appliedHitstopFrames;
			appliedShakeStrength = BlockShakeStrength;
			hitPushback = Mathf.Abs(blockPushback);
			return true;
		}
		_attackHasHit = true;
		_attackHasUnblockedHit = true;
		if (knocksDown)
		{
			int appliedKnockdownFrames = knockdownFrames > 0 ? knockdownFrames : hitstunFrames;
			float downwardSpeed = !defender.WasGrounded ? HeavyAirAttackSpikeSpeed : 0f;
			defender.ApplyKnockdown(appliedKnockdownFrames, appliedPushback, downwardSpeed, knockdownType);
		}
		else if (defender.WasGrounded)
			defender.ApplyHitstun(hitstunFrames, appliedPushback);
		else
			defender.ApplyAirPopHitstun(hitstunFrames, appliedPushback, AirHitPopUpSpeed);
		appliedHitstopFrames = hitstopFrames;
		LastContactDefenderHitstopFrames = appliedHitstopFrames;
		appliedShakeStrength = shakeStrength;
		hitPushback = pushback;
		heavySpark = true;
		LogHit(new FighterHitLogEntry
		{
			PhysicsFrame = Engine.GetPhysicsFrames(),
			AttackerName = Name,
			DefenderName = defender.Name,
			MoveName = "PROJECTILE",
			HitboxTag = hitbox.Source?.Tag ?? "",
			HurtboxTag = hurtbox.Source?.Tag ?? "",
			HitPoint = hitPoint,
			HitboxWorldRect = hitbox.Rect,
			HurtboxWorldRect = hurtbox.Rect,
			HitboxAttributes = hitbox.Source?.Attributes ?? FighterBoxAttribute.Projectile,
			HurtboxAttributes = hurtbox.Source?.Attributes ?? FighterBoxAttribute.Strike,
			AttackLevel = hitbox.Source?.AttackLevel ?? FighterAttackLevel.Mid,
			HitboxPriority = hitbox.Source?.Priority ?? 0,
			AttackFrame = CurrentAttackFrame,
			HitstunFrames = hitstunFrames,
			HitstopFrames = hitstopFrames,
			DefenderHitstopFrames = appliedHitstopFrames,
			Pushback = pushback,
			Projectile = true
		});
		if (defenderWasWallSliding)
			defender.QueueStateImpact(FighterHitState.WallSplat, defender._wallSplatDirection, true);
		return true;
	}
	public void RequestHitstop(int frames, bool continueVerticalPhysics = false)
	{
		if (frames > HitstopFramesLeft)
		{
			HitstopFramesLeft = frames;
			_continueVerticalPhysicsDuringHitstop = continueVerticalPhysics;
			_verticalHitstopFreezeFramesLeft = continueVerticalPhysics
				? Mathf.Max(0, JumpInInitialFullFreezeFrames)
				: 0;
		}
		else if (frames == HitstopFramesLeft && continueVerticalPhysics)
		{
			_continueVerticalPhysicsDuringHitstop = true;
			_verticalHitstopFreezeFramesLeft = Mathf.Max(
				_verticalHitstopFreezeFramesLeft,
				Mathf.Max(0, JumpInInitialFullFreezeFrames));
		}
	}

	public void AddHitstop(int frames)
	{
		if (frames <= 0) return;
		HitstopFramesLeft += frames;
		_continueVerticalPhysicsDuringHitstop = false;
		_verticalHitstopFreezeFramesLeft = 0;
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
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, Velocity.Y));
	}

	/// <summary>
	/// Universal back-to-block guard check. Standing guard stops mids/highs/overheads,
	/// down-back stops mids/lows, and airborne fighters guard by holding back.
	/// </summary>
	private bool CanBlockStrike(FighterAttackLevel attackLevel, FighterController attacker)
	{
		if (IsKnockedDown || IsWakingUp || IsMovementInvulnerable) return false;
		if (!WasGrounded && TrainingAutoBlock) return TrainingAirBlock;
		if (TrainingAutoBlock) return true;
		if (IsAttacking || (HitState != FighterHitState.None && HitState != FighterHitState.Blockstun)) return false;
		// Guard away from the attacker's actual side. This intentionally differs
		// from Facing during an airborne cross-up, when presentation stays locked
		// until landing but the required block direction has already switched.
		float attackerSide = attacker == null
			? Facing
			: Mathf.Sign(attacker.WorldPositionBox.GetCenter().X - WorldPositionBox.GetCenter().X);
		if (Mathf.IsZeroApprox(attackerSide)) attackerSide = Facing;
		if (CurrentInput.Horizontal * attackerSide >= -0.5f) return false;
		if (!WasGrounded) return true;

		bool crouching = CurrentInput.Vertical > 0.5f;
		return attackLevel switch
		{
			FighterAttackLevel.Low => crouching,
			FighterAttackLevel.High or FighterAttackLevel.Overhead => !crouching,
			_ => true
		};
	}

	private bool ResolveCrouchingGuard(FighterAttackLevel attackLevel)
	{
		if (!WasGrounded) return false;
		if (TrainingAutoBlock)
		{
			if (attackLevel == FighterAttackLevel.Low) return true;
			if (attackLevel is FighterAttackLevel.High or FighterAttackLevel.Overhead) return false;
		}
		return CurrentInput.Vertical > 0.5f;
	}

	private void TrackHorizontalHoldDuration(float horizontal)
	{
		int direction = horizontal > 0.5f ? 1 : horizontal < -0.5f ? -1 : 0;
		if (direction == 0)
		{
			_heldHorizontalDirection = 0;
			_horizontalDirectionHeldFrames = 0;
			return;
		}
		if (direction != _heldHorizontalDirection)
		{
			_heldHorizontalDirection = direction;
			_horizontalDirectionHeldFrames = 1;
			return;
		}
		_horizontalDirectionHeldFrames++;
	}

	private bool IsInstantBlockAgainst(FighterController attacker)
	{
		if (!InstantBlockEnabled || attacker == null) return false;
		// Auto-block has no physical direction edge to time. When instant block is
		// enabled for the training dummy, treat its automatic guard as frame-perfect
		// so the mechanic, yellow spark, and reduced blockstun can be tested directly.
		if (TrainingAutoBlock) return true;
		float attackerSide = Mathf.Sign(attacker.WorldPositionBox.GetCenter().X - WorldPositionBox.GetCenter().X);
		if (Mathf.IsZeroApprox(attackerSide)) attackerSide = Facing;
		int requiredBackDirection = attackerSide > 0f ? -1 : 1;
		return _heldHorizontalDirection == requiredBackDirection &&
			_horizontalDirectionHeldFrames is > 0 &&
			_horizontalDirectionHeldFrames <= Mathf.Max(1, InstantBlockWindowFrames);
	}

	internal void ApplyBlockstun(int frames, float horizontalPushback,
		GuardReactionStrength strength = GuardReactionStrength.Medium,
		SpecialReactionKind specialReaction = SpecialReactionKind.None,
		bool? crouchBlock = null)
	{
		BlockReactionSerial++;
		CurrentGuardReactionStrength = strength == GuardReactionStrength.None ? GuardReactionStrength.Medium : strength;
		HitstunFramesLeft = Mathf.Max(1, frames);
		HitState = FighterHitState.Blockstun;
		IsCrouchBlocking = WasGrounded && (crouchBlock ?? CurrentInput.Vertical > 0.5f);
		CurrentKnockdownType = KnockdownType.None;
		ClearBlowAwayState();
		CurrentWallBounceStrength = WallBounceReactionStrength.None;
		CurrentGroundBounceStrength = GroundBounceReactionStrength.None;
		CurrentSpecialReaction = specialReaction;
		float resolvedPushback = specialReaction is SpecialReactionKind.GuardPullbackWeak or
			SpecialReactionKind.GuardPullbackStrong or SpecialReactionKind.GuardPullbackAir
			? -horizontalPushback
			: horizontalPushback;
		Velocity = new Vector2(resolvedPushback, Velocity.Y);
		StopActiveAbility();
		ClearAttackState();
	}

	private GuardReactionStrength ResolveCurrentGuardReactionStrength(FighterBoxFrame hitboxData)
	{
		if (hitboxData?.GuardReactionStrength is { } boxStrength && boxStrength != GuardReactionStrength.None)
			return boxStrength;
		if (_currentAttackGuardReactionStrength != GuardReactionStrength.None)
			return _currentAttackGuardReactionStrength;
		if (_currentSuperMove != null || _currentSpecialMove != null || CurrentAttackName.StartsWith("SPECIAL"))
			return GuardReactionStrength.SpecialStrong;
		if (CurrentAttackName.Contains("HEAVY")) return GuardReactionStrength.Strong;
		if (CurrentAttackName.Contains("MEDIUM")) return GuardReactionStrength.Medium;
		return GuardReactionStrength.Weak;
	}

	private FighterAttackLevel ResolveCurrentAttackLevel(FighterBoxFrame hitboxData)
	{
		// Universal 2D-fighter rule: every grounded crouching kick is a low,
		// including legacy/fallback hitboxes that do not carry authored metadata.
		if (_currentAttackStartedCrouching && !_currentAttackStartedAirborne &&
			CurrentAttackName.Contains("KICK", StringComparison.OrdinalIgnoreCase))
			return FighterAttackLevel.Low;
		return hitboxData?.AttackLevel ?? FighterAttackLevel.Mid;
	}

	private SpecialReactionKind ResolveCurrentSpecialReaction(FighterBoxFrame hitboxData)
	{
		if (hitboxData?.SpecialReaction is { } boxReaction && boxReaction != SpecialReactionKind.None)
			return boxReaction;
		return _currentAttackSpecialReaction;
	}

	internal static bool ShouldUseAutomaticSpecialStagger(bool counterHit, bool specialOrSuper,
		bool hasDedicatedReaction) => !hasDedicatedReaction && (counterHit || specialOrSuper);

	private static bool IsGuardSpecialReaction(SpecialReactionKind reaction) => reaction is
		SpecialReactionKind.GuardPullbackWeak or SpecialReactionKind.GuardPullbackStrong or
		SpecialReactionKind.GuardPullbackAir;

	private static SpecialReactionKind ResolveGuardSpecialReaction(SpecialReactionKind reaction, bool grounded) => reaction switch
	{
		SpecialReactionKind.PullbackWeak => grounded
			? SpecialReactionKind.GuardPullbackWeak
			: SpecialReactionKind.GuardPullbackAir,
		SpecialReactionKind.PullbackStrong => grounded
			? SpecialReactionKind.GuardPullbackStrong
			: SpecialReactionKind.GuardPullbackAir,
		SpecialReactionKind.PullbackAir => SpecialReactionKind.GuardPullbackAir,
		SpecialReactionKind.GuardPullbackWeak or SpecialReactionKind.GuardPullbackStrong or
			SpecialReactionKind.GuardPullbackAir => reaction,
		_ => SpecialReactionKind.None
	};

	internal void ApplySpecialReactionHitstun(int frames, float horizontalPushback, SpecialReactionKind reaction)
	{
		int resolvedFrames = Mathf.Max(1, frames);
		switch (reaction)
		{
			case SpecialReactionKind.SlideDownHorizontal:
				ApplyHitReaction(resolvedFrames, FighterHitState.Knockdown);
				CurrentKnockdownType = KnockdownType.SoftKnockdown;
				Velocity = new Vector2(horizontalPushback * 1.35f, Mathf.Max(0f, Velocity.Y));
				break;
			case SpecialReactionKind.SlideDownDiagonal:
				ApplyHitReaction(resolvedFrames, FighterHitState.Knockdown);
				CurrentKnockdownType = KnockdownType.SoftKnockdown;
				Velocity = new Vector2(horizontalPushback, Mathf.Max(Velocity.Y, HitFallSpeed * 0.75f));
				break;
			case SpecialReactionKind.SlideDowned:
				ApplyHitReaction(resolvedFrames, FighterHitState.GroundedKnockdown);
				CurrentKnockdownType = KnockdownType.SoftKnockdown;
				Velocity = new Vector2(horizontalPushback, 0f);
				break;
			case SpecialReactionKind.DiagonalBounce:
				ApplyKnockdown(resolvedFrames, horizontalPushback, 0f, KnockdownType.GroundBounce,
					groundBounceIntoJuggle: true, groundBounceStrength: GroundBounceReactionStrength.Strong);
				break;
			case SpecialReactionKind.PullbackWeak:
			case SpecialReactionKind.PullbackStrong:
				ApplyHitReaction(resolvedFrames, FighterHitState.Hitstun);
				float pullScale = reaction == SpecialReactionKind.PullbackStrong ? 1.25f : 0.75f;
				Velocity = new Vector2(-horizontalPushback * pullScale, Velocity.Y);
				break;
			case SpecialReactionKind.PullbackAir:
				ApplyJuggleHitstun(resolvedFrames, -horizontalPushback, Mathf.Min(Velocity.Y, -AirHitPopUpSpeed * 0.35f), false);
				break;
			default:
				ApplyHitReaction(resolvedFrames, FighterHitState.Hitstun);
				Velocity = new Vector2(horizontalPushback * 0.35f, Velocity.Y);
				break;
		}
		CurrentSpecialReaction = reaction;
	}

	private bool TryParryIncomingHit(FighterController attacker, Vector2 hitPoint)
	{
		if (!IsParryWindowActive) return false;
		_attackActiveFramesLeft = 0;
		_attackRecoveryFramesLeft = Mathf.Max(_attackRecoveryFramesLeft, _currentAttackRecoveryFrames + 1);
		int presentationFrames = _currentSpecialMove?.Parry == true
			? _currentSpecialMove.ParrySuccessPresentationFrames
			: _currentSuperMove?.ParrySuccessPresentationFrames ?? 18;
		_parrySuccessPresentationFramesLeft = Mathf.Max(_parrySuccessPresentationFramesLeft,
			Mathf.Max(1, presentationFrames));
		ParrySuccessSerial++;
		RequestHitstop(12);
		attacker?.RequestHitstop(12);
		OnParrySuccessVisual(hitPoint);
		return true;
	}

	protected virtual void OnParrySuccessVisual(Vector2 hitPoint) { }

	/// <summary>Presentation hook for confirmed-hit burn effects; gameplay state remains in the core controller.</summary>
	protected virtual void OnMoveContactBurnVisual(bool blackenDefender, int silhouetteFrames,
		SpriteFrames fireFrames, string fireAnimationName) { }

	internal void PlayMoveContactBurnPresentation(bool blackenDefender, int silhouetteFrames,
		SpriteFrames fireFrames, string fireAnimationName) =>
		OnMoveContactBurnVisual(blackenDefender, silhouetteFrames, fireFrames, fireAnimationName);

	private void ApplyLaunchHitstun(int frames, float horizontalPushback, float verticalLaunchSpeed, bool counterHit = false)
	{
		ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Tumble);
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, -verticalLaunchSpeed));
	}

	private void ApplyJuggleHitstun(int frames, float horizontalPushback, float verticalVelocity, bool knockdownOnLanding)
	{
		JuggleHitCount = HitState == FighterHitState.Juggle ? JuggleHitCount + 1 : 1;
		ApplyHitReaction(frames, FighterHitState.Juggle);
		if (knockdownOnLanding)
			CurrentKnockdownType = KnockdownType.AirKnockdown;
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, verticalVelocity));
	}

	public void ApplyWallSplat(int wallDirection)
	{
		WallBounceReactionStrength reactionStrength = CurrentWallBounceStrength == WallBounceReactionStrength.None
			? WallBounceReactionStrength.Strong
			: CurrentWallBounceStrength;
		ApplyHitReaction(Mathf.Max(1, WallSplatHitstunFrames), FighterHitState.WallSplat);
		CurrentWallBounceStrength = reactionStrength;
		_pendingWallSplatKnockdown = true;
		_wallSplatDirection = wallDirection >= 0 ? 1 : -1;
		CurrentKnockdownType = KnockdownType.SoftKnockdown;
		Velocity = new Vector2(0f, Mathf.Max(0f, WallSplatSlideSpeed));
		QueueStateImpact(FighterHitState.WallSplat, _wallSplatDirection);
	}

	internal void ApplyWallBounceHitstun(int frames, int horizontalDirection,
		WallBounceReactionStrength strength = WallBounceReactionStrength.Strong)
	{
		ApplyKnockdown(Mathf.Max(1, frames), horizontalDirection >= 0 ? 1f : -1f, 0f,
			KnockdownType.WallBounce, wallBounceStrength: strength);
	}

	internal void ApplyGroundBounceHitstun(int frames, float horizontalPushback, float bounceSpeed = -1f,
		bool intoJuggle = true, GroundBounceReactionStrength strength = GroundBounceReactionStrength.Medium)
	{
		ApplyKnockdown(Mathf.Max(1, frames), horizontalPushback, 0f, KnockdownType.GroundBounce,
			groundBounceSpeed: bounceSpeed, groundBounceIntoJuggle: intoJuggle, groundBounceStrength: strength);
	}

	private void ApplyAirPopHitstun(int frames, float horizontalPushback, float popUpSpeed, bool tumble = false)
	{
		ApplyHitReaction(frames, tumble ? FighterHitState.Tumble : FighterHitState.Hitstun);
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, Mathf.Min(Velocity.Y, -popUpSpeed)));
	}

	private void ApplyAirSpikeHitstun(int frames, float horizontalPushback, float spikeSpeed, bool counterHit = false)
	{
		ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Tumble);
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, Mathf.Max(Velocity.Y, spikeSpeed)));
	}

	internal void ApplyStumbleHitstun(int frames, float horizontalPushback)
	{
		ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.Stumble);
		CurrentKnockdownType = KnockdownType.SoftKnockdown;
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, -Mathf.Abs(StumblePopUpSpeed)));
	}

	internal void ApplyHitFallHitstun(int frames, float horizontalPushback)
	{
		ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.HitFall);
		CurrentKnockdownType = KnockdownType.AirKnockdown;
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback,
			Mathf.Max(Velocity.Y, Mathf.Abs(HitFallSpeed))));
	}

	internal void ApplyBlowAwayHitstun(int frames, int horizontalDirection, BlowAwayDirection direction,
		BlowAwayStrength strength, bool noBounce = false, float authoredSpeed = -1f)
	{
		BlowAwayStrength resolvedStrength = strength == BlowAwayStrength.None ? BlowAwayStrength.Medium : strength;
		ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.Tumble);
		CurrentBlowAwayDirection = direction;
		CurrentBlowAwayStrength = resolvedStrength;
		CurrentBlowAwayNoBounce = noBounce;
		float speed = authoredSpeed > 0f ? authoredSpeed : GetBlowAwaySpeed(resolvedStrength);
		Velocity = ResolveWallSplatFollowupVelocity(ResolveBlowAwayVelocity(direction, resolvedStrength,
			horizontalDirection, speed));
	}

	internal static Vector2 ResolveBlowAwayVelocity(BlowAwayDirection direction, BlowAwayStrength strength,
		int horizontalDirection, float speed)
	{
		float resolvedSpeed = Mathf.Max(1f, speed);
		float facing = horizontalDirection >= 0 ? 1f : -1f;
		const float diagonal = 0.70710678f;
		return direction switch
		{
			BlowAwayDirection.Horizontal => new Vector2(facing * resolvedSpeed, -resolvedSpeed * 0.08f),
			BlowAwayDirection.Vertical => new Vector2(facing * resolvedSpeed * 0.12f, -resolvedSpeed),
			BlowAwayDirection.Diagonal => new Vector2(facing * resolvedSpeed * diagonal, -resolvedSpeed * diagonal),
			BlowAwayDirection.Downward => new Vector2(0f, resolvedSpeed),
			BlowAwayDirection.DiagonalDown => new Vector2(facing * resolvedSpeed * diagonal, resolvedSpeed * diagonal),
			_ => Vector2.Zero
		};
	}

	private float GetBlowAwaySpeed(BlowAwayStrength strength) => strength switch
	{
		BlowAwayStrength.Weak => WeakBlowAwaySpeed,
		BlowAwayStrength.Strong => StrongBlowAwaySpeed,
		_ => MediumBlowAwaySpeed
	};

	public static string ResolveBlowAwayAnimationName(BlowAwayDirection direction, BlowAwayStrength strength,
		bool noBounce = false)
	{
		string suffix = strength switch
		{
			BlowAwayStrength.Weak => "weak",
			BlowAwayStrength.Strong => "strong",
			_ => "medium"
		};
		return direction switch
		{
			BlowAwayDirection.Horizontal => "blow_away_horizontal",
			BlowAwayDirection.Vertical => $"blow_away_vertical_{suffix}",
			BlowAwayDirection.Diagonal => $"blow_away_diagonal_{suffix}",
			BlowAwayDirection.Downward when noBounce => "blow_away_downward_no_bounce",
			BlowAwayDirection.Downward => $"blow_away_downward_{suffix}",
			BlowAwayDirection.DiagonalDown when noBounce => "blow_away_diagonal_down_no_bounce",
			BlowAwayDirection.DiagonalDown => "blow_away_diagonal_down",
			_ => ""
		};
	}

	private static string ResolveBlowAwayStateName(BlowAwayDirection direction, BlowAwayStrength strength,
		bool noBounce = false)
	{
		string suffix = strength switch
		{
			BlowAwayStrength.Weak => "弱",
			BlowAwayStrength.Strong => "強",
			_ => "中"
		};
		return direction switch
		{
			BlowAwayDirection.Horizontal => "STATE [ヒット]吹っ飛び_真横",
			BlowAwayDirection.Vertical => $"STATE [ヒット]吹っ飛び_真上_{suffix}",
			BlowAwayDirection.Diagonal => $"STATE [ヒット]吹っ飛び_斜め_{suffix}",
			BlowAwayDirection.Downward when noBounce => "STATE [ヒット]吹っ飛び_真下_無バウンド",
			BlowAwayDirection.Downward => $"STATE [ヒット]吹っ飛び_真下_{suffix}",
			BlowAwayDirection.DiagonalDown when noBounce => "STATE [ヒット]吹っ飛び_斜め下_無バウンド",
			BlowAwayDirection.DiagonalDown => "STATE [ヒット]吹っ飛び_斜め下",
			_ => ""
		};
	}

	private void ApplyKnockdown(int frames, float horizontalPushback, float downwardSpeed, KnockdownType knockdownType,
		bool counterHit = false, float groundBounceSpeed = -1f, bool groundBounceIntoJuggle = false,
		GroundBounceReactionStrength groundBounceStrength = GroundBounceReactionStrength.None,
		WallBounceReactionStrength wallBounceStrength = WallBounceReactionStrength.None)
	{
		CurrentKnockdownType = knockdownType == KnockdownType.None ? KnockdownType.AirKnockdown : knockdownType;
		FighterHitState state = GetInitialKnockdownState(CurrentKnockdownType);
		ApplyHitReaction(frames, state);
		if (CurrentKnockdownType == KnockdownType.Sweep && IsOnFloor())
		{
			// Classic low sweep reaction: briefly lift the victim into the
			// knockdown animation, then ground them when the arc lands.
			Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, -Mathf.Abs(SweepPopUpSpeed)));
			return;
		}
		if (CurrentKnockdownType == KnockdownType.WallBounce)
		{
			CurrentWallBounceStrength = wallBounceStrength == WallBounceReactionStrength.None
				? WallBounceReactionStrength.Strong
				: wallBounceStrength;
			float direction = Mathf.Abs(horizontalPushback) > 1f ? Mathf.Sign(horizontalPushback) : Facing;
			float horizontalSpeed = CurrentWallBounceStrength == WallBounceReactionStrength.Weak
				? WeakWallBounceHorizontalSpeed
				: WallBounceHorizontalSpeed;
			Velocity = ResolveWallSplatFollowupVelocity(new Vector2(direction * horizontalSpeed,
				Mathf.Min(Velocity.Y, -GroundBounceSpeed * 0.35f)));
			return;
		}

		if (CurrentKnockdownType == KnockdownType.GroundBounce)
		{
			CurrentGroundBounceStrength = groundBounceStrength == GroundBounceReactionStrength.None
				? GroundBounceReactionStrength.Medium
				: groundBounceStrength;
			float resolvedBounceSpeed = groundBounceSpeed > 0f ? groundBounceSpeed : GroundBounceSpeed;
			_pendingGroundBounceSpeed = resolvedBounceSpeed;
			_pendingGroundBounceIntoJuggle = groundBounceIntoJuggle;
			if (IsOnFloor() && groundBounceIntoJuggle)
			{
				HitState = FighterHitState.Juggle;
				CurrentKnockdownType = KnockdownType.None;
				JuggleHitCount = Mathf.Max(1, JuggleHitCount + 1);
				Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, -resolvedBounceSpeed));
				_pendingGroundBounceSpeed = -1f;
				_pendingGroundBounceIntoJuggle = false;
				QueueStateImpact(FighterHitState.GroundBounce);
				return;
			}
			float verticalBounce = IsOnFloor() ? -resolvedBounceSpeed : Mathf.Max(Velocity.Y, downwardSpeed);
			Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, verticalBounce));
			return;
		}

		float vertical = downwardSpeed > 0f ? Mathf.Max(Velocity.Y, downwardSpeed) : Velocity.Y;
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, vertical));
	}

	private void CaptureThrowVictim(FighterController defender)
	{
		if (defender == null) return;
		_capturedThrowVictim = defender;
		defender._throwCaptor = this;
		defender.ClearAttackState();
		defender.Velocity = Vector2.Zero;
		if (IsSpdGrabAttackName(CurrentAttackName))
		{
			bool superSpd = CurrentAttackName == SanzoSuperSpdName;
			_spdGrabConnected = true;
			_spdHasLeftGround = false;
			CurrentAttackAnimationName = "spd_air_grab";
			Velocity = new Vector2(Velocity.X * 0.2f,
				-Mathf.Abs(superSpd ? SuperSpdRiseSpeed : SpdRiseSpeed));
			// A connected SPD owns the timeline until landing. Whiffs retain the
			// authored short recovery from the move resource.
			_attackStartupFramesLeft = 0;
			_attackActiveFramesLeft = 0;
			_attackRecoveryFramesLeft = superSpd ? 360 : 180;
			_currentAttackRecoveryFrames = _attackRecoveryFramesLeft;
		}
		else if (IsRegularThrowAttackName(CurrentAttackName) && _currentMoveData?.ConnectedThrowRecoveryFrames > 0)
		{
			// A successful regular throw owns its authored continuation. A whiff keeps
			// the shorter startup resource recovery instead.
			_attackStartupFramesLeft = 0;
			_attackActiveFramesLeft = 0;
			_attackRecoveryFramesLeft = _currentMoveData.ConnectedThrowRecoveryFrames;
			_currentAttackRecoveryFrames = _attackRecoveryFramesLeft;
		}
		UpdateCapturedThrowVictim();
	}

	private void UpdateCapturedThrowVictim()
	{
		if (!GodotObject.IsInstanceValid(_capturedThrowVictim))
		{
			_capturedThrowVictim = null;
			return;
		}

		bool foundAnchor = false;
		foreach (ActiveFighterBox anchor in GetActiveWorldBoxInstances(FighterBoxKind.ThrowVictimAnchor))
		{
			_capturedThrowVictim.GlobalPosition = anchor.Rect.GetCenter();
			_capturedThrowVictim.Velocity = Vector2.Zero;
			foundAnchor = true;
			break;
		}

		// Moves without authored anchors retain the legacy immediate-release behavior.
		if (!foundAnchor && !HasFutureThrowVictimAnchor()) ReleaseCapturedThrowVictim();
	}

	private bool HasFutureThrowVictimAnchor()
	{
		if (_currentMoveRule.BoxTimeline == null) return false;
		foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			if (box?.Kind == FighterBoxKind.ThrowVictimAnchor && box.EndFrame >= CurrentAttackFrame)
				return true;
		return false;
	}

	private void ReleaseCapturedThrowVictim()
	{
		FighterController victim = _capturedThrowVictim;
		_capturedThrowVictim = null;
		_spdGrabConnected = false;
		_spdHasLeftGround = false;
		if (!GodotObject.IsInstanceValid(victim)) return;
		victim._throwCaptor = null;
		float releaseFacing = CurrentAttackName == BackThrowAttackName ? -Facing : Facing;
		victim.ApplyThrowLaunch(HeavyAttackHitstunFrames + 24, releaseFacing * HeavyAttackPushback, ThrowLaunchSpeed);
	}

	private void ResolveSpdSlamLanding()
	{
		bool superSpd = CurrentAttackName == SanzoSuperSpdName;
		FighterController victim = _capturedThrowVictim;
		_capturedThrowVictim = null;
		_spdGrabConnected = false;
		_spdHasLeftGround = false;
		Velocity = new Vector2(Velocity.X * 0.2f, Velocity.Y);
		if (GodotObject.IsInstanceValid(victim))
		{
			victim._throwCaptor = null;
			victim.GlobalPosition = GlobalPosition + new Vector2(Facing * 18f, 0f);
			victim.ApplySpdSlamKnockdown(superSpd ? SuperSpdSlamKnockdownFrames : SpdSlamKnockdownFrames);
			_spdSlamVictim = victim;
			_spdSlamImpactPosition = victim.GlobalPosition;
			_spdSlamDamage = Mathf.Max(0, _currentAttackDamage);
			_spdSlamImpactWasSuper = superSpd;
			_spdSlamImpactPending = true;
		}
		CurrentAttackAnimationName = "heavy_punch";
		_attackStartupFramesLeft = 0;
		_attackActiveFramesLeft = 0;
		int landingRecovery = superSpd ? SuperSpdLandingRecoveryFrames : SpdLandingRecoveryFrames;
		_attackRecoveryFramesLeft = Mathf.Max(1, landingRecovery);
		_currentAttackRecoveryFrames = Mathf.Max(1, landingRecovery);
	}

	private void ApplySpdSlamKnockdown(int frames)
	{
		CurrentKnockdownType = KnockdownType.HardKnockdown;
		ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.GroundedKnockdown);
		Velocity = Vector2.Zero;
		QueueStateImpact(FighterHitState.GroundedKnockdown);
	}

	public bool TryConsumeSpdSlamImpact(out FighterController victim, out Vector2 position, out int damage)
	{
		return TryConsumeSpdSlamImpact(out victim, out position, out damage, out _);
	}

	public bool TryConsumeSpdSlamImpact(out FighterController victim, out Vector2 position, out int damage, out bool wasSuper)
	{
		victim = _spdSlamVictim;
		position = _spdSlamImpactPosition;
		damage = _spdSlamDamage;
		wasSuper = _spdSlamImpactWasSuper;
		if (!_spdSlamImpactPending) return false;
		_spdSlamImpactPending = false;
		_spdSlamVictim = null;
		_spdSlamImpactWasSuper = false;
		return true;
	}

	private void ApplyThrowLaunch(int frames, float horizontalPushback, float launchSpeed)
	{
		CurrentKnockdownType = KnockdownType.HardKnockdown;
		ApplyHitReaction(frames, FighterHitState.Knockdown);
		Velocity = ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, -Mathf.Abs(launchSpeed)));
	}

	private Vector2 ResolveWallSplatFollowupVelocity(Vector2 requestedVelocity) =>
		_pendingWallSplatKnockdown
			? new Vector2(0f, Mathf.Max(WallSplatSlideSpeed, requestedVelocity.Y))
			: requestedVelocity;

	private void ApplyHitReaction(int frames, FighterHitState state)
	{
		ClearBlowAwayState();
		CurrentWallBounceStrength = WallBounceReactionStrength.None;
		CurrentGroundBounceStrength = GroundBounceReactionStrength.None;
		CurrentGuardReactionStrength = GuardReactionStrength.None;
		CurrentSpecialReaction = SpecialReactionKind.None;
		if (state != FighterHitState.GroundBounce)
		{
			_pendingGroundBounceSpeed = -1f;
			_pendingGroundBounceIntoJuggle = false;
		}
		HitReactionSerial++;
		HitReactionStartedCrouching = WasGrounded &&
			(CurrentInput.Vertical > 0.5f || _currentBoxStateName is "STATE CROUCH START" or "STATE CROUCH" or
				"STATE CROUCH END" or "STATE CROUCH HITSTUN");
		ComboCount = HitstunFramesLeft > 0 ? ComboCount + 1 : 1;
		ComboDisplayFramesLeft = ComboDisplayFrames;
		HitstunFramesLeft = frames;
		HitState = state;
		if (state != FighterHitState.Knockdown && state != FighterHitState.GroundedKnockdown &&
			state != FighterHitState.WallBounce && state != FighterHitState.GroundBounce && state != FighterHitState.Crumple &&
			state != FighterHitState.Stumble && state != FighterHitState.HitFall)
			CurrentKnockdownType = KnockdownType.None;
		StopActiveAbility();
		ClearAttackState();
	}

	private void RecoverFromComboHitstun()
	{
		HitstunFramesLeft = 0;
		HitState = FighterHitState.None;
		CurrentKnockdownType = KnockdownType.None;
		ClearBlowAwayState();
		CurrentWallBounceStrength = WallBounceReactionStrength.None;
		CurrentGroundBounceStrength = GroundBounceReactionStrength.None;
		CurrentGuardReactionStrength = GuardReactionStrength.None;
		CurrentSpecialReaction = SpecialReactionKind.None;
		_pendingWallSplatKnockdown = false;
		_wallSplatDirection = 0;
		JuggleHitCount = 0;
		GroundNormalJuggleHitCount = 0;
		_pendingGroundBounceSpeed = -1f;
		_pendingGroundBounceIntoJuggle = false;
		ComboDisplayFramesLeft = ComboDisplayFrames;
		Velocity = new Vector2(Velocity.X, 0f);
	}

	internal void EnterGroundedKnockdown()
	{
		HitState = FighterHitState.GroundedKnockdown;
		HitReactionSerial++;
		HitstunFramesLeft = Mathf.Max(HitstunFramesLeft, GroundedKnockdownHoldFrames);
		if (_pendingWallSplatKnockdown) CurrentKnockdownType = KnockdownType.SoftKnockdown;
		if (CurrentKnockdownType == KnockdownType.None) CurrentKnockdownType = KnockdownType.AirKnockdown;
		ClearBlowAwayState();
		CurrentWallBounceStrength = WallBounceReactionStrength.None;
		CurrentGroundBounceStrength = GroundBounceReactionStrength.None;
		CurrentSpecialReaction = SpecialReactionKind.None;
		_pendingWallSplatKnockdown = false;
		Velocity = new Vector2(Velocity.X, 0f);
		QueueStateImpact(FighterHitState.GroundedKnockdown);
	}

	internal void BeginWakeup()
	{
		_activeWakeupTotalFrames = ResolveWakeupDurationFrames();
		_wakeupFramesLeft = _activeWakeupTotalFrames;
		HitstunFramesLeft = 0;
		HitState = FighterHitState.None;
		CurrentKnockdownType = KnockdownType.None;
		CurrentGroundBounceStrength = GroundBounceReactionStrength.None;
		CurrentSpecialReaction = SpecialReactionKind.None;
		Velocity = new Vector2(0f, Velocity.Y);
	}

	private int ResolveWakeupDurationFrames()
	{
		if (WakeupFrames > 0) return WakeupFrames;
		NormalMoveData state = Definition?.StateBoxes?.FindStateRule("STATE [やられ]起き上がり");
		if (state == null) return 0;
		return Mathf.Max(1, Mathf.Max(0, state.StartupFrames) + Mathf.Max(0, state.ActiveFrames) +
			Mathf.Max(0, state.RecoveryFrames));
	}

	private void ResolveGroundBounceLanding()
	{
		float bounceSpeed = _pendingGroundBounceSpeed > 0f ? _pendingGroundBounceSpeed : GroundBounceSpeed;
		bool bounceIntoJuggle = _pendingGroundBounceIntoJuggle;
		HitState = bounceIntoJuggle ? FighterHitState.Juggle : FighterHitState.Tumble;
		CurrentKnockdownType = KnockdownType.None;
		if (bounceIntoJuggle) JuggleHitCount = Mathf.Max(1, JuggleHitCount + 1);
		Velocity = new Vector2(Velocity.X, -bounceSpeed);
		_pendingGroundBounceSpeed = -1f;
		_pendingGroundBounceIntoJuggle = false;
		QueueStateImpact(FighterHitState.GroundBounce);
	}

	private void ResolveBlowAwayLanding()
	{
		bool downwardBounce = CurrentBlowAwayDirection is BlowAwayDirection.Downward or BlowAwayDirection.DiagonalDown &&
			!CurrentBlowAwayNoBounce;
		if (!downwardBounce)
		{
			EnterGroundedKnockdown();
			return;
		}

		float bounceSpeed = GetBlowAwaySpeed(CurrentBlowAwayStrength) * Mathf.Clamp(BlowAwayBounceScale, 0.1f, 1f);
		HitState = FighterHitState.Juggle;
		CurrentKnockdownType = KnockdownType.AirKnockdown;
		CurrentBlowAwayDirection = BlowAwayDirection.Vertical;
		CurrentBlowAwayStrength = BlowAwayStrength.Weak;
		CurrentBlowAwayNoBounce = true;
		HitstunFramesLeft = Mathf.Max(HitstunFramesLeft, 20);
		Velocity = new Vector2(Velocity.X * 0.6f, -bounceSpeed);
		QueueStateImpact(FighterHitState.GroundBounce);
	}

	private void ClearBlowAwayState()
	{
		CurrentBlowAwayDirection = BlowAwayDirection.None;
		CurrentBlowAwayStrength = BlowAwayStrength.None;
		CurrentBlowAwayNoBounce = false;
	}

	private bool ShouldPersistAirReactionUntilLanding() =>
		!WasGrounded && (CurrentBlowAwayDirection != BlowAwayDirection.None || _pendingWallSplatKnockdown ||
			HitState == FighterHitState.Knockdown || HitState == FighterHitState.GroundBounce ||
			HitState == FighterHitState.WallSplat || HitState == FighterHitState.Stumble || HitState == FighterHitState.HitFall ||
			(HitState == FighterHitState.Juggle && CurrentKnockdownType != KnockdownType.None));

	private void QueueStateImpact(FighterHitState state, int direction = 0, bool followup = false)
	{
		_stateImpactPending = true;
		_stateImpactState = state;
		_stateImpactPosition = GlobalPosition;
		_stateImpactDirection = direction;
		_stateImpactIsFollowup = followup;
	}

	public bool TryConsumeStateImpact(out FighterHitState state, out Vector2 position, out int direction, out bool followup)
	{
		state = _stateImpactState;
		position = _stateImpactPosition;
		direction = _stateImpactDirection;
		followup = _stateImpactIsFollowup;
		if (!_stateImpactPending) return false;
		_stateImpactPending = false;
		return true;
	}

	/// <summary>Queue the universal takeoff effect exactly when a grounded jump launches.</summary>
	internal void QueueGroundJumpStartEffect(bool isSuperJump = false)
	{
		_jumpStartEffectPending = true;
		_jumpStartEffectGroundPosition = GlobalPosition;
		_jumpStartEffectFacing = Facing;
		_jumpStartEffectIsSuperJump = isSuperJump;
	}

	public bool TryConsumeJumpStartEffect(out Vector2 groundPosition, out int facing, out bool isSuperJump)
	{
		groundPosition = _jumpStartEffectGroundPosition;
		facing = _jumpStartEffectFacing;
		isSuperJump = _jumpStartEffectIsSuperJump;
		if (!_jumpStartEffectPending) return false;
		_jumpStartEffectPending = false;
		return true;
	}

	/// <summary>Queue the universal run dust exactly once at run activation.</summary>
	internal void QueueRunDustEffect()
	{
		_runDustEffectPending = true;
		_runDustEffectGroundPosition = GlobalPosition;
		_runDustEffectFacing = Facing;
	}

	public bool TryConsumeRunDustEffect(out Vector2 groundPosition, out int facing)
	{
		groundPosition = _runDustEffectGroundPosition;
		facing = _runDustEffectFacing;
		if (!_runDustEffectPending) return false;
		_runDustEffectPending = false;
		return true;
	}

	private FighterHitState GetInitialKnockdownState(KnockdownType knockdownType)
	{
		return knockdownType switch
		{
			KnockdownType.Sweep => FighterHitState.Knockdown,
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

	private void LogHit(FighterHitLogEntry entry)
	{
		if (MaxHitLogEntries <= 0 || entry == null) return;
		_hitLog.Add(entry);
		while (_hitLog.Count > MaxHitLogEntries)
			_hitLog.RemoveAt(0);
	}

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

	private bool TryStartMovementAbilityCancel()
	{
		if (!IsAttacking || Definition?.Abilities == null) return false;
		// Special-recovery trait cancels belong exclusively to the blue-cancel path.
		// Letting the general movement cancel consume a buffered trait press here
		// could enter flight without incrementing the event or spawning its effect.
		if (CurrentAttackIsSpecial && IsAttackRecovering) return false;
		MovementAbility candidate = null;
		foreach (MovementAbility ability in Definition.Abilities)
		{
			if (ability == null || ability == ActiveAbility ||
				!ability.CanStartFromAttack(this, GetRuntime(ability))) continue;
			if (candidate == null || ability.Priority > candidate.Priority) candidate = ability;
		}
		if (candidate == null) return false;
		ClearAttackState();
		if (!StartAbility(candidate)) return false;
		ClearAttackInputBuffers();
		return true;
	}

	private bool TryUniversalBlueRecoveryCancel()
	{
		if (!BlueRecoveryCancelEnabled || !IsAttackRecovering || !CurrentAttackIsSpecial ||
			(CurrentAttackHasContact && !CurrentAttackHasUnblockedHit) ||
			IsPerformingSuperMove || !IsWithinBlueRecoveryCancelWindow ||
			Definition?.Abilities == null) return false;
		// Require a new trait-button edge. Buffered presses that originally started
		// the special must never turn into an automatic recovery cancel later.
		if (!CurrentInput.Special1Pressed && !CurrentInput.Special2Pressed) return false;

		MovementAbility candidate = null;
		foreach (MovementAbility ability in Definition.Abilities)
		{
			if (ability == null || ability == ActiveAbility ||
				!ability.CanStartFromAttack(this, GetRuntime(ability))) continue;
			if (candidate == null || ability.Priority > candidate.Priority) candidate = ability;
		}
		// Blue cancel is a route into an actual trait. If the pressed trait cannot
		// activate here, do not erase recovery or return the fighter to neutral.
		if (candidate == null || ActiveAbility != null && !ActiveAbility.CanBeInterruptedBy(candidate)) return false;
		ClearAttackState();
		if (!StartAbility(candidate)) return false;
		ClearAttackInputBuffers();
		BlueRecoveryCancelSerial++;
		PlayBlueCancelPresentation();
		return true;
	}

	private bool TryCancelNormalIntoFlightDeactivation()
	{
		if (!IsAttacking || !CurrentAttackIsNormal || ActiveAbility is not FlightAbility flight ||
			!flight.WantsManualDeactivation(this) ||
			!CanCancelCurrentNormalIntoSpecial("FLIGHT CANCEL")) return false;
		ClearAttackState();
		StopActiveAbility();
		ClearAttackInputBuffers();
		return true;
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
			bool repeatedHeldJump = HeldJumpRepeatsOnLanding && input.JumpHeld && WasGrounded && LandingLagFramesLeft <= 0;
			if (input.JumpPressed || repeatedHeldJump)
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
		bool repeatJumpOnLanding = HeldJumpRepeatsOnLanding && input.JumpHeld && WasGrounded;
		ActionInput = new FighterInput(input.Horizontal, input.Vertical,
			LandingLagFramesLeft <= 0 && (input.JumpPressed || JumpBufferFramesLeft > 0 || repeatJumpOnLanding), input.JumpHeld,
			input.DashPressed || DashBufferFramesLeft > 0, input.FlightHeld,
			input.LightPunchPressed || _lightPunchBufferFramesLeft > 0, input.LightPunchHeld,
			input.LightKickPressed || _lightKickBufferFramesLeft > 0, input.LightKickHeld,
			input.HeavyPunchPressed || _heavyPunchBufferFramesLeft > 0, input.HeavyPunchHeld,
			input.HeavyKickPressed || _heavyKickBufferFramesLeft > 0, input.HeavyKickHeld,
			input.Special1Pressed || _special1BufferFramesLeft > 0, input.Special1Held,
			input.Special2Pressed || _special2BufferFramesLeft > 0, input.Special2Held,
			input.FlightPressed, input.FlightReleased, input.Special1Released);
	}

	private void TryStartBasicAttack()
	{
		string attackName = GetPressedBasicAttackName(ActionInput);
		if (attackName == "") return;
		bool requestedAirborne = !WasGrounded;
		bool requestedCrouching = WasGrounded && ActionInput.Vertical > 0.5f;
		if (Definition?.AllowLegacyFallbackMoves == false &&
			GetConfiguredMoveData(attackName, requestedCrouching, requestedAirborne) == null &&
			GetConfiguredSuperMoveData(attackName) == null)
		{
			// Fully-authored characters must never fall through to the prototype
			// Kung Fu Man projectile, super, launcher, or directional-normal rules.
			ClearAttackInputBuffers();
			return;
		}
		NormalMoveData requestedMove = GetConfiguredMoveData(attackName, requestedCrouching, requestedAirborne);
		if (!UsesSuperJumpAirNormalRules && requestedAirborne && attackName.StartsWith("LIGHT") && requestedMove?.MaxUsesPerCombo > 0 &&
			GetAirTimeNormalUseCount(attackName) >= requestedMove.MaxUsesPerCombo)
		{
			// Aerial recovery must not reset a light-normal cap. The allowance
			// refreshes only after landing, so LP -> LK works but LP -> LP does not.
			ClearNormalAttackInputBuffers();
			return;
		}
		if (_flightUsedThisAirTime && !WasGrounded && ActiveAbility == null && IsNormalAttackName(attackName))
		{
			ClearNormalAttackInputBuffers();
			return;
		}
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
			else if (ActiveAbility is JumpAbility jumpAbility &&
				GetRuntime(jumpAbility).IntValue > 0 && IsSpecialAttackName(attackName))
			{
				// Grounded jump squat is special-cancelable. Once takeoff occurs,
				// the same buffered command can no longer cancel the jump.
			}
			else if (ActiveAbility is JumpAbility && IsInDoubleJumpState)
			{
				// Double-jump attacks should be immediate; the jump launch has already happened.
			}
			else if (ActiveAbility is RunAbility)
			{
				attackStartedFromRun = WasGrounded;
			}
			else if (!ActiveAbility.CanStartAttack(this, GetRuntime(ActiveAbility))) return;
			bool preserveButtonFlight = ActiveAbility is FlightAbility flight &&
				flight.ShouldPersistThroughNormal(this, attackName);
			if (!preserveButtonFlight) StopActiveAbility();
		}
		if (_pendingReusableMotionAttackName == attackName && _pendingReusableMotionConsumes)
			_motionInputBuffer.ConsumeReusableMotion(_pendingReusableMotion, _pendingReusableMotionCompletion);
		_pendingReusableMotion = null;
		_pendingReusableMotionCompletion = -1;
		_pendingReusableMotionAttackName = "";
		CurrentAttackName = attackName;
		_currentAttackStartedAirborne = !WasGrounded;
		if (_currentAttackStartedAirborne && IsNormalAttackName(attackName))
			_airNormalPerformedSinceTakeoff = true;
		_currentAttackStartedFromAirDash = attackStartedFromAirDash;
		_currentAttackStartedFromRun = attackStartedFromRun;
		_currentAttackStartedCrouching = WasGrounded && ActionInput.Vertical > 0.5f;
		_currentMoveData = GetConfiguredMoveData(attackName, _currentAttackStartedCrouching, _currentAttackStartedAirborne);
		_currentSpecialMove = _currentMoveData as SpecialMoveData;
		CurrentAttackAnimationName = _currentMoveData?.AnimationName ?? "";
		_currentMoveRule = GetNormalMoveRule(attackName, _currentAttackStartedCrouching, _currentAttackStartedAirborne, _currentMoveData);
		_currentSuperMove = GetSuperMoveData(attackName);
		if (_currentSuperMove != null && !string.IsNullOrWhiteSpace(_currentSuperMove.AnimationName))
			CurrentAttackAnimationName = _currentSuperMove.AnimationName;
		RegisterNormalUse(attackName);
		if (_currentAttackStartedAirborne && attackName.StartsWith("LIGHT"))
			RegisterAirTimeNormalUse(attackName);
		_currentAttackStartupFrames = GetBasicAttackStartupFrames(attackName);
		_currentAttackActiveFrames = GetBasicAttackActiveFrames(attackName);
		_currentAttackRecoveryFrames = GetBasicAttackRecoveryFrames(attackName);
		_currentAttackHitstunFrames = GetBasicAttackHitstunFrames(attackName);
		_currentAttackPushback = GetBasicAttackPushback(attackName);
		_currentAttackHitstopFrames = GetBasicAttackHitstopFrames(attackName);
		_currentAttackShakeStrength = GetBasicAttackShakeStrength(attackName);
		ApplyMoveDataCombatOverrides();
		_currentAttackHitboxLocal = GetBasicAttackHitboxLocal(attackName);
		// TickBasicAttack runs later in this same physics step. Starting at -1
		// makes the first evaluated/displayed gameplay frame truly frame zero.
		CurrentAttackFrame = -1;
		_attackStartupFramesLeft = _currentAttackStartupFrames;
		// Zero-startup authored moves (notably Sanzou's S1 parry) must enter
		// their active timeline immediately instead of creating an attack state
		// with all three counters at zero.
		_attackActiveFramesLeft = _currentAttackStartupFrames <= 0 ? _currentAttackActiveFrames : 0;
		_attackRecoveryFramesLeft = 0;
		_attackHasHit = false;
		_attackHasUnblockedHit = false;
		_attackHitGroups.Clear();
		_projectileSpawnedThisAttack = false;
		_projectilesSpawnedThisAttack = 0;
		_moveVisualEffectSpawned = false;
		_currentAttackChargeFrames = 0;
		_currentAttackFullyCharged = false;
		_sustainMashGraceFramesLeft = _currentSpecialMove?.SustainWithMash == true
			? Mathf.Max(1, _currentSpecialMove.SustainMashGraceFrames)
			: 0;
		_sustainMashHitIntervalFramesLeft = _currentSpecialMove?.SustainWithMash == true
			? Mathf.Max(1, _currentSpecialMove.SustainMashHitIntervalFrames)
			: 0;
		_currentAttackHitsRemaining = _currentSuperMove?.HitCount ?? 1;
		_currentAttackHitCooldownFramesLeft = 0;
		_currentSuperConfirmed = false;
		_currentSuperConfirmedFrame = -1;
		_launcherJumpCancelFramesLeft = 0;
		if (_currentSuperMove != null)
		{
			SuperActivationFreezeRequested = true;
			SuperActivationFreezeFramesRequested = Mathf.Max(SuperActivationFreezeFramesRequested, _currentSuperMove.ActivationFreezeFrames);
			SuperBackdropFramesRequested = Mathf.Max(SuperBackdropFramesRequested,
				_currentSuperMove.BackdropFrames > 0 ? _currentSuperMove.BackdropFrames :
					_currentAttackStartupFrames + _currentAttackActiveFrames + _currentAttackRecoveryFrames + _currentSuperMove.ActivationFreezeFrames);
			if (_currentSuperMove.RushesForward) Velocity = new Vector2(Facing * _currentSuperMove.RushSpeed, Velocity.Y);
		}
		else if (_currentSpecialMove?.TriggersSuperPresentation == true)
		{
			SuperActivationFreezeRequested = true;
			SuperActivationFreezeFramesRequested = Mathf.Max(SuperActivationFreezeFramesRequested,
				_currentSpecialMove.SuperActivationFreezeFrames);
			SuperBackdropFramesRequested = Mathf.Max(SuperBackdropFramesRequested,
				_currentSpecialMove.SuperBackdropFrames);
		}
		_currentSpecialSelfLaunchApplied = false;
		if (_currentSpecialMove is { SelfLaunch: true, SelfLaunchStartFrame: <= 0 })
			ApplyCurrentSpecialSelfLaunch(attackName);
		if (_currentSpecialMove?.SelfDrive == true)
			Velocity = new Vector2(Facing * _currentSpecialMove.SelfDriveSpeed, Velocity.Y);
		if (attackName == ElectricWindGodFistName || IsProjectileAttackName(attackName) || _currentSuperMove != null ||
			(_currentSpecialMove == null && IsNormalAttackName(attackName)))
			ConsumeQuarterCircleForwardCommand();
		if (attackName == StompSpecialName) _motionInputBuffer.ConsumeChargedDownUpCommand();
		if (attackName == CommandRunLightName || attackName == CommandRunHeavyName)
			_motionInputBuffer.ConsumeChargedBackForwardCommand();
		ConsumeDashBuffer();
		ClearAttackInputBuffers();
		ApplyAirAttackMomentum(attackName);
	}

	private void TrySpawnProjectileForCurrentAttack()
	{
		if (!_projectileSpawnedThisAttack && _currentSpecialMove?.ReflectorScene != null && _attackStartupFramesLeft <= 0)
		{
			_projectileSpawnedThisAttack = true;
			Node reflectorNode = _currentSpecialMove.ReflectorScene.Instantiate();
			if (reflectorNode is ProjectileReflector reflector)
			{
				Vector2 reflectorOffset = _currentSpecialMove.ReflectorSpawnOffset;
				reflector.GlobalPosition = GlobalPosition + new Vector2(reflectorOffset.X * Facing, reflectorOffset.Y);
				reflector.Initialize(this);
				GetParent()?.AddChild(reflector);
			}
			else reflectorNode?.QueueFree();
			return;
		}
		if (!_projectileSpawnedThisAttack && _currentSuperMove?.ProjectileScene != null && _attackStartupFramesLeft <= 0)
		{
			_projectileSpawnedThisAttack = true;
			Node projectileNode = _currentSuperMove.ProjectileScene.Instantiate();
			if (projectileNode is ProjectileReflector reflector)
			{
				Vector2 superReflectorOffset = _currentSuperMove.ProjectileSpawnOffset;
				reflector.GlobalPosition = GlobalPosition + new Vector2(superReflectorOffset.X * Facing, superReflectorOffset.Y);
				reflector.Initialize(this, Facing, true);
				GetParent()?.AddChild(reflector);
			}
			else projectileNode?.QueueFree();
			return;
		}
		SuperMoveData superMove = _currentSuperMove;
		bool super = superMove?.Projectile == true;
		int projectileCount = super ? Mathf.Max(1, superMove.ProjectileCount) : 1;
		if (_projectilesSpawnedThisAttack >= projectileCount ||
			(!IsProjectileAttackName(CurrentAttackName) && _currentSpecialMove?.Projectile != true && !super) ||
			_attackStartupFramesLeft > 0) return;
		int activeElapsed = Mathf.Max(0, CurrentAttackFrame - _currentAttackStartupFrames);
		int spawnInterval = super ? Mathf.Max(1, superMove.ProjectileSpawnIntervalFrames) : 1;
		if (activeElapsed < _projectilesSpawnedThisAttack * spawnInterval) return;
		int volleyIndex = _projectilesSpawnedThisAttack++;
		_projectileSpawnedThisAttack = _projectilesSpawnedThisAttack >= projectileCount;

		bool heavy = CurrentAttackName == HeavyProjectileName || _currentSpecialMove?.HeavyProjectile == true || super;
		var projectile = new BasicProjectile { Name = super ? "SuperFireball" : heavy ? "HeavyProjectile" : "LightProjectile" };
		Vector2 configuredOffset = super
			? superMove.ProjectileSpawnOffset
			: _currentSpecialMove?.ProjectileSpawnOffset ?? ProjectileSpawnOffset;
		if (super && projectileCount > 1)
			configuredOffset.Y += (volleyIndex - (projectileCount - 1) * 0.5f) * superMove.ProjectileVolleyVerticalSpacing;
		Vector2 offset = new(configuredOffset.X * Facing, configuredOffset.Y);
		projectile.GlobalPosition = GlobalPosition + offset;
		projectile.Initialize(this, Facing,
			super ? superMove.ProjectileSpeed : _currentSpecialMove?.Projectile == true
				? _currentSpecialMove.ProjectileSpeed
				: heavy ? HeavyProjectileSpeed : LightProjectileSpeed,
			super ? superMove.HitstunFrames : _currentAttackHitstunFrames,
			super ? superMove.Pushback : _currentAttackPushback,
			super ? superMove.HitstopFrames : _currentAttackHitstopFrames,
			super ? superMove.ShakeStrength : _currentAttackShakeStrength,
			heavy,
			super ? superMove.HitCount : _currentSpecialMove?.ProjectileHitCount ?? 1,
			super ? superMove.ProjectileHitCooldownFrames : _currentSpecialMove?.ProjectileHitCooldownFrames ?? 4,
			super,
			super && superMove.FinalHitKnocksDown,
			super ? superMove.FinalKnockdownType : KnockdownType.SoftKnockdown,
			super ? superMove.FinalKnockdownFrames : 0,
			true,
			super ? 58f : _currentMoveData?.Damage ?? 72f);
		if (_currentSpecialMove?.Projectile == true)
		{
			projectile.HitboxLocal = _currentSpecialMove.ProjectileHitboxLocal;
			projectile.ConfigureVisual(_currentSpecialMove.ProjectileSpriteFrames,
				_currentSpecialMove.ProjectileAnimationName, _currentSpecialMove.ProjectileVisualOffset,
				_currentSpecialMove.ProjectileVisualScale, _currentSpecialMove.ProjectileVisualAdditiveBlend);
			projectile.ConfigureSourceFormula(_currentSpecialMove.ProjectileLifetimeFrames,
				_currentSpecialMove.ProjectileSecondarySpeed, _currentSpecialMove.ProjectileSecondarySpeedFrame,
				_currentSpecialMove.ProjectileVisualStartScale, _currentSpecialMove.ProjectileVisualScale,
				_currentSpecialMove.ProjectileVisualScaleStartFrame, _currentSpecialMove.ProjectileVisualScaleEndFrame,
				_currentSpecialMove.ProjectileVisualBottomAnchored);
			if (_currentSpecialMove.ProjectileAnchoredToOwner)
				projectile.ConfigureOwnerAnchor(_currentSpecialMove.ProjectileSpawnOffset,
					_currentSpecialMove.ProjectileDirectionalHitbox);
			projectile.ConfigureImpact(_currentSpecialMove.ProjectileImpactSpriteFrames,
				_currentSpecialMove.ProjectileImpactAnimationName, _currentSpecialMove.ProjectileImpactVisualOffset,
				_currentSpecialMove.ProjectileImpactScale, _currentSpecialMove.ProjectileImpactAdditiveBlend,
				_currentSpecialMove.ProjectileImpactBlackKey, _currentSpecialMove.ProjectileImpactBlackensDefender,
				_currentSpecialMove.ProjectileImpactBlackSilhouetteFrames,
				_currentSpecialMove.ProjectileImpactDefenderFireSpriteFrames,
				_currentSpecialMove.ProjectileImpactDefenderFireAnimationName);
			projectile.ConfigurePath(_currentSpecialMove.ProjectilePath, _currentSpecialMove.ProjectilePathTravelFrames);
		}
		else if (super)
		{
			projectile.HitboxLocal = superMove.ProjectileHitboxLocal;
			projectile.ConfigureVisual(superMove.ProjectileSpriteFrames, superMove.ProjectileAnimationName,
				superMove.ProjectileVisualOffset, superMove.ProjectileVisualScale);
			projectile.ConfigureImpact(superMove.ProjectileImpactSpriteFrames, superMove.ProjectileImpactAnimationName,
				superMove.ProjectileImpactVisualOffset, superMove.ProjectileImpactScale,
				superMove.ProjectileImpactAdditiveBlend, superMove.ProjectileImpactBlackKey,
				superMove.ProjectileImpactBlackensDefender, superMove.ProjectileImpactBlackSilhouetteFrames,
				superMove.ProjectileImpactDefenderFireSpriteFrames,
				superMove.ProjectileImpactDefenderFireAnimationName);
			Curve2D volleyPath = ResolveSuperProjectilePath(superMove, volleyIndex);
			if (volleyPath != null)
				projectile.ConfigurePath(volleyPath, superMove.ProjectilePathTravelFrames, superMove.ProjectileAlignToPath);
		}
		GetParent()?.AddChild(projectile);
	}

	private static Curve2D ResolveSuperProjectilePath(SuperMoveData superMove, int volleyIndex)
	{
		if (superMove?.ProjectilePathLayoutScene == null) return null;
		Node layout = superMove.ProjectilePathLayoutScene.Instantiate();
		Godot.Collections.Array<Node> paths = layout.FindChildren("*", "Path2D", true, false);
		if (paths.Count == 0)
		{
			layout.Free();
			return null;
		}
		Path2D selected = paths[Mathf.PosMod(volleyIndex, paths.Count)] as Path2D;
		Curve2D result = selected?.Curve?.Duplicate(true) as Curve2D;
		layout.Free();
		return result;
	}

	private void TrySpawnMoveVisualEffect()
	{
		if (_moveVisualEffectSpawned || _currentMoveData?.EffectSpriteFrames == null ||
			_currentMoveData.EffectSpawnOnHitContact ||
			_currentMoveData.EffectSpawnFrame < 0 || CurrentAttackFrame < _currentMoveData.EffectSpawnFrame ||
			(_currentMoveData.EffectRequiresFullCharge && !_currentAttackFullyCharged) ||
			string.IsNullOrWhiteSpace(_currentMoveData.EffectAnimationName)) return;
		_moveVisualEffectSpawned = true;
		Vector2 offset = _currentMoveData.EffectSpawnOffset;
		SpawnConfiguredMoveVisualEffect(GlobalPosition + new Vector2(offset.X * Facing, offset.Y));
	}

	private bool TrySpawnMoveContactEffect(Vector2 hitPoint)
	{
		if (_currentMoveData?.EffectSpawnOnHitContact != true || _currentMoveData.EffectSpriteFrames == null ||
			(_currentMoveData.EffectRequiresFullCharge && !_currentAttackFullyCharged) ||
			string.IsNullOrWhiteSpace(_currentMoveData.EffectAnimationName)) return false;
		SpawnConfiguredMoveVisualEffect(hitPoint);
		return true;
	}

	private void SpawnConfiguredMoveVisualEffect(Vector2 globalPosition)
	{
		Node effectHost = GetParent();
		if (effectHost == null) return;
		var effect = new MoveVisualEffect
		{
			Name = $"{CurrentAttackName} Effect",
			TopLevel = true,
			ZAsRelative = false,
			ZIndex = 4095
		};
		effectHost.AddChild(effect);
		// Assign after parenting so a transformed arena can never offset the shared contact coordinate.
		effect.GlobalPosition = globalPosition;
		effect.Initialize(_currentMoveData.EffectSpriteFrames, _currentMoveData.EffectAnimationName,
			Facing, _currentMoveData.EffectScale, _currentMoveData.EffectVisualOffset,
			_currentMoveData.EffectAdditiveBlend, _currentMoveData.EffectBlackKey);
	}

	public void PlayBlueCancelPresentation()
	{
		SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(
			"res://Assets/Effects/BigBangCommon/blue_cancel_360_363_frames.tres");
		Node effectHost = GetParent();
		if (frames == null || effectHost == null) return;
		var effect = new MoveVisualEffect
		{
			Name = "Blue Recovery Cancel",
			TopLevel = true,
			ZAsRelative = false,
			ZIndex = 4096
		};
		effectHost.AddChild(effect);
		effect.GlobalPosition = WorldPositionBox.GetCenter();
		effect.Initialize(frames, "blue_cancel", Facing, Vector2.One, Vector2.Zero,
			additiveBlend: true, blackKey: true);
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
		if (IsInShortHopRoute && IsNormalAttackName(CurrentAttackName) && IsNormalAttackName(nextAttackName))
			return false;
		bool nextStartedCrouching = WasGrounded && ActionInput.Vertical > 0.5f;
		bool nextStartedAirborne = !WasGrounded;
		NormalMoveRule nextRule = GetNormalMoveRule(nextAttackName, nextStartedCrouching, nextStartedAirborne);
		if (nextRule.MaxUsesPerCombo > 0 && GetNormalUseCount(nextAttackName) >= nextRule.MaxUsesPerCombo) return false;
		if (nextAttackName == QcfPowerPunchRekkaName &&
			(CurrentAttackName == LightProjectileName || CurrentAttackName == HeavyProjectileName ||
			 CurrentAttackName == QcfPowerPunchLightName || CurrentAttackName == QcfPowerPunchHeavyName))
			return CurrentAttackFrame >= _currentAttackStartupFrames;
		if ((CurrentAttackName == CommandRunLightName || CurrentAttackName == CommandRunHeavyName) &&
			(nextAttackName == CommandRunHopName || nextAttackName == CommandRunPunchName))
			return true;
		SpecialMoveData nextSpecial = Definition?.SpecialMoves?.FindMove(nextAttackName,
			nextStartedCrouching, nextStartedAirborne);
		if (IsSpecialAttackName(nextAttackName) || nextSpecial?.CanCancelIntoFromNormals == true)
		{
			// Per-move normal data is the primary authoring path. Global CancelRules
			// remain available for broad mechanics and legacy character definitions.
			if (_currentMoveRule.CanChainToSpecial &&
				(!_currentMoveRule.ChainRequiresContact || _attackHasHit) &&
				IsWithinCurrentMoveCancelWindow(_currentMoveRule.CancelWindowStartFrame,
					_currentMoveRule.CancelWindowEndFrame, _currentMoveRule.ChainEarliestActiveFramesLeft))
				return true;
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
		if (kind == CancelKind.Special && IsRegularThrowAttackName(CurrentAttackName)) return false;

		foreach (CancelRule rule in Definition.CancelRules)
		{
			if (rule == null) continue;
			if (rule.Allows(CurrentAttackName, targetMove, kind, currentMoveIsNormal, _attackHasHit,
				elapsedFrames, _attackStartupFramesLeft, _attackActiveFramesLeft)) return true;
		}
		return false;
	}

	/// <summary>Uses the same authored normal-to-special rules as an ordinary special move.</summary>
	public bool CanCancelCurrentNormalIntoSpecial(string targetMove)
	{
		if (!CurrentAttackIsNormal) return false;
		if (_currentMoveRule.CanChainToSpecial &&
			(!_currentMoveRule.ChainRequiresContact || _attackHasHit) &&
			IsWithinCurrentMoveCancelWindow(_currentMoveRule.CancelWindowStartFrame,
				_currentMoveRule.CancelWindowEndFrame, _currentMoveRule.ChainEarliestActiveFramesLeft))
			return true;
		return CanCancelCurrentMove(CancelKind.Special, targetMove);
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

	private void RegisterAirTimeNormalUse(string attackName)
	{
		string key = attackName.ToUpperInvariant();
		_normalUsesThisAirTime.TryGetValue(key, out int uses);
		_normalUsesThisAirTime[key] = uses + 1;
	}

	private int GetAirTimeNormalUseCount(string attackName)
	{
		_normalUsesThisAirTime.TryGetValue(attackName.ToUpperInvariant(), out int uses);
		return uses;
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
		_currentAttackBlowAwayDirection = _currentMoveRule.BlowAwayDirection;
		_currentAttackBlowAwayStrength = _currentMoveRule.BlowAwayStrength;
		_currentAttackBlowAwayNoBounce = _currentMoveRule.BlowAwayNoBounce;
		_currentAttackWallBounceStrength = _currentMoveRule.WallBounceStrength;
		_currentAttackGroundBounceStrength = _currentMoveRule.GroundBounceStrength;
		_currentAttackGuardReactionStrength = _currentMoveRule.GuardReactionStrength;
		_currentAttackSpecialReaction = _currentMoveRule.SpecialReaction;
		if (_currentMoveRule.HitstunFramesOverride > 0) _currentAttackHitstunFrames = _currentMoveRule.HitstunFramesOverride;
		if (_currentMoveRule.HitstopFramesOverride > 0) _currentAttackHitstopFrames = _currentMoveRule.HitstopFramesOverride;
		if (_currentMoveRule.PushbackOverride > 0f) _currentAttackPushback = _currentMoveRule.PushbackOverride;
		if (_currentMoveRule.ShakeStrengthOverride >= 0f) _currentAttackShakeStrength = _currentMoveRule.ShakeStrengthOverride;
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

	private NormalMoveData GetConfiguredMoveData(string attackName, bool startedCrouching, bool startedAirborne)
	{
		NormalMoveData normal = Definition?.NormalMoves?.FindRule(attackName, startedCrouching, startedAirborne);
		if (normal != null) return normal;
		return Definition?.SpecialMoves?.FindMove(attackName, startedCrouching, startedAirborne);
	}

	private NormalMoveRule GetNormalMoveRule(string attackName, bool startedCrouching, bool startedAirborne,
		NormalMoveData configuredMove = null)
	{
		NormalMoveData moveData = configuredMove ?? GetConfiguredMoveData(attackName, startedCrouching, startedAirborne);
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

		if ((attackName == "HEAVY PUNCH" || attackName == CrouchingHeavyPunchName) && startedCrouching && !startedAirborne)
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
		if (_currentSpecialSelfLaunchApplied && _currentSpecialMove?.SelfHorizontalDeceleration > 0f)
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f,
				_currentSpecialMove.SelfHorizontalDeceleration / 60f), Velocity.Y);
		if (_currentSpecialMove?.SustainWithMash == true && _attackStartupFramesLeft <= 0)
		{
			MotionAttackButton pressed = MotionAttackButton.None;
			if (ActionInput.LightPunchPressed) pressed |= MotionAttackButton.LightPunch;
			if (ActionInput.HeavyPunchPressed) pressed |= MotionAttackButton.HeavyPunch;
			if (ActionInput.LightKickPressed) pressed |= MotionAttackButton.LightKick;
			if (ActionInput.HeavyKickPressed) pressed |= MotionAttackButton.HeavyKick;
			if ((pressed & _currentSpecialMove.SustainMashButtons) != 0)
				_sustainMashGraceFramesLeft = Mathf.Max(1, _currentSpecialMove.SustainMashGraceFrames);
			else if (_attackActiveFramesLeft > 0 && _sustainMashGraceFramesLeft > 0)
				_sustainMashGraceFramesLeft--;
			if (_attackActiveFramesLeft > 0 && _sustainMashGraceFramesLeft <= 0)
			{
				_attackActiveFramesLeft = 0;
				_attackRecoveryFramesLeft = _currentAttackRecoveryFrames + 1;
			}
			else if (_attackActiveFramesLeft > 0 && --_sustainMashHitIntervalFramesLeft <= 0)
			{
				_attackHitGroups.Clear();
				_sustainMashHitIntervalFramesLeft = Mathf.Max(1, _currentSpecialMove.SustainMashHitIntervalFrames);
			}
		}
		if (_currentSpecialMove?.SelfDrive == true && WasGrounded)
			Velocity = new Vector2(Facing * _currentSpecialMove.SelfDriveSpeed, Velocity.Y);
		if (_currentSpecialMove is { SelfRiseDuringAttack: true } risingMove &&
			CurrentAttackFrame >= risingMove.SelfRiseStartFrame &&
			(risingMove.SelfRiseEndFrame < 0 || CurrentAttackFrame <= risingMove.SelfRiseEndFrame))
			Velocity = new Vector2(Velocity.X, -Mathf.Abs(risingMove.SelfRiseSpeed));
		if (_currentSpecialMove is { ForceDownwardStartFrame: >= 0 } descentMove &&
			CurrentAttackFrame >= descentMove.ForceDownwardStartFrame && !WasGrounded)
		{
			// The stomp phase is a committed dive, not ordinary jump gravity.
			// Reassert its minimum downward speed every tick until floor contact.
			Velocity = new Vector2(Velocity.X, Mathf.Max(Velocity.Y, descentMove.ForceDownwardSpeed));
		}
		if (_attackStartupFramesLeft > 0)
		{
			if (_currentMoveData?.Chargeable == true && _attackStartupFramesLeft == 1 &&
				ActionInput.HeavyPunchHeld && _currentAttackChargeFrames < Mathf.Max(1, _currentMoveData.MaxChargeFrames))
			{
				_currentAttackChargeFrames++;
				if (_currentAttackChargeFrames < Mathf.Max(1, _currentMoveData.MaxChargeFrames)) return;
				_currentAttackFullyCharged = true;
			}
			_attackStartupFramesLeft--;
			if (_attackStartupFramesLeft == 0) _attackActiveFramesLeft = _currentAttackActiveFrames;
		}
		else if (_attackActiveFramesLeft > 0)
		{
			bool holdWhiffedAirLightActive = _currentAttackStartedAirborne && !UsesSuperJumpAirNormalRules && !_attackHasHit && !WasGrounded &&
				(CurrentAttackName == "LIGHT PUNCH" || CurrentAttackName == "LIGHT KICK");
			if (!holdWhiffedAirLightActive)
			{
				_attackActiveFramesLeft--;
				// Keep the final authored recovery frame visible for a full tick.
				// The extra counter step clears on the following tick without adding
				// another displayed gameplay frame.
				if (_attackActiveFramesLeft == 0) _attackRecoveryFramesLeft = _currentAttackRecoveryFrames + 1;
			}
		}
		else if (_attackRecoveryFramesLeft > 0)
		{
			bool waitingForForcedDescentLanding = _attackRecoveryFramesLeft == 1 &&
				_currentSpecialMove?.HoldUntilLanding == true && !WasGrounded;
			if (!waitingForForcedDescentLanding) _attackRecoveryFramesLeft--;
			if (_attackRecoveryFramesLeft == 0) ClearAttackState();
		}
		if (_currentAttackHitCooldownFramesLeft > 0) _currentAttackHitCooldownFramesLeft--;
		if (IsAttacking)
		{
			CurrentAttackFrame++;
			if (!_currentSpecialSelfLaunchApplied &&
				_currentSpecialMove is { SelfLaunch: true } launchMove &&
				CurrentAttackFrame >= launchMove.SelfLaunchStartFrame)
				ApplyCurrentSpecialSelfLaunch(CurrentAttackName);
			if (_currentSpecialLandingRecovery) _currentSpecialLandingRecoveryFrame++;
		}
	}

	private void ApplyCurrentSpecialSelfLaunch(string attackName)
	{
		if (_currentSpecialMove?.SelfLaunch != true) return;
		float horizontal = _currentSpecialMove.SelfLaunchUsesFacing || attackName == CommandRunHopName
			? Facing * _currentSpecialMove.SelfHorizontalSpeed
			: Mathf.Abs(ActionInput.Horizontal) > 0.5f
				? Mathf.Sign(ActionInput.Horizontal) * _currentSpecialMove.SelfHorizontalSpeed
				: 0f;
		Velocity = new Vector2(horizontal, -_currentSpecialMove.SelfLaunchSpeed);
		_currentSpecialSelfLaunchApplied = true;
	}

	private void ClearAttackState()
	{
		ReleaseCapturedThrowVictim();
		_attackStartupFramesLeft = 0;
		_attackActiveFramesLeft = 0;
		_attackRecoveryFramesLeft = 0;
		CurrentAttackFrame = 0;
		_attackHasHit = false;
		_attackHasUnblockedHit = false;
		_attackHitGroups.Clear();
		_projectileSpawnedThisAttack = false;
		_projectilesSpawnedThisAttack = 0;
		_moveVisualEffectSpawned = false;
		_currentAttackChargeFrames = 0;
		_currentAttackFullyCharged = false;
		_sustainMashGraceFramesLeft = 0;
		_sustainMashHitIntervalFramesLeft = 0;
		_currentAttackHitsRemaining = 0;
		_currentAttackHitCooldownFramesLeft = 0;
		_currentSuperConfirmed = false;
		_currentSuperConfirmedFrame = -1;
		_currentSuperLockedDefender = null;
		_currentSuperLockedDefenderPosition = Vector2.Zero;
		_currentSuperLockedAttackerOffset = Vector2.Zero;
		_currentAttackStartedAirborne = false;
		_currentAttackStartedFromAirDash = false;
		_currentAttackStartedFromRun = false;
		_currentAttackStartedCrouching = false;
		_currentSpecialLandingRecovery = false;
		_currentSpecialSelfLaunchApplied = false;
		_currentSpecialLandingRecoveryFrame = 0;
		CurrentAttackName = "";
		CurrentAttackAnimationName = "";
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
		_currentAttackBlowAwayDirection = BlowAwayDirection.None;
		_currentAttackBlowAwayStrength = BlowAwayStrength.None;
		_currentAttackBlowAwayNoBounce = false;
		_currentAttackWallBounceStrength = WallBounceReactionStrength.None;
		_currentAttackGroundBounceStrength = GroundBounceReactionStrength.None;
		_currentAttackGuardReactionStrength = GuardReactionStrength.None;
		_currentAttackSpecialReaction = SpecialReactionKind.None;
		_currentAttackHitboxLocal = HitboxLocal;
		_currentSuperMove = null;
		_currentMoveData = null;
		_currentContactHitSparkScene = null;
		_currentSpecialMove = null;
		SuperActivationFreezeRequested = false;
		SuperActivationFreezeFramesRequested = 0;
		SuperBackdropFramesRequested = 0;
		_launcherJumpCancelFramesLeft = 0;
		_airLightJumpCancelFramesLeft = 0;
		_currentMoveRule = NormalMoveRule.None;
	}

	private void UpdateStateBoxTimeline(FighterInput input)
	{
		string nextState = "";
		if (IsWakingUp && Definition?.StateBoxes?.FindStateRule("STATE [やられ]起き上がり") != null)
		{
			nextState = "STATE [やられ]起き上がり";
		}
		else if (!IsAttacking && ActiveAbility is FlightAbility flightAbility &&
			Definition?.StateBoxes?.FindStateRule(flightAbility.ResolveStateName(this)) != null)
		{
			nextState = flightAbility.ResolveStateName(this);
		}
		else if (ActiveAbility is JetEscapeAbility jetEscape &&
			!string.IsNullOrWhiteSpace(jetEscape.StateName) &&
			Definition?.StateBoxes?.FindStateRule(jetEscape.StateName) != null)
		{
			nextState = jetEscape.StateName;
		}
		else if (HitstunFramesLeft > 0 && CurrentSpecialReaction != SpecialReactionKind.None &&
			Definition?.StateBoxes?.FindStateRule(CurrentSpecialReactionStateName) != null)
		{
			nextState = CurrentSpecialReactionStateName;
		}
		else if (IsInBlockstun && WasGrounded)
		{
			string guardState = IsCrouchBlocking ? CurrentCrouchingGuardStateName : CurrentStandingGuardStateName;
			if (Definition?.StateBoxes?.FindStateRule(guardState) != null)
				nextState = guardState;
		}
		else if (IsInBlockstun && !WasGrounded &&
			Definition?.StateBoxes?.FindStateRule(CurrentAirGuardStateName) != null)
		{
			nextState = CurrentAirGuardStateName;
		}
		else if (HitstunFramesLeft > 0 && CurrentBlowAwayDirection != BlowAwayDirection.None)
		{
			string blowAwayState = ResolveBlowAwayStateName(CurrentBlowAwayDirection,
				CurrentBlowAwayStrength, CurrentBlowAwayNoBounce);
			if (Definition?.StateBoxes?.FindStateRule(blowAwayState) != null)
				nextState = blowAwayState;
		}
		else if (HitstunFramesLeft > 0 && HitState == FighterHitState.Stumble &&
			Definition?.StateBoxes?.FindStateRule("STATE [ヒット]躓き") != null)
		{
			nextState = "STATE [ヒット]躓き";
		}
		else if (HitstunFramesLeft > 0 && HitState == FighterHitState.HitFall &&
			Definition?.StateBoxes?.FindStateRule("STATE [やられ]ヒット落下") != null)
		{
			nextState = "STATE [やられ]ヒット落下";
		}
		else if (HitstunFramesLeft > 0 && HitState is FighterHitState.WallBounce or FighterHitState.WallSplat &&
			Definition?.StateBoxes?.FindStateRule(CurrentWallBounceStateName) != null)
		{
			nextState = CurrentWallBounceStateName;
		}
		else if (HitstunFramesLeft > 0 && HitState == FighterHitState.GroundBounce &&
			Definition?.StateBoxes?.FindStateRule(CurrentGroundBounceStateName) != null)
		{
			nextState = CurrentGroundBounceStateName;
		}
		else if (HitstunFramesLeft > 0 && HitState == FighterHitState.GroundedKnockdown &&
			Definition?.StateBoxes?.FindStateRule("STATE [やられ]ダウン") != null)
		{
			nextState = "STATE [やられ]ダウン";
		}
		else if (HitstunFramesLeft > 0 && HitReactionStartedCrouching &&
			HitState is FighterHitState.Hitstun or FighterHitState.CounterHit &&
			Definition?.StateBoxes?.FindStateRule("STATE CROUCH HITSTUN") != null)
		{
			nextState = "STATE CROUCH HITSTUN";
		}
		else if (IsInAirAttackLanding &&
			Definition?.StateBoxes?.FindStateRule("STATE AIR ATTACK LANDING") != null)
		{
			nextState = "STATE AIR ATTACK LANDING";
		}
		else if (IsInFlightLanding &&
			Definition?.StateBoxes?.FindStateRule("STATE FLIGHT LANDING") != null)
		{
			nextState = "STATE FLIGHT LANDING";
		}
		else if (!IsAttacking && HitstunFramesLeft <= 0 && _wakeupFramesLeft <= 0)
		{
			if (!WasGrounded)
			{
				if (Velocity.Y < 0f)
				{
					string directionalJumpState = IsInSuperJumpRoute
						? SuperJumpPresentationDirection > 0
							? "STATE SUPER JUMP FORWARD"
							: SuperJumpPresentationDirection < 0
								? "STATE SUPER JUMP BACKWARD"
								: "STATE SUPER JUMP NEUTRAL"
						: Velocity.X * Facing > 25f
							? "STATE JUMP FORWARD"
							: Velocity.X * Facing < -25f
								? "STATE JUMP BACK"
								: "STATE JUMP RISE";
					nextState = Definition?.StateBoxes?.FindStateRule(directionalJumpState) != null
						? directionalJumpState
						: "STATE JUMP RISE";
				}
				else
					nextState = "STATE FALL";
			}
			else if (input.Vertical > 0.5f)
			{
				NormalMoveData crouchStart = Definition?.StateBoxes?.FindStateRule("STATE CROUCH START");
				if (crouchStart != null && _currentBoxStateName != "STATE CROUCH")
				{
					int transitionTicks = Mathf.Max(1,
						Mathf.Max(0, crouchStart.StartupFrames) + Mathf.Max(0, crouchStart.ActiveFrames) +
						Mathf.Max(0, crouchStart.RecoveryFrames));
					nextState = _currentBoxStateName == "STATE CROUCH START" &&
						_currentBoxStateFrame + 1 >= transitionTicks
						? "STATE CROUCH"
						: "STATE CROUCH START";
				}
				else
					nextState = "STATE CROUCH";
			}
			else
			{
				NormalMoveData crouchEnd = Definition?.StateBoxes?.FindStateRule("STATE CROUCH END");
				bool leavingCrouchState = _currentBoxStateName is "STATE CROUCH START" or "STATE CROUCH" or "STATE CROUCH END";
				if (crouchEnd != null && leavingCrouchState)
				{
					int transitionTicks = Mathf.Max(1,
						Mathf.Max(0, crouchEnd.StartupFrames) + Mathf.Max(0, crouchEnd.ActiveFrames) +
						Mathf.Max(0, crouchEnd.RecoveryFrames));
					if (_currentBoxStateName != "STATE CROUCH END" || _currentBoxStateFrame + 1 < transitionTicks)
						nextState = "STATE CROUCH END";
				}
				if (nextState == "")
				{
					float movementDirection = Mathf.Abs(input.Horizontal) > 0.1f ? input.Horizontal : Velocity.X;
					if (Mathf.Abs(movementDirection) > 0.1f)
						nextState = movementDirection * Facing < 0f ? "STATE WALK BACK" : "STATE WALK FORWARD";
					else if (ActiveAbility == null)
						nextState = "STATE IDLE";
				}
			}
		}
		if (nextState != _currentBoxStateName)
		{
			_currentBoxStateName = nextState;
			_currentBoxStateFrame = 0;
			return;
		}
		if (nextState == "") return;
		NormalMoveData state = Definition?.StateBoxes?.FindStateRule(nextState);
		int total = state == null ? 1 : Mathf.Max(1,
			Mathf.Max(0, state.StartupFrames) + Mathf.Max(0, state.ActiveFrames) + Mathf.Max(0, state.RecoveryFrames));
		_currentBoxStateFrame = (_currentBoxStateFrame + 1) % total;
	}

	private string GetPressedBasicAttackName(FighterInput input)
	{
		_pendingReusableMotion = null;
		_pendingReusableMotionCompletion = -1;
		_pendingReusableMotionAttackName = "";
		if (IsAttacking && input.LightPunchPressed &&
			!string.IsNullOrWhiteSpace(_currentMoveRule.RepeatLightPunchChainTarget))
			return _currentMoveRule.RepeatLightPunchChainTarget;
		if (_startingBlockReflector) return BlockReflectorName;
		if (TryGetReusableMotionAttack(input, out string reusableAttackName, out MotionInputBinding reusableBinding,
			out long reusableCompletion))
		{
			_pendingReusableMotion = reusableBinding.Motion;
			_pendingReusableMotionCompletion = reusableCompletion;
			_pendingReusableMotionAttackName = reusableAttackName;
			_pendingReusableMotionConsumes = reusableBinding.ConsumeOnUse;
			return reusableAttackName;
		}
		// Sanzou's character buttons are isolated from the arena clone prototype:
		// O is his regular SPD and L is his standing-block parry.
		if (input.Special2Pressed && WasGrounded &&
			Definition?.SpecialMoves?.FindMove(SanzoParryName, false, false)?.Parry == true)
			return SanzoParryName;
		if (input.Special1Pressed && WasGrounded &&
			Definition?.SpecialMoves?.FindMove(SanzoSpdName, false, false) != null)
			return SanzoSpdName;
		if (IsAttacking && (CurrentAttackName == CommandRunLightName || CurrentAttackName == CommandRunHeavyName))
		{
			if (CurrentInput.HeavyPunchPressed) return CommandRunPunchName;
			if (CurrentInput.LightPunchPressed) return CommandRunHopName;
		}
		if (_motionInputBuffer.HasChargedBackForwardCommand && WasGrounded)
		{
			if (input.HeavyPunchPressed) return CommandRunHeavyName;
			if (input.LightPunchPressed) return CommandRunLightName;
		}
		if (_motionInputBuffer.HasChargedDownUpCommand && (input.LightKickPressed || input.HeavyKickPressed))
			return StompSpecialName;
		// Throw is temporarily assigned to a fresh LP+LK chord. Buffered normals from
		// a previous attack cannot turn into a throw after recovery ends.
		if (CurrentInput.LightPunchPressed && CurrentInput.LightKickPressed && CanAttemptDirectionalThrow())
		{
			if (input.Horizontal * Facing < -0.5f &&
				Definition?.NormalMoves?.FindRule(BackThrowAttackName, false, false) != null)
				return BackThrowAttackName;
			Vector2 throwSeparation = _opponent.GlobalPosition - GlobalPosition;
			if (input.Horizontal * throwSeparation.X > 0f) return ThrowAttackName;
		}
		if (IsAttacking && (CurrentAttackName == LightProjectileName || CurrentAttackName == HeavyProjectileName ||
			CurrentAttackName == QcfPowerPunchLightName || CurrentAttackName == QcfPowerPunchHeavyName) &&
			(CurrentInput.LightPunchPressed || CurrentInput.HeavyPunchPressed))
			return QcfPowerPunchRekkaName;
		bool hasQuarterCircleForward = _motionInputBuffer.HasQuarterCircleForwardCommand;
		bool punchSuperChord = input.LightPunchPressed && input.HeavyPunchPressed;
		bool kickSuperChord = input.LightKickPressed && input.HeavyKickPressed;
		if (hasQuarterCircleForward && punchSuperChord && IsOnFloor())
			return GetSuperMoveData(SanzoSuperSpdName) != null ? SanzoSuperSpdName : SuperRushName;
		if (hasQuarterCircleForward && kickSuperChord)
			return GetSuperMoveData(SanzoSuperReflectorName) != null ? SanzoSuperReflectorName : SuperFireballName;
		// Down-forward LP is an authored low launcher. Resolve it before the
		// QCF chord grace period so the diagonal cannot be stolen by the latch.
		if (input.LightPunchPressed && WasGrounded && input.Vertical > 0.5f && input.Horizontal * Facing > 0.5f)
			return CrouchingMediumJabName;
		// Give near-simultaneous attack buttons a brief chance to become a super
		// chord before resolving QCF+LP/HP as a projectile or a kick as a normal.
		if (hasQuarterCircleForward && _motionInputBuffer.QuarterCircleForwardCommandAgeFrames < SuperChordGraceFrames &&
			(input.LightPunchPressed || input.HeavyPunchPressed || input.LightKickPressed || input.HeavyKickPressed))
			return "";
		// Command moves outrank directional normals. QCF commonly ends while the
		// player is still holding forward or down-forward, which must not turn
		// QCF+HP into forward HP/crouching HP.
		if (input.LightPunchPressed && CanUseMotionSpecialCommand())
			return Definition?.SpecialMoves?.FindMove(QcfPowerPunchLightName, false, false) != null
				? QcfPowerPunchLightName : LightProjectileName;
		if (input.HeavyPunchPressed && CanUseMotionSpecialCommand())
			return Definition?.SpecialMoves?.FindMove(QcfPowerPunchHeavyName, false, false) != null
				? QcfPowerPunchHeavyName : HeavyProjectileName;
		if (input.HeavyPunchPressed && WasGrounded && input.Vertical > 0.5f && input.Horizontal * Facing > 0.5f &&
			Definition?.NormalMoves?.FindRule(DownForwardHeavyPunchName, true, false) != null)
			return DownForwardHeavyPunchName;
		if (input.HeavyPunchPressed && WasGrounded && input.Vertical > 0.5f)
			return CrouchingHeavyPunchName;
		if (input.HeavyPunchPressed && WasGrounded && input.Horizontal * Facing > 0.5f &&
			Definition?.NormalMoves?.FindRule(ForwardHeavyPunchName, true, false) != null)
			return ForwardHeavyPunchName;
		if (input.LightPunchPressed && !WasGrounded && input.Horizontal * Facing < -0.5f &&
			Definition?.NormalMoves?.FindRule(AirBackLightPunchName, false, true) != null)
			return AirBackLightPunchName;
		if (input.LightPunchPressed && WasGrounded && input.Horizontal * Facing < -0.5f) return BackLightPunchName;
		if (input.LightPunchPressed) return "LIGHT PUNCH";
		if (input.LightKickPressed && WasGrounded && input.Vertical > 0.5f && input.Horizontal * Facing < -0.5f &&
			Definition?.NormalMoves?.FindRule(CrouchingMediumKickName, true, false) != null)
			return CrouchingMediumKickName;
		if (input.LightKickPressed && !WasGrounded && input.Horizontal * Facing < -0.5f &&
			Definition?.NormalMoves?.FindRule(AirBackLightKickName, false, true) != null)
			return AirBackLightKickName;
		if (input.LightKickPressed && WasGrounded && input.Horizontal * Facing > 0.5f) return ForwardLightKickName;
		if (input.LightKickPressed) return "LIGHT KICK";
		if (input.HeavyPunchPressed && !WasGrounded) return AirHeavyPunchName;
		if (input.HeavyPunchPressed) return "HEAVY PUNCH";
		if (input.HeavyKickPressed && WasGrounded && input.Vertical > 0.5f) return CrouchingHeavyKickName;
		if (input.HeavyKickPressed && WasGrounded && input.Horizontal * Facing > 0.5f &&
			Definition?.NormalMoves?.FindRule(ForwardHeavyKickName, false, false) != null)
			return ForwardHeavyKickName;
		if (input.HeavyKickPressed) return "HEAVY KICK";
		return "";
	}

	private bool TryGetReusableMotionAttack(FighterInput input, out string attackName,
		out MotionInputBinding matchedBinding, out long completionFrame)
	{
		attackName = "";
		matchedBinding = null;
		completionFrame = -1;
		int bestPriority = int.MinValue;
		bool startedCrouching = WasGrounded && input.Vertical > 0.5f;
		bool startedAirborne = !WasGrounded;

		foreach (SuperMoveData move in Definition?.SuperMoves ?? Array.Empty<SuperMoveData>())
		{
			MotionInputBinding binding = move?.CommandInput;
			if (!CanUseMotionBinding(binding) ||
				!_motionInputBuffer.TryMatchReusableMotion(binding, input, out long candidateCompletion)) continue;
			int priority = 10000 + binding.Priority;
			if (priority <= bestPriority) continue;
			bestPriority = priority;
			attackName = move.AttackName;
			matchedBinding = binding;
			completionFrame = candidateCompletion;
		}

		foreach (SpecialMoveData move in Definition?.SpecialMoves?.Moves ?? Array.Empty<SpecialMoveData>())
		{
			MotionInputBinding binding = move?.CommandInput;
			if (move == null || !move.Matches(move.AttackName, startedCrouching, startedAirborne) ||
				!CanUseMotionBinding(binding) ||
				!_motionInputBuffer.TryMatchReusableMotion(binding, input, out long candidateCompletion)) continue;
			if (binding.Priority <= bestPriority) continue;
			bestPriority = binding.Priority;
			attackName = move.AttackName;
			matchedBinding = binding;
			completionFrame = candidateCompletion;
		}
		return matchedBinding != null && !string.IsNullOrEmpty(attackName);
	}

	private bool CanUseMotionBinding(MotionInputBinding binding)
	{
		if (binding?.Motion == null) return false;
		if (binding.GroundOnly && !WasGrounded) return false;
		if (binding.AirOnly && WasGrounded) return false;
		return !(binding.GroundOnly && binding.AirOnly);
	}

	public bool ConsumeSuperActivationFreezeRequest()
	{
		if (!SuperActivationFreezeRequested) return false;
		SuperActivationFreezeRequested = false;
		SuperActivationFreezeFramesRequested = 0;
		return true;
	}

	public bool ConsumeSuperBackdropCancelRequest()
	{
		if (!_superBackdropCancelRequested) return false;
		_superBackdropCancelRequested = false;
		return true;
	}

	public int ConsumeSuperActivationFreezeFrames()
	{
		int frames = SuperActivationFreezeFramesRequested;
		SuperActivationFreezeRequested = false;
		SuperActivationFreezeFramesRequested = 0;
		return frames;
	}

	public bool ConsumeSuperActivationData(out int freezeFrames, out int backdropFrames)
	{
		freezeFrames = SuperActivationFreezeFramesRequested;
		backdropFrames = SuperBackdropFramesRequested;
		bool requested = SuperActivationFreezeRequested || freezeFrames > 0 || backdropFrames > 0;
		SuperActivationFreezeRequested = false;
		SuperActivationFreezeFramesRequested = 0;
		SuperBackdropFramesRequested = 0;
		return requested;
	}

	private void ConfirmCurrentSuper(FighterController defender)
	{
		if (_currentSuperMove == null || _currentSuperConfirmed) return;
		_currentSuperConfirmed = true;
		_currentSuperConfirmedFrame = CurrentAttackFrame;
		if (_currentSuperMove.StopRushOnFirstHit) Velocity = new Vector2(0f, Velocity.Y);
		if (CurrentAttackName == SuperRushName && defender != null)
		{
			float closeX = defender.GlobalPosition.X + _currentSuperMove.ConfirmedAttackerOffsetFromDefender.X * Facing;
			GlobalPosition = new Vector2(closeX, GlobalPosition.Y);
		}
		if (_currentSuperMove.RequiresHitConfirmForMultiHit)
			_attackActiveFramesLeft = Mathf.Max(_attackActiveFramesLeft, _currentSuperMove.ConfirmedActiveFrames);
		if (_currentSuperMove.LockPositionsDuringConfirmedHits && defender != null)
		{
			_currentSuperLockedDefender = defender;
			_currentSuperLockedDefenderPosition = defender.GlobalPosition;
			_currentSuperLockedAttackerOffset = new Vector2(_currentSuperMove.ConfirmedAttackerOffsetFromDefender.X * Facing,
				_currentSuperMove.ConfirmedAttackerOffsetFromDefender.Y);
			MaintainSuperHitLock();
		}
	}

	/// <summary>Future block logic can call this when the opening Super Rush hit is guarded.</summary>
	public void ResolveBlockedSuperRush()
	{
		if (CurrentAttackName != SuperRushName || _currentSuperConfirmed) return;
		Velocity = new Vector2(-Facing * 420f, Velocity.Y);
		_attackStartupFramesLeft = 0;
		_attackActiveFramesLeft = 0;
		_attackRecoveryFramesLeft = 30;
		_currentAttackRecoveryFrames = 30;
		_currentAttackHitCooldownFramesLeft = 0;
	}

	public void MaintainSuperHitLock()
	{
		if (_currentSuperMove?.LockPositionsDuringConfirmedHits != true || !_currentSuperConfirmed || _currentAttackHitsRemaining <= 0) return;
		if (!GodotObject.IsInstanceValid(_currentSuperLockedDefender)) return;

		_currentSuperLockedDefender.GlobalPosition = _currentSuperLockedDefenderPosition;
		_currentSuperLockedDefender.Velocity = new Vector2(0f, _currentSuperLockedDefender.Velocity.Y);
		GlobalPosition = _currentSuperLockedDefenderPosition + _currentSuperLockedAttackerOffset;
		Velocity = new Vector2(0f, Velocity.Y);
	}

	private bool CanUseMotionSpecialCommand()
	{
		if (!_motionInputBuffer.HasMotionSpecialCommand) return false;
		if (_motionInputBuffer.FramesSinceJumpPress > QuarterCircleForwardLatchFrames) return true;
		return _motionInputBuffer.MotionSpecialCommandAgeFrames <= UpInputMotionSpecialStrictWindowFrames;
	}

	private static bool IsSpecialAttackName(string attackName) =>
		attackName.StartsWith("SPECIAL") || attackName == ElectricWindGodFistName || attackName == QcfPowerPunchRekkaName ||
		attackName == QcfPowerPunchLightName || attackName == QcfPowerPunchHeavyName ||
		attackName == BlockReflectorName || attackName == SanzoParryName || attackName == StompSpecialName || IsSpdGrabAttackName(attackName) ||
		attackName == CommandRunLightName || attackName == CommandRunHeavyName ||
		attackName == CommandRunHopName || attackName == CommandRunPunchName ||
		IsSuperAttackName(attackName) || IsProjectileAttackName(attackName);

	private static bool IsProjectileAttackName(string attackName) =>
		attackName == LightProjectileName || attackName == HeavyProjectileName || attackName == SuperFireballName ||
		attackName == SanzoSuperReflectorName;

	private static bool IsSuperAttackName(string attackName) =>
		attackName.StartsWith("SUPER") || attackName == SuperFireballName || attackName == SuperRushName ||
		attackName == SanzoSuperReflectorName || attackName == SanzoSuperSpdName;

	private static bool IsSpdGrabAttackName(string attackName) =>
		attackName == SanzoSpdName || attackName == SanzoSuperSpdName;

	private static bool IsRegularThrowAttackName(string attackName) =>
		attackName == ThrowAttackName || attackName == BackThrowAttackName;

	public static bool IsNormalAttackName(string attackName) =>
		attackName == "LIGHT PUNCH" || attackName == "LIGHT KICK" ||
		attackName == "HEAVY PUNCH" || attackName == "HEAVY KICK" || attackName == CrouchingMediumJabName || attackName == DownForwardHeavyPunchName ||
		IsRegularThrowAttackName(attackName) || attackName == ForwardHeavyPunchName || attackName == ForwardLightKickName || attackName == ForwardHeavyKickName || attackName == BackLightPunchName ||
		attackName == CrouchingMediumKickName || attackName == AirBackLightPunchName || attackName == AirBackLightKickName ||
		attackName == CrouchingHeavyKickName || attackName == CrouchingHeavyPunchName ||
		attackName == AirHeavyPunchName;

	private bool IsCurrentAttackHeavyNormal() =>
		_currentSpecialMove == null && _currentSuperMove == null &&
		IsNormalAttackName(CurrentAttackName) && CurrentAttackName.Contains("HEAVY");

	private bool IsCurrentAttackLightNormal() =>
		CurrentAttackIsLightNormal;

	private bool CanAttemptDirectionalThrow()
	{
		if (IsAttacking || HitState != FighterHitState.None || !WasGrounded || ActiveAbility != null ||
			!GodotObject.IsInstanceValid(_opponent) || !_opponent.WasGrounded || _opponent.HitState != FighterHitState.None)
			return false;
		Vector2 separation = _opponent.GlobalPosition - GlobalPosition;
		return Mathf.Abs(CurrentInput.Horizontal) > 0.5f &&
			Mathf.Abs(separation.X) <= DirectionalThrowRange && Mathf.Abs(separation.Y) <= 100f;
	}

	private static bool HasPressedBasicAttack(FighterInput input) =>
		input.LightPunchPressed || input.LightKickPressed || input.HeavyPunchPressed || input.HeavyKickPressed ||
		input.Special1Pressed || input.Special2Pressed;

	private int GetBasicAttackRecoveryFrames(string attackName)
	{
		if (_currentMoveData?.RecoveryFrames >= 0) return _currentMoveData.RecoveryFrames;
		if (attackName == ThrowAttackName) return 15;
		if (attackName == ForwardHeavyPunchName) return 2;
		if (attackName == AirHeavyPunchName) return 4;
		if (attackName == CrouchingMediumJabName) return 6;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.RecoveryFrames;
		if (attackName == ElectricWindGodFistName) return 20;
		if (attackName == SuperFireballName) return 28;
		if (IsProjectileAttackName(attackName)) return SpecialAttackRecoveryFrames;
		if (attackName.StartsWith("LIGHT")) return _currentAttackStartedAirborne ? LightAttackRecoveryFrames : GroundLightAttackRecoveryFrames;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackRecoveryFrames;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackRecoveryFrames;
		return BasicAttackRecoveryFrames;
	}

	private int GetBasicAttackActiveFrames(string attackName)
	{
		if (_currentMoveData?.ActiveFrames >= 0) return _currentMoveData.ActiveFrames;
		if (attackName == ThrowAttackName) return 1;
		if (attackName == ForwardHeavyPunchName) return 4;
		if (attackName == CrouchingHeavyPunchName) return 10;
		if (attackName == AirHeavyPunchName) return 2;
		if (attackName == ForwardLightKickName) return LightKickActiveFrames;
		if (attackName == CrouchingHeavyKickName) return HeavyKickActiveFrames;
		if (attackName == CrouchingMediumJabName) return 2;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.ActiveFrames;
		if (attackName == ElectricWindGodFistName) return 4;
		if (attackName == SuperFireballName) return 2;
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
		if (_currentMoveData?.StartupFrames >= 0) return _currentMoveData.StartupFrames;
		if (attackName == ThrowAttackName) return 10;
		if (attackName == ForwardHeavyPunchName) return 8;
		if (attackName == AirHeavyPunchName) return 4;
		if (attackName == CrouchingMediumJabName) return 4;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.StartupFrames;
		if (attackName == ElectricWindGodFistName) return 5;
		if (attackName == SuperFireballName) return 12;
		if (IsProjectileAttackName(attackName)) return ProjectileAttackStartupFrames;
		if (attackName.StartsWith("LIGHT")) return _currentAttackStartedAirborne ? LightAttackStartupFrames : GroundLightAttackStartupFrames;
		return BasicAttackStartupFrames;
	}

	private int GetBasicAttackHitstunFrames(string attackName)
	{
		if (attackName == ThrowAttackName) return HeavyAttackHitstunFrames;
		if (attackName == CrouchingMediumJabName) return LightAttackHitstunFrames;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.HitstunFrames;
		if (attackName == ElectricWindGodFistName) return HeavyAttackHitstunFrames;
		if (attackName == SuperFireballName) return 8;
		if (attackName == LightProjectileName) return LightAttackHitstunFrames;
		if (attackName == HeavyProjectileName) return HeavyAttackHitstunFrames;
		if (attackName.StartsWith("LIGHT")) return LightAttackHitstunFrames;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackHitstunFrames;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackHitstunFrames;
		return BasicAttackHitstunFrames;
	}

	private float GetBasicAttackPushback(string attackName)
	{
		if (attackName == ThrowAttackName) return HeavyAttackPushback;
		if (attackName == CrouchingMediumJabName) return LightAttackPushback;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.Pushback;
		if (attackName == ElectricWindGodFistName) return 360f;
		if (attackName == SuperFireballName) return HeavyAttackPushback * 0.18f;
		if (attackName == LightProjectileName) return LightAttackPushback;
		if (attackName == HeavyProjectileName) return HeavyAttackPushback;
		if (attackName.StartsWith("LIGHT")) return LightAttackPushback;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackPushback;
		if (attackName.StartsWith("SPECIAL")) return SpecialAttackPushback;
		return BasicAttackPushback;
	}

	private int GetBasicAttackHitstopFrames(string attackName)
	{
		if (attackName == ThrowAttackName) return HeavyAttackHitstopFrames;
		if (attackName == CrouchingMediumJabName) return SpecialAttackHitstopFrames;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.HitstopFrames;
		if (attackName == ElectricWindGodFistName) return HeavyAttackHitstopFrames;
		if (attackName == SuperFireballName) return 3;
		if (attackName == LightProjectileName) return LightAttackHitstopFrames;
		if (attackName == HeavyProjectileName) return HeavyAttackHitstopFrames;
		if (attackName.StartsWith("LIGHT")) return _currentAttackStartedAirborne ? LightAttackHitstopFrames : ScaleNormalHitstop(LightAttackHitstopFrames);
		if (attackName.StartsWith("HEAVY")) return _currentAttackStartedAirborne ? HeavyAttackHitstopFrames : ScaleNormalHitstop(HeavyAttackHitstopFrames);
		return SpecialAttackHitstopFrames;
	}

	private int ScaleNormalHitstop(int frames) => Mathf.Max(1, Mathf.RoundToInt(frames * NormalAttackHitstopMultiplier));
	private int ScaleAirAttackHitstop(int frames)
	{
		if (CurrentAttackIsNormal) return Mathf.Max(1, AirNormalHitstopFrames);
		float multiplier = IsCurrentAttackLightNormal()
			? AirLightHitstopMultiplier
			: AirAttackHitstopMultiplier;
		return Mathf.Max(1, Mathf.CeilToInt(frames * Mathf.Clamp(multiplier, 0f, 1f)));
	}
	private int ScaleSpecialMoveHitstop(int frames) => _currentSpecialMove == null
		? frames
		: Mathf.Max(1, Mathf.CeilToInt(frames * Mathf.Clamp(_currentSpecialMove.ContactHitstopMultiplier, 0f, 1f)));

	private float GetBasicAttackShakeStrength(string attackName)
	{
		if (attackName == ThrowAttackName) return HeavyAttackShakeStrength;
		if (attackName == CrouchingMediumJabName) return LightAttackShakeStrength;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.ShakeStrength;
		if (attackName == ElectricWindGodFistName) return HeavyAttackShakeStrength;
		if (attackName == SuperFireballName) return 8f;
		if (attackName == LightProjectileName) return LightAttackShakeStrength;
		if (attackName == HeavyProjectileName) return HeavyAttackShakeStrength;
		if (attackName.StartsWith("LIGHT")) return LightAttackShakeStrength;
		if (attackName.StartsWith("HEAVY")) return HeavyAttackShakeStrength;
		return SpecialAttackShakeStrength;
	}

	private Rect2 GetBasicAttackHitboxLocal(string attackName)
	{
		if (attackName == ThrowAttackName) return LightPunchHitboxLocal;
		if (attackName == ForwardHeavyPunchName) return HeavyPunchHitboxLocal;
		if (attackName == CrouchingHeavyPunchName) return HeavyPunchHitboxLocal;
		if (attackName == AirHeavyPunchName) return HeavyPunchHitboxLocal;
		if (attackName == ForwardLightKickName) return LightKickHitboxLocal;
		if (attackName == CrouchingHeavyKickName) return CrouchingHeavyKickHitboxLocal;
		if (attackName == CrouchingMediumJabName) return LightPunchHitboxLocal;
		if (GetSuperMoveData(attackName) is { } superMove) return superMove.HitboxLocal;
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

	private SuperMoveData GetSuperMoveData(string attackName)
	{
		SuperMoveData configuredMove = GetConfiguredSuperMoveData(attackName);
		if (configuredMove != null) return configuredMove;

		if (attackName == SuperFireballName)
			return new SuperMoveData
			{
				AttackName = SuperFireballName,
				StartupFrames = 12,
				ActiveFrames = 2,
				RecoveryFrames = 28,
				ActivationFreezeFrames = SuperActivationFreezeFrames,
				BackdropFrames = SuperActivationFreezeFrames + 42,
				HitCount = SuperProjectileHits,
				HitIntervalFrames = SuperProjectileHitCooldownFrames,
				HitstunFrames = 8,
				HitstopFrames = 3,
				Pushback = HeavyAttackPushback * 0.18f,
				FinalHitstunFrames = 34,
				FinalHitstopFrames = 8,
				FinalPushback = HeavyAttackPushback * 0.85f,
				ShakeStrength = 8f,
				FinalShakeStrength = 11f,
				FinalHitKnocksDown = true,
				FinalKnockdownType = KnockdownType.HardKnockdown,
				FinalKnockdownFrames = 58,
				Projectile = true,
				ProjectileSpeed = SuperProjectileSpeed,
				ProjectileHitCooldownFrames = SuperProjectileHitCooldownFrames
			};
		if (attackName == SuperRushName)
			return new SuperMoveData
			{
				AttackName = SuperRushName,
				StartupFrames = 7,
				ActiveFrames = 90,
				RecoveryFrames = 24,
				ActivationFreezeFrames = SuperActivationFreezeFrames,
				BackdropFrames = SuperActivationFreezeFrames + 105,
				HitCount = 16,
				HitIntervalFrames = 4,
				HitstunFrames = 10,
				HitstopFrames = 3,
				AddsGlobalHitstopBonus = false,
				Pushback = 0f,
				FinalHitstunFrames = 72,
				FinalHitstopFrames = 26,
				FinalPushback = 2200f,
				ShakeStrength = 5f,
				FinalShakeStrength = 16f,
				HitboxLocal = Special1HitboxLocal,
				FinalHitKnocksDown = true,
				FinalKnockdownType = KnockdownType.HardKnockdown,
				FinalKnockdownFrames = 72,
				RushesForward = true,
				RushSpeed = 600f,
				StopRushOnFirstHit = true,
				RequiresHitConfirmForMultiHit = true,
				ConfirmedActiveFrames = 86,
				LockPositionsDuringConfirmedHits = false,
				ConfirmedAttackerOffsetFromDefender = new Vector2(-48f, 0f)
			};
		return null;
	}

	private SuperMoveData GetConfiguredSuperMoveData(string attackName)
	{
		if (Definition?.SuperMoves == null) return null;
		foreach (SuperMoveData move in Definition.SuperMoves)
			if (move != null && string.Equals(move.AttackName, attackName, StringComparison.OrdinalIgnoreCase))
				return move;
		return null;
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
		attackName.StartsWith("HEAVY") || attackName.StartsWith("SPECIAL") || attackName.StartsWith("SUPER") || attackName == ElectricWindGodFistName;

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

	private void ClearNormalAttackInputBuffers()
	{
		_lightPunchBufferFramesLeft = 0;
		_lightKickBufferFramesLeft = 0;
		_heavyPunchBufferFramesLeft = 0;
		_heavyKickBufferFramesLeft = 0;
		AttackBufferFramesLeft = Mathf.Max(_special1BufferFramesLeft, _special2BufferFramesLeft);
	}

	private void ApplyBaseMotion(float delta)
	{
		if (IsWakingUp)
		{
			Velocity = new Vector2(0f, Velocity.Y);
			return;
		}
		if (IsInAirAttackLanding || IsInFlightLanding)
		{
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, BasicAttackFriction * delta), Velocity.Y);
			return;
		}
		if (_pendingWallSplatKnockdown && !WasGrounded)
		{
			Velocity = new Vector2(0f, WallSplatSlideSpeed);
			if (!VisualCorrectionOffset.IsZeroApprox())
				VisualCorrectionOffset = VisualCorrectionOffset.MoveToward(Vector2.Zero, VisualCorrectionSlideSpeed * delta);
			return;
		}
		// An active ability can opt out of any of these rules by setting its flags.
		bool ownsHorizontal = ActiveAbility?.OwnsHorizontalVelocity ?? false;
		bool ownsGravity = ActiveAbility?.OwnsGravity ?? false;
		// Standard fighters commit to their jump trajectory. Exotic movement (super jumps,
		// flight, air walks) can opt in per character or take ownership in an ability.
		if (!ownsHorizontal && (WasGrounded || Definition.Tuning.AllowAirControl || EnablesAirControlWhileAirborne))
		{
			if (IsAttacking && WasGrounded)
			{
				if (_currentSuperMove?.RushesForward == true && !_currentSuperConfirmed) return;
				// A source-authored launch M command owns this takeoff tick. Applying
				// grounded attack friction here would erase part of Mecha Heita's DP
				// velocity before the body has even left the floor.
				if (_currentSpecialSelfLaunchApplied && _currentSpecialMove?.SelfLaunch == true) return;
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
			float gravityScale = HitState == FighterHitState.Juggle
				? Mathf.Min(MaxJuggleGravityScale, 1f + Mathf.Max(0, JuggleHitCount - 1) * JuggleGravityScalePerHit)
				: HitstunFramesLeft > 0
					? Mathf.Min(MaxComboGravityScale, 1f + Mathf.Max(0, ComboCount - 1) * ComboGravityScalePerHit)
					: 1f;
			Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y + Definition.Tuning.Gravity * gravityScale * delta, Definition.Tuning.TerminalFallSpeed));
		}
		if (!VisualCorrectionOffset.IsZeroApprox())
			VisualCorrectionOffset = VisualCorrectionOffset.MoveToward(Vector2.Zero, VisualCorrectionSlideSpeed * delta);
	}

	private void AdvanceVerticalPhysicsDuringHitstop(float delta)
	{
		float preservedHorizontalVelocity = Velocity.X;
		// A jump-in connecting with a grounded opponent should not erase the
		// attacker's arc. Carry most horizontal travel through contact freeze,
		// then restore the original velocity for the remaining descent.
		Velocity = new Vector2(preservedHorizontalVelocity * AirToGroundHitstopMomentumScale, Mathf.Min(
			Velocity.Y + Definition.Tuning.Gravity * delta,
			Definition.Tuning.TerminalFallSpeed));
		MoveAndSlide();
		Velocity = new Vector2(preservedHorizontalVelocity, Velocity.Y);
		if (!IsOnFloor()) return;

		JustLanded = true;
		if (HitstunFramesLeft <= 0 && _airNormalPerformedSinceTakeoff)
		{
			BeginAirAttackLanding();
			_airNormalPerformedSinceTakeoff = false;
		}
		if (IsAttacking && _currentAttackStartedAirborne)
			ClearAttackState();
	}

	private void ResetAirResources()
	{
		_flightUsedThisAirTime = false;
		SuppressesGroundedPushWhileAirborne = false;
		EnablesAirControlWhileAirborne = false;
		AirDecelerationMultiplierWhileAirborne = 1f;
		AirActionsUsed = 0;
		AirActionsRequirePeakThisJump = false;
		AirJumpsDisabledThisJump = false;
		IsInSuperJumpRoute = false;
		SuperJumpPresentationDirection = 0;
		IsInDoubleJumpState = false;
		_doubleJumpAirDashAvailable = false;
		ShortHopInteractsWithGroundedPushbox = false;
		ShortHopPushesGroundedOpponent = false;
		IsInShortHopRoute = false;
		JumpInteractsWithGroundedPushbox = false;
		JumpGroundedPushStrength = 0f;
		_pendingLandingLagFrames = 0;
		_airNormalPerformedSinceTakeoff = false;
		_airJumpUses.Clear();
		_normalUsesThisAirTime.Clear();
		foreach (var runtime in Runtime.Values)
		{
			runtime.UsesThisAirTime = 0;
			runtime.IntValue = 0;
			runtime.IntValue2 = 0;
			runtime.FloatValue = 0;
			runtime.BoolValue = false;
			runtime.VectorValue = Vector2.Zero;
		}
	}

	public IEnumerable<Rect2> GetActiveLocalBoxes(FighterBoxKind kind)
	{
		foreach (ActiveFighterBox box in GetActiveLocalBoxInstances(kind))
			yield return box.Rect;
	}

	public IEnumerable<ActiveFighterBox> GetActiveLocalBoxInstances(FighterBoxKind kind)
	{
		if (!ParticipatesInPointCollision && (kind == FighterBoxKind.Hurtbox || kind == FighterBoxKind.Pushbox))
			yield break;
		if (kind == FighterBoxKind.Hitbox && !IsAttackActive) yield break;
		NormalMoveData stateData = GetCurrentStateBoxData();
		bool hasActiveStateBox = HasActiveStateBox(stateData, kind);
		bool hasActiveTimelineBox = HasActiveTimelineBox(kind) || hasActiveStateBox;
		bool hasReplacingBox = HasActiveReplacingTimelineBox(kind) || HasActiveReplacingStateBox(stateData, kind);
		if (!hasActiveTimelineBox)
		{
			if (kind == FighterBoxKind.Hurtbox)
				yield return new ActiveFighterBox(HurtboxLocal);
			else if (kind == FighterBoxKind.Pushbox)
				yield return new ActiveFighterBox(ActivePushboxLocal);
			else if (kind == FighterBoxKind.Hitbox && IsAttackActive && !_currentMoveRule.SuppressFallbackHitbox)
				yield return new ActiveFighterBox(GetFacingLocalBox(_currentAttackHitboxLocal, true));
		}

		if (IsAttacking && _currentMoveRule.BoxTimeline != null)
		{
			foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			{
				if (box == null || box.Kind != kind) continue;
				if (box.IsActiveOnFrame(CurrentAttackFrame) && (!hasReplacingBox || box.ReplacesSameKindWhileActive))
					yield return new ActiveFighterBox(GetFacingLocalBox(box.LocalRect, box.MirrorWithFacing), box);
			}
		}
		if (!IsAttacking && stateData?.BoxTimeline != null)
		{
			foreach (FighterBoxFrame box in stateData.BoxTimeline)
			{
				if (box == null || box.Kind != kind || !box.IsActiveOnFrame(_currentBoxStateFrame)) continue;
				if (!hasReplacingBox || box.ReplacesSameKindWhileActive)
					yield return new ActiveFighterBox(GetFacingLocalBox(box.LocalRect, box.MirrorWithFacing), box);
			}
		}
	}

	public IEnumerable<Rect2> GetActiveWorldBoxes(FighterBoxKind kind)
	{
		foreach (ActiveFighterBox box in GetActiveWorldBoxInstances(kind))
			yield return box.Rect;
	}

	public IEnumerable<ActiveFighterBox> GetActiveWorldBoxInstances(FighterBoxKind kind)
	{
		if (!ParticipatesInPointCollision && (kind == FighterBoxKind.Hurtbox || kind == FighterBoxKind.Pushbox))
			yield break;
		if (kind == FighterBoxKind.Hitbox && !IsAttackActive) yield break;
		NormalMoveData stateData = GetCurrentStateBoxData();
		bool hasActiveStateBox = HasActiveStateBox(stateData, kind);
		bool hasActiveTimelineBox = HasActiveTimelineBox(kind) || hasActiveStateBox;
		bool hasReplacingBox = HasActiveReplacingTimelineBox(kind) || HasActiveReplacingStateBox(stateData, kind);
		if (!hasActiveTimelineBox)
		{
			if (kind == FighterBoxKind.Hurtbox)
				yield return new ActiveFighterBox(GetWorldFacingBox(HurtboxLocal, false));
			else if (kind == FighterBoxKind.Pushbox)
				yield return new ActiveFighterBox(new Rect2(GlobalPosition + ActivePushboxLocal.Position, ActivePushboxLocal.Size));
			else if (kind == FighterBoxKind.Hitbox && IsAttackActive && !_currentMoveRule.SuppressFallbackHitbox)
				yield return new ActiveFighterBox(GetWorldFacingBox(_currentAttackHitboxLocal, true));
		}

		if (IsAttacking && _currentMoveRule.BoxTimeline != null)
		{
			foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			{
				if (box == null || box.Kind != kind) continue;
				if (box.IsActiveOnFrame(CurrentAttackFrame) && (!hasReplacingBox || box.ReplacesSameKindWhileActive))
					yield return new ActiveFighterBox(GetWorldFacingBox(box.LocalRect, box.MirrorWithFacing), box);
			}
		}
		if (!IsAttacking && stateData?.BoxTimeline != null)
		{
			foreach (FighterBoxFrame box in stateData.BoxTimeline)
			{
				if (box == null || box.Kind != kind || !box.IsActiveOnFrame(_currentBoxStateFrame)) continue;
				if (!hasReplacingBox || box.ReplacesSameKindWhileActive)
					yield return new ActiveFighterBox(GetWorldFacingBox(box.LocalRect, box.MirrorWithFacing), box);
			}
		}
	}

	private NormalMoveData GetCurrentStateBoxData() => string.IsNullOrEmpty(_currentBoxStateName)
		? null : Definition?.StateBoxes?.FindStateRule(_currentBoxStateName);

	private bool HasActiveStateBox(NormalMoveData state, FighterBoxKind kind)
	{
		if (IsAttacking || state?.BoxTimeline == null) return false;
		foreach (FighterBoxFrame box in state.BoxTimeline)
			if (box != null && box.Kind == kind && box.IsActiveOnFrame(_currentBoxStateFrame)) return true;
		return false;
	}

	private bool HasActiveReplacingStateBox(NormalMoveData state, FighterBoxKind kind)
	{
		if (IsAttacking || state?.BoxTimeline == null) return false;
		foreach (FighterBoxFrame box in state.BoxTimeline)
			if (box != null && box.Kind == kind && box.ReplacesSameKindWhileActive && box.IsActiveOnFrame(_currentBoxStateFrame)) return true;
		return false;
	}

	private bool HasActiveReplacingTimelineBox(FighterBoxKind kind)
	{
		if (!IsAttacking || _currentMoveRule.BoxTimeline == null) return false;
		foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			if (box != null && box.Kind == kind && box.ReplacesSameKindWhileActive && box.IsActiveOnFrame(CurrentAttackFrame))
				return true;
		return false;
	}

	private bool HasActiveTimelineBox(FighterBoxKind kind)
	{
		if (!IsAttacking || _currentMoveRule.BoxTimeline == null) return false;
		foreach (FighterBoxFrame box in _currentMoveRule.BoxTimeline)
			if (box != null && box.Kind == kind && box.IsActiveOnFrame(CurrentAttackFrame)) return true;
		return false;
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

	private Rect2 GetCombinedActiveWorldBox(FighterBoxKind kind, Rect2 fallback, bool mirrorFallback)
	{
		bool found = false;
		Rect2 combined = default;
		foreach (Rect2 box in GetActiveWorldBoxes(kind))
		{
			combined = found ? MergeRects(combined, box) : box;
			found = true;
		}
		return found ? combined : GetWorldFacingBox(fallback, mirrorFallback);
	}

	private static Rect2 MergeRects(Rect2 first, Rect2 second)
	{
		float left = Mathf.Min(first.Position.X, second.Position.X);
		float top = Mathf.Min(first.Position.Y, second.Position.Y);
		float right = Mathf.Max(first.End.X, second.End.X);
		float bottom = Mathf.Max(first.End.Y, second.End.Y);
		return new Rect2(left, top, right - left, bottom - top);
	}

	private void EnsureCollisionPolicy()
	{
		if (!FighterCollisionPolicy.IsNormalized(this)) FighterCollisionPolicy.Apply(this);
	}

	private static bool TryFindBoxContact(IEnumerable<ActiveFighterBox> attackerBoxes, IEnumerable<ActiveFighterBox> defenderBoxes,
		out Vector2 hitPoint, out ActiveFighterBox attackerBox, out ActiveFighterBox defenderBox)
	{
		foreach (ActiveFighterBox attackBox in attackerBoxes)
		{
			foreach (ActiveFighterBox hurtBox in defenderBoxes)
			{
				if (!attackBox.Rect.Intersects(hurtBox.Rect)) continue;
				if (!attackBox.CanInteractWith(hurtBox)) continue;
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
