using System;
using System.Collections.Generic;
using Godot;

namespace ModularFighter.Demo;

public enum BigBangCommonEffectKind
{
	HitWeak,
	HitStrong,
	GuardImpact,
	Dust,
	BloodBurst,
	JumpStart,
	Numbered000To015,
	BlockCandidate039To045,
	GroundBounce055To061,
	Burst071To079,
	SmallDust080To089,
	SuperJump111To118,
	Unassigned120To158,
	TechniqueHitRing161To168,
	AdditiveImpact181To186,
	WallJump189To196,
	WallHit198To204,
	RunDust205To217,
	BStonePickup230To236,
	AirDashParticle272,
	ParticleB273,
	UnusedCore274,
	EnergyRingCandidate275To280
}

/// <summary>
/// Plays the original Big Bang Beat common effects at the source-authored 60 Hz
/// timing, with every drawing positioned by its script origin instead of by the
/// center of the PNG.
/// </summary>
public partial class BigBangCommonEffect : Node2D
{
	private const string AssetRoot = "res://Assets/Effects/BigBangCommon";
	private static Shader _greenKeyShader;
	private static Shader _additiveBlackKeyShader;

	[Export] public BigBangCommonEffectKind EffectKind { get; set; }
	[Export] public int Facing { get; set; } = 1;
	[Export] public int DelayTicks { get; set; }
	[Export] public bool InstantBlockTint { get; set; }

	private Sprite2D _sprite;
	private EffectSpec _spec;
	private Texture2D[] _textures = Array.Empty<Texture2D>();
	private Vector2[] _textureCropOffsets = Array.Empty<Vector2>();
	private int _age;
	private int _drawingIndex = -1;
	private float _currentAuthoredScale = 1f;
	private float _presentationScaleMultiplier = 1f;
	private readonly List<BloodParticle> _bloodParticles = new();

	public int CurrentTick => Mathf.Max(0, _age);
	public int CurrentDrawingIndex => _drawingIndex;
	public int CurrentSourceFrame => _drawingIndex >= 0 ? _spec.SourceFrames[_drawingIndex] : -1;
	public int CurrentTextureFrame => _drawingIndex >= 0 ? _spec.TextureFrames[_drawingIndex] : -1;
	public int TotalTicks => _spec?.TotalTicks ?? 0;
	public Vector2 CurrentSourceOrigin => _drawingIndex >= 0 ? _spec.Origins[_drawingIndex] : Vector2.Zero;
	public Vector2 CurrentSpritePosition => _sprite?.Position ?? Vector2.Zero;
	public float CurrentAuthoredScale => _currentAuthoredScale;
	public float PresentationScaleMultiplier => _presentationScaleMultiplier;
	public Sprite2D EffectSprite => _sprite;
	public int BloodParticleCount => _bloodParticles.Count;
	public bool UsesAdditiveBlackKey => _spec?.AdditiveBlackKey ?? false;

	public override void _Ready()
	{
		_spec = BuildSpec(EffectKind);
		_textures = new Texture2D[_spec.TextureFrames.Length];
		_textureCropOffsets = new Vector2[_spec.TextureFrames.Length];
		for (int i = 0; i < _textures.Length; i++)
		{
			string textureFolder = InstantBlockTint && EffectKind == BigBangCommonEffectKind.GuardImpact
				? $"{AssetRoot}/PerfectBlock"
				: AssetRoot;
			Texture2D sourceTexture = GD.Load<Texture2D>($"{textureFolder}/{_spec.TextureFrames[i]:D3}.png");
			(_textures[i], _textureCropOffsets[i]) = CropToVisiblePixels(sourceTexture, _spec.AdditiveBlackKey);
		}

		Shader effectShader;
		if (_spec.AdditiveBlackKey)
		{
			_additiveBlackKeyShader ??= CreateAdditiveBlackKeyShader();
			effectShader = _additiveBlackKeyShader;
		}
		else
		{
			_greenKeyShader ??= CreateGreenKeyShader();
			effectShader = _greenKeyShader;
		}
		_sprite = new Sprite2D
		{
			Name = "SourceDrawing",
			Centered = false,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Material = new ShaderMaterial { Shader = effectShader }
		};
		AddChild(_sprite);
		if (EffectKind == BigBangCommonEffectKind.BloodBurst)
			SpawnSourceBloodParticles();

		_age = -Mathf.Max(0, DelayTicks);
		Visible = _age >= 0;
		if (Visible) ApplyCurrentTick();
	}

	public override void _PhysicsProcess(double delta)
	{
		AdvanceOneTick();
		AdvanceBloodParticles((float)delta);
	}

	public void AdvanceOneTick()
	{
		_age++;
		if (_age < 0) return;
		if (!Visible) Visible = true;
		if (_age >= _spec.TotalTicks)
		{
			if (EffectKind == BigBangCommonEffectKind.BloodBurst && _age < 60)
			{
				_sprite.Visible = false;
				return;
			}
			QueueFree();
			return;
		}
		ApplyCurrentTick();
	}

	/// <summary>Scale an authored effect without disturbing its source origins or timing.</summary>
	public void SetPresentationScaleMultiplier(float multiplier)
	{
		_presentationScaleMultiplier = Mathf.Max(0.1f, multiplier);
		if (_spec != null && _age >= 0 && _age < _spec.TotalTicks)
			ApplyCurrentTick();
	}

	private void ApplyCurrentTick()
	{
		int drawing = ResolveDrawing(_age, _spec.Holds);
		if (drawing != _drawingIndex)
		{
			_drawingIndex = drawing;
			_sprite.Texture = _textures[drawing];
			if (EffectKind == BigBangCommonEffectKind.GuardImpact)
			{
				// Guard sparks belong to the collision point. Center every cropped
				// drawing there so changing source origins cannot make the animation
				// climb away from the part of the defender that was actually hit.
				_sprite.Position = -_textures[drawing].GetSize() * 0.5f;
			}
			else
			{
				// BBB's I command places the source origin at the effect coordinate.
				// The crop removes only invisible canvas. Add its source-space offset
				// back so every visible pixel remains at the exact authored position.
				_sprite.Position = -_spec.Origins[drawing] + _textureCropOffsets[drawing];
			}
		}
		if (EffectKind == BigBangCommonEffectKind.BloodBurst)
		{
			// Source M command: initial Y -100 with +4 per authored tick.
			float seconds = _age / 60f;
			_sprite.Position += new Vector2(0f, -100f * seconds + 0.5f * 240f * seconds * seconds);
		}

		float authoredScale = _spec.InitialScale;
		if (_spec.ScaleGrowthPerTick > 0f)
			authoredScale = Mathf.Min(_spec.MaximumScale,
				_spec.InitialScale + Mathf.Min(_age, _spec.ScaleGrowthTicks - 1) * _spec.ScaleGrowthPerTick);
		int horizontalSign = (Facing >= 0 ? 1 : -1) * (_spec.SourceFlip ? -1 : 1);
		_currentAuthoredScale = authoredScale;
		float presentationScale = authoredScale * _presentationScaleMultiplier;
		Scale = new Vector2(horizontalSign * presentationScale, presentationScale);
	}

	private static (Texture2D Texture, Vector2 Offset) CropToVisiblePixels(Texture2D source, bool blackKey)
	{
		if (source == null) return (null, Vector2.Zero);
		Image image = source.GetImage();
		if (image == null || image.IsEmpty()) return (source, Vector2.Zero);

		int left = image.GetWidth();
		int top = image.GetHeight();
		int right = -1;
		int bottom = -1;
		for (int y = 0; y < image.GetHeight(); y++)
		{
			for (int x = 0; x < image.GetWidth(); x++)
			{
				Color pixel = image.GetPixel(x, y);
				float maximumOther = Mathf.Max(pixel.R, pixel.B);
				bool keyedGreen = pixel.G >= 115f / 255f && pixel.G - maximumOther >= 56f / 255f;
				bool keyedBlack = blackKey && Mathf.Max(pixel.R, Mathf.Max(pixel.G, pixel.B)) < 8f / 255f;
				if (pixel.A <= 0f || keyedGreen || keyedBlack) continue;
				left = Mathf.Min(left, x);
				top = Mathf.Min(top, y);
				right = Mathf.Max(right, x);
				bottom = Mathf.Max(bottom, y);
			}
		}
		if (right < left || bottom < top)
			return (ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8)), Vector2.Zero);

		Rect2I used = new(left, top, right - left + 1, bottom - top + 1);
		Image cropped = image.GetRegion(used);
		return (ImageTexture.CreateFromImage(cropped), new Vector2(left, top));
	}

	private static int ResolveDrawing(int tick, int[] holds)
	{
		int cursor = 0;
		for (int i = 0; i < holds.Length; i++)
		{
			cursor += holds[i];
			if (tick < cursor) return i;
		}
		return holds.Length - 1;
	}

	private static EffectSpec BuildSpec(BigBangCommonEffectKind kind)
	{
		return kind switch
		{
			// BBB actions 9 and 11 intentionally share this exact core drawing,
			// timing, origin and 100% scale. Strength changes sound/debris, not size.
			BigBangCommonEffectKind.HitWeak => new EffectSpec(
				new[] { 57, 58, 59, 60, 61, 62, 63, 64 },
				new[] { 1, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(1, 24), new Vector2(64, 93), new Vector2(77, 98), new Vector2(76, 88),
					new Vector2(68, 82), new Vector2(74, 85), new Vector2(76, 82), new Vector2(76, 34) },
				textureFrames: new[] { 62, 63, 64, 65, 66, 67, 68, 69 }),
			BigBangCommonEffectKind.HitStrong => new EffectSpec(
				new[] { 57, 58, 59, 60, 61, 62, 63, 64 },
				new[] { 1, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(1, 24), new Vector2(64, 93), new Vector2(77, 98), new Vector2(76, 88),
					new Vector2(68, 82), new Vector2(74, 85), new Vector2(76, 82), new Vector2(76, 34) },
				textureFrames: new[] { 62, 63, 64, 65, 66, 67, 68, 69 }),
			// BBB action 13. The cropped drawings stay centered on the gameplay
			// contact point while drawing 192 expands from 30% to 100%.
			BigBangCommonEffectKind.GuardImpact => new EffectSpec(
				new[] { 192, 193, 194, 195, 196, 197, 198, 199 },
				new[] { 8, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(6, 66), new Vector2(6, 108), new Vector2(8, 114), new Vector2(12, 118),
					new Vector2(12, 118), new Vector2(12, 118), new Vector2(12, 118), new Vector2(14, 118) },
				sourceFlip: false, initialScale: 0.3f, scaleGrowthPerTick: 0.1f, scaleGrowthTicks: 8,
				textureFrames: new[] { 237, 238, 239, 240, 241, 242, 243, 244 }),
			// BBB actions 2/3 (smoke 2 and its exact mirrored variant).
			BigBangCommonEffectKind.Dust => new EffectSpec(
				new[] { 85, 86, 87, 88, 89, 90, 91, 92, 93, 94 },
				new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
				new[] { new Vector2(0, 4), new Vector2(0, 4), new Vector2(-40, 4), new Vector2(-40, 4),
					new Vector2(-34, 4), new Vector2(-34, 4), new Vector2(-34, 4), new Vector2(-60, 0),
					new Vector2(-60, -2), new Vector2(-66, -8) },
				textureFrames: new[] { 90, 91, 92, 93, 94, 95, 96, 97, 98, 99 }),
			// Common visual 26, 出血. It also spawns fourteen instances of
			// common visual 27 (frame 211) using the source random velocity ranges.
			BigBangCommonEffectKind.BloodBurst => new EffectSpec(
				new[] { 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210 },
				new[] { 3, 2, 3, 4, 3, 3, 3, 3, 3, 3, 3 },
				new[] { new Vector2(4, 36), new Vector2(10, 36), new Vector2(17, 26), new Vector2(24, 18),
					new Vector2(29, 11), new Vector2(23, -32), new Vector2(24, -38), new Vector2(22, -68),
					new Vector2(22, -70), new Vector2(22, -74), new Vector2(24, -64) },
				textureFrames: new[] { 260, 261, 262, 263, 264, 265, 266, 267, 268, 269, 270 }),
			// Common source section 2, ジャンプ開始. Drawings 100-102 are
			// referenced by the script but absent from the extracted archive. Keep
			// all eight authored ticks and use drawing 099 as the explicit visual
			// placeholder for those three missing texture slots.
			BigBangCommonEffectKind.JumpStart => new EffectSpec(
				new[] { 95, 96, 97, 98, 99, 100, 101, 102 },
				new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
				new[] { new Vector2(2, 20), new Vector2(2, 20), new Vector2(2, 20), new Vector2(2, 20),
					new Vector2(2, 20), new Vector2(2, 20), new Vector2(2, 20), new Vector2(2, 20) },
				textureFrames: new[] { 95, 96, 97, 98, 99, 99, 99, 99 }),
			// User-authored grouping: play every available common drawing 000-015
			// strictly by filename. The source script does not define these sixteen
			// files as one action, so their raw canvas centers are the only neutral,
			// non-invented shared anchors. Two ticks matches the authored hold used
			// by the neighboring common thunder drawings.
			BigBangCommonEffectKind.Numbered000To015 => new EffectSpec(
				new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
				new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(108f, 128f), new Vector2(99f, 128f), new Vector2(107.5f, 128f),
					new Vector2(109.5f, 128f), new Vector2(88.5f, 128f), new Vector2(101.5f, 125f),
					new Vector2(117.5f, 126f), new Vector2(85.5f, 124f), new Vector2(256f, 256f),
					new Vector2(256f, 256f), new Vector2(256f, 256f), new Vector2(256f, 256f),
					new Vector2(256f, 256f), new Vector2(150f, 150f), new Vector2(150f, 150f),
					new Vector2(124f, 85.5f) }),
			// Provisional user grouping. Drawing 039 is not part of the source air-dash
			// action that consumes 040-045, so keep this seven-file sequence benched
			// and centered rather than inventing gameplay placement for it. Two ticks
			// per drawing makes the candidate readable while retaining 60 Hz timing.
			BigBangCommonEffectKind.BlockCandidate039To045 => new EffectSpec(
				new[] { 39, 40, 41, 42, 43, 44, 45 },
				new[] { 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(64f, 64f), new Vector2(14.5f, 50.5f), new Vector2(21.5f, 64f),
					new Vector2(21.5f, 65f), new Vector2(22f, 66.5f), new Vector2(22.5f, 67f),
					new Vector2(23f, 67.5f) }),
			// BBB common action 40, 地面バウンド (ground bounce). KIR drawing
			// indices 50-56 resolve to extracted filenames 055-061. Keep this source
			// action benched so it can later be assigned to ground bounce without
			// duplicating the visual timeline.
			BigBangCommonEffectKind.GroundBounce055To061 => new EffectSpec(
				new[] { 55, 56, 57, 58, 59, 60, 61 },
				new[] { 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(0f, 24f), new Vector2(0f, 24f), new Vector2(0f, 26f),
					new Vector2(0f, 26f), new Vector2(0f, 28f), new Vector2(-2f, 28f),
					new Vector2(-4f, 28f) }),
			// BBB common action 91, [エフェクト]バースト. Its KIR drawing IDs
			// 66-74 resolve to PNG filenames 071-079 and each lasts exactly one tick.
			// PNG 070 is KIR drawing 65, a separate reusable particle—not part of this
			// main burst drawing sequence. Keep the burst benched until gameplay design
			// assigns it to Burst or deliberately repurposes it for a parry.
			BigBangCommonEffectKind.Burst071To079 => new EffectSpec(
				new[] { 66, 67, 68, 69, 70, 71, 72, 73, 74 },
				new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
				new[] { new Vector2(-41f, 272f), new Vector2(-22f, 305f), new Vector2(1f, 314f),
					new Vector2(-25f, 211f), new Vector2(-29f, 234f), new Vector2(-34f, 264f),
					new Vector2(-36f, 284f), new Vector2(-41f, 297f), new Vector2(-46f, 301f) },
				textureFrames: new[] { 71, 72, 73, 74, 75, 76, 77, 78, 79 }),
			// BBB common action 1, 煙_1 (Smoke 1). KIR drawing IDs 75-84
			// resolve to PNG filenames 080-089. This compact dust puff stays as an
			// unassigned reusable visual until a movement or impact rule requests it.
			BigBangCommonEffectKind.SmallDust080To089 => new EffectSpec(
				new[] { 75, 76, 77, 78, 79, 80, 81, 82, 83, 84 },
				new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(0f, 4f), new Vector2(0f, 4f), new Vector2(0f, 4f),
					new Vector2(0f, 4f), new Vector2(0f, 4f), new Vector2(0f, 2f),
					new Vector2(0f, 2f), Vector2.Zero, new Vector2(-4f, 0f), new Vector2(-6f, 0f) },
				textureFrames: new[] { 80, 81, 82, 83, 84, 85, 86, 87, 88, 89 }),
			// User assignment: the source ジャンプ開始 action becomes the dedicated
			// super-jump takeoff visual. KIR IDs 95-102 map to PNG filenames 111-118.
			BigBangCommonEffectKind.SuperJump111To118 => new EffectSpec(
				new[] { 95, 96, 97, 98, 99, 100, 101, 102 },
				new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
				new[] { new Vector2(2f, 20f), new Vector2(2f, 20f), new Vector2(2f, 20f),
					new Vector2(2f, 20f), new Vector2(2f, 20f), new Vector2(2f, 20f),
					new Vector2(2f, 20f), new Vector2(2f, 20f) },
				textureFrames: new[] { 111, 112, 113, 114, 115, 116, 117, 118 }),
			// User-benched common KIR block 104-127. The recovered script does not
			// provide one complete action or authored placement for this block, so
			// retain the extracted order and center every raw canvas. Two ticks per
			// drawing keeps the provisional animation readable at the engine's 60 Hz.
			BigBangCommonEffectKind.Unassigned120To158 => new EffectSpec(
				new[] { 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115,
					116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127 },
				new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
					2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(44.5f, 40f), new Vector2(41f, 32f), new Vector2(42f, 36f),
					new Vector2(43.5f, 38f), new Vector2(44f, 37.5f), new Vector2(45f, 38.5f),
					new Vector2(45.5f, 41f), new Vector2(46f, 42.5f), new Vector2(47f, 44.5f),
					new Vector2(46.5f, 45.5f), new Vector2(45f, 47.5f), new Vector2(41f, 45f),
					new Vector2(41f, 40f), new Vector2(40f, 40f), new Vector2(40f, 39.5f),
					new Vector2(40.5f, 40f), new Vector2(83.5f, 26f), new Vector2(83.5f, 28.5f),
					new Vector2(84f, 28.5f), new Vector2(83f, 28f), new Vector2(80f, 28f),
					new Vector2(76.5f, 29f), new Vector2(77f, 29f), new Vector2(77.5f, 27f) },
				textureFrames: new[] { 120, 122, 124, 126, 128, 130, 132, 134,
					136, 138, 140, 142, 144, 146, 148, 150, 151, 152, 153, 154,
					155, 156, 157, 158 },
				additiveBlackKey: true),
			// Shared tail from 技ヒット_打撃 (technique/move hit: strike) and
			// 投げヒット (throw hit). KIR 128-135 map to PNG 161-168 and are
			// drawn for one authored tick each at source origin (0,320).
			BigBangCommonEffectKind.TechniqueHitRing161To168 => new EffectSpec(
				new[] { 128, 129, 130, 131, 132, 133, 134, 135 },
				new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
				new[] { new Vector2(0f, 320f), new Vector2(0f, 320f), new Vector2(0f, 320f),
					new Vector2(0f, 320f), new Vector2(0f, 320f), new Vector2(0f, 320f),
					new Vector2(0f, 320f), new Vector2(0f, 320f) },
				textureFrames: new[] { 161, 162, 163, 164, 165, 166, 167, 168 },
				additiveBlackKey: true),
			// Provisional adjacent KIR block 136-141. These opaque RGB sheets mix a
			// green chroma background with near-black additive backing. Apply both
			// keys exactly as the legacy additive-effect path does, and keep the six
			// drawings centered until a recovered action supplies authored origins.
			BigBangCommonEffectKind.AdditiveImpact181To186 => new EffectSpec(
				new[] { 136, 137, 138, 139, 140, 141 },
				new[] { 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(52f, 34f), new Vector2(52f, 15f), new Vector2(47.5f, 11.5f),
					new Vector2(45.5f, 9.5f), new Vector2(43.5f, 5f), new Vector2(43f, 5f) },
				textureFrames: new[] { 181, 182, 183, 184, 185, 186 },
				additiveBlackKey: true),
			// User assignment: reusable wall-jump launch visual. The recovered common
			// script does not claim KIR 144-151, so retain filename order at one tick
			// each. A stable X=6 ring center pins every drawing to the wall contact.
			BigBangCommonEffectKind.WallJump189To196 => new EffectSpec(
				new[] { 144, 145, 146, 147, 148, 149, 150, 151 },
				new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
				new[] { new Vector2(6f, 12.5f), new Vector2(6f, 60f), new Vector2(6f, 54.5f),
					new Vector2(6f, 45f), new Vector2(6f, 35f), new Vector2(6f, 29.5f),
					new Vector2(6f, 32f), new Vector2(6f, 34.5f) },
				textureFrames: new[] { 189, 190, 191, 192, 193, 194, 195, 196 },
				additiveBlackKey: true),
			// BBB common action 41, 壁バウンド (wall bounce / wall hit). KIR
			// drawings 153-159 map to PNG 198-204. Every drawing is held for
			// two source ticks and uses the exact I-command origin recovered from
			// the common script, keeping the effect pinned to the wall contact.
			BigBangCommonEffectKind.WallHit198To204 => new EffectSpec(
				new[] { 153, 154, 155, 156, 157, 158, 159 },
				new[] { 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(0f, 80f), new Vector2(10f, 132f), new Vector2(10f, 136f),
					new Vector2(14f, 141f), new Vector2(14f, 138f), new Vector2(14f, 134f),
					new Vector2(12f, 130f) },
				textureFrames: new[] { 198, 199, 200, 201, 202, 203, 204 }),
			// User assignment of BBB common action 4, 煙3 (Smoke 3), as the
			// dedicated run-start dust. KIR 160-172 map to PNG 205-217. The
			// omitted 217 file is the authored thirteenth drawing that completes
			// the fade; all drawings use their exact two-tick source timing.
			BigBangCommonEffectKind.RunDust205To217 => new EffectSpec(
				new[] { 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172 },
				new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(-26f, 9f), new Vector2(-28f, 23f), new Vector2(-33f, 9f),
					new Vector2(-45f, 9f), new Vector2(-56f, 11f), new Vector2(-61f, 13f),
					new Vector2(-59f, 13f), new Vector2(-65f, 12f), new Vector2(-66f, 14f),
					new Vector2(-67f, 16f), new Vector2(-71f, 17f), new Vector2(-102f, -9f),
					new Vector2(-107f, -26f) },
				textureFrames: new[] { 205, 206, 207, 208, 209, 210, 211, 212, 213, 214,
					215, 216, 217 }),
			// BBB's two B-Stone pickup actions use KIR 185-191 / PNG 230-236.
			// Their FA setup selects the legacy additive path, so black is backing
			// energy rather than opaque art. Keep this separate from Guard.
			BigBangCommonEffectKind.BStonePickup230To236 => new EffectSpec(
				new[] { 185, 186, 187, 188, 189, 190, 191 },
				new[] { 1, 1, 1, 1, 1, 1, 1 },
				new[] { new Vector2(0f, 24f), new Vector2(2f, 32f), new Vector2(0f, 36f),
					new Vector2(0f, 36f), new Vector2(0f, 38f), new Vector2(2f, 40f),
					new Vector2(2f, 40f) },
				textureFrames: new[] { 230, 231, 232, 233, 234, 235, 236 },
				initialScale: 0.7f, additiveBlackKey: true),
			// Verified common Air Dash Particle: KIR 212 / PNG 272. The two
			// source actions hold this drawing for 30 ticks at 70% scale and move
			// it in opposite horizontal directions. Movement stays caller-owned.
			BigBangCommonEffectKind.AirDashParticle272 => new EffectSpec(
				new[] { 212 }, new[] { 30 }, new[] { new Vector2(0f, 48f) },
				initialScale: 0.7f, textureFrames: new[] { 272 }, additiveBlackKey: true),
			// Verified Particle B: KIR 213 / PNG 273, held for 60 ticks. Its
			// source action applies randomized movement and additive blending.
			BigBangCommonEffectKind.ParticleB273 => new EffectSpec(
				new[] { 213 }, new[] { 60 }, new[] { new Vector2(0f, 22f) },
				textureFrames: new[] { 273 }, additiveBlackKey: true),
			// KIR 214 exists in the extracted drawing table but is never used by
			// the recovered common script. Preserve it without gameplay assignment.
			BigBangCommonEffectKind.UnusedCore274 => new EffectSpec(
				new[] { 214 }, new[] { 2 }, new[] { new Vector2(19.5f, 33f) },
				textureFrames: new[] { 274 }, additiveBlackKey: true),
			// KIR 215-220 / PNG 275-280 visually form an energy-ring sequence,
			// but no recovered action references them. Keep a readable benched
			// preview at two ticks per drawing rather than inventing gameplay data.
			BigBangCommonEffectKind.EnergyRingCandidate275To280 => new EffectSpec(
				new[] { 215, 216, 217, 218, 219, 220 },
				new[] { 2, 2, 2, 2, 2, 2 },
				new[] { new Vector2(62.5f, 55f), new Vector2(68f, 62f), new Vector2(71f, 67.5f),
					new Vector2(71.5f, 69.5f), new Vector2(71f, 69f), new Vector2(68f, 64f) },
				textureFrames: new[] { 275, 276, 277, 278, 279, 280 },
				additiveBlackKey: true),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
		};
	}

	private void SpawnSourceBloodParticles()
	{
		// BBB common visual 27, 血しぶき: KIR 211 maps to extracted PNG 271.
		Texture2D texture = GD.Load<Texture2D>($"{AssetRoot}/271.png");
		var random = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
		for (int i = 0; i < 14; i++)
		{
			var sprite = new Sprite2D
			{
				Texture = texture,
				Centered = false,
				Position = -new Vector2(2f, 5f),
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				Material = new ShaderMaterial { Shader = _greenKeyShader }
			};
			AddChild(sprite);
			_bloodParticles.Add(new BloodParticle
			{
				Sprite = sprite,
				Velocity = new Vector2(random.RandfRange(-500f, 500f), random.RandfRange(-1000f, -150f))
			});
		}
	}

	private void AdvanceBloodParticles(float delta)
	{
		if (_bloodParticles.Count == 0 || _age < 0) return;
		for (int i = 0; i < _bloodParticles.Count; i++)
		{
			BloodParticle particle = _bloodParticles[i];
			particle.Velocity += new Vector2(0f, 1800f) * delta; // source +30/tick gravity
			particle.Sprite.Position += particle.Velocity * delta;
			float sourceScale = Mathf.Max(0f, 1f - (_age + 1) * 0.02f);
			particle.Sprite.Scale = Vector2.One * sourceScale;
			particle.Sprite.Visible = sourceScale > 0f;
			_bloodParticles[i] = particle;
		}
	}

	private static Shader CreateGreenKeyShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;

			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float dominant_green = texel.g - max(texel.r, texel.b);
				float keyed = step(0.45, texel.g) * step(0.22, dominant_green);
				float alpha = texel.a * (1.0 - keyed);
				COLOR = vec4(texel.rgb, alpha);
			}
			""";
		return shader;
	}

	private static Shader CreateAdditiveBlackKeyShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;
			render_mode blend_add;

			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float dominant_green = texel.g - max(texel.r, texel.b);
				float green_key = step(0.45, texel.g) * step(0.22, dominant_green);
				float energy = max(texel.r, max(texel.g, texel.b));
				float black_key_alpha = smoothstep(0.035, 0.12, energy);
				float alpha = texel.a * (1.0 - green_key) * black_key_alpha;
				COLOR = vec4(texel.rgb, alpha);
			}
			""";
		return shader;
	}

	private sealed class EffectSpec
	{
		public int[] SourceFrames { get; }
		public int[] TextureFrames { get; }
		public int[] Holds { get; }
		public Vector2[] Origins { get; }
		public bool SourceFlip { get; }
		public bool AdditiveBlackKey { get; }
		public float InitialScale { get; }
		public float ScaleGrowthPerTick { get; }
		public int ScaleGrowthTicks { get; }
		public float MaximumScale { get; }
		public int TotalTicks { get; }

		public EffectSpec(int[] sourceFrames, int[] holds, Vector2[] origins, bool sourceFlip = false,
			float initialScale = 1f, float scaleGrowthPerTick = 0f, int scaleGrowthTicks = 0,
			float maximumScale = 1f, int[] textureFrames = null, bool additiveBlackKey = false)
		{
			if (sourceFrames.Length != holds.Length || sourceFrames.Length != origins.Length)
				throw new ArgumentException("BBB effect drawing, hold and origin counts must match.");
			if (textureFrames != null && textureFrames.Length != sourceFrames.Length)
				throw new ArgumentException("BBB effect source and fallback texture counts must match.");
			SourceFrames = sourceFrames;
			TextureFrames = textureFrames ?? sourceFrames;
			Holds = holds;
			Origins = origins;
			SourceFlip = sourceFlip;
			AdditiveBlackKey = additiveBlackKey;
			InitialScale = initialScale;
			ScaleGrowthPerTick = scaleGrowthPerTick;
			ScaleGrowthTicks = scaleGrowthTicks;
			MaximumScale = maximumScale;
			foreach (int hold in holds) TotalTicks += hold;
		}
	}

	private struct BloodParticle
	{
		public Sprite2D Sprite;
		public Vector2 Velocity;
	}
}
