#if TOOLS
using Godot;

namespace ModularFighter.Editor;

[Tool]
public partial class ProjectilePathEditorPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		AddCustomType("ProjectilePathAuthoring", "Path2D",
			GD.Load<Script>("res://addons/projectile_path_editor/ProjectilePathAuthoring.cs"), null);
	}

	public override void _ExitTree() => RemoveCustomType("ProjectilePathAuthoring");
}
#endif
