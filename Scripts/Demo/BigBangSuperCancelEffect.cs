using Godot;

namespace ModularFighter.Demo;

/// <summary>Revolve common action 17: master core, paired thunder, and two impact cores.</summary>
public partial class BigBangSuperCancelEffect : Node2D
{
	private const string CommonRoot = "res://Assets/Effects/BigBangCommon";
	private const string ExtractedRoot = "res://Extraction/BigBangBeatRevolve/_common_pct";
	private static readonly Vector2[] ThunderForwardOrigins = { new(-507,1000), new(-503,1000), new(-507,1000), new(-508,972), new(-508,976), new(-297,566), new(-289,469), new(-245,334) };
	private static readonly Vector2[] ThunderReverseOrigins = { new(509,8), new(507,9), new(507,14), new(511,58), new(509,50), new(295,34), new(293,131), new(245,12) };
	private static readonly int[] CoreFrames = { 360,362,362,363,17,18,19,20,21,22,23,24,26,27,28,29,30,31,32,33,34,35,36,37,38,38 };
	private static readonly int[] CoreHolds = { 10,5,2,30,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1 };
	private static readonly Vector2[] CoreOrigins = { new(0,128),new(0,128),new(0,128),new(0,128),new(3,135),new(3,133),new(3,124),new(0,110),new(2,102),new(2,89),new(1,97),new(1,105),new(-1,201),new(-2,174),new(1,166),new(0,156),new(1,144),new(2,129),new(2,112),new(1,92),new(2,96),new(-3,105),new(-1,113),new(-2,125),new(-1,138),new(-1,138) };
	private static readonly Vector2[] MasterOrigins = { new(4,147),new(0,230),new(0,242),new(4,255),new(1,273),new(1,274),new(-1,265) };
	private static Shader _greenKeyShader;
	private readonly Texture2D[] _innerThunderTextures = new Texture2D[8];
	private readonly Texture2D[] _outerThunderTextures = new Texture2D[8];
	private readonly Texture2D[] _masterTextures = new Texture2D[7];
	private readonly Texture2D[] _coreTextures = new Texture2D[CoreFrames.Length];
	private Sprite2D _inner, _outer, _core, _delayedCore, _master;
	private int _age;
	private Vector2 _coverageWorldSize, _impactLocalPosition;
	private float _presentationScale = 1f;

	public int CurrentTick => _age;
	public int CurrentInnerFrame => Mathf.Min(7, _age / 2);
	public int CurrentOuterFrame => 8 + Mathf.Min(7, _age / 2);
	public int CurrentCoreFrame { get { int drawing = ResolveDrawing(Mathf.Max(0, _age), CoreHolds); return CoreFrames[Mathf.Min(drawing, CoreFrames.Length - 1)]; } }
	public bool LightningVisible => _age < 16;
	public int TotalTicks => 80;
	public Vector2 CoverageWorldSize => _coverageWorldSize;

	public void ConfigureScreenCoverage(Vector2 coverageWorldSize, Vector2 impactLocalPosition, float presentationScale = 1f)
	{
		_coverageWorldSize = coverageWorldSize;
		_impactLocalPosition = impactLocalPosition;
		_presentationScale = Mathf.Max(0.1f, presentationScale);
	}

	public override void _Ready()
	{
		for (int i = 0; i < 8; i++)
		{
			_innerThunderTextures[i] = GD.Load<Texture2D>($"{CommonRoot}/{i:D3}.png");
			_outerThunderTextures[i] = GD.Load<Texture2D>($"{CommonRoot}/{i + 8:D3}.png");
		}
		for (int i = 0; i < 7; i++) _masterTextures[i] = GD.Load<Texture2D>($"{ExtractedRoot}/{i + 348:D3}.png");
		for (int i = 0; i < CoreFrames.Length; i++) { int frame = CoreFrames[i]; string root = frame >= 360 ? ExtractedRoot : CommonRoot; _coreTextures[i] = GD.Load<Texture2D>($"{root}/{frame:D3}.png"); }
		_greenKeyShader ??= CreateGreenKeyShader();
		_inner = CreateLayer("InnerLightning", 0);
		_outer = CreateLayer("OuterLightning", 1);
		_core = CreateLayer("ImpactRing", 2);
		_delayedCore = CreateLayer("DelayedImpactRing", 3);
		_master = CreateLayer("MasterCore", 4);
		ApplyTick();
	}

	public override void _PhysicsProcess(double delta) => AdvanceOneTick();
	public void AdvanceOneTick() { _age++; if (_age >= TotalTicks) { QueueFree(); return; } ApplyTick(); }

	private Sprite2D CreateLayer(string name, int zIndex)
	{
		var sprite = new Sprite2D { Name = name, Centered = true, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, ZIndex = zIndex, Material = new ShaderMaterial { Shader = _greenKeyShader } };
		AddChild(sprite); return sprite;
	}

	private void ApplyTick()
	{
		int thunder = Mathf.Min(7, _age / 2);
		ApplyAuthored(_inner, _innerThunderTextures[thunder], ThunderForwardOrigins[thunder], Vector2.One, LightningVisible);
		ApplyAuthored(_outer, _outerThunderTextures[thunder], ThunderReverseOrigins[thunder], Vector2.One, LightningVisible);
		_outer.FlipH = true;
		ScaleLightningToCoverage();
		ApplyCore(_core, _age);
		ApplyCore(_delayedCore, _age - 11);
		int master = Mathf.Min(6, _age / 2);
		ApplyAuthored(_master, _masterTextures[master], MasterOrigins[master], Vector2.One, _age < 14);
	}

	private void ScaleLightningToCoverage()
	{
		if (!LightningVisible || _coverageWorldSize.IsZeroApprox() ||
			_inner.Texture == null || _outer.Texture == null) return;

		Vector2 halfCoverage = _coverageWorldSize * 0.5f;
		Vector2 innerSize = _inner.Texture.GetSize();
		Vector2 outerSize = _outer.Texture.GetSize();
		float innerScale = 1.12f * Mathf.Max(
			halfCoverage.X / Mathf.Max(1f, innerSize.X),
			halfCoverage.Y / Mathf.Max(1f, innerSize.Y));
		float outerScale = 1.12f * Mathf.Max(
			halfCoverage.X / Mathf.Max(1f, outerSize.X),
			halfCoverage.Y / Mathf.Max(1f, outerSize.Y));

		// Restore the previous diagonal coverage: both sheets meet at the fixed
		// activation center while extending toward opposite screen corners.
		_inner.Scale = Vector2.One * innerScale;
		_inner.Position = _impactLocalPosition +
			new Vector2(innerSize.X, -innerSize.Y) * innerScale * 0.5f;
		_outer.Scale = Vector2.One * outerScale;
		_outer.Position = _impactLocalPosition +
			new Vector2(-outerSize.X, outerSize.Y) * outerScale * 0.5f;
	}

	private void ApplyCore(Sprite2D sprite, int tick)
	{
		if (tick < 0) { sprite.Visible = false; return; }
		int drawing = ResolveDrawing(tick, CoreHolds);
		if (drawing >= CoreFrames.Length) { sprite.Visible = false; return; }
		float scale = drawing == 0 ? Mathf.Max(0.2f, 2f - tick * 0.2f) : drawing == 1 ? 0.5f + Mathf.Max(0, tick - 10) * 0.3f : drawing <= 3 ? 2f : 1f;
		ApplyAuthored(sprite, _coreTextures[drawing], CoreOrigins[drawing], Vector2.One * scale, true);
		sprite.Rotation = drawing is 1 or 2 ? Mathf.DegToRad((tick - 10) * 10f) : 0f;
		Color color = Colors.White;
		if (drawing == 2) color.A = Mathf.Max(0f, 1f - (tick - 15) * 10f / 255f);
		sprite.Modulate = color;
	}

	private void ApplyAuthored(Sprite2D sprite, Texture2D texture, Vector2 origin, Vector2 sourceScale, bool visible)
	{
		sprite.Visible = visible;
		if (!visible) return;
		sprite.Texture = texture;
		// Hold every changing drawing on the same activation center. The source
		// offsets describe its 640x480 canvas placement; applying those again to
		// individually extracted PNGs makes the cropped frames visibly wander.
		sprite.Position = _impactLocalPosition;
		sprite.Scale = sourceScale * _presentationScale;
	}

	private static int ResolveDrawing(int tick, int[] holds)
	{
		int cursor = 0;
		for (int i = 0; i < holds.Length; i++) { cursor += holds[i]; if (tick < cursor) return i; }
		return holds.Length;
	}

	private static Shader CreateGreenKeyShader()
	{
		var shader = new Shader();
		shader.Code = """
			shader_type canvas_item;
			render_mode blend_add;
			void fragment() {
				vec4 texel = texture(TEXTURE, UV);
				float key = smoothstep(0.04, 0.18, distance(texel.rgb, vec3(0.0, 1.0, 0.0)));
				COLOR = vec4(texel.rgb, texel.a * key) * COLOR;
			}
			""";
		return shader;
	}
}
