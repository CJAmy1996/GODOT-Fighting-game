using Godot;
using ModularFighter.Characters;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>Loads only the two fighters participating in the current match.</summary>
public partial class ArenaCharacterLoader : Node2D
{
	private const string KungFuManPath = "res://Scenes/Characters/KungFuMan.tscn";
	private const string SanzouPath = "res://Scenes/Characters/SanzoKongoumaru.tscn";

	public enum CharacterChoice { KungFuMan, Sanzou, Kinako, Senna, MechaHeita, Kunagi, Daigo, Rouga, Kamui, Heita, Agito }
	public static CharacterChoice SelectedCharacter { get; set; } = CharacterChoice.KungFuMan;
	// Kept as a non-exported test injection seam; production scenes never preload it.
	public PackedScene SanzouScene { get; set; }

	public override void _Ready() => GetNodeOrNull<Node>("/root/AudioController")?.Call("play_music");

	public override void _EnterTree()
	{
		PackedScene selectedScene = SelectedCharacter == CharacterChoice.Sanzou && SanzouScene != null
			? SanzouScene
			: ResourceLoader.Load<PackedScene>(GetSelectedScenePath());
		PackedScene opponentScene = SanzouScene ?? ResourceLoader.Load<PackedScene>(SanzouPath);
		if (selectedScene == null || opponentScene == null)
		{
			GD.PushError("The selected match fighters could not be loaded.");
			return;
		}

		Node2D fighter = selectedScene.Instantiate<Node2D>();
		fighter.Name = "Fighter";
		fighter.Position = new Vector2(420, 570);
		AddChild(fighter);

		Node2D opponent = opponentScene.Instantiate<Node2D>();
		opponent.Name = "Opponent";
		opponent.Position = new Vector2(860, 570);
		if (opponent is FighterController opponentController) opponentController.ReadLocalInput = false;
		AddChild(opponent);

		Node cloneNode = GetNodeOrNull("NarutoCloneController");
		if (SelectedCharacter != CharacterChoice.KungFuMan)
		{
			if (cloneNode != null)
			{
				RemoveChild(cloneNode);
				cloneNode.Free();
			}
		}
		else if (cloneNode is NarutoCloneController clones)
		{
			clones.CloneScene = selectedScene;
		}
	}

	private static string GetSelectedScenePath() => SelectedCharacter switch
	{
		CharacterChoice.Sanzou => SanzouPath,
		CharacterChoice.Kinako => "res://Scenes/Characters/Kinako.tscn",
		CharacterChoice.Senna => "res://Scenes/Characters/Senna.tscn",
		CharacterChoice.MechaHeita => "res://Scenes/Characters/MechaHeita.tscn",
		CharacterChoice.Kunagi => "res://Scenes/Characters/Kunagi.tscn",
		CharacterChoice.Daigo => "res://Scenes/Characters/Daigo.tscn",
		CharacterChoice.Rouga => "res://Scenes/Characters/Rouga.tscn",
		CharacterChoice.Kamui => "res://Scenes/Characters/Kamui.tscn",
		CharacterChoice.Heita => "res://Scenes/Characters/Heita.tscn",
		CharacterChoice.Agito => "res://Scenes/Characters/Agito.tscn",
		_ => KungFuManPath
	};
}
