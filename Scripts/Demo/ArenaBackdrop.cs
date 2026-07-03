using Godot;

namespace ModularFighter.Demo;

/// <summary>Simple visual context for movement testing; no gameplay logic.</summary>
public partial class ArenaBackdrop : Node2D
{
    [Export] public float StageWidth { get; set; } = 3360f;
    [Export] public float StageHeight { get; set; } = 1008f;
    [Export] public float StageTopY { get; set; } = -650f;
    [Export] public float FloorY { get; set; } = 650f;

    public override void _Draw()
    {
        DrawRect(new Rect2(0, StageTopY, StageWidth, StageHeight - StageTopY), new Color("121827"));
        for (int x = 0; x <= StageWidth; x += 80) DrawLine(new Vector2(x, StageTopY), new Vector2(x, FloorY), new Color("202b42"), 1f);
        for (float y = StageTopY + 10f; y <= FloorY; y += 80f) DrawLine(new Vector2(0, y), new Vector2(StageWidth, y), new Color("202b42"), 1f);
        DrawRect(new Rect2(0, FloorY, StageWidth, StageHeight - FloorY), new Color("26334d"));
        DrawLine(new Vector2(0, FloorY), new Vector2(StageWidth, FloorY), new Color("7bc8e8"), 3f);
    }
}
