using System.Collections.Generic;
using Godot;

namespace ModularFighter.Core;

public partial class BasicProjectile : Node2D
{
	public const string ProjectileGroup = "fighter_projectiles";
	private static Shader _projectileBlackKeyShader;

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
	public bool CanHit => HitsRemaining > 0 && _hitCooldownFramesLeft <= 0 && _ageFrames >= HitStartFrame;
	public int HitStartFrame { get; private set; }
	public bool PersistsVisuallyAfterFinalHit { get; private set; }
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
	public bool ScreenCarry { get; private set; }
	public bool ElectrocutesDefender { get; private set; }
	public bool PlaysElectricitySound { get; private set; }
	public bool IsFinalVolleyProjectile { get; private set; }
	public int CarryDirection { get; private set; }
	public float AttackerDashSpeed { get; private set; }
	public int CarryFrames { get; private set; }
	public float RequiredCarryDistance { get; private set; }
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
	private int[] _visualOpacityFrames = System.Array.Empty<int>();
	private float[] _visualOpacityValues = System.Array.Empty<float>();
	private float[] _visualOpacityLossPerFrame = System.Array.Empty<float>();
	private int _postHitFadeAge = -1;
	private float _postHitOpacityLossPerFrame;
	private bool _anchoredToOwner;
	private Vector2 _ownerAnchorOffset;
	private bool _directionalHitbox;
	private sealed class TrailParticle
	{
		public Sprite2D Sprite { get; init; }
		public int AgeFrames { get; set; }
	}
	private readonly List<TrailParticle> _trailParticles = new();
	private Texture2D _trailTexture;
	private int _trailFrameSpacing = 4;
	private int _trailLifetimeFrames = 30;
	private float _trailStartOpacity = 1f;
	private float _trailScaleGrowthPerFrame;
	private float _trailOpacityLossPerFrame;
	private bool _trailBlackKey;
	private float _speedDeltaPerFrame;
	private bool _emitsAssistProjectile;
	private bool _assistProjectileEmitted;
	private int _assistProjectileSpawnFrame;
	private Vector2 _assistProjectileSpawnOffset;
	private float _assistProjectileSpeed;
	private float _assistProjectileVerticalSpeed;
	private float _assistProjectileGravity;
	private Rect2 _assistProjectileHitbox;
	private SpriteFrames _assistProjectileFrames;
	private string _assistProjectileAnimation = "";
	private Vector2 _assistProjectileVisualOffset;
	private Vector2 _assistProjectileVisualScale = Vector2.One;
	private bool _assistProjectileDirectionalHitbox;
	private string _assistProjectileGroundAnimation = "";
	private int _assistProjectileLifetimeFrames;
	private int _assistProjectileGroundLifetimeFrames;
	private bool _usesGroundImpact;
	private float _verticalSpeed;
	private float _gravity;
	private float _groundY;
	private float _groundContactOffset;
	private string _groundAnimationName = "";
	private int _groundLifetimeFrames;
	private bool _groundImpactTriggered;

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

	public void ConfigureHitWindow(int startFrame, bool persistVisualAfterFinalHit = false)
	{
		HitStartFrame = Mathf.Max(0, startFrame);
		PersistsVisuallyAfterFinalHit = persistVisualAfterFinalHit;
	}

	public void ConfigureVolleyCarry(bool enabled, int carryDirection, float carrySpeed, float attackerDashSpeed,
		int carryFrames, bool finalProjectile, bool finalOnlyKnockdown, bool electricitySound, bool electrocutesDefender,
		float requiredCarryDistance)
	{
		ScreenCarry = enabled;
		CarryDirection = carryDirection >= 0 ? 1 : -1;
		AttackerDashSpeed = Mathf.Max(0f, attackerDashSpeed);
		CarryFrames = Mathf.Max(1, carryFrames);
		IsFinalVolleyProjectile = finalProjectile;
		PlaysElectricitySound = electricitySound;
		ElectrocutesDefender = electrocutesDefender;
		RequiredCarryDistance = Mathf.Max(0f, requiredCarryDistance);
		if (enabled && !finalProjectile)
		{
			Pushback = (CarryDirection == Direction ? 1f : -1f) * Mathf.Max(0f, carrySpeed);
			Launches = true;
			LaunchGroundedOnly = false;
			LaunchSpeed = 430f;
			LaunchPushback = Pushback;
			LaunchHitstunFrames = Mathf.Max(HitstunFrames, carryFrames + 8);
		}
		if (finalOnlyKnockdown && !finalProjectile)
			FinalHitKnocksDown = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		_ageFrames++;
		if (_emitsAssistProjectile && !_assistProjectileEmitted && _ageFrames >= _assistProjectileSpawnFrame)
			EmitAssistProjectile();
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
		{
			GlobalPosition += Vector2.Right * Direction * Speed * (float)delta;
			Speed = Mathf.Max(0f, Speed + _speedDeltaPerFrame);
		}
		if (_usesGroundImpact && !_groundImpactTriggered)
		{
			_verticalSpeed += _gravity * (float)delta;
			GlobalPosition += Vector2.Down * _verticalSpeed * (float)delta;
			if (GlobalPosition.Y + _groundContactOffset >= _groundY) TriggerGroundImpact();
		}
		UpdateVisualTrail();
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
			if (PlaysElectricitySound)
				GetNodeOrNull<Node>("/root/AudioController")?.Call("play_electricity");
			if (ElectrocutesDefender)
				defender.TriggerElectrocutionPresentation(Mathf.Max(8, HitstunFrames));
			SpawnImpactEffect(contactPoint ?? GlobalPosition);
			defender.PlayMoveContactBurnPresentation(_impactBlackensDefender, _impactBlackSilhouetteFrames,
				_impactDefenderFireFrames, _impactDefenderFireAnimationName);
		}
		HitsRemaining--;
		if (HitsRemaining <= 0)
		{
			if (!PersistsVisuallyAfterFinalHit)
			{
				QueueFree();
				return;
			}
			_postHitFadeAge = _ageFrames + ResolvePostHitFadeDelayFrames();
			_postHitOpacityLossPerFrame = ResolveFinalOpacityLossPerFrame();
			int fadeFrames = Mathf.CeilToInt(255f / _postHitOpacityLossPerFrame) + 1;
			LifetimeFrames = Mathf.Max(LifetimeFrames, fadeFrames);
			if (_authoredVisual != null)
				_authoredVisual.Modulate = Colors.White;
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
		bool additiveBlend = false, bool blackKey = false)
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
			Material = ResolveProjectileMaterial(additiveBlend, blackKey)
		};
		AddChild(_authoredVisual);
		_authoredVisual.Play();
		UpdateAuthoredVisualTransform();
		QueueRedraw();
	}

	/// <summary>Emits independent source-style decoration actions from the projectile at a fixed 60 Hz cadence.</summary>
	public void ConfigureVisualTrail(SpriteFrames frames, string animationName, int count, int frameSpacing,
		float opacity, float scaleStep, int lifetimeFrames, float opacityLossPerFrame, bool blackKey = false)
	{
		if (frames == null || count <= 0 || string.IsNullOrWhiteSpace(animationName) ||
			!frames.HasAnimation(animationName)) return;
		_trailTexture = frames.GetFrameTexture(animationName, 0);
		if (_trailTexture == null) return;
		_trailFrameSpacing = Mathf.Max(1, frameSpacing);
		_trailLifetimeFrames = Mathf.Max(1, lifetimeFrames);
		_trailStartOpacity = Mathf.Clamp(opacity, 0f, 1f);
		_trailScaleGrowthPerFrame = Mathf.Max(0f, scaleStep);
		_trailOpacityLossPerFrame = Mathf.Max(0f, opacityLossPerFrame) / 255f;
		_trailBlackKey = blackKey;
		SpawnTrailParticle(); // Source O command precedes the first four-frame core drawing.
	}

	private static Material ResolveProjectileMaterial(bool additiveBlend, bool blackKey)
	{
		if (blackKey)
		{
			_projectileBlackKeyShader ??= CreateProjectileBlackKeyShader();
			return new ShaderMaterial { Shader = _projectileBlackKeyShader };
		}
		return additiveBlend
			? new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
			: null;
	}

	private static Shader CreateProjectileBlackKeyShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;
			render_mode blend_add;

			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float energy = max(texel.r, max(texel.g, texel.b));
				// The extracted fireball drawings carry opaque black ink around the glow.
				// Cut that ink completely and feather only into the illuminated pixels.
				float keyed_alpha = smoothstep(0.10, 0.28, energy) * texel.a;
				COLOR = vec4(texel.rgb, keyed_alpha);
			}
			""";
		return shader;
	}

	private void UpdateVisualTrail()
	{
		if (_trailTexture == null) return;
		for (int index = _trailParticles.Count - 1; index >= 0; index--)
		{
			TrailParticle particle = _trailParticles[index];
			particle.AgeFrames++;
			float alpha = _trailStartOpacity - _trailOpacityLossPerFrame * particle.AgeFrames;
			if (particle.AgeFrames >= _trailLifetimeFrames || alpha <= 0f)
			{
				particle.Sprite.QueueFree();
				_trailParticles.RemoveAt(index);
				continue;
			}
			float scale = 1f + _trailScaleGrowthPerFrame * particle.AgeFrames;
			particle.Sprite.Scale = Vector2.One * scale;
			particle.Sprite.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
		}
		if (_ageFrames > 0 && _ageFrames % _trailFrameSpacing == 0)
			SpawnTrailParticle();
	}

	private void SpawnTrailParticle()
	{
		var ring = new Sprite2D
		{
			Texture = _trailTexture,
			Centered = true,
			TopLevel = true,
			GlobalPosition = GlobalPosition,
			ZAsRelative = false,
			ZIndex = 4079,
			Scale = Vector2.One,
			Modulate = new Color(1f, 1f, 1f, _trailStartOpacity),
			Material = ResolveProjectileMaterial(additiveBlend: true, blackKey: _trailBlackKey)
		};
		AddChild(ring);
		_trailParticles.Add(new TrailParticle { Sprite = ring });
	}

	/// <summary>Applies the original projectile script's deterministic 60 Hz lifetime, speed, and scale commands.</summary>
	public void ConfigureSourceFormula(int lifetimeFrames, float secondarySpeed, int secondarySpeedFrame,
		Vector2 startScale, Vector2 endScale, int scaleStartFrame, int scaleEndFrame, bool bottomAnchored,
		float speedDeltaPerFrame = 0f)
	{
		LifetimeFrames = Mathf.Max(1, lifetimeFrames);
		_secondarySpeed = secondarySpeed;
		_secondarySpeedFrame = secondarySpeedFrame;
		_visualStartScale = startScale;
		_visualEndScale = endScale;
		_visualScaleStartFrame = Mathf.Max(0, scaleStartFrame);
		_visualScaleEndFrame = Mathf.Max(_visualScaleStartFrame, scaleEndFrame);
		_visualBottomAnchored = bottomAnchored;
		_speedDeltaPerFrame = speedDeltaPerFrame;
		_ageFrames = 0;
		UpdateAuthoredVisualTransform();
	}

	public void ConfigureVisualOpacityTimeline(int[] frames, float[] values, float[] lossPerFrame)
	{
		_visualOpacityFrames = frames ?? System.Array.Empty<int>();
		_visualOpacityValues = values ?? System.Array.Empty<float>();
		_visualOpacityLossPerFrame = lossPerFrame ?? System.Array.Empty<float>();
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
		int segmentCount = Mathf.Min(_visualOpacityFrames.Length,
			Mathf.Min(_visualOpacityValues.Length, _visualOpacityLossPerFrame.Length));
		if (_postHitFadeAge >= 0)
		{
			float elapsed = Mathf.Max(0, _ageFrames - _postHitFadeAge);
			float alpha = 1f - _postHitOpacityLossPerFrame * elapsed / 255f;
			_authoredVisual.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
		}
		else if (segmentCount > 0)
		{
			int segment = 0;
			for (int index = 1; index < segmentCount && _ageFrames >= _visualOpacityFrames[index]; index++)
				segment = index;
			float elapsed = Mathf.Max(0, _ageFrames - _visualOpacityFrames[segment]);
			float alpha = (_visualOpacityValues[segment] -
				_visualOpacityLossPerFrame[segment] * elapsed) / 255f;
			_authoredVisual.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
		}
	}

	private float ResolveFinalOpacityLossPerFrame()
	{
		for (int index = _visualOpacityLossPerFrame.Length - 1; index >= 0; index--)
			if (_visualOpacityLossPerFrame[index] > 0f)
				return _visualOpacityLossPerFrame[index];
		return 5f;
	}

	private int ResolvePostHitFadeDelayFrames()
	{
		for (int index = 0; index < _visualOpacityFrames.Length; index++)
			if (_visualOpacityFrames[index] > 0)
				return _visualOpacityFrames[index];
		return 1;
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

	public void ConfigureAssistEmission(bool enabled, int spawnFrame, Vector2 spawnOffset, float speed,
		float verticalSpeed, float gravity, Rect2 hitbox, SpriteFrames frames, string animationName,
		Vector2 visualOffset, Vector2 visualScale, bool directionalHitbox, string groundAnimationName, float groundContactOffset,
		int lifetimeFrames, int groundLifetimeFrames)
	{
		_emitsAssistProjectile = enabled && frames != null && !string.IsNullOrWhiteSpace(animationName);
		_assistProjectileSpawnFrame = Mathf.Max(0, spawnFrame);
		_assistProjectileSpawnOffset = spawnOffset;
		_assistProjectileSpeed = speed;
		_assistProjectileVerticalSpeed = verticalSpeed;
		_assistProjectileGravity = Mathf.Max(0f, gravity);
		_assistProjectileHitbox = hitbox;
		_assistProjectileFrames = frames;
		_assistProjectileAnimation = animationName;
		_assistProjectileVisualOffset = visualOffset;
		_assistProjectileVisualScale = visualScale;
		_assistProjectileDirectionalHitbox = directionalHitbox;
		_assistProjectileGroundAnimation = groundAnimationName;
		_groundContactOffset = Mathf.Max(0f, groundContactOffset);
		_assistProjectileLifetimeFrames = Mathf.Max(1, lifetimeFrames);
		_assistProjectileGroundLifetimeFrames = Mathf.Max(1, groundLifetimeFrames);
	}

	private void EmitAssistProjectile()
	{
		_assistProjectileEmitted = true;
		Node host = GetParent();
		if (host == null || !GodotObject.IsInstanceValid(OwnerFighter)) return;
		var assist = new BasicProjectile { Name = "KinakoYellowGhostAssist" };
		assist.GlobalPosition = GlobalPosition + new Vector2(_assistProjectileSpawnOffset.X * Direction,
			_assistProjectileSpawnOffset.Y);
		assist.Initialize(OwnerFighter, Direction, _assistProjectileSpeed, HitstunFrames, Pushback,
			HitstopFrames, ShakeStrength, Heavy, damage: Damage);
		assist.HitboxLocal = _assistProjectileHitbox;
		assist.ConfigureVisual(_assistProjectileFrames, _assistProjectileAnimation, _assistProjectileVisualOffset,
			_assistProjectileVisualScale);
		assist.ConfigureDirectionalHitbox(_assistProjectileDirectionalHitbox);
		assist.ConfigureSourceFormula(_assistProjectileLifetimeFrames, -1f, -1, Vector2.One, Vector2.One,
			0, 0, false);
		assist.ConfigureGroundImpact(_assistProjectileVerticalSpeed, _assistProjectileGravity,
			OwnerFighter.GlobalPosition.Y, _assistProjectileGroundAnimation, _groundContactOffset,
			_assistProjectileGroundLifetimeFrames);
		host.AddChild(assist);
	}

	public void ConfigureGroundImpact(float verticalSpeed, float gravity, float groundY,
		string groundAnimationName, float groundContactOffset, int groundLifetimeFrames)
	{
		_usesGroundImpact = !string.IsNullOrWhiteSpace(groundAnimationName) ||
			!Mathf.IsZeroApprox(verticalSpeed) || !Mathf.IsZeroApprox(gravity);
		_verticalSpeed = verticalSpeed;
		_gravity = Mathf.Max(0f, gravity);
		_groundY = groundY;
		_groundAnimationName = groundAnimationName;
		_groundContactOffset = Mathf.Max(0f, groundContactOffset);
		_groundLifetimeFrames = Mathf.Max(1, groundLifetimeFrames);
	}

	public void ConfigureDirectionalHitbox(bool enabled) => _directionalHitbox = enabled;

	private void TriggerGroundImpact()
	{
		_groundImpactTriggered = true;
		GlobalPosition = new Vector2(GlobalPosition.X, _groundY - _groundContactOffset);
		Speed = 0f;
		_verticalSpeed = 0f;
		LifetimeFrames = Mathf.Max(LifetimeFrames, _groundLifetimeFrames);
		SpriteFrames visualFrames = _authoredVisual?.SpriteFrames;
		if (_authoredVisual != null && visualFrames != null &&
			!string.IsNullOrWhiteSpace(_groundAnimationName) &&
			visualFrames.HasAnimation(_groundAnimationName))
		{
			_authoredVisual.Animation = _groundAnimationName;
			_authoredVisual.Play();
		}
	}

	public void Reflect(FighterController newOwner, int newDirection)
	{
		OwnerFighter = newOwner;
		Direction = newDirection >= 0 ? 1 : -1;
		_latchedDefender = null;
		_hitCooldownFramesLeft = 2;
	}

}
