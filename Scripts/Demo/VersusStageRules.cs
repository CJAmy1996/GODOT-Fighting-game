using Godot;
using ModularFighter.Core;
using System.Collections.Generic;

namespace ModularFighter.Demo;

/// <summary>Shared versus rules: opponent-facing and Street Fighter-style pushboxes.</summary>
public partial class VersusStageRules : Node
{
	private const string KungFuManSuperPortraitPath = "res://Assets/TestFighter/KungFuMan/kung_fu_man_super_portrait.png";
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath FighterTwoPath { get; set; }
	[Export] public NodePath CameraPath { get; set; }
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

	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private Camera2D _stageCamera;
	private StageCamera _fightCamera;
	private HitSparkLayer _hitSparkLayer;
	private SuperBackdrop _superBackdrop;
	private SuperPortraitOverlay _superPortrait;
	private Texture2D _kungFuManSuperPortrait;
	private readonly List<FighterController> _primaryTeam = new();

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
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_fighterOne == null || _fighterTwo == null) return;
		ResolveSuperBackdropCancellation();
		ResolveSuperActivations();
		UpdateFacing();
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		if (_fighterOne.JustLanded) ResolveLandingOverlap(_fighterOne, _fighterTwo, leftEdge, rightEdge);
		if (_fighterTwo.JustLanded) ResolveLandingOverlap(_fighterTwo, _fighterOne, leftEdge, rightEdge);
		ResolvePushboxes(leftEdge, rightEdge);
		ResolveBasicAttackHits();
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
		ClampFightersToCameraCorners(leftEdge, rightEdge);
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

		int freezeFrames = Mathf.Max(firstFreezeFrames, secondFreezeFrames);
		int backdropFrames = Mathf.Max(Mathf.Max(firstBackdropFrames, secondBackdropFrames), freezeFrames);
		if (freezeFrames > 0)
		{
			_fighterOne.RequestHitstop(freezeFrames);
			_fighterTwo.RequestHitstop(freezeFrames);
			_fightCamera?.ShakeSuper(8.5f, freezeFrames);
			FighterController activatingFighter = firstSuper ? _fighterOne : _fighterTwo;
			FighterController otherFighter = activatingFighter == _fighterOne ? _fighterTwo : _fighterOne;
			SpawnSuperPortrait(activatingFighter, otherFighter, freezeFrames);
		}
		SpawnSuperBackdrop(backdropFrames);
	}

	private void SpawnSuperPortrait(FighterController activatingFighter, FighterController otherFighter, int freezeFrames)
	{
		if (_kungFuManSuperPortrait == null || activatingFighter is not SpriteTestFighter) return;
		if (GodotObject.IsInstanceValid(_superPortrait)) _superPortrait.QueueFree();

		bool entersFromLeft = activatingFighter.GlobalPosition.X <= otherFighter.GlobalPosition.X;
		var portrait = new SuperPortraitOverlay
		{
			Name = "KungFuManSuperPortrait",
			Portrait = _kungFuManSuperPortrait,
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

	private void SpawnSuperBackdrop(int backdropFrames)
	{
		if (GodotObject.IsInstanceValid(_superBackdrop)) _superBackdrop.QueueFree();
		_superBackdrop = null;

		SuperBackdrop backdrop = new()
		{
			Name = "SuperBackdrop",
			LifetimeFrames = Mathf.Max(75, backdropFrames),
			Width = ViewportWidth + 240f,
			Height = 900f,
			ParticleCount = 110,
			ZIndex = -50
		};
		Vector2 center = _stageCamera?.GlobalPosition ?? new Vector2(ViewportWidth * 0.5f, 450f);
		backdrop.GlobalPosition = new Vector2(center.X - backdrop.Width * 0.5f, center.Y - backdrop.Height * 0.5f);
		backdrop.TreeExited += () =>
		{
			if (_superBackdrop == backdrop) _superBackdrop = null;
		};
		_superBackdrop = backdrop;
		GetParent().AddChild(backdrop);
	}

	private void UpdateFacing()
	{
		float delta = _fighterTwo.WorldPositionBox.GetCenter().X - _fighterOne.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(delta) <= FacingSideSwitchDeadZone) return;
		int direction = delta >= 0f ? 1 : -1;
		if (CanAutoFaceOpponent(_fighterOne)) _fighterOne.SetFacing(direction);
		if (CanAutoFaceOpponent(_fighterTwo)) _fighterTwo.SetFacing(-direction);
	}

	private bool CanAutoFaceOpponent(FighterController fighter) =>
		fighter.WasGrounded || fighter.JustLanded || !fighter.SuppressesGroundedPushWhileAirborne || fighter.EnablesAirControlWhileAirborne;

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
		if (attacker == null || defender == null || attacker.IsSameTeam(defender) || !attacker.TryApplyBasicAttackHit(defender,
			out int hitstop, out float shake, out float pushback, out Vector2 hitPoint, out bool heavySpark)) return;

		ApplyHitstopForHit(attacker, defender, hitstop);
		if (!attacker.IsPerformingThrow) ApplyCornerPushbackTransfer(attacker, defender, pushback);
		if (attacker.LastContactWasBlocked) _hitSparkLayer?.SpawnBlockShield(hitPoint, defender.Facing);
		else if (!attacker.IsPerformingThrow) _hitSparkLayer?.Spawn(hitPoint, heavySpark);
		if (attacker.CurrentAttackName.StartsWith("SUPER"))
			_fightCamera?.ShakeSuper(Mathf.Max(8f, shake), Mathf.Max(12, hitstop));
		else if (shake > 0f)
			_fightCamera?.Shake(shake, hitstop);
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
			if (projectile.Super)
				_fightCamera?.ShakeSuper(Mathf.Max(9f, shake), Mathf.Max(14, hitstop));
			else if (shake > 0f)
				_fightCamera?.Shake(shake, hitstop);
			if (projectile.OwnerFighter.LastContactWasBlocked) _hitSparkLayer?.SpawnBlockShield(hitPoint, defender.Facing);
			else _hitSparkLayer?.Spawn(hitPoint, heavySpark);
			projectile.MarkHit(defender);
		}
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

	private static void ApplyHitstopForHit(FighterController attacker, FighterController defender, int hitstop)
	{
		if (hitstop <= 0) return;
		bool airAttackHitGroundedDefender = attacker.CurrentAttackStartedAirborne && defender.WasGrounded;
		int attackerHitstop = airAttackHitGroundedDefender
			? System.Math.Min(hitstop, attacker.CurrentAirToGroundAttackerHitstopFrames)
			: hitstop;
		if (attackerHitstop > 0) attacker.RequestHitstop(attackerHitstop);
		defender.RequestHitstop(hitstop);
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
