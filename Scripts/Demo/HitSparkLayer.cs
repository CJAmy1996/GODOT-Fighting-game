using Godot;

namespace ModularFighter.Demo;

/// <summary>World-space combat effects, using the original BBB common assets where available.</summary>
public partial class HitSparkLayer : Node2D
{
	private const string HitSparkScenePath = "res://Effects/BigBangHitImpact.tscn";
	private const string GuardImpactScenePath = "res://Effects/BigBangGuardImpact.tscn";
	private const string DustScenePath = "res://Effects/BigBangDust.tscn";
	private const string JumpStartScenePath = "res://Effects/BigBangJumpStart.tscn";
	private const string SuperJumpStartScenePath = "res://Effects/BigBangSuperJump111To118.tscn";
	private const string WallJumpScenePath = "res://Effects/BigBangWallJump189To196.tscn";
	private const string WallHitScenePath = "res://Effects/BigBangWallHit198To204.tscn";
	private const string RunDustScenePath = "res://Effects/BigBangRunDust205To217.tscn";
	private const string SlashHitSparkScenePath = "res://Effects/GenericRevolveSlashHitSpark.tscn";
	private const string BloodHitSparkScenePath = "res://Effects/BigBangBloodHitSpark.tscn";
	private const int SparkZIndex = 4096;
	private static readonly Vector2[] AuthoredDustOffsets =
	{
		new(6f, -3f), new(-5f, -1f), new(21f, -4f),
		new(-35f, -1f), new(52f, -5f), new(-52f, -5f)
	};

	private PackedScene _hitSparkScene;
	private PackedScene _guardImpactScene;
	private PackedScene _dustScene;
	private PackedScene _jumpStartScene;
	private PackedScene _superJumpStartScene;
	private PackedScene _wallJumpScene;
	private PackedScene _wallHitScene;
	private PackedScene _runDustScene;
	private PackedScene _slashHitSparkScene;
	private PackedScene _bloodHitSparkScene;

	/// <summary>Spawn BBB action 41 at the wall contact, extending inward from either wall.</summary>
	public BigBangCommonEffect SpawnWallSplat(Vector2 position, int wallDirection, float scale = 1f)
	{
		// The source sheet grows toward +X. At the right wall it must be mirrored
		// so the animation remains inside the playfield; at the left it stays raw.
		BigBangCommonEffect effect = SpawnScene(
			_wallHitScene, position, wallDirection >= 0 ? -1 : 1);
		effect?.SetPresentationScaleMultiplier(scale);
		return effect;
	}

	/// <summary>Spawn the complete Smoke 3 trail once when a grounded run begins.</summary>
	public BigBangCommonEffect SpawnRunDust(Vector2 groundPosition, int facing)
	{
		return SpawnScene(_runDustScene, groundPosition, facing);
	}

	public void SpawnDust(Vector2 position, int particleCount, float spread)
	{
		if (_dustScene == null) return;
		// The old API expressed density as generated-particle count. BBB's source
		// instead uses four- and six-emitter smoke sets, staggered every two ticks.
		int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, particleCount) / 4f), 1, AuthoredDustOffsets.Length);
		float sourceEnvelope = 52f;
		float envelopeScale = Mathf.Max(0.2f, spread / sourceEnvelope);
		for (int i = 0; i < count; i++)
		{
			Vector2 authoredOffset = AuthoredDustOffsets[i];
			Vector2 offset = new(authoredOffset.X * envelopeScale, authoredOffset.Y);
			SpawnScene(_dustScene, position + offset, i % 2 == 0 ? 1 : -1, i * 2);
		}
	}

	public void SpawnBlockShield(Vector2 position, int defenderFacing, bool instantBlock = false)
	{
		SpawnScene(_guardImpactScene, position, defenderFacing, instantBlock: instantBlock);
	}

	/// <summary>Spawn common source section 2 at its authored jump offset.</summary>
	public BigBangCommonEffect SpawnJumpStart(Vector2 groundPosition, int fighterFacing, bool isSuperJump = false)
	{
		// Every grounded jump call site in the legacy character scripts uses O 0 10 2.
		PackedScene scene = isSuperJump ? _superJumpStartScene : _jumpStartScene;
		return SpawnScene(scene, groundPosition + new Vector2(0f, 10f), fighterFacing);
	}

	/// <summary>Spawn the reusable wall-jump launch ring at the exact wall contact.</summary>
	public BigBangCommonEffect SpawnWallJump(Vector2 wallContactPosition, int launchDirection)
	{
		return SpawnScene(_wallJumpScene, wallContactPosition, launchDirection);
	}

	public void Spawn(Vector2 position, bool heavy, int facing = 1)
	{
		SpawnScene(_hitSparkScene, position, facing);
	}

	public void Spawn(Vector2 position, bool heavy, PackedScene authoredScene, int facing = 1)
	{
		if (authoredScene != null)
		{
			Node instance = authoredScene.Instantiate();
			if (instance is Node2D node)
			{
				if (node is BigBangCommonEffect sourceEffect)
					sourceEffect.Facing = facing >= 0 ? 1 : -1;
				node.TopLevel = true;
				node.ZAsRelative = false;
				node.ZIndex = SparkZIndex;
				node.GlobalPosition = position;
				AddChild(node);
				return;
			}
			instance?.QueueFree();
		}
		Spawn(position, heavy, facing);
	}

	public void SpawnContact(Vector2 position, bool heavy, bool slash, PackedScene authoredScene, int facing = 1)
	{
		if (!slash)
		{
			Spawn(position, heavy, authoredScene, facing);
			return;
		}

		if (_slashHitSparkScene == null)
		{
			Spawn(position, heavy, authoredScene, facing);
			return;
		}

		// Common action 60 layers the slash child over the standard contact core.
		// Its action 79 child is the shared blood burst; restore both layers here.
		Spawn(position, heavy, facing);
		if (_bloodHitSparkScene != null)
			SpawnScene(_bloodHitSparkScene, position, facing);

		Node instance = _slashHitSparkScene.Instantiate();
		if (instance is not Node2D node)
		{
			instance?.QueueFree();
			Spawn(position, heavy, authoredScene, facing);
			return;
		}
		if (node is GenericSlashHitSpark slashEffect)
		{
			slashEffect.Facing = facing >= 0 ? 1 : -1;
			slashEffect.Heavy = heavy;
		}
		node.TopLevel = true;
		node.ZAsRelative = false;
		node.ZIndex = SparkZIndex;
		node.GlobalPosition = position;
		AddChild(node);
	}

	private BigBangCommonEffect SpawnScene(PackedScene scene, Vector2 position, int facing, int delayTicks = 0,
		bool instantBlock = false)
	{
		if (scene == null) return null;
		Node instance = scene.Instantiate();
		if (instance is not Node2D node)
		{
			instance?.QueueFree();
			return null;
		}
		if (node is BigBangCommonEffect sourceEffect)
		{
			sourceEffect.Facing = facing >= 0 ? 1 : -1;
			sourceEffect.DelayTicks = Mathf.Max(0, delayTicks);
			sourceEffect.InstantBlockTint = instantBlock;
		}
		node.TopLevel = true;
		node.ZAsRelative = false;
		node.ZIndex = SparkZIndex;
		node.GlobalPosition = position;
		AddChild(node);
		return node as BigBangCommonEffect;
	}

	public override void _Ready()
	{
		TopLevel = true;
		ZAsRelative = false;
		ZIndex = SparkZIndex;
		GlobalPosition = Vector2.Zero;
		if (ResourceLoader.Exists(HitSparkScenePath))
			_hitSparkScene = GD.Load<PackedScene>(HitSparkScenePath);
		if (ResourceLoader.Exists(GuardImpactScenePath))
			_guardImpactScene = GD.Load<PackedScene>(GuardImpactScenePath);
		if (ResourceLoader.Exists(DustScenePath))
			_dustScene = GD.Load<PackedScene>(DustScenePath);
		if (ResourceLoader.Exists(JumpStartScenePath))
			_jumpStartScene = GD.Load<PackedScene>(JumpStartScenePath);
		if (ResourceLoader.Exists(SuperJumpStartScenePath))
			_superJumpStartScene = GD.Load<PackedScene>(SuperJumpStartScenePath);
		if (ResourceLoader.Exists(WallJumpScenePath))
			_wallJumpScene = GD.Load<PackedScene>(WallJumpScenePath);
		if (ResourceLoader.Exists(WallHitScenePath))
			_wallHitScene = GD.Load<PackedScene>(WallHitScenePath);
		if (ResourceLoader.Exists(RunDustScenePath))
			_runDustScene = GD.Load<PackedScene>(RunDustScenePath);
		if (ResourceLoader.Exists(SlashHitSparkScenePath))
			_slashHitSparkScene = GD.Load<PackedScene>(SlashHitSparkScenePath);
		if (ResourceLoader.Exists(BloodHitSparkScenePath))
			_bloodHitSparkScene = GD.Load<PackedScene>(BloodHitSparkScenePath);
	}
}
