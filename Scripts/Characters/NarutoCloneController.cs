using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Characters;

/// <summary>
/// Two-slot clone gameplay prototype. Each special button owns one clone slot:
/// first press summons it, later presses transfer control until its lifetime expires.
/// </summary>
public partial class NarutoCloneController : Node
{
	[Export] public NodePath OriginalFighterPath { get; set; }
	[Export] public NodePath OpponentPath { get; set; }
	[Export] public NodePath StageRulesPath { get; set; }
	[Export] public NodePath StageCameraPath { get; set; }
	[Export] public PackedScene CloneScene { get; set; }
	[Export] public int CloneLifetimeFrames { get; set; } = 600;
	[Export] public float FirstCloneForwardOffset { get; set; } = 135f;
	[Export] public float SecondCloneRearOffset { get; set; } = 115f;
	[Export] public float SecondCloneSpawnHeight { get; set; } = 165f;
	[Export] public float SecondCloneFallSpeed { get; set; } = 430f;

	private FighterController _original;
	private FighterController _opponent;
	private VersusStageRules _stageRules;
	private StageCamera _stageCamera;
	private FighterController _controlled;
	private readonly CloneSlot[] _slots = { new(), new() };

	private sealed class CloneSlot
	{
		public FighterController Fighter;
		public int FramesLeft;
		public bool EntranceActive;
		public bool EntranceCommandSent;
		public bool EntranceSawAttack;
	}

	public override void _Ready()
	{
		_original = GetNode<FighterController>(OriginalFighterPath);
		_opponent = GetNode<FighterController>(OpponentPath);
		_stageRules = GetNodeOrNull<VersusStageRules>(StageRulesPath);
		_stageCamera = GetNodeOrNull<StageCamera>(StageCameraPath);
		SwitchControl(_original);
		if (_original is SanzoKongoumaruFighter ||
			string.Equals(_original.Definition?.FighterName, "Sanzou Kongoumaru", System.StringComparison.OrdinalIgnoreCase))
		{
			// Defensive guard for custom arenas: Sanzou never owns clone slots.
			SetPhysicsProcess(false);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		TickCloneLifetime(0);
		TickCloneLifetime(1);
		TickCloneEntrance(0);
		TickCloneEntrance(1);

		if (Input.IsActionJustPressed("special_1")) ActivateSlot(0);
		if (Input.IsActionJustPressed("special_2")) ActivateSlot(1);

		// Bodies not controlled by the player stay neutral instead of repeating
		// the last command they received before a control transfer.
		if (_original != _controlled) _original.SetExternalInput(default);
		for (int index = 0; index < _slots.Length; index++)
			if (GodotObject.IsInstanceValid(_slots[index].Fighter) && _slots[index].Fighter != _controlled &&
				!_slots[index].EntranceActive)
				_slots[index].Fighter.SetExternalInput(default);
	}

	private void ActivateSlot(int index)
	{
		CloneSlot slot = _slots[index];
		if (!GodotObject.IsInstanceValid(slot.Fighter))
		{
			if (!GodotObject.IsInstanceValid(_controlled) || !_controlled.TryBeginCloneCall()) return;
			SummonClone(index);
			return;
		}
		if (slot.EntranceActive) return;
		SwitchControl(_controlled == slot.Fighter ? _original : slot.Fighter);
	}

	private void SummonClone(int index)
	{
		if (CloneScene == null || !GodotObject.IsInstanceValid(_controlled)) return;
		FighterController clone = CloneScene.Instantiate<FighterController>();
		clone.Name = index == 0 ? "NarutoCloneS1" : "NarutoCloneS2";
		clone.ReadLocalInput = false;
		CopyKungFuManPhysicsProfile(_original, clone);
		FighterCollisionPolicy.Apply(clone);
		float offset = index == 0 ? FirstCloneForwardOffset * _controlled.Facing : -SecondCloneRearOffset * _controlled.Facing;
		float height = index == 1 ? -SecondCloneSpawnHeight : 0f;
		clone.GlobalPosition = _controlled.GlobalPosition + new Vector2(offset, height);
		clone.Velocity = index == 1 ? new Vector2(0f, SecondCloneFallSpeed) : Vector2.Zero;
		GetParent().AddChild(clone);
		if (index == 0) clone.ApplyFloorSnap();
		clone.SetFacing(_controlled.Facing);
		clone.SetOpponent(_opponent);
		_stageRules?.RegisterPrimaryTeamFighter(clone);
		MoveCloneOutOfOpponent(clone);
		TintClone(clone, index);
		SpawnSmoke(clone.GlobalPosition);

		_slots[index].Fighter = clone;
		_slots[index].FramesLeft = 0;
		_slots[index].EntranceActive = true;
		_slots[index].EntranceCommandSent = false;
		_slots[index].EntranceSawAttack = false;
		SetStandby(clone, true);
	}

	private void TickCloneEntrance(int index)
	{
		CloneSlot slot = _slots[index];
		if (!slot.EntranceActive || !GodotObject.IsInstanceValid(slot.Fighter)) return;
		FighterController clone = slot.Fighter;
		if (!slot.EntranceCommandSent)
		{
			FighterInput entrance = index == 0
				? new FighterInput(clone.Facing, 0f, false, false, false, false,
					heavyPunchPressed: true, heavyPunchHeld: true)
				: new FighterInput(0f, 0f, false, false, false, false,
					heavyKickPressed: true, heavyKickHeld: true);
			clone.SetExternalInput(entrance);
			slot.EntranceCommandSent = true;
			return;
		}

		clone.SetExternalInput(default);
		if (clone.IsAttacking)
		{
			slot.EntranceSawAttack = true;
			return;
		}
		if (!slot.EntranceSawAttack || (index == 1 && !clone.WasGrounded)) return;

		slot.EntranceActive = false;
		slot.FramesLeft = Mathf.Max(1, CloneLifetimeFrames);
		SwitchControl(clone);
	}

	private void TickCloneLifetime(int index)
	{
		CloneSlot slot = _slots[index];
		if (!GodotObject.IsInstanceValid(slot.Fighter)) return;
		slot.FramesLeft--;
		// A projectile keeps its owner alive because hit resolution/logging still
		// needs the fighter snapshot. The clone is freed as soon as its last attack
		// and projectile are both finished.
		if (slot.EntranceActive || slot.FramesLeft > 0 || slot.Fighter.IsAttacking || HasLiveOwnedProjectile(slot.Fighter)) return;

		FighterController expired = slot.Fighter;
		if (_controlled == expired) SwitchControl(_original);
		SpawnSmoke(expired.GlobalPosition);
		slot.Fighter = null;
		slot.FramesLeft = 0;
		_stageRules?.UnregisterPrimaryTeamFighter(expired);
		expired.QueueFree();
	}

	private void SpawnSmoke(Vector2 worldPosition)
	{
		CloneSmokeBurst smoke = new() { GlobalPosition = worldPosition + new Vector2(0f, -48f) };
		GetParent().AddChild(smoke);
	}

	private void SwitchControl(FighterController fighter)
	{
		if (!GodotObject.IsInstanceValid(fighter)) fighter = _original;
		if (!GodotObject.IsInstanceValid(fighter)) return;
		if (GodotObject.IsInstanceValid(_controlled))
			SetStandby(_controlled, true);

		_controlled = fighter;
		SetStandby(_controlled, false);
		_controlled.SetOpponent(_opponent);
		_opponent?.SetOpponent(_controlled);
		_stageRules?.SetPrimaryFighter(_controlled);
		_stageCamera?.SetPrimaryFighter(_controlled);
	}

	private static void SetStandby(FighterController fighter, bool standby)
	{
		if (!GodotObject.IsInstanceValid(fighter)) return;
		fighter.ReadLocalInput = !standby;
		fighter.SetPointCollisionParticipation(!standby);
		fighter.SetExternalInput(default);
	}

	private bool HasLiveOwnedProjectile(FighterController owner)
	{
		foreach (Node node in GetTree().GetNodesInGroup(BasicProjectile.ProjectileGroup))
			if (node is BasicProjectile projectile && projectile.OwnerFighter == owner && !projectile.IsQueuedForDeletion())
				return true;
		return false;
	}

	private static void CopyKungFuManPhysicsProfile(FighterController source, FighterController target)
	{
		if (!GodotObject.IsInstanceValid(source) || !GodotObject.IsInstanceValid(target)) return;
		// Share the exact same data resource so walk friction, acceleration, gravity,
		// jump abilities, air rules, attacks, and cancels cannot diverge by clone slot.
		target.Definition = source.Definition;
		target.TeamId = source.TeamId;
		target.MotionMode = source.MotionMode;
		target.UpDirection = source.UpDirection;
		target.FloorStopOnSlope = source.FloorStopOnSlope;
		target.FloorConstantSpeed = source.FloorConstantSpeed;
		target.FloorBlockOnWall = source.FloorBlockOnWall;
		target.FloorMaxAngle = source.FloorMaxAngle;
		target.FloorSnapLength = source.FloorSnapLength;
		target.MaxSlides = source.MaxSlides;
		target.SafeMargin = source.SafeMargin;
		target.PushboxLocal = source.PushboxLocal;
		target.AirbornePushboxLocal = source.AirbornePushboxLocal;
		target.HurtboxLocal = source.HurtboxLocal;
		target.PositionBoxLocal = source.PositionBoxLocal;
	}

	private void MoveCloneOutOfOpponent(FighterController clone)
	{
		if (!GodotObject.IsInstanceValid(clone) || !GodotObject.IsInstanceValid(_opponent) ||
			!clone.WorldPushbox.Intersects(_opponent.WorldPushbox)) return;

		const float separation = 10f;
		bool cloneBelongsOnLeft = _controlled.GlobalPosition.X <= _opponent.GlobalPosition.X;
		float safeX = cloneBelongsOnLeft
			? _opponent.WorldPushbox.Position.X - separation - clone.ActivePushboxLocal.End.X
			: _opponent.WorldPushbox.End.X + separation - clone.ActivePushboxLocal.Position.X;
		clone.GlobalPosition = new Vector2(safeX, clone.GlobalPosition.Y);
	}

	private static void TintClone(FighterController clone, int index)
	{
		Color tint = index == 0 ? new Color(0.58f, 0.82f, 1f, 0.78f) : new Color(1f, 0.72f, 0.42f, 0.78f);
		if (clone.GetNodeOrNull<CanvasItem>("CharacterSprite") is { } sprite) sprite.Modulate = tint;
	}
}
