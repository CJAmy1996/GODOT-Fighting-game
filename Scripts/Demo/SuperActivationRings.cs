using Godot;

namespace ModularFighter.Demo;

/// <summary>Portrait-container strokes for a super activation cut-in.</summary>
public partial class SuperActivationRings : Node2D
{
	public Sprite2D PortraitSprite { get; set; }
	public Vector2 FocusPosition { get; set; }
	public Rect2 CameraRect { get; set; }
	public bool PortraitEntersFromLeft { get; set; }
	public int LifetimeFrames { get; set; } = 1;
	public int FramesElapsed { get; set; }

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (PortraitSprite == null) return;
		// MVC1-style portrait panel: the circle is deliberately much taller than
		// the camera. Only its inward-facing curved edge can be seen; there is no
		// visible top or bottom edge that reads as a separate circular badge.
		// Draw this first so the animated activation energy remains in front.
		Vector2 portraitCenter = PortraitSprite.Position;
		float panelCenterX = portraitCenter.X + (PortraitEntersFromLeft ? -CameraRect.Size.X * 0.25f : CameraRect.Size.X * 0.25f);
		Vector2 panelCenter = new(panelCenterX, CameraRect.GetCenter().Y);
		float panelRadius = Mathf.Max(CameraRect.Size.Y * 0.72f, CameraRect.Size.X * 0.48f);
		float startAngle = PortraitEntersFromLeft ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f;
		float endAngle = startAngle + Mathf.Pi;
		DrawArc(panelCenter, panelRadius + 4f, startAngle, endAngle, 96, Colors.Black, 24f, true);
		DrawArc(panelCenter, panelRadius - 7f, startAngle, endAngle, 96, new Color(0.78f, 0.82f, 0.88f, 0.95f), 5f, true);
	}
}
