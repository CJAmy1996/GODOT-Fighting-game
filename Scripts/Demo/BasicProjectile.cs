using Godot;

namespace ModularFighter.Core;

public partial class BasicProjectile : Node2D
{
	public const string ProjectileGroup = "fighter_projectiles";

	[Export] public Rect2 HitboxLocal { get; set; } = new(-18f, -18f, 36f, 36f);
	[Export] public int LifetimeFrames { get; set; } = 90;

	public FighterController OwnerFighter { get; private set; }
	public int Direction { get; private set; } = 1;
	public float Speed { get; private set; } = 760f;
	public int HitstunFrames { get; private set; } = 18;
	public float Pushback { get; private set; } = 520f;
	public int HitstopFrames { get; private set; } = 6;
	public float ShakeStrength { get; private set; } = 3f;
	public float Damage { get; private set; } = 72f;
	public bool Heavy { get; private set; }
	public bool HasHit => HitsRemaining <= 0;
	public bool Super { get; private set; }
	public int HitsRemaining { get; private set; } = 1;
	public int HitCooldownFrames { get; private set; } = 4;
	public bool CanHit => HitsRemaining > 0 && _hitCooldownFramesLeft <= 0;
	public bool NextHitIsFinal => HitsRemaining == 1;
	public bool FinalHitKnocksDown { get; private set; }
	public KnockdownType FinalKnockdownType { get; private set; } = KnockdownType.SoftKnockdown;
	public int FinalKnockdownFrames { get; private set; }
	public bool LatchOnMultiHit { get; private set; } = true;
	public bool Launches { get; private set; }
	public bool LaunchGroundedOnly { get; private set; }
	public float LaunchSpeed { get; private set; }
	public float LaunchPushback { get; private set; }
	public int LaunchHitstunFrames { get; private set; }
	public Rect2 WorldHitbox
	{
		get
		{
			Vector2 localPosition = HitboxLocal.Position;
			if (_directionalHitbox && Direction < 0)
				localPosition.X = -HitboxLocal.Position.X - HitboxLocal.Size.X;
			return new Rect2(GlobalPosition + localPosition, HitboxLocal.Size);
		}
	}
	private int _hitCooldownFramesLeft;
	private FighterController _latchedDefender;
	private Vector2 _latchedDefenderOffset;
	private AnimatedSprite2D _authoredVisual;
	private Curve2D _authoredPath;
	private Vector2 _pathOrigin;
	private int _pathFrame;
	private int _pathTravelFrames;
	private bool _alignVisualToPath;
	private SpriteFrames _impactFrames;
	private string _impactAnimationName = "";
	private Vector2 _impactVisualOffset;
	private Vector2 _impactScale = Vector2.One;
	private bool _impactAdditiveBlend;
	private bool _impactBlackKey;
	private bool _impactBlackensDefender;
	private int _impactBlackSilhouetteFrames = 8;
	private SpriteFrames _impactDefenderFireFrames;
	private string _impactDefenderFireAnimationName = "";
	private int _ageFrames;
	private float _secondarySpeed = -1f;
	private int _secondarySpeedFrame = -1;
	private Vector2 _authoredVisualOffset;
	private Vector2 _visualStartScale = Vector2.One;
	private Vector2 _visualEndScale = Vector2.One;
	private int _visualScaleStartFrame;
	private int _visualScaleEndFrame;
	private bool _visualBottomAnchored;
	private bool _anchoredToOwner;
	private Vector2 _ownerAnchorOffset;
	private bool _directionalHitbox;

	public override void _Ready()
	{
		AddToGroup(ProjectileGroup);
	}

	public void Initialize(FighterController owner, int direction, float speed, int hitstunFrames, float pushback, int hitstopFrames, float shakeStrength, bool heavy,
		int hits = 1, int hitCooldownFrames = 4, bool super = false, bool finalHitKnocksDown = false,
		KnockdownType finalKnockdownType = KnockdownType.SoftKnockdown, int finalKnockdownFrames = 0,
		bool latchOnMultiHit = true, float damage = 72f)
	{
		OwnerFighter = owner;
		Direction = direction >= 0 ? 1 : -1;
		Speed = speed;
		HitstunFrames = hitstunFrames;
		Pushback = pushback;
		HitstopFrames = hitstopFrames;
		ShakeStrength = shakeStrength;
		Damage = Mathf.Max(0f, damage);
		Heavy = heavy;
		Super = super;
		HitsRemaining = Mathf.Max(1, hits);
		HitCooldownFrames = Mathf.Max(1, hitCooldownFrames);
		FinalHitKnocksDown = finalHitKnocksDown;
		FinalKnockdownType = finalKnockdownType;
		FinalKnockdownFrames = finalKnockdownFrames;
		LatchOnMultiHit = latchOnMultiHit;
		if (Super) HitboxLocal = new Rect2(-36f, -34f, 72f, 68f);
	}

	public void ConfigureLaunch(bool launches, bool groundedOnly, float launchSpeed, float launchPushback, int launchHitstunFrames)
	{
		Launches = launches;
		LaunchGroundedOnly = groundedOnly;
		LaunchSpeed = Mathf.Max(0f, launchSpeed);
		LaunchPushback = Mathf.Max(0f, launchPushback);
		LaunchHitstunFrames = Mathf.Max(1, launchHitstunFrames);
	}

	public override void _PhysicsProcess(double delta)
	{
		_ageFrames++;
		if (_secondarySpeedFrame >= 0 && _ageFrames >= _secondarySpeedFrame && _secondarySpeed >= 0f)
			Speed = _secondarySpeed;
		if (Super && HitsRemaining > 0 && GodotObject.IsInstanceValid(_latchedDefender))
			GlobalPosition = _latchedDefender.GlobalPosition + _latchedDefenderOffset;
		else if (_authoredPath != null && _pathTravelFrames > 0)
		{
			_pathFrame++;
			float distance = _authoredPath.GetBakedLength() * Mathf.Clamp(_pathFrame / (float)_pathTravelFrames, 0f, 1f);
			Vector2 point = _authoredPath.SampleBaked(distance, true);
			Vector2 nextPosition = _pathOrigin + new Vector2(point.X * Direction, point.Y);
			Vector2 travel = nextPosition - GlobalPosition;
			if (_alignVisualToPath && travel.LengthSquared() > 0.001f)
				Rotation = travel.Angle() - (Direction < 0 ? Mathf.Pi : 0f);
			GlobalPosition = nextPosition;
		}
		else if (_anchoredToOwner && GodotObject.IsInstanceValid(OwnerFighter))
			GlobalPosition = OwnerFighter.GlobalPosition + new Vector2(_ownerAnchorOffset.X * Direction, _ownerAnchorOffset.Y);
		else
			GlobalPosition += Vector2.Right * Direction * Speed * (float)delta;
		if (_hitCooldownFramesLeft > 0) _hitCooldownFramesLeft--;
		UpdateAuthoredVisualTransform();
		LifetimeFrames--;
		if (LifetimeFrames <= 0) QueueFree();
	}

	public void MarkHit(FighterController defender = null, Vector2? contactPoint = null)
	{
		bool confirmedHit = defender != null && OwnerFighter != null &&
			!OwnerFighter.LastContactWasBlocked && !OwnerFighter.LastContactWasParried;
		if (confirmedHit)
		{
			SpawnImpactEffect(contactPoint ?? GlobalPosition);
			defender.PlayMoveContactBurnPresentation(_impactBlackensDefender, _impactBlackSilhouetteFrames,
				_impactDefenderFireFrames, _impactDefenderFireAnimationName);
		}
		HitsRemaining--;
		if (HitsRemaining <= 0)
		{
			QueueFree();
			return;
		}
		if (Super && LatchOnMultiHit && defender != null)
		{
			_latchedDefender = defender;
			_latchedDefenderOffset = GlobalPosition - defender.GlobalPosition;
		}
		_hitCooldownFramesLeft = HitCooldownFrames;
	}

	public void ConfigureImpact(SpriteFrames frames, string animationName, Vector2 visualOffset, Vector2 scale,
		bool additiveBlend = false, bool blackKey = false, bool blackensDefender = false,
		int blackSilhouetteFrames = 8, SpriteFrames defenderFireFrames = null,
		string defenderFireAnimationName = "")
	{
		_impactFrames = frames;
		_impactAnimationName = animationName;
		_impactVisualOffset = visualOffset;
		_impactScale = scale;
		_impactAdditiveBlend = additiveBlend;
		_impactBlackKey = blackKey;
		_impactBlackensDefender = blackensDefender;
		_impactBlackSilhouetteFrames = Mathf.Max(1, blackSilhouetteFrames);
		_impactDefenderFireFrames = defenderFireFrames;
		_impactDefenderFireAnimationName = defenderFireAnimationName;
	}

	private void SpawnImpactEffect(Vector2 contactPoint)
	{
		if (_impactFrames == null || string.IsNullOrWhiteSpace(_impactAnimationName) ||
			!_impactFrames.HasAnimation(_impactAnimationName)) return;
		if (_impactAnimationName.Contains("explosion", System.StringComparison.OrdinalIgnoreCase))
			GetNodeOrNull<Node>("/root/AudioController")?.Call("play_explosion");
		Node effectHost = GetParent();
		if (effectHost == null) return;
		var effect = new MoveVisualEffect
		{
			Name = "Projectile Impact",
			TopLevel = true,
			ZAsRelative = false,
			ZIndex = 4095
		};
		effectHost.AddChild(effect);
		effect.GlobalPosition = contactPoint;
		effect.Initialize(_impactFrames, _impactAnimationName, Direction, _impactScale, _impactVisualOffset,
			_impactAdditiveBlend, _impactBlackKey);
	}

	public void Despawn() => QueueFree();

	public void ConfigureVisual(SpriteFrames frames, string animationName, Vector2 offset, Vector2 scale,
		bool additiveBlend = false)
	{
		if (frames == null || string.IsNullOrWhiteSpace(animationName) || !frames.HasAnimation(animationName)) return;
		_authoredVisualOffset = offset;
		_visualStartScale = scale;
		_visualEndScale = scale;
		_authoredVisual = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Animation = animationName,
			Position = offset,
			Scale = new Vector2(scale.X * Direction, scale.Y),
			Centered = true,
			ZAsRelative = false,
			ZIndex = 4080,
			Material = additiveBlend
				? new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
				: null
		};
		AddChild(_authoredVisual);
		_authoredVisual.Play();
		UpdateAuthoredVisualTransform();
		QueueRedraw();
	}

	/// <summary>Applies the original projectile script's deterministic 60 Hz lifetime, speed, and scale commands.</summary>
	public void ConfigureSourceFormula(int lifetimeFrames, float secondarySpeed, int secondarySpeedFrame,
		Vector2 startScale, Vector2 endScale, int scaleStartFrame, int scaleEndFrame, bool bottomAnchored)
	{
		LifetimeFrames = Mathf.Max(1, lifetimeFrames);
		_secondarySpeed = secondarySpeed;
		_secondarySpeedFrame = secondarySpeedFrame;
		_visualStartScale = startScale;
		_visualEndScale = endScale;
		_visualScaleStartFrame = Mathf.Max(0, scaleStartFrame);
		_visualScaleEndFrame = Mathf.Max(_visualScaleStartFrame, scaleEndFrame);
		_visualBottomAnchored = bottomAnchored;
		_ageFrames = 0;
		UpdateAuthoredVisualTransform();
	}

	public void ConfigureOwnerAnchor(Vector2 ownerOffset, bool directionalHitbox)
	{
		_anchoredToOwner = true;
		_ownerAnchorOffset = ownerOffset;
		_directionalHitbox = directionalHitbox;
	}

	private void UpdateAuthoredVisualTransform()
	{
		if (_authoredVisual == null) return;
		float weight = _visualScaleEndFrame <= _visualScaleStartFrame
			? (_ageFrames >= _visualScaleEndFrame ? 1f : 0f)
			: Mathf.Clamp((_ageFrames - _visualScaleStartFrame) /
				(float)(_visualScaleEndFrame - _visualScaleStartFrame), 0f, 1f);
		Vector2 resolvedScale = _visualStartScale.Lerp(_visualEndScale, weight);
		_authoredVisual.Scale = new Vector2(resolvedScale.X * Direction, resolvedScale.Y);
		Vector2 position = _authoredVisualOffset;
		if (_directionalHitbox) position.X *= Direction;
		if (_visualBottomAnchored && _authoredVisual.SpriteFrames != null)
		{
			Texture2D texture = _authoredVisual.SpriteFrames.GetFrameTexture(_authoredVisual.Animation, _authoredVisual.Frame);
			if (texture != null) position.Y -= texture.GetHeight() * resolvedScale.Y * 0.5f;
		}
		_authoredVisual.Position = position;
	}

	public void ConfigurePath(Curve2D path, int travelFrames, bool alignVisualToPath = false)
	{
		if (path == null || path.PointCount < 2) return;
		_authoredPath = path;
		_pathOrigin = GlobalPosition;
		_pathFrame = 0;
		_pathTravelFrames = Mathf.Max(1, travelFrames);
		_alignVisualToPath = alignVisualToPath;
		LifetimeFrames = Mathf.Max(LifetimeFrames, _pathTravelFrames + 1);
	}

	public void Reflect(FighterController newOwner, int newDirection)
	{
		OwnerFighter = newOwner;
		Direction = newDirection >= 0 ? 1 : -1;
		_latchedDefender = null;
		_hitCooldownFramesLeft = 2;
	}

}
