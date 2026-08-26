using Godot;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>Small keyboard/mouse roster screen used before entering the test arena.</summary>
public partial class CharacterSelectScreen : Control
{
	private static readonly string[] CharacterNames =
	{
		"KUNG FU MAN", "SANZOU KONGOUMARU", "KINAKO", "SENNA", "MECHA HEITA",
		"KUNAGI", "DAIGO", "ROUGA", "KAMUI", "HEITA", "AGITO"
	};
	private static readonly ArenaCharacterLoader.CharacterChoice[] CharacterChoices =
	{
		ArenaCharacterLoader.CharacterChoice.KungFuMan,
		ArenaCharacterLoader.CharacterChoice.Sanzou,
		ArenaCharacterLoader.CharacterChoice.Kinako,
		ArenaCharacterLoader.CharacterChoice.Senna,
		ArenaCharacterLoader.CharacterChoice.MechaHeita,
		ArenaCharacterLoader.CharacterChoice.Kunagi,
		ArenaCharacterLoader.CharacterChoice.Daigo,
		ArenaCharacterLoader.CharacterChoice.Rouga,
		ArenaCharacterLoader.CharacterChoice.Kamui,
		ArenaCharacterLoader.CharacterChoice.Heita,
		ArenaCharacterLoader.CharacterChoice.Agito
	};
	private static readonly string[] CharacterButtonNodes =
	{
		"KungFuMan", "Sanzou", "Kinako", "Senna", "MechaHeita",
		"Kunagi", "Daigo", "Rouga", "Kamui", "Heita", "Agito"
	};
	private int _selectedIndex;
	private Label _choiceLabel;
	private readonly Button[] _rosterButtons = new Button[CharacterNames.Length];

	public override void _Ready()
	{
		GetNodeOrNull<Node>("/root/AudioController")?.Call("stop_music");
		_choiceLabel = GetNode<Label>("Center/Panel/Layout/Choice");
		for (int index = 0; index < CharacterButtonNodes.Length; index++)
		{
			int rosterIndex = index;
			Button button = GetNode<Button>($"Center/Panel/Layout/Roster/{CharacterButtonNodes[index]}");
			button.Pressed += () => Confirm(rosterIndex);
			button.MouseEntered += () => SelectFromPointer(rosterIndex);
			_rosterButtons[index] = button;
		}
		RefreshChoice();
	}

	public override void _Process(double delta)
	{
		NativeInputFrame input = NativeInputRouter.GetUiFrame();
		if (input.WasPressed(NativeInputButtons.Left))
		{
			MoveSelection(-1);
		}
		else if (input.WasPressed(NativeInputButtons.Right))
		{
			MoveSelection(1);
		}
		else if (input.WasPressed(NativeInputButtons.Up))
		{
			MoveSelection(-2);
		}
		else if (input.WasPressed(NativeInputButtons.Down))
		{
			MoveSelection(2);
		}
		else if (input.WasPressed(NativeInputButtons.MenuAccept))
		{
			Confirm(_selectedIndex);
		}
	}

	private void MoveSelection(int amount)
	{
		_selectedIndex = (_selectedIndex + amount + CharacterNames.Length) % CharacterNames.Length;
		GetNodeOrNull<Node>("/root/AudioController")?.Call("play_cursor");
		RefreshChoice();
	}

	private void SelectFromPointer(int index)
	{
		if (index == _selectedIndex) return;
		_selectedIndex = index;
		GetNodeOrNull<Node>("/root/AudioController")?.Call("play_cursor");
		RefreshChoice();
	}

	private void RefreshChoice()
	{
		_choiceLabel.Text = $"<  {CharacterNames[_selectedIndex]}  >";
		for (int index = 0; index < _rosterButtons.Length; index++)
			if (_rosterButtons[index] != null)
				_rosterButtons[index].Modulate = index == _selectedIndex
					? new Color(1f, 0.84f, 0.34f)
					: Colors.White;
	}

	private void Confirm(int index)
	{
		GetNodeOrNull<Node>("/root/AudioController")?.Call("play_select");
		ArenaCharacterLoader.SelectedCharacter = CharacterChoices[index];
		GetTree().ChangeSceneToFile("res://Arena.tscn");
	}
}
