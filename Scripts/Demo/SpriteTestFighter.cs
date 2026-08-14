using System.Collections.Generic;
using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Demo;

/// <summary>Minimal AnimatedSprite2D presentation layer for testing imported sprite animations.</summary>
public partial class SpriteTestFighter : FighterController
{
	[Export] public AnimatedSprite2D CharacterSprite { get; set; }
	[ExportGroup("Heavy Walk Presentation")]
	[Export] public bool HeavyWalkFootstepShake { get; set; }
	[Export] public float HeavyWalkShakeStrength { get; set; } = 1.1f;
	[Export] public int HeavyWalkShakeFrames { get; set; } = 3;
	[Export] public bool HeavyLandingShake { get; set; }
	[Export] public float HeavyLandingShakeStrength { get; set; } = 4.25f;
	[Export] public int HeavyLandingShakeFrames { get; set; } = 6;
	[ExportGroup("Selected Move Presentation")]
	[Export(PropertyHint.Range, "0.1,1.0,0.01")]
	public float SweepAndSpdVisualScale { get; set; } = 1f;
	[Export] public float AuthoredSpriteFloorOffset { get; set; } = 58f;
	private bool _forwardJumpIntroStarted;
	private bool _crouchIntroStarted;
	private bool _crouchExitStarted;
	private bool _boosterWasActive;
	private bool _awaitingFlightLanding;
	private bool _awaitingJetEscapeLanding;
	private bool _wasVisuallyAttacking;
	private string _lastVisualAttackName = "";
	private int _lastVisualAttackFrame = -1;
	private bool _superOneVisualActive;
	private ulong _lastVisualHitReactionSerial;
	private ulong _lastVisualBlockReactionSerial;
	private StringName _lastFootstepAnimation;
	private int _lastFootstepDrawing = -1;
	private Vector2 _baseCharacterSpritePosition;
	private Vector2 _baseCharacterSpriteScale = Vector2.One;
	private bool _capturedCharacterSpriteTransform;
	private static readonly int[] SuperAfterimageDelays = { 5, 10, 15, 20 };
	private readonly List<SuperShadowSample> _superShadowHistory = new();
	private readonly List<Sprite2D> _superAfterimages = new();
	private Sprite2D _electricityGreyBody;
	private int _burnSilhouetteFramesLeft;
	private Color _burnRestoreSelfModulate = Colors.White;
	private int _burnFlameSerial;
	private readonly List<AnimatedSprite2D> _activeBurnFlames = new();
	private static readonly StringName[] SuperOneAttackCycle =
	{
		"heavy_punch", "standing_light_kick", "light_punch", "standing_heavy_kick",
		"crouching_light_punch", "forward_heavy_punch", "crouching_light_kick", "forward_light_kick"
	};

	public override void _Ready()
	{
		CharacterSprite ??= GetNodeOrNull<AnimatedSprite2D>("CharacterSprite");
		if (CharacterSprite != null)
		{
			_baseCharacterSpritePosition = CharacterSprite.Position;
			_baseCharacterSpriteScale = CharacterSprite.Scale;
			_capturedCharacterSpriteTransform = true;
			CharacterSprite.AnimationFinished += OnCharacterAnimationFinished;
		}
		UpdateAnimation();
		ApplySelectedMoveVisualScale();
		UpdateElectricityGreyBodyFlash();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateAnimation();
		ApplySelectedMoveVisualScale();
		UpdateElectricityGreyBodyFlash();
		UpdateHeavyWalkFootsteps();
		UpdateHeavyLandingShake();
		UpdateSuperShadows();
		UpdateBurnHitPresentation();
	}

	protected override void OnMoveContactBurnVisual(bool blackenDefender, int silhouetteFrames,
		SpriteFrames fireFrames, string fireAnimationName)
	{
		if (CharacterSprite == null) return;
		if (blackenDefender && silhouetteFrames > 0)
		{
			if (_burnSilhouetteFramesLeft <= 0)
				_burnRestoreSelfModulate = CharacterSprite.SelfModulate;
			// Hitstun is the authoritative burn lifetime. The authored value remains
			// a safety fallback for presentation-only contacts.
			_burnSilhouetteFramesLeft = Mathf.Max(_burnSilhouetteFramesLeft,
				Mathf.Max(silhouetteFrames, HitstunFramesLeft));
			CharacterSprite.SelfModulate = Colors.Black;
		}

		if (fireFrames == null || string.IsNullOrWhiteSpace(fireAnimationName) ||
			!fireFrames.HasAnimation(fireAnimationName)) return;
		SpawnDefenderFlame(fireFrames, fireAnimationName, new Vector2(-20f * Facing, -74f), -1.48f, 0.34f, -1);
		SpawnDefenderFlame(fireFrames, fireAnimationName, new Vector2(18f * Facing, -102f), -1.72f, 0.26f, 2);
		SpawnDefenderFlame(fireFrames, fireAnimationName, new Vector2(2f * Facing, -46f), -1.32f, 0.25f, 1);
		SpawnDefenderFlame(fireFrames, fireAnimationName, new Vector2(24f * Facing, -76f), -1.66f, 0.29f, 2);
		SpawnDefenderFlame(fireFrames, fireAnimationName, new Vector2(-17f * Facing, -48f), -1.24f, 0.23f, 0);
		SpawnDefenderFlame(fireFrames, fireAnimationName, new Vector2(13f * Facing, -22f), -1.43f, 0.21f, 1);
	}

	private void SpawnDefenderFlame(SpriteFrames frames, string animationName, Vector2 localPosition,
		float rotation, float scale, int relativeZ)
	{
		SpriteFrames loopingFrames = frames.Duplicate(true) as SpriteFrames ?? frames;
		loopingFrames.SetAnimationLoop(animationName, true);
		var flame = new AnimatedSprite2D
		{
			Name = $"SystemBurnFlame_{++_burnFlameSerial}",
			SpriteFrames = loopingFrames,
			Animation = animationName,
			Position = localPosition,
			Rotation = rotation * Facing,
			Scale = Vector2.One * scale,
			Centered = true,
			ZIndex = relativeZ,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		CharacterSprite.AddChild(flame);
		_activeBurnFlames.Add(flame);
		flame.Play();
	}

	private void UpdateBurnHitPresentation()
	{
		if (_burnSilhouetteFramesLeft <= 0 || CharacterSprite == null) return;
		if (HitstunFramesLeft > 0)
		{
			// Keep one cleanup tick armed; the gameplay counter itself determines
			// the lifetime and already pauses correctly during hitstop.
			_burnSilhouetteFramesLeft = 1;
			return;
		}
		_burnSilhouetteFramesLeft--;
		if (_burnSilhouetteFramesLeft > 0) return;
		CharacterSprite.SelfModulate = _burnRestoreSelfModulate;
		foreach (AnimatedSprite2D flame in _activeBurnFlames)
			if (GodotObject.IsInstanceValid(flame)) flame.QueueFree();
		_activeBurnFlames.Clear();
	}

	private void UpdateElectricityGreyBodyFlash()
	{
		if (CharacterSprite == null) return;
		bool electricityActive = IsAttackActive && CurrentAttackName == "MECHA ELECTRICITY" &&
			CurrentAttackActiveLoopAnimationName == "anim_149";
		if (!electricityActive)
		{
			if (_electricityGreyBody != null) _electricityGreyBody.Visible = false;
			return;
		}

		if (_electricityGreyBody == null)
		{
			Texture2D greyBody = CharacterSprite.SpriteFrames?.HasAnimation("anim_148") == true &&
				CharacterSprite.SpriteFrames.GetFrameCount("anim_148") > 4
				? CharacterSprite.SpriteFrames.GetFrameTexture("anim_148", 4)
				: null;
			if (greyBody == null) return;
			_electricityGreyBody = new Sprite2D
			{
				Name = "ElectricityGreyBody",
				Texture = greyBody,
				Centered = true,
				ZIndex = 1
			};
			CharacterSprite.AddChild(_electricityGreyBody);
		}

		_electricityGreyBody.FlipH = CharacterSprite.FlipH;
		// Grey for two 60 Hz ticks, absent for exactly one tick, then repeat.
		_electricityGreyBody.Visible = Mathf.PosMod(CurrentAttackFrame - CurrentAttackStartupFrames, 3) != 2;
	}

	private void ApplySelectedMoveVisualScale()
	{
		if (CharacterSprite == null || !_capturedCharacterSpriteTransform) return;
		bool sanzou = string.Equals(Definition?.FighterName, "Sanzou Kongoumaru", System.StringComparison.OrdinalIgnoreCase);
		bool selectedMove = sanzou && (CurrentAttackName == CrouchingHeavyKickName ||
			CurrentAttackName == SanzoSpdName || CurrentAttackName == SanzoSuperSpdName);
		bool selectedAnimation = sanzou && (CharacterSprite.Animation == "crouching_heavy_kick" ||
			CharacterSprite.Animation == "spd_grab" || CharacterSprite.Animation == "spd_air_grab");
		float selectedFactor = selectedMove || selectedAnimation
			? Mathf.Clamp(SweepAndSpdVisualScale, 0.1f, 1f)
			: 1f;
		float moveFactor = IsAttacking ? Mathf.Clamp(CurrentAttackCharacterVisualScale, 0.1f, 2f) : 1f;
		CharacterSprite.Scale = _baseCharacterSpriteScale * selectedFactor * moveFactor;
		float floorCompensation = AuthoredSpriteFloorOffset * (1f - selectedFactor) * Mathf.Abs(_baseCharacterSpriteScale.Y);
		Vector2 authoredOffset = IsAttacking ? CurrentAttackCharacterVisualOffset : Vector2.Zero;
		authoredOffset.X *= Facing;
		Vector2 drawingOffset = Vector2.Zero;
		Vector2[] drawingOffsets = CurrentAttackAnimationDrawingOffsets;
		if (IsAttacking && drawingOffsets.Length > 0)
		{
			int drawing = Mathf.Clamp(CharacterSprite.Frame, 0, drawingOffsets.Length - 1);
			drawingOffset = drawingOffsets[drawing];
			drawingOffset.X *= Facing;
		}
		CharacterSprite.Position = _baseCharacterSpritePosition + Vector2.Down * floorCompensation +
			authoredOffset + drawingOffset;
	}

	private void UpdateHeavyWalkFootsteps()
	{
		if (!HeavyWalkFootstepShake || CharacterSprite == null) return;
		StringName animation = CharacterSprite.Animation;
		int drawing = CharacterSprite.Frame;
		bool walking = animation == "walk" || animation == "walk_back";
		if (!walking)
		{
			_lastFootstepAnimation = animation;
			_lastFootstepDrawing = -1;
			return;
		}
		if (animation == _lastFootstepAnimation && drawing == _lastFootstepDrawing) return;
		_lastFootstepAnimation = animation;
		_lastFootstepDrawing = drawing;
		bool impactDrawing = animation == "walk"
			? drawing == 1 || drawing == 7       // authored ticks 8 and 56
			: drawing == 3 || drawing == 6;      // authored ticks 24 and 48
		if (!impactDrawing) return;
		if (GetViewport().GetCamera2D() is StageCamera camera)
			camera.Shake(HeavyWalkShakeStrength, HeavyWalkShakeFrames);
	}

	private void UpdateHeavyLandingShake()
	{
		if (!HeavyLandingShake || !JustLanded) return;
		if (GetViewport().GetCamera2D() is StageCamera camera)
		{
			bool stompLanding = CurrentAttackName == StompSpecialName;
			camera.Shake(stompLanding ? HeavyLandingShakeStrength * 1.8f : HeavyLandingShakeStrength,
				stompLanding ? Mathf.Max(10, HeavyLandingShakeFrames + 3) : HeavyLandingShakeFrames);
		}
	}

	private void UpdateAnimation()
	{
		if (CharacterSprite?.SpriteFrames == null) return;
		// SpeedScale freezes the exact displayed sprite frame without resetting animation state.
		// Attack drawings are selected manually from the deterministic combat
		// frame below; AnimatedSprite must not independently advance between ticks.
		CharacterSprite.SpeedScale = IsInHitstop || IsAttacking ? 0f : 1f;
		CharacterSprite.FlipH = Facing < 0;
		if (IsPlayingWinAnimation)
		{
			CharacterSprite.SpeedScale = 1f;
			if (CharacterSprite.Animation == "win" && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation == "win" && CharacterSprite.SpriteFrames.HasAnimation("win_loop"))
			{
				CharacterSprite.Play("win_loop");
				return;
			}
			if (CharacterSprite.Animation != "win" && CharacterSprite.Animation != "win_loop" &&
				CharacterSprite.SpriteFrames.HasAnimation("win"))
				CharacterSprite.Play("win");
			return;
		}
		bool restartAttackAnimation = IsAttacking &&
			(!_wasVisuallyAttacking || CurrentAttackName != _lastVisualAttackName || CurrentAttackFrame < _lastVisualAttackFrame);
		bool jetEscaping = ActiveAbility is JetEscapeAbility;
		JetEscapeAbility activeJetEscape = ActiveAbility as JetEscapeAbility;
		if (jetEscaping) _awaitingJetEscapeLanding = true;
		bool forwardShortHopping = ActiveAbility?.Id == "forward_short_hop";
		bool airDashing = ActiveAbility?.Id == "air_dash";
		bool holdingCrouch = WasGrounded && CurrentInput.Vertical > 0.5f;
		bool leavingCrouchForAnotherAction = !holdingCrouch &&
			(ActiveAbility != null || Mathf.Abs(CurrentInput.Horizontal) > 0.1f);
		if (restartAttackAnimation)
		{
			// An attack owns the presentation immediately. A standing attack must not
			// inherit a pending crouch-exit, while a crouching attack returns to crouch hold.
			_crouchIntroStarted = IsCrouchAttackLocked;
			_crouchExitStarted = false;
		}
		else if (jetEscaping || !WasGrounded || leavingCrouchForAnotherAction)
		{
			// Dash/jump states replace crouch outright; never resume crouch_end afterward.
			_crouchIntroStarted = false;
			_crouchExitStarted = false;
		}
		_wasVisuallyAttacking = IsAttacking;
		_lastVisualAttackName = CurrentAttackName;
		_lastVisualAttackFrame = CurrentAttackFrame;
		if (IsAttacking && CurrentAttackName == "SUPER RUSH")
		{
			if (!_superOneVisualActive)
			{
				_superOneVisualActive = true;
				CharacterSprite.Stop();
				CharacterSprite.Play("air_dash");
				CharacterSprite.SetFrameAndProgress(1, 0f);
			}
			UpdateSuperOneAnimation();
			return;
		}
		_superOneVisualActive = false;
		if (IsParrySuccessPresentationActive)
		{
			if (CharacterSprite.Animation != "stand_block") CharacterSprite.Play("stand_block");
			CharacterSprite.SetFrameAndProgress(0, 0f);
			return;
		}
		if (IsWakingUp)
		{
			if (CharacterSprite.Animation != "get_up") CharacterSprite.Play("get_up");
			return;
		}
		if (IsInBlockstun)
		{
			_boosterWasActive = false;
			_awaitingFlightLanding = false;
			bool specialGuardReaction = (CurrentSpecialReaction is SpecialReactionKind.GuardPullbackWeak or
				SpecialReactionKind.GuardPullbackStrong or SpecialReactionKind.GuardPullbackAir) &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentSpecialReactionAnimationName);
			bool authoredAirGuard = !WasGrounded &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentAirGuardAnimationName);
			bool authoredCrouchingGuard = WasGrounded && IsCrouchBlocking &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentCrouchingGuardAnimationName);
			bool authoredStandingGuard = WasGrounded && !IsCrouchBlocking &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentStandingGuardAnimationName);
			StringName blockAnimation = specialGuardReaction
				? CurrentSpecialReactionAnimationName
				: authoredAirGuard
				? CurrentAirGuardAnimationName
				: authoredCrouchingGuard
				? CurrentCrouchingGuardAnimationName
				: authoredStandingGuard
				? CurrentStandingGuardAnimationName
				: !WasGrounded ? "air_block" : IsCrouchBlocking ? "crouch_block" : "stand_block";
			StringName impactAnimation = specialGuardReaction
				? CurrentSpecialReactionAnimationName
				: authoredAirGuard
				? CurrentAirGuardAnimationName
				: authoredCrouchingGuard
				? CurrentCrouchingGuardAnimationName
				: authoredStandingGuard
				? CurrentStandingGuardAnimationName
				: !WasGrounded ? "air_block_impact" : IsCrouchBlocking ? "crouch_block_impact" : "stand_block_impact";
			if (_lastVisualBlockReactionSerial != BlockReactionSerial)
			{
				_lastVisualBlockReactionSerial = BlockReactionSerial;
				CharacterSprite.Play(impactAnimation);
				return;
			}
			if (CharacterSprite.Animation == impactAnimation && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != blockAnimation) CharacterSprite.Play(blockAnimation);
			return;
		}
		if (IsInHitstun)
		{
			_boosterWasActive = false;
			_awaitingFlightLanding = false;
			StringName reactionAnimation;
			string blowAwayAnimation = CurrentBlowAwayAnimationName;
			if (CurrentSpecialReaction != SpecialReactionKind.None &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentSpecialReactionAnimationName))
				reactionAnimation = CurrentSpecialReactionAnimationName;
			else if (!string.IsNullOrEmpty(blowAwayAnimation) && CharacterSprite.SpriteFrames.HasAnimation(blowAwayAnimation))
				reactionAnimation = blowAwayAnimation;
			else if (HitState == FighterHitState.Stumble && CharacterSprite.SpriteFrames.HasAnimation("stumble"))
				reactionAnimation = "stumble";
			else if (HitState == FighterHitState.HitFall && CharacterSprite.SpriteFrames.HasAnimation("hit_fall"))
				reactionAnimation = "hit_fall";
			else if (HitState is FighterHitState.WallBounce or FighterHitState.WallSplat &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentWallBounceAnimationName))
				reactionAnimation = CurrentWallBounceAnimationName;
			else if (HitState == FighterHitState.GroundBounce &&
				CharacterSprite.SpriteFrames.HasAnimation(CurrentGroundBounceAnimationName))
				reactionAnimation = CurrentGroundBounceAnimationName;
			else if (IsGroundedKnockdown)
				reactionAnimation = "knockdown";
			else if (HitState == FighterHitState.Juggle || HitState == FighterHitState.WallSplat || HitState == FighterHitState.Tumble || HitState == FighterHitState.Knockdown ||
				HitState == FighterHitState.WallBounce || HitState == FighterHitState.GroundBounce)
				reactionAnimation = "tumble";
			else if (!WasGrounded)
				reactionAnimation = "air_hitstun";
			else if (HitReactionStartedCrouching)
				reactionAnimation = "crouch_hit";
			else if (LastHitReactionLevel >= 2 && LastHitCameFromAir)
				reactionAnimation = "hitstun_heavy_air";
			else if (LastHitReactionLevel >= 2)
				reactionAnimation = "hitstun_heavy";
			else if (LastHitReactionLevel == 1)
				reactionAnimation = "hitstun_medium";
			else
				reactionAnimation = "hitstun_light";
			if (CharacterSprite.Animation != reactionAnimation || _lastVisualHitReactionSerial != HitReactionSerial)
			{
				CharacterSprite.Play(reactionAnimation);
				CharacterSprite.SetFrameAndProgress(0, 0f);
				_lastVisualHitReactionSerial = HitReactionSerial;
			}
			return;
		}
		if (IsInAirAttackLanding && CharacterSprite.SpriteFrames.HasAnimation(CurrentAirAttackLandingAnimationName))
		{
			if (CharacterSprite.Animation != CurrentAirAttackLandingAnimationName)
				CharacterSprite.Play(CurrentAirAttackLandingAnimationName);
			return;
		}
		bool boosterActive = !IsAttacking && ActiveAbility is FlightAbility &&
			CharacterSprite.SpriteFrames.HasAnimation("booster_loop");
		if (boosterActive)
		{
			_awaitingFlightLanding = true;
			FlightAbility flightAbility = (FlightAbility)ActiveAbility;
			StringName boosterAnimation = flightAbility.ResolveAnimationName(this);
			if (!CharacterSprite.SpriteFrames.HasAnimation(boosterAnimation)) boosterAnimation = "booster_loop";
			if (!_boosterWasActive)
			{
				_boosterWasActive = true;
				CharacterSprite.Play(boosterAnimation == "booster_loop" ? "booster_start" : boosterAnimation);
				return;
			}
			if (boosterAnimation == "booster_loop" && CharacterSprite.Animation == "booster_start" &&
				CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != boosterAnimation) CharacterSprite.Play(boosterAnimation);
			return;
		}
		// Attacks temporarily replace the booster drawing without ending flight.
		// Only resolve the flight-exit presentation after the ability itself stops.
		if (_boosterWasActive && ActiveAbility is not FlightAbility)
		{
			_boosterWasActive = false;
			if (JustLanded && CharacterSprite.SpriteFrames.HasAnimation("flight_landing"))
			{
				_awaitingFlightLanding = false;
				BeginFlightLanding();
				CharacterSprite.Play("flight_landing");
				return;
			}
			// Ground button-flight can be toggled off without crossing the floor.
			// That is a trait deactivation, not an authored flight landing.
			if (WasGrounded) _awaitingFlightLanding = false;
		}
		if (!IsAttacking && _awaitingFlightLanding && JustLanded &&
			CharacterSprite.SpriteFrames.HasAnimation("flight_landing"))
		{
			_awaitingFlightLanding = false;
			BeginFlightLanding();
			CharacterSprite.Play("flight_landing");
			return;
		}
		if (!IsAttacking && CharacterSprite.Animation == "flight_landing" &&
			CharacterSprite.IsPlaying()) return;
		if (!IsAttacking && _awaitingFlightLanding && !WasGrounded &&
			CharacterSprite.SpriteFrames.HasAnimation("flight_fall"))
		{
			if (CharacterSprite.Animation != "flight_fall") CharacterSprite.Play("flight_fall");
			return;
		}
		if (!IsAttacking && !jetEscaping && _awaitingJetEscapeLanding && JustLanded &&
			CharacterSprite.SpriteFrames.HasAnimation("escape_landing"))
		{
			_awaitingJetEscapeLanding = false;
			CharacterSprite.Play("escape_landing");
			return;
		}
		if (!IsAttacking && !jetEscaping && CharacterSprite.Animation == "escape_landing" &&
			CharacterSprite.IsPlaying()) return;
		if (!IsAttacking && JustLanded && !_awaitingFlightLanding && !_awaitingJetEscapeLanding &&
			!IsInAirAttackLanding && CharacterSprite.SpriteFrames.HasAnimation("landing"))
		{
			CharacterSprite.Play("landing");
			return;
		}
		// Ordinary landing is visual flavor: attacks, movement, and held-jump
		// repetition may interrupt it immediately.
		if (!IsAttacking && WasGrounded && Velocity.Y >= 0f &&
			CharacterSprite.Animation == "landing" && CharacterSprite.IsPlaying()) return;
		bool forwardAirborne = !WasGrounded && Velocity.X * Facing > 25f;
		if (!forwardAirborne) _forwardJumpIntroStarted = false;
		bool crouching = holdingCrouch;
		bool superJumpRising = !IsAttacking && !WasGrounded && IsInSuperJumpRoute && Velocity.Y < 0f;
		if (superJumpRising)
		{
			StringName superJumpAnimation = SuperJumpPresentationDirection > 0
				? "super_jump_forward"
				: SuperJumpPresentationDirection < 0
					? "super_jump_backward"
					: "super_jump_neutral";
			if (CharacterSprite.Animation != superJumpAnimation) CharacterSprite.Play(superJumpAnimation);
			return;
		}

		if (!IsAttacking && !IsInSuperJumpRoute && !jetEscaping && !airDashing && !forwardShortHopping && forwardAirborne)
		{
			if (!_forwardJumpIntroStarted)
			{
				_forwardJumpIntroStarted = true;
				CharacterSprite.Play("forward_jump_start");
				return;
			}
			if (CharacterSprite.Animation == "forward_jump_start" && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != "forward_jump_loop") CharacterSprite.Play("forward_jump_loop");
			return;
		}

		if (!IsAttacking && !jetEscaping && crouching)
		{
			_crouchExitStarted = false;
			if (!_crouchIntroStarted)
			{
				_crouchIntroStarted = true;
				CharacterSprite.Play("crouch_start");
				return;
			}
			if (CharacterSprite.Animation == "crouch_start" && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != "crouch_hold") CharacterSprite.Play("crouch_hold");
			return;
		}

		if (!IsAttacking && !jetEscaping && !crouching && _crouchIntroStarted)
		{
			if (!_crouchExitStarted)
			{
				_crouchExitStarted = true;
				CharacterSprite.Play("crouch_end");
				return;
			}
			if (CharacterSprite.Animation == "crouch_end" && CharacterSprite.IsPlaying()) return;
			_crouchIntroStarted = false;
			_crouchExitStarted = false;
			return;
		}

		StringName nextAnimation;
		if (IsAttacking && CurrentAttackName == "SUPER FIREBALL")
			nextAnimation = "super_fireball";
		else if (IsAttacking && !string.IsNullOrEmpty(CurrentAttackAnimationName))
			nextAnimation = CurrentAttackAnimationName;
		else if (IsAttacking && (CurrentAttackName == "LIGHT PROJECTILE" || CurrentAttackName == "HEAVY PROJECTILE"))
			nextAnimation = "fireball";
		else if (IsAttacking && CurrentAttackName == ThrowAttackName) nextAnimation = "throw";
		else if (IsAttacking && CurrentAttackName == ForwardHeavyPunchName) nextAnimation = "forward_heavy_punch";
		else if (IsAttacking && CurrentAttackName == CrouchingMediumJabName) nextAnimation = "crouching_medium_punch";
		else if (IsAttacking && CurrentAttackName == CrouchingHeavyPunchName) nextAnimation = "crouching_heavy_punch";
		else if (IsAttacking && CurrentAttackName == AirHeavyPunchName) nextAnimation = "air_heavy_punch";
		else if (IsAttacking && CurrentAttackName == "HEAVY PUNCH") nextAnimation = "heavy_punch";
		else if (IsAttacking && IsCrouchAttackLocked && CurrentAttackName == "LIGHT PUNCH") nextAnimation = "crouching_light_punch";
		else if (IsAttacking && CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT PUNCH") nextAnimation = "air_light_punch";
		else if (IsAttacking && !CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT PUNCH") nextAnimation = "light_punch";
		else if (IsAttacking && CurrentAttackName == ForwardLightKickName) nextAnimation = "forward_light_kick";
		else if (IsAttacking && !IsCrouchAttackLocked && !CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT KICK") nextAnimation = "standing_light_kick";
		else if (IsAttacking && !IsCrouchAttackLocked && !CurrentAttackStartedAirborne && CurrentAttackName == "HEAVY KICK") nextAnimation = "standing_heavy_kick";
		else if (IsAttacking && CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT KICK") nextAnimation = "air_light_kick";
		else if (IsAttacking && CurrentAttackStartedAirborne && CurrentAttackName == "HEAVY KICK") nextAnimation = "air_heavy_kick";
		else if (IsAttacking && IsCrouchAttackLocked && CurrentAttackName == "LIGHT KICK") nextAnimation = "crouching_light_kick";
		else if (IsAttacking && CurrentAttackName == CrouchingHeavyKickName) nextAnimation = "crouching_heavy_kick";
		else if (IsAttacking) nextAnimation = "attack";
		else if (jetEscaping && !string.IsNullOrWhiteSpace(activeJetEscape.AnimationName))
			nextAnimation = activeJetEscape.AnimationName;
		else if (forwardShortHopping) nextAnimation = "forward_dash";
		else if (airDashing) nextAnimation = "air_dash";
		else if (!WasGrounded) nextAnimation = Velocity.Y < 0f ? "neutral_jump" : "fall";
		else if (ActiveAbility is RunAbility) nextAnimation = "run";
		else if (IsInRunStopSlide && CharacterSprite.SpriteFrames.HasAnimation("run_stop")) nextAnimation = "run_stop";
		else if (Mathf.Abs(Velocity.X) > 25f)
			nextAnimation = Velocity.X * Facing < 0f ? "walk_back" : "walk";
		else nextAnimation = "idle";

		if (IsCurrentSpecialLandingRecovery && !string.IsNullOrEmpty(CurrentAttackLandingAnimationName) &&
			CharacterSprite.SpriteFrames.HasAnimation(CurrentAttackLandingAnimationName))
			nextAnimation = CurrentAttackLandingAnimationName;

		bool usingNaturalAttackTail = IsAttacking && CurrentAttackAnimationTailStartFrame >= 0 &&
			CurrentAttackFrame >= CurrentAttackAnimationTailStartFrame &&
			!string.IsNullOrEmpty(CurrentAttackAnimationTailName) &&
			CharacterSprite.SpriteFrames.HasAnimation(CurrentAttackAnimationTailName);
		if (usingNaturalAttackTail) nextAnimation = CurrentAttackAnimationTailName;
		bool usingActiveAttackLoop = IsAttackActive &&
			!string.IsNullOrEmpty(CurrentAttackActiveLoopAnimationName) &&
			CharacterSprite.SpriteFrames.HasAnimation(CurrentAttackActiveLoopAnimationName);
		if (usingActiveAttackLoop) nextAnimation = CurrentAttackActiveLoopAnimationName;
		if (usingActiveAttackLoop)
		{
			if (CharacterSprite.Animation != nextAnimation || restartAttackAnimation)
				CharacterSprite.Play(nextAnimation);
			SyncActiveLoopDrawingToCombatFrame(nextAnimation);
			return;
		}

		if (IsAttacking && !usingNaturalAttackTail && CharacterSprite.SpriteFrames.HasAnimation(nextAnimation))
		{
			if (CharacterSprite.Animation != nextAnimation || restartAttackAnimation)
				CharacterSprite.Play(nextAnimation);
			SyncAttackDrawingToCombatFrame(nextAnimation);
			return;
		}
		if (CharacterSprite.Animation == nextAnimation && !restartAttackAnimation) return;
		CharacterSprite.Play(nextAnimation);
	}

	private void SyncActiveLoopDrawingToCombatFrame(StringName animation)
	{
		int drawingCount = CharacterSprite.SpriteFrames.GetFrameCount(animation);
		if (drawingCount <= 0) return;
		int totalTicks = 0;
		for (int drawing = 0; drawing < drawingCount; drawing++)
			totalTicks += Mathf.Max(1, Mathf.RoundToInt((float)CharacterSprite.SpriteFrames.GetFrameDuration(animation, drawing)));
		int loopTick = Mathf.PosMod(CurrentAttackFrame - CurrentAttackStartupFrames, Mathf.Max(1, totalTicks));
		for (int drawing = 0; drawing < drawingCount; drawing++)
		{
			int holdTicks = Mathf.Max(1, Mathf.RoundToInt((float)CharacterSprite.SpriteFrames.GetFrameDuration(animation, drawing)));
			if (loopTick < holdTicks)
			{
				CharacterSprite.SetFrameAndProgress(drawing, 0f);
				return;
			}
			loopTick -= holdTicks;
		}
	}

	/// <summary>
	/// Attack art uses the same deterministic 60 Hz clock as combat boxes. Sprite
	/// frame durations are authored in gameplay ticks, so rendering can neither
	/// race ahead of nor lag behind startup/active/recovery state.
	/// </summary>
	private void SyncAttackDrawingToCombatFrame(StringName animation)
	{
		if ((CurrentAttackName == SanzoSpdName || CurrentAttackName == SanzoSuperSpdName) &&
			SpdGrabConnected && animation == "spd_air_grab")
		{
			int drawings = CharacterSprite.SpriteFrames.GetFrameCount(animation);
			int flightTick = Mathf.Max(0, CurrentAttackFrame - CurrentAttackStartupFrames);
			CharacterSprite.SetFrameAndProgress((flightTick / 4) % Mathf.Max(1, drawings), 0f);
			return;
		}
		int drawing = ResolveAttackDrawing(animation);
		CharacterSprite.SetFrameAndProgress(drawing, 0f);
	}

	protected virtual int ResolveAttackDrawing(StringName animation)
	{
		if (IsCurrentSpecialLandingRecovery && CurrentAttackLandingAnimationSourceTimeline is { Length: > 0 } landing)
		{
			int sourceTick = landing[Mathf.Clamp(CurrentSpecialLandingRecoveryFrame, 0, landing.Length - 1)];
			return AttackDrawingTimeline.ResolveSourceTick(CharacterSprite.SpriteFrames, animation, sourceTick);
		}

		int forceDownFrame = CurrentAttackForceDownwardStartFrame;
		if (forceDownFrame >= 0 && CurrentAttackFrame >= forceDownFrame &&
			CurrentAttackDescentAnimationSourceCycle is { Length: > 0 } descent)
			return AttackDrawingTimeline.ResolveSourceCycle(CharacterSprite.SpriteFrames, animation, descent,
				CurrentAttackFrame - forceDownFrame, CurrentAttackDescentAnimationTicksPerSource);

		if (forceDownFrame >= 0 && CurrentAttackFrame >= CurrentAttackStartupFrames &&
			CurrentAttackRiseAnimationSourceCycle is { Length: > 0 } rise)
			return AttackDrawingTimeline.ResolveSourceCycle(CharacterSprite.SpriteFrames, animation, rise,
				CurrentAttackFrame - CurrentAttackStartupFrames, CurrentAttackRiseAnimationTicksPerSource);

		return AttackDrawingTimeline.Resolve(CharacterSprite.SpriteFrames, animation, CurrentAttackFrame,
			CurrentAttackStartupFrames, CurrentAttackActiveFrames, CurrentAttackRecoveryFrames,
			ReverseAttackRecoveryToNeutral, CurrentAttackAnimationSourceTimeline);
	}

	private void UpdateSuperOneAnimation()
	{
		const int startupFrames = 7;
		const int finisherStartFrame = 63;
		const int finisherMotionStartFrame = 67;
		if (!IsCurrentSuperConfirmed)
		{
			if (CharacterSprite.Animation != "air_dash") CharacterSprite.Play("air_dash");
			CharacterSprite.Frame = 1; // Source frame 22_01 is the rush pose.
			return;
		}
		int confirmedAnimationFrame = startupFrames + (CurrentAttackFrame - CurrentSuperConfirmedFrame);

		if (confirmedAnimationFrame >= finisherStartFrame)
		{
			if (CharacterSprite.Animation != "super_one_finisher") CharacterSprite.Play("super_one_finisher");
			if (CurrentAttackHitsRemaining == 0)
			{
				if (IsInHitstop)
				{
					CharacterSprite.Frame = 6;
					return;
				}
				if (!IsAttackRecovering)
				{
					CharacterSprite.Frame = 4 + (confirmedAnimationFrame & 1);
					return;
				}
				// Finish on 27_07; intentionally never display the final 27_08 frame.
				CharacterSprite.Frame = confirmedAnimationFrame < 100 ? 6 : 7;
				return;
			}
			CharacterSprite.Frame = confirmedAnimationFrame < finisherMotionStartFrame
				? 0
				: Mathf.Clamp((confirmedAnimationFrame - finisherMotionStartFrame) / 2, 0, 8);
			return;
		}

		int montageFrame = confirmedAnimationFrame - startupFrames;
		StringName animation = SuperOneAttackCycle[(montageFrame / 3) % SuperOneAttackCycle.Length];
		if (CharacterSprite.Animation != animation) CharacterSprite.Play(animation);
		int frameCount = CharacterSprite.SpriteFrames.GetFrameCount(animation);
		CharacterSprite.Frame = Mathf.Clamp(montageFrame % 3, 0, frameCount - 1);
	}

	private void UpdateSuperShadows()
	{
		bool superAttackAfterimagesActive = IsPerformingSuperMove;
		bool superJumpAfterimagesActive = IsInSuperJumpRoute && !WasGrounded;
		bool superAfterimagesActive = superAttackAfterimagesActive || superJumpAfterimagesActive;
		if (!superAfterimagesActive)
		{
			ClearSuperAfterimages();
			return;
		}

		EnsureSuperAfterimages();
		Texture2D texture = CharacterSprite.SpriteFrames.GetFrameTexture(CharacterSprite.Animation, CharacterSprite.Frame);
		if (texture == null) return;
		_superShadowHistory.Add(new SuperShadowSample
		{
			Texture = texture,
			WorldPosition = CharacterSprite.GlobalPosition,
			WorldScale = CharacterSprite.GlobalScale,
			FlipH = CharacterSprite.FlipH,
			FlipV = CharacterSprite.FlipV
		});
		while (_superShadowHistory.Count > SuperAfterimageDelays[^1] + 1) _superShadowHistory.RemoveAt(0);

		for (int index = 0; index < _superAfterimages.Count; index++)
		{
			Sprite2D afterimage = _superAfterimages[index];
			afterimage.Modulate = superJumpAfterimagesActive && !superAttackAfterimagesActive
				? new Color(0.3f, 0.8f, 1.8f, 0.28f)
				: new Color(0.25f, 1.25f, 4f, 1f);
			int sampleIndex = _superShadowHistory.Count - 1 - SuperAfterimageDelays[index];
			afterimage.Visible = sampleIndex >= 0;
			if (sampleIndex < 0) continue;
			SuperShadowSample sample = _superShadowHistory[sampleIndex];
			afterimage.Texture = sample.Texture;
			afterimage.GlobalPosition = sample.WorldPosition;
			afterimage.GlobalScale = sample.WorldScale;
			afterimage.FlipH = sample.FlipH;
			afterimage.FlipV = sample.FlipV;
		}
	}

	private void EnsureSuperAfterimages()
	{
		if (_superAfterimages.Count == SuperAfterimageDelays.Length) return;
		Node shadowHost = GetParent() ?? this;
		for (int index = _superAfterimages.Count; index < SuperAfterimageDelays.Length; index++)
		{
			Sprite2D afterimage = new()
			{
				Name = $"SuperAfterimage{index + 1}",
				Centered = CharacterSprite.Centered,
				Offset = CharacterSprite.Offset,
				Visible = false,
				ZIndex = CharacterSprite.ZIndex - index - 1,
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
				Modulate = new Color(0.25f, 1.25f, 4f, 1f)
			};
			shadowHost.AddChild(afterimage);
			_superAfterimages.Add(afterimage);
		}
	}

	private void ClearSuperAfterimages()
	{
		_superShadowHistory.Clear();
		foreach (Sprite2D afterimage in _superAfterimages)
			if (GodotObject.IsInstanceValid(afterimage)) afterimage.QueueFree();
		_superAfterimages.Clear();
	}

	protected override void OnWinAnimationRequested()
	{
		if (CharacterSprite?.SpriteFrames == null)
		{
			MarkWinAnimationFinished();
			return;
		}
		CharacterSprite.SpeedScale = 1f;
		if (CharacterSprite.SpriteFrames.HasAnimation("win"))
			CharacterSprite.Play("win");
		else if (CharacterSprite.SpriteFrames.HasAnimation("win_loop"))
		{
			CharacterSprite.Play("win_loop");
			MarkWinAnimationFinished();
		}
		else
			MarkWinAnimationFinished();
	}

	private void OnCharacterAnimationFinished()
	{
		if (!IsPlayingWinAnimation || CharacterSprite.Animation != "win") return;
		MarkWinAnimationFinished();
		if (CharacterSprite.SpriteFrames.HasAnimation("win_loop"))
			CharacterSprite.Play("win_loop");
	}

	protected override void OnDefeatedKoRequested()
	{
		if (CharacterSprite?.SpriteFrames == null) return;
		CharacterSprite.SpeedScale = 1f;
		if (CharacterSprite.SpriteFrames.HasAnimation("knockdown"))
		{
			CharacterSprite.Play("knockdown");
			CharacterSprite.SetFrameAndProgress(
				Mathf.Max(0, CharacterSprite.SpriteFrames.GetFrameCount("knockdown") - 1), 0f);
			CharacterSprite.Pause();
		}
	}

	private sealed class SuperShadowSample
	{
		public Texture2D Texture;
		public Vector2 WorldPosition;
		public Vector2 WorldScale;
		public bool FlipH;
		public bool FlipV;
	}
}
