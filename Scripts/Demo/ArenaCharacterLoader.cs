using Godot;

namespace ModularFighter.Demo;

/// <summary>Replaces the arena's Player 1 placeholder before dependent children enter the tree.</summary>
public partial class ArenaCharacterLoader : Node2D
{
	public enum CharacterChoice { KungFuMan, Sanzou }

	public static CharacterChoice SelectedCharacter { get; set; } = CharacterChoice.KungFuMan;

	[Export] public PackedScene KungFuManScene { get; set; }
	[Export] public PackedScene SanzouScene { get; set; }

	public override void _EnterTree()
	{
		if (SelectedCharacter == CharacterChoice.Sanzou)
		{
			// The arena clone prototype owns O/L for Kung Fu Man. Sanzou has his
			// own character actions on those buttons, so it must not enter the tree.
			Node cloneController = GetNodeOrNull("NarutoCloneController");
			if (cloneController != null)
			{
				RemoveChild(cloneController);
				cloneController.Free();
			}
		}

		Node oldFighter = GetNodeOrNull("Fighter");
		if (oldFighter == null) return;

		PackedScene selectedScene = SelectedCharacter == CharacterChoice.Sanzou ? SanzouScene : KungFuManScene;
		if (selectedScene == null) return;

		int siblingIndex = oldFighter.GetIndex();
		Vector2 spawnPosition = oldFighter is Node2D oldNode ? oldNode.Position : new Vector2(420, 570);
		RemoveChild(oldFighter);
		oldFighter.Free();

		Node2D fighter = selectedScene.Instantiate<Node2D>();
		fighter.Name = "Fighter";
		fighter.Position = spawnPosition;
		AddChild(fighter);
		MoveChild(fighter, siblingIndex);
	}
}
