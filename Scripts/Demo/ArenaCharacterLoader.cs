using Godot;

namespace ModularFighter.Demo;

/// <summary>Replaces the arena's Player 1 placeholder before dependent children enter the tree.</summary>
public partial class ArenaCharacterLoader : Node2D
{
	public enum CharacterChoice
	{
		KungFuMan,
		Sanzou,
		Kinako,
		Senna,
		MechaHeita,
		Kunagi,
		Daigo,
		Rouga,
		Kamui,
		Heita,
		Agito
	}

	public static CharacterChoice SelectedCharacter { get; set; } = CharacterChoice.KungFuMan;

	[Export] public PackedScene KungFuManScene { get; set; }
	[Export] public PackedScene SanzouScene { get; set; }
	[Export] public PackedScene KinakoScene { get; set; }
	[Export] public PackedScene SennaScene { get; set; }
	[Export] public PackedScene MechaHeitaScene { get; set; }
	[Export] public PackedScene KunagiScene { get; set; }
	[Export] public PackedScene DaigoScene { get; set; }
	[Export] public PackedScene RougaScene { get; set; }
	[Export] public PackedScene KamuiScene { get; set; }
	[Export] public PackedScene HeitaScene { get; set; }
	[Export] public PackedScene AgitoScene { get; set; }

	public override void _EnterTree()
	{
		if (SelectedCharacter != CharacterChoice.KungFuMan)
		{
			// The arena clone prototype belongs only to Kung Fu Man. Imported
			// fighters are independent staging characters and must not inherit it.
			Node cloneController = GetNodeOrNull("NarutoCloneController");
			if (cloneController != null)
			{
				RemoveChild(cloneController);
				cloneController.Free();
			}
		}

		Node oldFighter = GetNodeOrNull("Fighter");
		if (oldFighter == null) return;

		PackedScene selectedScene = GetSelectedScene();
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

	private PackedScene GetSelectedScene() => SelectedCharacter switch
	{
		CharacterChoice.Sanzou => SanzouScene,
		CharacterChoice.Kinako => KinakoScene,
		CharacterChoice.Senna => SennaScene,
		CharacterChoice.MechaHeita => MechaHeitaScene,
		CharacterChoice.Kunagi => KunagiScene,
		CharacterChoice.Daigo => DaigoScene,
		CharacterChoice.Rouga => RougaScene,
		CharacterChoice.Kamui => KamuiScene,
		CharacterChoice.Heita => HeitaScene,
		CharacterChoice.Agito => AgitoScene,
		_ => KungFuManScene
	};
}
