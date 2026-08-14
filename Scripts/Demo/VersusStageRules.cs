using Godot;
using ModularFighter.Core;
using System.Collections.Generic;

namespace ModularFighter.Demo;

/// <summary>Shared versus rules: opponent-facing and Street Fighter-style pushboxes.</summary>
public partial class VersusStageRules : Node
{
	private const int BackFighterZIndex = 0;
	private const int LatestAttackerZIndex = 10;
	private const string KungFuManSuperPortraitPath = "res://Assets/TestFighter/KungFuMan/kung_fu_man_super_portrait.png";
	private const string SanzouSuperPortraitPath = "res://Assets/TestFighter/Sanzo/sanzou_kongoumaru/9999.png";
	private const string BigBangSuperCancelEffectPath = "res://Effects/BigBangSuperCancelEffect.tscn";
	private const string HyperComboFinishOverlayName = "HyperComboFinishOverlay";
	private static readonly string[] HyperComboBackdropPaths =
	{
		"res://Assets/Backgrounds/ALidej.gif",
		"res://Assets/Backgrounds/ALidej2.gif"
	};
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath FighterTwoPath { get; set; }
	[Export] public NodePath CameraPath { get; set; }
	[ExportGroup("Universal Super Presentation")]
	[Export(PropertyHint.Range, "1,180,1")] public int UniversalSuperFreezeFrames { get; set; } = 45;
	[Export(PropertyHint.Range, "1,600,1")] public int UniversalSuperBackdropMinimumFrames { get; set; } = 90;
	[Export] public float UniversalSuperShakeStrength { get; set; } = 8.5f;
	[Export(PropertyHint.Range, "0.1,3.0,0.05")] public float UniversalSuperCancelEffectScale { get; set; } = 1f;
	[Export(PropertyHint.Range, "1.0,8.0,0.1")] public float HyperComboFinishMinimumSeconds { get; set; } = 4f;
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float FinishingSuperTimeScale { get; set; } = 0.5f;
	[Export] public float StageWidth { get; set; } = 3360f;
	[Export] public float ViewportWidth { get; set; } = 1280f;
	[Export] public float CornerPushbackTransferStartDistance { get; set; } = 28f;
	[Export] public float CornerPushbackTransferMultiplier { get; set; } = 0.75f;
	[Export] public float GroundedLightCornerPushbackTransferMultiplier { get; set; } = 1.65f;
	[Export] public float FacingSideSwitchDeadZone { get; set; } = 18f;
	[Export] public float LandingCrossupDeadZone { get; set; } = 10f;
	[Export] public float CornerProtectionDistance { get; set; } = 8f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float PushboxInstantCorrectionShare { get; set; } = 0.55f;
	[Export] public float PushboxSmoothVelocityScale { get; set; } = 30f;
	[Export] public float JuggleWallSplatDetectionDistance { get; set; } = 30f;
	[Export] public float JuggleWallSplatPushProjection { get; set; } = 0.08f;
	[Export] public float AuthoredWallBounceDetectionDistance { get; set; } = 8f;
	[Export] public bool AllowHealthToReachZero { get; set; } = false;
	[Export] public float RoundTimeSeconds { get; set; } = 99f;
	[Export] public float KoResetDelaySeconds { get; set; } = 1.5f;
	[Export] public float TrainingLifeRecoveryPerSecond { get; set; } = 650f;
	[Export] public float TrainingLifeRecoveryDelaySeconds { get; set; } = 2.25f;
	public float RoundSecondsRemaining { get; private set; }
	public bool IsKoActive { get; private set; }
	[Export] public StateImpactEffectProfile KnockdownLandingEffect { get; set; } = new()
	{
		TriggerState = FighterHitState.GroundedKnockdown,
		SpawnDust = true,
		DustParticles = 7,
		DustSpread = 42f,
		ShakeStrength = 2.25f,
		ShakeFrames = 6
	};
	[Export] public StateImpactEffectProfile WallSplatImpactEffect { get; set; } = new()
	{
		TriggerState = FighterHitState.WallSplat,
		SpawnDust = false,
		SpawnWallBurst = true,
		WallBurstScale = 1.65f,
		ShakeStrength = 9f,
		ShakeFrames = 11,
		FreezeFrames = 5
	};
	[Export] public StateImpactEffectProfile WallSplatFollowupImpactEffect { get; set; } = new()
	{
		TriggerState = FighterHitState.WallSplat,
		SpawnDust = true,
		SpawnWallBurst = true,
		WallBurstScale = 0.55f,
		DustParticles = 3,
		DustSpread = 18f,
		ShakeStrength = 2.4f,
		ShakeFrames = 4,
		FreezeFrames = 1
	};

	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private Camera2D _stageCamera;
	private StageCamera _fightCamera;
	private HitSparkLayer _hitSparkLayer;
	private SuperBackdrop _superBackdrop;
	private SuperPortraitOverlay _superPortrait;
	private Texture2D _kungFuManSuperPortrait;
	private Texture2D _sanzouSuperPortrait;
	private PackedScene _bigBangSuperCancelEffectScene;
	private readonly RandomNumberGenerator _hyperComboBackdropRandom = new();
	private readonly List<FighterController> _primaryTeam = new();
	private double _koResetCountdown;
	private FighterController _koWinner;
	private HyperComboFinishOverlay _hyperComboFinishOverlay;
	private FighterController _pendingSuperKoAttacker;
	private bool _pendingSuperKo;
	private float _fighterOneRecoveryDelay;
	private float _fighterTwoRecoveryDelay;
	private float _fighterOneLastLife;
	private float _fighterTwoLastLife;
	private bool _finishingSuperSlowMotionActive;
	private double _timeScaleBeforeFinishingSuper = 1.0;

	public void SetPrimaryFighter(FighterController fighter)
	{
		if (fighter == null || fighter == _fighterOne) return;
		FighterCollisionPolicy.Apply(fighter);
		fighter.TeamId = 1;
		_fighterOne = fighter;
		_fighterOne.FaceWithMovement = false;
		_fighterOne.SetOpponent(_fighterTwo);
		_fighterTwo?.SetOpponent(_fighterOne);
	}

	public void RegisterPrimaryTeamFighter(FighterController fighter)
	{
		if (fighter == null || _primaryTeam.Contains(fighter)) return;
		FighterCollisionPolicy.Apply(fighter);
		fighter.TeamId = 1;
		_primaryTeam.Add(fighter);
		fighter.ResetPlaceholderGauges();
		fighter.FaceWithMovement = false;
		fighter.SetOpponent(_fighterTwo);
	}

	public void UnregisterPrimaryTeamFighter(FighterController fighter)
	{
		if (fighter != null) _primaryTeam.Remove(fighter);
	}

	public override void _Ready()
	{
		// Fighters simulate first; the match resolver consumes their final fixed-step state.
		ProcessPhysicsPriority = 100;
		_fighterOne = GetNode<FighterController>(FighterOnePath);
		_fighterTwo = GetNode<FighterController>(FighterTwoPath);
		FighterCollisionPolicy.Apply(_fighterOne);
		FighterCollisionPolicy.Apply(_fighterTwo);
		_fighterOne.TeamId = 1;
		_fighterTwo.TeamId = 2;
		_fighterTwo.ResetPlaceholderGauges();
		_stageCamera = CameraPath == null || CameraPath.IsEmpty ? GetParent().GetNodeOrNull<Camera2D>("StageCamera") : GetNode<Camera2D>(CameraPath);
		_fightCamera = _stageCamera as StageCamera;
		_fighterOne.FaceWithMovement = false;
		_fighterTwo.FaceWithMovement = false;
		_fighterOne.SetOpponent(_fighterTwo);
		_fighterTwo.SetOpponent(_fighterOne);
		RegisterPrimaryTeamFighter(_fighterOne);
		if (GetParent().GetNodeOrNull<Node2D>("ArenaBackdrop") is { } arenaBackdrop) arenaBackdrop.ZIndex = -100;
		_fighterOne.ZIndex = 0;
		_fighterTwo.ZIndex = 0;
		_hitSparkLayer = new HitSparkLayer { Name = "HitSparkLayer" };
		_hitSparkLayer.TopLevel = true;
		_hitSparkLayer.ZAsRelative = false;
		_hitSparkLayer.ZIndex = 4096;
		GetParent().CallDeferred(Node.MethodName.AddChild, _hitSparkLayer);
		_kungFuManSuperPortrait = ResourceLoader.Load<Texture2D>(KungFuManSuperPortraitPath);
		_sanzouSuperPortrait = ResourceLoader.Load<Texture2D>(SanzouSuperPortraitPath);
		_bigBangSuperCancelEffectScene = ResourceLoader.Load<PackedScene>(BigBangSuperCancelEffectPath);
		_hyperComboBackdropRandom.Randomize();
		RoundSecondsRemaining = Mathf.Max(0f, RoundTimeSeconds);
		_fighterOneLastLife = _fighterOne.PlaceholderLife;
		_fighterTwoLastLife = _fighterTwo.PlaceholderLife;
	}

	public override void _Process(double delta)
	{
		if (_pendingSuperKo)
		{
			bool superMoveDone = !GodotObject.IsInstanceValid(_pendingSuperKoAttacker) ||
				!_pendingSuperKoAttacker.CurrentAttackTriggersHyperComboFinish;
			if (superMoveDone)
			{
				RestoreFinishingSuperTimeScale();
				_hyperComboFinishOverlay?.RequestOutro();
			}
			bool finishAnimationDone = _hyperComboFinishOverlay?.IsFinished == true;
			if (finishAnimationDone && superMoveDone)
				BeginOfficialKo();
			return;
		}
		if (IsKoActive)
		{
			_koResetCountdown -= delta;
			if (_koResetCountdown <= 0.0 &&
				(_koWinner == null || _koWinner.WinAnimationFinished) &&
				(_hyperComboFinishOverlay == null || _hyperComboFinishOverlay.IsFinished))
				GetTree().ReloadCurrentScene();
			return;
		}
		RoundSecondsRemaining = Mathf.Max(0f, RoundSecondsRemaining - (float)delta);
		if (!AllowHealthToReachZero)
		{
			RecoverTrainingLife(_fighterOne, ref _fighterOneLastLife, ref _fighterOneRecoveryDelay, (float)delta);
			RecoverTrainingLife(_fighterTwo, ref _fighterTwoLastLife, ref _fighterTwoRecoveryDelay, (float)delta);
		}
	}

	private void RecoverTrainingLife(FighterController fighter, ref float lastLife, ref float recoveryDelay, float delta)
	{
		if (fighter == null) return;
		if (fighter.PlaceholderLife < lastLife)
			recoveryDelay = TrainingLifeRecoveryDelaySeconds;
		lastLife = fighter.PlaceholderLife;
		if (fighter.HitstunFramesLeft > 0 || fighter.ComboCount > 0) return;
		if (recoveryDelay > 0f)
		{
			recoveryDelay -= delta;
			return;
		}
		fighter.RecoverPlaceholderLife(TrainingLifeRecoveryPerSecond * delta);
		lastLife = fighter.PlaceholderLife;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_fighterOne == null || _fighterTwo == null) return;
		ResolveSuperBackdropCancellation();
		ResolveSuperActivations();
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		ResolveWallSplatCornerProtection(_fighterOne, _fighterTwo, leftEdge, rightEdge);
		ResolveWallSplatCornerProtection(_fighterTwo, _fighterOne, leftEdge, rightEdge);
		if (_fighterTwo.IsWallSplatSliding)
			foreach (FighterController ally in _primaryTeam)
				if (ally != _fighterOne && GodotObject.IsInstanceValid(ally))
					ResolveWallSplatCornerProtection(_fighterTwo, ally, leftEdge, rightEdge);
		UpdateFacing();
		if (_fighterOne.JustLanded) ResolveLandingOverlap(_fighterOne, _fighterTwo, leftEdge, rightEdge);
		if (_fighterTwo.JustLanded) ResolveLandingOverlap(_fighterTwo, _fighterOne, leftEdge, rightEdge);
		ResolvePushboxes(leftEdge, rightEdge);
		ResolveJumpStartEffects();
		ResolveRunDustEffects();
		ResolveBasicAttackHits();
		ResolveSpdSlamImpacts();
		for (int index = _primaryTeam.Count - 1; index >= 0; index--)
		{
			FighterController ally = _primaryTeam[index];
			if (!GodotObject.IsInstanceValid(ally))
			{
				_primaryTeam.RemoveAt(index);
				continue;
			}
			ally.MaintainSuperHitLock();
		}
		_fighterTwo.MaintainSuperHitLock();
		ResolveProjectileHits();
		ResolveAuthoredWallBounceImpacts(leftEdge, rightEdge);
		ClampFightersToCameraCorners(leftEdge, rightEdge);
		ResolveStateImpactEffects();
	}

	private void SetLatestAttackerLayer(FighterController fighter)
	{
		if (!GodotObject.IsInstanceValid(fighter)) return;
		foreach (FighterController ally in _primaryTeam)
			if (GodotObject.IsInstanceValid(ally)) ally.ZIndex = BackFighterZIndex;
		if (GodotObject.IsInstanceValid(_fighterTwo)) _fighterTwo.ZIndex = BackFighterZIndex;
		fighter.ZIndex = LatestAttackerZIndex;
	}

	private void ResolveWallSplatCornerProtection(FighterController wallSliding, FighterController other,
		float leftEdge, float rightEdge)
	{
		if (wallSliding == null || other == null || !wallSliding.IsWallSplatSliding) return;
		int openSide = wallSliding.WorldPushbox.GetCenter().X <= (leftEdge + rightEdge) * 0.5f ? 1 : -1;
		float currentDelta = other.WorldPositionBox.GetCenter().X - wallSliding.WorldPositionBox.GetCenter().X;
		if (currentDelta * openSide >= 8f) return;

		Rect2 otherLocal = GetCurrentPushboxLocal(other);
		Rect2 wallBox = wallSliding.WorldPushbox;
		float targetX = openSide > 0
			? wallBox.End.X + 4f - otherLocal.Position.X
			: wallBox.Position.X - 4f - otherLocal.End.X;
		float oldX = other.GlobalPosition.X;
		targetX = ClampOriginX(other, targetX, leftEdge, rightEdge);
		other.GlobalPosition = new Vector2(targetX, other.GlobalPosition.Y);
		other.AddVisualCorrection(new Vector2(targetX - oldX, 0f));
		if (other.Velocity.X * openSide < 0f)
			other.Velocity = new Vector2(0f, other.Velocity.Y);
	}

	private void ResolveSuperBackdropCancellation()
	{
		bool cancel = _fighterOne.ConsumeSuperBackdropCancelRequest() || _fighterTwo.ConsumeSuperBackdropCancelRequest();
		if (!cancel || !GodotObject.IsInstanceValid(_superBackdrop)) return;
		_superBackdrop.QueueFree();
		_superBackdrop = null;
	}

	private void ResolveSuperActivations()
	{
		bool firstSuper = _fighterOne.ConsumeSuperActivationData(out int firstFreezeFrames, out int firstBackdropFrames);
		bool secondSuper = _fighterTwo.ConsumeSuperActivationData(out int secondFreezeFrames, out int secondBackdropFrames);
		if (!firstSuper && !secondSuper) return;

		// Presentation rules belong to the match, not to an individual character.
		// Move data may request a longer backdrop for a long cinematic, but every
		// super receives the same ignition freeze, portrait, rings, and backdrop.
		int freezeFrames = Mathf.Max(1, UniversalSuperFreezeFrames);
		int backdropFrames = Mathf.Max(UniversalSuperBackdropMinimumFrames,
			Mathf.Max(firstBackdropFrames, secondBackdropFrames));
		FighterController activatingFighter = firstSuper ? _fighterOne : _fighterTwo;
		FighterController otherFighter = activatingFighter == _fighterOne ? _fighterTwo : _fighterOne;
		// Portrait ignition and its collapsing spark rings are shared by every
		// super, including supers with a zero-frame gameplay freeze.
		SpawnSuperPortrait(activatingFighter, otherFighter, Mathf.Max(1, freezeFrames));
		SpawnBigBangSuperCancelEffect(activatingFighter);
		if (freezeFrames > 0)
		{
			_fighterOne.RequestHitstop(freezeFrames);
			_fighterTwo.RequestHitstop(freezeFrames);
			_fightCamera?.ShakeSuper(UniversalSuperShakeStrength, freezeFrames);
		}
		SpawnSuperBackdrop(backdropFrames);
	}

	private void SpawnBigBangSuperCancelEffect(FighterController activatingFighter)
	{
		if (_bigBangSuperCancelEffectScene == null || activatingFighter == null) return;
		BigBangSuperCancelEffect effect = _bigBangSuperCancelEffectScene.Instantiate<BigBangSuperCancelEffect>();
		effect.Name = "UniversalBigBangSuperCancelEffect";
		effect.TopLevel = true;
		effect.ZAsRelative = false;
		effect.ZIndex = 4096;
		effect.Scale = Vector2.One * Mathf.Max(0.1f, UniversalSuperCancelEffectScale);
		effect.GlobalPosition = activatingFighter.WorldPositionBox.GetCenter();
		GetParent().AddChild(effect);
	}

	private void SpawnSuperPortrait(FighterController activatingFighter, FighterController otherFighter, int freezeFrames)
	{
		bool sanzou = string.Equals(activatingFighter.Definition?.FighterName,
			"Sanzou Kongoumaru", System.StringComparison.OrdinalIgnoreCase);
		bool kungFuMan = string.Equals(activatingFighter.Definition?.FighterName,
			"Kung Fu Man", System.StringComparison.OrdinalIgnoreCase);
		Texture2D portraitTexture = ResolveSuperPortrait(activatingFighter, sanzou, kungFuMan);
		if (portraitTexture == null) return;
		if (GodotObject.IsInstanceValid(_superPortrait)) _superPortrait.QueueFree();

		bool entersFromLeft = activatingFighter.GlobalPosition.X <= otherFighter.GlobalPosition.X;
		var portrait = new SuperPortraitOverlay
		{
			Name = sanzou ? "SanzouSuperPortrait9999" : $"{SanitizeNodeName(activatingFighter.Definition?.FighterName)}SuperPortrait",
			Portrait = portraitTexture,
			FightCamera = _fightCamera,
			FocusPosition = activatingFighter.GlobalPosition + new Vector2(0f, -55f),
			EntersFromLeft = entersFromLeft,
			LifetimeFrames = Mathf.Max(1, freezeFrames)
		};
		portrait.TreeExited += () =>
		{
			if (_superPortrait == portrait) _superPortrait = null;
		};
		_superPortrait = portrait;
		GetParent().AddChild(portrait);
	}

	private Texture2D ResolveSuperPortrait(FighterController fighter, bool sanzou, bool kungFuMan)
	{
		if (fighter.Definition?.SuperPortrait != null) return fighter.Definition.SuperPortrait;
		// Preserve the two existing authored cut-ins while allowing every roster
		// fighter to participate without any character-specific stage code.
		if (sanzou) return _sanzouSuperPortrait;
		if (kungFuMan) return _kungFuManSuperPortrait;
		if (fighter is not SpriteTestFighter spriteFighter) return null;
		AnimatedSprite2D sprite = spriteFighter.CharacterSprite;
		if (sprite?.SpriteFrames == null) return null;
		StringName animation = sprite.Animation;
		if (!sprite.SpriteFrames.HasAnimation(animation) || sprite.SpriteFrames.GetFrameCount(animation) <= 0)
			return null;
		return sprite.SpriteFrames.GetFrameTexture(animation,
			Mathf.Clamp(sprite.Frame, 0, sprite.SpriteFrames.GetFrameCount(animation) - 1));
	}

	private static string SanitizeNodeName(string fighterName) =>
		string.IsNullOrWhiteSpace(fighterName) ? "Fighter" : fighterName.Replace(" ", "");

	private void SpawnSuperBackdrop(int backdropFrames)
	{
		if (GodotObject.IsInstanceValid(_superBackdrop)) _superBackdrop.QueueFree();
		_superBackdrop = null;

		SuperBackdrop backdrop = new()
		{
			Name = "SuperBackdrop",
			LifetimeFrames = Mathf.Max(75, backdropFrames),
			AnimatedBackgroundPath = ChooseHyperComboBackdropPath(_hyperComboBackdropRandom.Randi()),
			FollowCamera = _stageCamera,
			ParticleCount = 110,
			ZIndex = -50
		};
		backdrop.TreeExited += () =>
		{
			if (_superBackdrop == backdrop) _superBackdrop = null;
		};
		_superBackdrop = backdrop;
		GetParent().AddChild(backdrop);
	}

	public static string ChooseHyperComboBackdropPath(ulong randomValue) =>
		HyperComboBackdropPaths[(int)(randomValue % (ulong)HyperComboBackdropPaths.Length)];

	private void UpdateFacing()
	{
		float delta = _fighterTwo.WorldPositionBox.GetCenter().X - _fighterOne.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(delta) <= FacingSideSwitchDeadZone) return;
		int direction = delta >= 0f ? 1 : -1;
		if (CanAutoFaceOpponent(_fighterOne, _fighterTwo)) _fighterOne.SetFacing(direction);
		if (CanAutoFaceOpponent(_fighterTwo, _fighterOne)) _fighterTwo.SetFacing(-direction);
	}

	private bool CanAutoFaceOpponent(FighterController fighter, FighterController opponent)
	{
		// Preserve facing through a cross-up. A grounded defender must manually
		// switch from back to the opposite direction when an airborne attacker
		// passes over them; both sides may auto-correct once that attacker lands.
		if (!IsGroundedForPushbox(opponent)) return false;
		return fighter.WasGrounded || fighter.JustLanded ||
			!fighter.SuppressesGroundedPushWhileAirborne || fighter.EnablesAirControlWhileAirborne;
	}

	private static bool IsGroundedForPushbox(FighterController fighter) =>
		fighter.IsOnFloor() || fighter.JustLanded;

	private static bool IsAirborneGroundedPushSuppressed(FighterController fighter) =>
		!IsGroundedForPushbox(fighter) && fighter.SuppressesGroundedPushWhileAirborne;

	private void ResolvePushboxes(float leftEdge, float rightEdge)
	{
		// A jump passes through a grounded opponent without body push, but two airborne
		// fighters still collide. Air dashes use the default false rule and can push ground.
		if (IsGroundedForPushbox(_fighterOne) && IsAirborneGroundedPushSuppressed(_fighterTwo))
		{
			if (_fighterTwo.JumpInteractsWithGroundedPushbox || _fighterTwo.ShortHopInteractsWithGroundedPushbox)
				ResolveAirborneVsGroundedPushbox(_fighterTwo, _fighterOne, leftEdge, rightEdge);
			return;
		}
		if (IsGroundedForPushbox(_fighterTwo) && IsAirborneGroundedPushSuppressed(_fighterOne))
		{
			if (_fighterOne.JumpInteractsWithGroundedPushbox || _fighterOne.ShortHopInteractsWithGroundedPushbox)
				ResolveAirborneVsGroundedPushbox(_fighterOne, _fighterTwo, leftEdge, rightEdge);
			return;
		}
		if (!_fighterOne.WorldPushbox.Intersects(_fighterTwo.WorldPushbox)) return;

		FighterController left = _fighterOne.WorldPushbox.GetCenter().X <= _fighterTwo.WorldPushbox.GetCenter().X ? _fighterOne : _fighterTwo;
		FighterController right = left == _fighterOne ? _fighterTwo : _fighterOne;
		float overlap = HorizontalOverlap(left.WorldPushbox, right.WorldPushbox);
		float requestedLeft = -overlap * 0.5f;
		float requestedRight = overlap * 0.5f;
		float appliedLeft = ApplyImmediateHorizontalPush(left, requestedLeft, leftEdge, rightEdge);
		ApplyImmediateHorizontalPush(right, requestedRight, leftEdge, rightEdge);

		float remaining = HorizontalOverlap(left.WorldPushbox, right.WorldPushbox);
		if (remaining > 0)
		{
			if (Mathf.Abs(appliedLeft) + 0.01f < Mathf.Abs(requestedLeft))
				ApplyImmediateHorizontalPush(right, remaining, leftEdge, rightEdge);
			else
				ApplyImmediateHorizontalPush(left, -remaining, leftEdge, rightEdge);
		}
	}

	private void ResolveAirborneVsGroundedPushbox(FighterController airborne, FighterController grounded, float leftEdge, float rightEdge)
	{
		if (!airborne.WorldPushbox.Intersects(grounded.WorldPushbox)) return;

		float overlap = HorizontalOverlap(airborne.WorldPushbox, grounded.WorldPushbox);
		if (overlap <= 0f) return;
		if (TryGetCornerProtectionOpenSide(grounded, out int openSide) && IsOnProtectedCornerSide(airborne, grounded, openSide))
		{
			ApplySmoothHorizontalPush(airborne, openSide * overlap, leftEdge, rightEdge);
			return;
		}
		float currentDelta = airborne.WorldPositionBox.GetCenter().X - grounded.WorldPositionBox.GetCenter().X;
		float previousDelta = airborne.PreviousWorldPositionBox.GetCenter().X - grounded.WorldPositionBox.GetCenter().X;
		int side = !Mathf.IsZeroApprox(currentDelta)
			? Mathf.Sign(currentDelta) >= 0 ? 1 : -1
			: (!Mathf.IsZeroApprox(previousDelta) ? (Mathf.Sign(previousDelta) >= 0 ? 1 : -1) : (airborne.Velocity.X >= 0 ? 1 : -1));

		float groundedShare = Mathf.Clamp(airborne.JumpGroundedPushStrength, 0f, 1f);
		float airborneShare = 1f - groundedShare;
		ApplySmoothHorizontalPush(airborne, side * overlap * airborneShare, leftEdge, rightEdge);
		ApplySmoothHorizontalPush(grounded, -side * overlap * groundedShare, leftEdge, rightEdge);
	}

	private void ResolveLandingOverlap(FighterController lander, FighterController opponent, float leftEdge, float rightEdge)
	{
		if (!lander.WorldPushbox.Intersects(opponent.WorldPushbox)) return;

		// Basic fighting-game landing resolver:
		// the torso/axis position box chooses side; pushboxes only provide spacing.
		int side = TryGetCornerProtectionOpenSide(opponent, out int openSide)
			? openSide
			: ChooseLandingSide(lander, opponent);
		float overlap = HorizontalOverlap(lander.WorldPushbox, opponent.WorldPushbox);
		// Resolve by pressure, not perfect placement. This lets the corner deny
		// a crossup with a basic push instead of snapping the lander across the body.
		ApplySmoothHorizontalPush(lander, side * overlap, leftEdge, rightEdge);
	}

	private int ChooseLandingSide(FighterController lander, FighterController opponent)
	{
		float previousDelta = lander.PreviousWorldPositionBox.GetCenter().X - opponent.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(previousDelta) > LandingCrossupDeadZone) return previousDelta >= 0f ? 1 : -1;

		float currentDelta = lander.WorldPositionBox.GetCenter().X - opponent.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(currentDelta) > LandingCrossupDeadZone) return currentDelta >= 0f ? 1 : -1;

		return lander.Velocity.X >= 0f ? 1 : -1;
	}

	private bool TryGetCornerProtectionOpenSide(FighterController grounded, out int openSide)
	{
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		if (grounded.WorldPushbox.Position.X <= leftEdge + CornerProtectionDistance)
		{
			openSide = 1;
			return true;
		}
		if (grounded.WorldPushbox.End.X >= rightEdge - CornerProtectionDistance)
		{
			openSide = -1;
			return true;
		}
		openSide = 0;
		return false;
	}

	private bool IsOnProtectedCornerSide(FighterController airborne, FighterController grounded, int openSide)
	{
		float delta = airborne.WorldPositionBox.GetCenter().X - grounded.WorldPositionBox.GetCenter().X;
		return delta * openSide < 0f;
	}

	private float HorizontalOverlap(Rect2 first, Rect2 second) =>
		Mathf.Max(0, Mathf.Min(first.End.X, second.End.X) - Mathf.Max(first.Position.X, second.Position.X));

	private void ApplySmoothHorizontalPush(FighterController fighter, float pushDelta, float leftEdge, float rightEdge)
	{
		if (Mathf.IsZeroApprox(pushDelta)) return;

		float oldX = fighter.GlobalPosition.X;
		float targetX = ClampOriginX(fighter, fighter.GlobalPosition.X + pushDelta, leftEdge, rightEdge);
		fighter.GlobalPosition = new Vector2(targetX, fighter.GlobalPosition.Y);
		fighter.AddVisualCorrection(new Vector2(targetX - oldX, 0f));
	}

	private float ApplyImmediateHorizontalPush(FighterController fighter, float pushDelta, float leftEdge, float rightEdge)
	{
		if (Mathf.IsZeroApprox(pushDelta)) return 0f;
		float oldX = fighter.GlobalPosition.X;
		fighter.GlobalPosition = new Vector2(ClampOriginX(fighter, oldX + pushDelta, leftEdge, rightEdge), fighter.GlobalPosition.Y);
		return fighter.GlobalPosition.X - oldX;
	}

	private void ResolveBasicAttackHits()
	{
		foreach (FighterController ally in _primaryTeam.ToArray())
			if (GodotObject.IsInstanceValid(ally)) ResolveOneBasicAttack(ally, _fighterTwo);
		ResolveOneBasicAttack(_fighterTwo, _fighterOne);
	}

	private void ResolveOneBasicAttack(FighterController attacker, FighterController defender)
	{
		bool defenderWasJuggled = defender?.HitState == FighterHitState.Juggle;
		bool groundedNormal = attacker?.CurrentAttackIsGroundedNormal == true;
		if (attacker == null || defender == null || attacker.IsSameTeam(defender) || !attacker.TryApplyBasicAttackHit(defender,
			out int hitlag, out float shake, out float pushback, out Vector2 hitPoint, out bool heavySpark)) return;

		ApplyHitstopForHit(attacker, defender, hitlag);
		if (attacker.LastContactWasParried)
		{
			_fightCamera?.Shake(Mathf.Max(4.5f, shake), Mathf.Max(8, hitlag));
			return;
		}
		// A real strike/throw contact earns foreground priority. Whiffed attacks never reorder sprites.
		SetLatestAttackerLayer(attacker);
		if (defenderWasJuggled && groundedNormal && !attacker.LastContactWasBlocked)
			TryApplyJuggleWallSplat(attacker, defender, pushback);
		if (!attacker.IsPerformingThrow) ApplyCornerPushbackTransfer(attacker, defender, pushback);
		if (attacker.LastContactWasBlocked)
			_hitSparkLayer?.SpawnBlockShield(hitPoint, defender.Facing, attacker.LastContactWasInstantBlocked);
		else
		{
			if (!attacker.IsPerformingThrow) _hitSparkLayer?.Spawn(hitPoint, heavySpark, attacker.CurrentHitSparkScene, attacker.Facing);
			float dramaticDrain = attacker.CurrentAttackName.StartsWith("SUPER")
				? 58f
				: heavySpark ? 92f : 44f;
			defender.ApplyPlaceholderLifeDrain(dramaticDrain, AllowHealthToReachZero);
			CheckForKo(attacker.CurrentAttackTriggersHyperComboFinish, attacker);
		}
		if (attacker.CurrentAttackName.StartsWith("SUPER"))
			_fightCamera?.ShakeSuper(Mathf.Max(8f, shake), Mathf.Max(12, hitlag));
		else if (shake > 0f)
			_fightCamera?.Shake(shake, hitlag);
	}

	private void TryApplyJuggleWallSplat(FighterController attacker, FighterController defender, float pushback)
	{
		if (defender.WasGrounded || pushback <= 0f) return;
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		int pushDirection = attacker.Facing >= 0 ? 1 : -1;
		float available = pushDirection > 0
			? rightEdge - defender.WorldPushbox.End.X
			: defender.WorldPushbox.Position.X - leftEdge;
		float projectedReach = JuggleWallSplatDetectionDistance + pushback * JuggleWallSplatPushProjection;
		if (available > projectedReach) return;

		float wallX = pushDirection > 0
			? MaxOriginX(defender, rightEdge)
			: MinOriginX(defender, leftEdge);
		defender.GlobalPosition = new Vector2(wallX, defender.GlobalPosition.Y);
		defender.ApplyWallSplat(pushDirection);
	}

	private void ResolveAuthoredWallBounceImpacts(float leftEdge, float rightEdge)
	{
		foreach (FighterController ally in _primaryTeam)
			if (GodotObject.IsInstanceValid(ally)) ResolveAuthoredWallBounceImpact(ally, leftEdge, rightEdge);
		ResolveAuthoredWallBounceImpact(_fighterTwo, leftEdge, rightEdge);
	}

	private void ResolveAuthoredWallBounceImpact(FighterController fighter, float leftEdge, float rightEdge)
	{
		if (fighter == null || fighter.HitState != FighterHitState.WallBounce || fighter.WasGrounded ||
			Mathf.IsZeroApprox(fighter.Velocity.X)) return;

		float detection = Mathf.Max(0f, AuthoredWallBounceDetectionDistance);
		bool hitsLeft = fighter.Velocity.X < 0f && fighter.WorldPushbox.Position.X <= leftEdge + detection;
		bool hitsRight = fighter.Velocity.X > 0f && fighter.WorldPushbox.End.X >= rightEdge - detection;
		if (!hitsLeft && !hitsRight) return;

		int wallDirection = hitsRight ? 1 : -1;
		float wallX = wallDirection > 0
			? MaxOriginX(fighter, rightEdge)
			: MinOriginX(fighter, leftEdge);
		fighter.GlobalPosition = new Vector2(wallX, fighter.GlobalPosition.Y);
		fighter.ApplyWallSplat(wallDirection);
	}

	private void ResolveStateImpactEffects()
	{
		ResolveStateImpactEffect(_fighterOne);
		ResolveStateImpactEffect(_fighterTwo);
		foreach (FighterController ally in _primaryTeam)
			if (ally != _fighterOne && GodotObject.IsInstanceValid(ally)) ResolveStateImpactEffect(ally);
	}

	private void ResolveJumpStartEffects()
	{
		foreach (FighterController ally in _primaryTeam)
			if (GodotObject.IsInstanceValid(ally)) ResolveJumpStartEffect(ally);
		ResolveJumpStartEffect(_fighterTwo);
	}

	private void ResolveJumpStartEffect(FighterController fighter)
	{
		if (fighter == null ||
			!fighter.TryConsumeJumpStartEffect(out Vector2 groundPosition, out int facing, out bool isSuperJump)) return;
		_hitSparkLayer?.SpawnJumpStart(groundPosition, facing, isSuperJump);
	}

	private void ResolveRunDustEffects()
	{
		foreach (FighterController ally in _primaryTeam)
			if (GodotObject.IsInstanceValid(ally)) ResolveRunDustEffect(ally);
		ResolveRunDustEffect(_fighterTwo);
	}

	private void ResolveRunDustEffect(FighterController fighter)
	{
		if (fighter == null ||
			!fighter.TryConsumeRunDustEffect(out Vector2 groundPosition, out int facing)) return;
		_hitSparkLayer?.SpawnRunDust(groundPosition, facing);
	}

	private void ResolveSpdSlamImpacts()
	{
		foreach (FighterController ally in _primaryTeam)
			if (GodotObject.IsInstanceValid(ally)) ResolveSpdSlamImpact(ally);
		ResolveSpdSlamImpact(_fighterTwo);
	}

	private void ResolveSpdSlamImpact(FighterController attacker)
	{
		if (attacker == null || !attacker.TryConsumeSpdSlamImpact(
			out FighterController victim, out Vector2 position, out int damage, out bool wasSuper)) return;
		_hitSparkLayer?.SpawnDust(position, wasSuper ? 30 : 18, wasSuper ? 180f : 105f);
		_fightCamera?.ShakeSuper(wasSuper ? 34f : 18f, wasSuper ? 45 : 24);
		int impactFreeze = wasSuper ? 18 : 10;
		attacker.AddHitstop(impactFreeze);
		victim?.AddHitstop(impactFreeze);
		if (victim != null)
		{
			victim.ApplyPlaceholderLifeDrain(damage > 0 ? damage : 220f, AllowHealthToReachZero);
			CheckForKo(wasSuper, attacker);
		}
	}

	private void ResolveStateImpactEffect(FighterController fighter)
	{
		if (fighter == null || !fighter.TryConsumeStateImpact(out FighterHitState state, out Vector2 position,
			out int direction, out bool followup)) return;
		StateImpactEffectProfile profile = state == FighterHitState.WallSplat
			? (followup ? WallSplatFollowupImpactEffect : WallSplatImpactEffect)
			: KnockdownLandingEffect;
		if (profile == null || !profile.Matches(state)) return;
		Vector2 effectPosition = state == FighterHitState.WallSplat
			? position + new Vector2(direction * 24f, -48f)
			: position;
		if (profile.SpawnDust)
			_hitSparkLayer?.SpawnDust(effectPosition, profile.DustParticles, profile.DustSpread);
		if (profile.SpawnWallBurst)
			_hitSparkLayer?.SpawnWallSplat(effectPosition, direction, profile.WallBurstScale);
		if (profile.ShakeStrength > 0f && profile.ShakeFrames > 0)
			_fightCamera?.Shake(profile.ShakeStrength, profile.ShakeFrames);
		if (profile.FreezeFrames > 0)
		{
			_fighterOne?.AddHitstop(profile.FreezeFrames);
			_fighterTwo?.AddHitstop(profile.FreezeFrames);
		}
	}

	private void ResolveProjectileHits()
	{
		foreach (Node node in GetTree().GetNodesInGroup(BasicProjectile.ProjectileGroup))
		{
			if (node is not BasicProjectile projectile || projectile.HasHit || !projectile.CanHit) continue;
			if (!GodotObject.IsInstanceValid(projectile.OwnerFighter))
			{
				projectile.QueueFree();
				continue;
			}
			FighterController defender = projectile.OwnerFighter.TeamId == _fighterOne.TeamId
				? _fighterTwo
				: projectile.OwnerFighter.TeamId == _fighterTwo.TeamId ? _fighterOne : null;
			if (defender == null) continue;

			bool finalProjectileHit = projectile.NextHitIsFinal;
			if (!projectile.OwnerFighter.TryApplyProjectileHit(defender, projectile.WorldHitbox, projectile.HitstunFrames, projectile.Pushback,
				projectile.HitstopFrames, projectile.ShakeStrength,
				finalProjectileHit && projectile.FinalHitKnocksDown, projectile.FinalKnockdownType, projectile.FinalKnockdownFrames,
				out int hitstop, out float shake, out _, out Vector2 hitPoint, out bool heavySpark)) continue;

			if (hitstop > 0) defender.RequestHitstop(hitstop);
			if (projectile.OwnerFighter.LastContactWasParried)
			{
				_fightCamera?.Shake(Mathf.Max(4.5f, shake), Mathf.Max(8, hitstop));
				projectile.Despawn();
				continue;
			}
			SetLatestAttackerLayer(projectile.OwnerFighter);
			if (projectile.Super)
				_fightCamera?.ShakeSuper(Mathf.Max(9f, shake), Mathf.Max(14, hitstop));
			else if (shake > 0f)
				_fightCamera?.Shake(shake, hitstop);
			if (projectile.OwnerFighter.LastContactWasBlocked)
				_hitSparkLayer?.SpawnBlockShield(hitPoint, defender.Facing,
					projectile.OwnerFighter.LastContactWasInstantBlocked);
			else
			{
				_hitSparkLayer?.Spawn(hitPoint, heavySpark, projectile.OwnerFighter.Facing);
				defender.ApplyPlaceholderLifeDrain(projectile.Damage, AllowHealthToReachZero);
				CheckForKo(projectile.Super, projectile.OwnerFighter);
			}
			projectile.MarkHit(defender, hitPoint);
		}
	}

	private void CheckForKo(bool killedBySuper, FighterController superAttacker = null)
	{
		if (!AllowHealthToReachZero || IsKoActive || _pendingSuperKo ||
			(_fighterOne.PlaceholderLife > 0f && _fighterTwo.PlaceholderLife > 0f)) return;
		if (killedBySuper)
		{
			_pendingSuperKo = true;
			_pendingSuperKoAttacker = superAttacker;
			_hyperComboFinishOverlay = GetParent().GetNodeOrNull<HyperComboFinishOverlay>(HyperComboFinishOverlayName);
			if (_hyperComboFinishOverlay == null)
			{
				_hyperComboFinishOverlay = new HyperComboFinishOverlay
				{
					Name = HyperComboFinishOverlayName,
					MinimumPresentationSeconds = Mathf.Max(0.1f, HyperComboFinishMinimumSeconds)
				};
				_hyperComboFinishOverlay.SetArenaBackdrop(
					GetParent().GetNodeOrNull<CanvasItem>("ArenaBackdrop"));
				_hyperComboFinishOverlay.SetFightCamera(_fightCamera);
				GetParent().AddChild(_hyperComboFinishOverlay);
			}
			if (GodotObject.IsInstanceValid(_superBackdrop))
			{
				_superBackdrop.QueueFree();
				_superBackdrop = null;
			}
			BeginFinishingSuperSlowMotion();
			return;
		}
		_pendingSuperKo = true;
		_pendingSuperKoAttacker = null;
		_hyperComboFinishOverlay = GetParent().GetNodeOrNull<HyperComboFinishOverlay>(HyperComboFinishOverlayName);
		if (_hyperComboFinishOverlay == null)
		{
			_hyperComboFinishOverlay = new HyperComboFinishOverlay { Name = HyperComboFinishOverlayName };
			_hyperComboFinishOverlay.SetArenaBackdrop(
				GetParent().GetNodeOrNull<CanvasItem>("ArenaBackdrop"));
			_hyperComboFinishOverlay.SetFightCamera(_fightCamera);
			GetParent().AddChild(_hyperComboFinishOverlay);
		}
		_hyperComboFinishOverlay.StartNormalKoImpact();
	}

	private void BeginOfficialKo()
	{
		RestoreFinishingSuperTimeScale();
		_pendingSuperKo = false;
		_pendingSuperKoAttacker = null;
		IsKoActive = true;
		_koWinner = _fighterOne.PlaceholderLife > 0f ? _fighterOne : _fighterTwo;
		FighterController defeated = _koWinner == _fighterOne ? _fighterTwo : _fighterOne;
		defeated?.BeginDefeatedKoState();
		_koWinner?.BeginWinAnimation();
		_koResetCountdown = Mathf.Max(0.1f, KoResetDelaySeconds);
		_fighterOne.SetPhysicsProcess(false);
		_fighterTwo.SetPhysicsProcess(false);
	}

	private void BeginFinishingSuperSlowMotion()
	{
		if (_finishingSuperSlowMotionActive) return;
		_timeScaleBeforeFinishingSuper = Engine.TimeScale;
		Engine.TimeScale = Mathf.Clamp(FinishingSuperTimeScale, 0.1f, 1f);
		_finishingSuperSlowMotionActive = true;
	}

	private void RestoreFinishingSuperTimeScale()
	{
		if (!_finishingSuperSlowMotionActive) return;
		Engine.TimeScale = _timeScaleBeforeFinishingSuper;
		_finishingSuperSlowMotionActive = false;
	}

	public override void _ExitTree()
	{
		RestoreFinishingSuperTimeScale();
	}

	private void ApplyCornerPushbackTransfer(FighterController attacker, FighterController defender, float hitPushback)
	{
		if (hitPushback <= 0f) return;
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		float pushDirection = Mathf.Sign(attacker.Facing);
		float availableSpace = pushDirection > 0f
			? rightEdge - defender.WorldPushbox.End.X
			: defender.WorldPushbox.Position.X - leftEdge;

		float blockedFraction = 1f - Mathf.Clamp(availableSpace / CornerPushbackTransferStartDistance, 0f, 1f);
		if (blockedFraction <= 0f) return;

		float moveMultiplier = !attacker.CurrentAttackStartedAirborne && attacker.CurrentAttackName.StartsWith("LIGHT")
			? GroundedLightCornerPushbackTransferMultiplier
			: 1f;
		float recoil = hitPushback * CornerPushbackTransferMultiplier * moveMultiplier * blockedFraction;
		float recoilVelocity = -pushDirection * recoil;
		if (Mathf.Abs(recoilVelocity) > Mathf.Abs(attacker.Velocity.X))
			attacker.Velocity = new Vector2(recoilVelocity, attacker.Velocity.Y);
	}

	internal static void ApplyHitstopForHit(FighterController attacker, FighterController defender, int attackerHitlag)
	{
		bool jumpInHitGroundedDefender = attacker.CurrentAttackStartedAirborne && defender.WasGrounded;
		if (attackerHitlag > 0)
			attacker.RequestHitstop(attackerHitlag, continueVerticalPhysics: jumpInHitGroundedDefender);
		int defenderHitstop = attacker.LastContactDefenderHitstopFrames;
		if (defenderHitstop > 0) defender.RequestHitstop(defenderHitstop);
	}

	private void ClampFightersToCameraCorners(float leftEdge, float rightEdge)
	{
		ClampFighterToCameraRange(_fighterOne, leftEdge, rightEdge);
		ClampFighterToCameraRange(_fighterTwo, leftEdge, rightEdge);
	}

	private void GetFightBoxEdges(out float leftEdge, out float rightEdge)
	{
		float halfView = _stageCamera == null || Mathf.IsZeroApprox(_stageCamera.Zoom.X)
			? ViewportWidth * 0.5f
			: (ViewportWidth / _stageCamera.Zoom.X) * 0.5f;
		leftEdge = _fightCamera != null ? _fightCamera.FightBoxLeft : (_stageCamera == null ? 0f : _stageCamera.GlobalPosition.X - halfView);
		rightEdge = _fightCamera != null ? _fightCamera.FightBoxRight : (_stageCamera == null ? StageWidth : _stageCamera.GlobalPosition.X + halfView);
		leftEdge = Mathf.Clamp(leftEdge, 0f, StageWidth);
		rightEdge = Mathf.Clamp(rightEdge, 0f, StageWidth);
	}

	private void ClampFighterToCameraRange(FighterController fighter, float leftEdge, float rightEdge)
	{
		Rect2 localPushbox = GetCurrentPushboxLocal(fighter);
		float min = Mathf.Max(MinOriginX(fighter), leftEdge - localPushbox.Position.X);
		float max = Mathf.Min(MaxOriginX(fighter), rightEdge - localPushbox.End.X);
		float clampedX = Mathf.Clamp(fighter.GlobalPosition.X, min, max);
		if (Mathf.IsEqualApprox(clampedX, fighter.GlobalPosition.X)) return;

		// Camera corners are game rules, not physical walls. Clamp position only so
		// movement intent/velocity remains alive and walk/run animations keep playing.
		fighter.GlobalPosition = new Vector2(clampedX, fighter.GlobalPosition.Y);
	}

	private static Rect2 GetCurrentPushboxLocal(FighterController fighter)
	{
		Rect2 world = fighter.WorldPushbox;
		return new Rect2(world.Position - fighter.GlobalPosition, world.Size);
	}

	private float MinOriginX(FighterController fighter) => -GetCurrentPushboxLocal(fighter).Position.X;
	private float MaxOriginX(FighterController fighter) => StageWidth - GetCurrentPushboxLocal(fighter).End.X;
	private float MinOriginX(FighterController fighter, float leftEdge) => Mathf.Max(MinOriginX(fighter), leftEdge - GetCurrentPushboxLocal(fighter).Position.X);
	private float MaxOriginX(FighterController fighter, float rightEdge) => Mathf.Min(MaxOriginX(fighter), rightEdge - GetCurrentPushboxLocal(fighter).End.X);
	private float ClampOriginX(FighterController fighter, float x, float leftEdge, float rightEdge) => Mathf.Clamp(x, MinOriginX(fighter, leftEdge), MaxOriginX(fighter, rightEdge));
}
