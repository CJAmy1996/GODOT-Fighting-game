#if TOOLS
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Editor;

/// <summary>Draw this Path2D with Godot's curve tool; it writes the curve and timing into a special move.</summary>
[Tool]
public partial class ProjectilePathAuthoring : Path2D
{
	[Export] public SpecialMoveData TargetMove { get; set; }
	[Export(PropertyHint.Range, "1,600,1")] public int TravelFrames { get; set; } = 60;
	[Export] public bool CalculateFramesFromSpeed { get; set; }
	[Export(PropertyHint.Range, "1,5000,1")] public float SpeedPixelsPerSecond { get; set; } = 900f;
	[Export] public float PathLengthPixels { get; private set; }

	private float _lastLength = -1f;
	private int _lastFrames = -1;
	private float _lastSpeed = -1f;

	public override void _Process(double delta)
	{
		if (!Engine.IsEditorHint() || Curve == null) return;
		float length = Curve.GetBakedLength();
		if (Mathf.IsEqualApprox(length, _lastLength) && TravelFrames == _lastFrames &&
			Mathf.IsEqualApprox(SpeedPixelsPerSecond, _lastSpeed)) return;
		PathLengthPixels = length;
		if (CalculateFramesFromSpeed)
			TravelFrames = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(1f, SpeedPixelsPerSecond) * 60f));
		else
			SpeedPixelsPerSecond = length <= 0f ? 0f : length / Mathf.Max(1, TravelFrames) * 60f;
		if (TargetMove != null)
		{
			TargetMove.ProjectilePath = Curve;
			TargetMove.ProjectilePathTravelFrames = TravelFrames;
			TargetMove.EmitChanged();
		}
		_lastLength = length;
		_lastFrames = TravelFrames;
		_lastSpeed = SpeedPixelsPerSecond;
		NotifyPropertyListChanged();
	}
}
#endif
