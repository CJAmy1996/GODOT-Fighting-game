#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Editor;

[Tool]
public partial class FighterBoxEditorPlugin : EditorPlugin
{
	private const string KungFuManDefinitionPath = "res://Data/Characters/kung_fu_man_test.tres";
	private const string KungFuManNormalSetPath = "res://Data/Characters/KungFuMan/kung_fu_man_normal_moves.tres";
	private const string KungFuManSpecialSetPath = "res://Data/Characters/KungFuMan/kung_fu_man_special_moves.tres";
	private const string KungFuManSpriteFramesPath = "res://Assets/TestFighter/KungFuMan/kung_fu_man_sprite_frames.tres";
	private const string SanzoDefinitionPath = "res://Data/Characters/Sanzo/sanzo_kongoumaru.tres";
	private const string SanzoNormalSetPath = "res://Data/Characters/Sanzo/sanzo_normal_moves.tres";
	private const string SanzoSpecialSetPath = "res://Data/Characters/Sanzo/sanzo_special_moves.tres";
	private const string SanzoStateSetPath = "res://Data/Characters/Sanzo/sanzo_state_boxes.tres";
	private const string SanzoSpriteFramesPath = "res://Assets/TestFighter/Sanzo/sanzo_sprite_frames.tres";
	private const double FrameSeconds = 1.0 / 60.0;

	private sealed class CharacterProfile
	{
		public string Name { get; init; }
		public string DefinitionPath { get; init; }
		public string NormalSetPath { get; init; }
		public string SpecialSetPath { get; init; }
		public string StateSetPath { get; init; } = "";
		public string SpriteFramesPath { get; init; }
		public string AnimationCatalogPath { get; init; } = "";
	}

	private static readonly CharacterProfile[] CharacterProfiles =
	{
		new() { Name = "Kung Fu Man", DefinitionPath = KungFuManDefinitionPath, NormalSetPath = KungFuManNormalSetPath, SpecialSetPath = KungFuManSpecialSetPath, SpriteFramesPath = KungFuManSpriteFramesPath },
		new() { Name = "Sanzou Kongoumaru", DefinitionPath = SanzoDefinitionPath, NormalSetPath = SanzoNormalSetPath, SpecialSetPath = SanzoSpecialSetPath, StateSetPath = SanzoStateSetPath, SpriteFramesPath = SanzoSpriteFramesPath, AnimationCatalogPath = "res://Assets/TestFighter/Sanzo/animation_catalog.csv" },
		ImportedProfile("Kinako", "Kinako", "kinako"),
		ImportedProfile("Senna", "Senna", "senna"),
		ImportedProfile("Mecha Heita", "MechaHeita", "m_heita"),
		ImportedProfile("Kunagi", "Kunagi", "kunagi"),
		ImportedProfile("Daigo", "Daigo", "daigo"),
		ImportedProfile("Rouga", "Rouga", "rouga"),
		ImportedProfile("Kamui", "Kamui", "kamui"),
		ImportedProfile("Heita", "Heita", "heita"),
		ImportedProfile("Agito", "Agito", "agito")
	};

	private static CharacterProfile ImportedProfile(string name, string directory, string slug)
	{
		string dataRoot = $"res://Data/Characters/BigBangBeatRevolve/{directory}/{slug}";
		string assetRoot = $"res://Assets/TestFighter/BigBangBeatRevolve/{directory}";
		return new CharacterProfile
		{
			Name = name,
			DefinitionPath = $"{dataRoot}_definition.tres",
			NormalSetPath = $"{dataRoot}_normal_moves.tres",
			SpecialSetPath = $"{dataRoot}_special_moves.tres",
			StateSetPath = $"{dataRoot}_state_boxes.tres",
			SpriteFramesPath = $"{assetRoot}/{slug}_sprite_frames.tres",
			AnimationCatalogPath = $"{assetRoot}/animation_catalog.csv"
		};
	}

	private ScrollContainer _dock;
	private VBoxContainer _content;
	private Label _status;
	private Label _coverage;
	private OptionButton _characterPicker;
	private OptionButton _movePicker;
	private OptionButton _animationPicker;
	private KungFuManBoxPreview _preview;
	private HSlider _timeline;
	private SpinBox _frameNumber;
	private Label _frameState;
	private Button _playButton;
	private SpinBox _startup;
	private SpinBox _active;
	private SpinBox _recovery;
	private ItemList _boxList;
	private OptionButton _boxKind;
	private SpinBox _boxStart;
	private SpinBox _boxEnd;
	private SpinBox _boxX;
	private SpinBox _boxY;
	private SpinBox _boxWidth;
	private SpinBox _boxHeight;
	private LineEdit _boxTag;
	private CheckBox _replaceSameKind;
	private LineEdit _newMoveName;
	private OptionButton _newMoveStance;
	private CheckBox _newMoveSpecial;

	private FighterDefinition _definition;
	private NormalMoveSet _normalSet;
	private SpecialMoveSet _specialSet;
	private NormalMoveSet _stateSet;
	private SpriteFrames _spriteFrames;
	private readonly List<MoveEntry> _moves = new();
	private NormalMoveData _currentMove;
	private bool _currentMoveIsSpecial;
	private bool _currentMoveIsState;
	private int _selectedBox = -1;
	private bool _updatingControls;
	private bool _playing;
	private double _playAccumulator;
	private FighterBoxKind _pendingDrawKind = FighterBoxKind.Hitbox;
	private string _definitionPath = KungFuManDefinitionPath;
	private string _normalSetPath = KungFuManNormalSetPath;
	private string _specialSetPath = KungFuManSpecialSetPath;
	private string _stateSetPath = "";
	private string _spriteFramesPath = KungFuManSpriteFramesPath;
	private string _characterName = "Kung Fu Man";
	private readonly Dictionary<string, string> _animationLabels = new(StringComparer.Ordinal);

	private sealed class MoveEntry
	{
		public NormalMoveData Move { get; init; }
		public bool Special { get; init; }
		public bool State { get; init; }
	}

	public override void _EnterTree()
	{
		BuildDock();
		AddControlToDock(DockSlot.RightUl, _dock);
		LoadSelectedCharacter();
		SetProcess(true);
	}

	public override void _ExitTree()
	{
		SetProcess(false);
		if (_dock == null) return;
		RemoveControlFromDocks(_dock);
		_dock.QueueFree();
	}

	public override bool _Handles(GodotObject @object) => @object is Node2D;

	public override void _Process(double delta)
	{
		if (!_playing || _timeline == null) return;
		_playAccumulator += delta;
		while (_playAccumulator >= FrameSeconds)
		{
			_playAccumulator -= FrameSeconds;
			int next = (int)_timeline.Value + 1;
			if (next > (int)_timeline.MaxValue) next = 0;
			SetTimelineFrame(next);
		}
	}

	private void BuildDock()
	{
		_dock = new ScrollContainer { Name = "Fighter Hitboxes", CustomMinimumSize = new Vector2(390f, 0f) };
		_content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_dock.AddChild(_content);

		_content.AddChild(new Label { Text = "FIGHTER MOVE & HITBOX EDITOR", HorizontalAlignment = HorizontalAlignment.Center });
		_content.AddChild(new Label
		{
			Text = "Choose a fighter and move, scrub its 60 Hz timeline, then draw directly over the sprite.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		_characterPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		foreach (CharacterProfile profile in CharacterProfiles)
			_characterPicker.AddItem(profile.Name);
		_characterPicker.ItemSelected += _ => LoadSelectedCharacter();
		AddLabeledControl("Character", _characterPicker);

		var reload = new Button { Text = "Reload selected character" };
		reload.Pressed += LoadSelectedCharacter;
		_content.AddChild(reload);

		_movePicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_movePicker.ItemSelected += index => SelectMove((int)index);
		AddLabeledControl("Move", _movePicker);
		var openSanzoThrow = new Button { Text = "Open Sanzou Throw / Victim Anchors" };
		openSanzoThrow.Pressed += OpenSanzoThrow;
		_content.AddChild(openSanzoThrow);

		_animationPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_animationPicker.ItemSelected += _ => OnAnimationSelected();
		AddLabeledControl("Animation", _animationPicker);

		_preview = new KungFuManBoxPreview { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_preview.BoxDrawn += AddDrawnBox;
		_content.AddChild(_preview);

		var transport = new HBoxContainer();
		var previous = new Button { Text = "< Drawing" };
		previous.Pressed += () => StepDrawing(-1);
		_playButton = new Button { Text = "Play" };
		_playButton.Pressed += TogglePlayback;
		var next = new Button { Text = "Drawing >" };
		next.Pressed += () => StepDrawing(1);
		_frameNumber = MakeSpinBox(0, 999, 0);
		_frameNumber.CustomMinimumSize = new Vector2(78f, 0f);
		_frameNumber.ValueChanged += value => { if (!_updatingControls) SetTimelineFrame((int)value); };
		transport.AddChild(previous);
		transport.AddChild(_playButton);
		transport.AddChild(next);
		transport.AddChild(new Label { Text = "Frame" });
		transport.AddChild(_frameNumber);
		_content.AddChild(transport);

		_timeline = new HSlider { MinValue = 0, MaxValue = 1, Step = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_timeline.ValueChanged += value => { if (!_updatingControls) SetTimelineFrame((int)value); };
		_content.AddChild(_timeline);
		_frameState = new Label { Text = "Frame 0 @ 60 Hz", HorizontalAlignment = HorizontalAlignment.Center };
		_content.AddChild(_frameState);

		_content.AddChild(new HSeparator());
		_content.AddChild(new Label { Text = "MOVE TIMING (game frames)" });
		var timing = new HBoxContainer();
		_startup = AddCompactSpin(timing, "Startup");
		_active = AddCompactSpin(timing, "Active");
		_recovery = AddCompactSpin(timing, "Recovery");
		_content.AddChild(timing);
		var saveTiming = new Button { Text = "Apply timing & animation" };
		saveTiming.Pressed += ApplyMoveTiming;
		_content.AddChild(saveTiming);

		_content.AddChild(new HSeparator());
		_content.AddChild(new Label { Text = "ADD A BOX — CURRENT FRAME" });
		var addBoxActions = new HBoxContainer();
		var drawHitbox = new Button { Text = "+ DRAW HITBOX", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		drawHitbox.Pressed += () => BeginDraw(FighterBoxKind.Hitbox);
		var drawHurtbox = new Button { Text = "+ DRAW HURTBOX", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		drawHurtbox.Pressed += () => BeginDraw(FighterBoxKind.Hurtbox);
		var drawThrowAnchor = new Button { Text = "+ THROW VICTIM", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		drawThrowAnchor.TooltipText = "Draw a placeholder where the captured opponent's sprite origin belongs on this frame.";
		drawThrowAnchor.Pressed += () => BeginDraw(FighterBoxKind.ThrowVictimAnchor);
		addBoxActions.AddChild(drawHitbox);
		addBoxActions.AddChild(drawHurtbox);
		addBoxActions.AddChild(drawThrowAnchor);
		_content.AddChild(addBoxActions);
		_content.AddChild(new Label
		{
			Text = "Click a button, then drag directly on the sprite preview above. Releasing the mouse adds one box.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		_content.AddChild(new HSeparator());
		_content.AddChild(new Label { Text = "BOXES (★ = active on this frame)" });
		_boxList = new ItemList { CustomMinimumSize = new Vector2(0f, 125f), SelectMode = ItemList.SelectModeEnum.Single };
		_boxList.ItemSelected += index => SelectBox((int)index);
		_content.AddChild(_boxList);
		var deleteSelected = new Button { Text = "− DELETE SELECTED BOX", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		deleteSelected.Pressed += DeleteSelectedBox;
		_content.AddChild(deleteSelected);

		_boxKind = MakeKindPicker();
		AddLabeledControl("Selected kind", _boxKind);
		_content.AddChild(new Label { Text = "APPLY SELECTED BOX TO FRAME RANGE" });
		var range = new HBoxContainer();
		_boxStart = AddCompactSpin(range, "First frame", 0, 999);
		_boxEnd = AddCompactSpin(range, "Last frame", -1, 999);
		_content.AddChild(range);
		var applyRange = new Button { Text = "APPLY BOX TO EVERY FRAME IN RANGE", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		applyRange.Pressed += ApplySelectedBox;
		_content.AddChild(applyRange);
		_content.AddChild(new Label
		{
			Text = "Example: First 4, Last 8 makes this box active instantly on frames 4, 5, 6, 7, and 8. Use Last -1 to keep it active through the rest of the move.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		var position = new HBoxContainer();
		_boxX = AddCompactSpin(position, "X", -999, 999);
		_boxY = AddCompactSpin(position, "Y", -999, 999);
		_content.AddChild(position);
		var size = new HBoxContainer();
		_boxWidth = AddCompactSpin(size, "Width", 1, 999);
		_boxHeight = AddCompactSpin(size, "Height", 1, 999);
		_content.AddChild(size);
		_boxTag = new LineEdit { PlaceholderText = "Optional: fist, head, torso..." };
		AddLabeledControl("Tag", _boxTag);
		_replaceSameKind = new CheckBox { Text = "Replace other boxes of this kind while active" };
		_content.AddChild(_replaceSameKind);
		_content.AddChild(new Label
		{
			Text = "Off: combine active authored boxes for coverage (still only one hit). On: while this box is active, ignore the move's other authored boxes of the same kind. Uncovered frames use the fighter fallback box.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		var boxActions = new HBoxContainer();
		var applyBox = new Button { Text = "APPLY ALL BOX CHANGES", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		applyBox.Pressed += ApplySelectedBox;
		var duplicate = new Button { Text = "Copy +1f", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		duplicate.Pressed += DuplicateSelectedBoxToNextFrame;
		boxActions.AddChild(applyBox);
		boxActions.AddChild(duplicate);
		_content.AddChild(boxActions);

		var inspect = new Button { Text = "Open selected move in Inspector" };
		inspect.Pressed += () => { if (_currentMove != null) EditorInterface.Singleton.EditResource(_currentMove); };
		_content.AddChild(inspect);

		_coverage = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_content.AddChild(_coverage);

		_content.AddChild(new HSeparator());
		_content.AddChild(new Label { Text = "CREATE ANOTHER MOVE FOR THIS CHARACTER" });
		_newMoveName = new LineEdit { PlaceholderText = "Exact runtime name, e.g. SPECIAL KICK" };
		AddLabeledControl("Attack name", _newMoveName);
		_newMoveStance = new OptionButton();
		foreach (string name in Enum.GetNames<NormalMoveStance>()) _newMoveStance.AddItem(name);
		AddLabeledControl("Stance", _newMoveStance);
		_newMoveSpecial = new CheckBox { Text = "Special move (projectile-capable)" };
		_content.AddChild(_newMoveSpecial);
		var create = new Button { Text = "Create, assign, and select move" };
		create.Pressed += CreateMove;
		_content.AddChild(create);

		_status = new Label { Text = "Loading…", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_content.AddChild(_status);
	}

	private void LoadSelectedCharacter()
	{
		int selected = Mathf.Clamp(_characterPicker?.Selected ?? 0, 0, CharacterProfiles.Length - 1);
		CharacterProfile profile = CharacterProfiles[selected];
		_characterName = profile.Name;
		_definitionPath = profile.DefinitionPath;
		_normalSetPath = profile.NormalSetPath;
		_specialSetPath = profile.SpecialSetPath;
		_stateSetPath = profile.StateSetPath;
		_spriteFramesPath = profile.SpriteFramesPath;
		_definition = ResourceLoader.Load<FighterDefinition>(_definitionPath, cacheMode: ResourceLoader.CacheMode.Replace);
		_normalSet = ResourceLoader.Load<NormalMoveSet>(_normalSetPath, cacheMode: ResourceLoader.CacheMode.Replace);
		_specialSet = ResourceLoader.Load<SpecialMoveSet>(_specialSetPath, cacheMode: ResourceLoader.CacheMode.Replace);
		_stateSet = string.IsNullOrEmpty(_stateSetPath) ? _definition?.StateBoxes :
			ResourceLoader.Load<NormalMoveSet>(_stateSetPath, cacheMode: ResourceLoader.CacheMode.Replace);
		_spriteFrames = ResourceLoader.Load<SpriteFrames>(_spriteFramesPath, cacheMode: ResourceLoader.CacheMode.Replace);

		if (_definition == null || _normalSet == null || _specialSet == null || _spriteFrames == null)
		{
			_status.Text = $"{_characterName} assets are missing. Reimport the project, then press Reload.";
			return;
		}

		if (_definition.NormalMoves != _normalSet || _definition.SpecialMoves != _specialSet ||
			(_stateSet != null && _definition.StateBoxes != _stateSet))
		{
			_definition.NormalMoves = _normalSet;
			_definition.SpecialMoves = _specialSet;
			if (_stateSet != null) _definition.StateBoxes = _stateSet;
			SaveResource(_definition, _definitionPath);
		}

		_preview.SetSpriteFrames(_spriteFrames);
		LoadAnimationLabels(profile.AnimationCatalogPath);
		PopulateAnimations();
		PopulateMoves();
		_currentMove = null;
		_currentMoveIsSpecial = false;
		_currentMoveIsState = false;
		_preview.SetMove(null);
		if (_moves.Count > 0)
			SelectMove(Mathf.Clamp(_movePicker.Selected, 0, _moves.Count - 1));
		else
			SelectAnimationByName(_spriteFrames.HasAnimation("anim_000") ? "anim_000" : "idle");
		_status.Text = $"Loaded {_moves.Count} {_characterName} moves and {_spriteFrames.GetAnimationNames().Length} animations. Changes save only to this character.";
	}

	private void LoadAnimationLabels(string catalogPath)
	{
		_animationLabels.Clear();
		if (string.IsNullOrEmpty(catalogPath)) return;
		string systemPath = ProjectSettings.GlobalizePath(catalogPath);
		if (!File.Exists(systemPath)) return;
		foreach (string line in File.ReadLines(systemPath).Skip(1))
		{
			string[] columns = line.Split(',');
			if (columns.Length < 7) continue;
			string animation = columns[0].Trim().TrimStart('\ufeff');
			string sourceAction = columns[2].Trim().Trim('"');
			_animationLabels[animation] = $"{sourceAction} | {columns[3]} | {columns[4]} | {columns[6]} ticks";
		}
	}

	private void PopulateAnimations()
	{
		_animationPicker.Clear();
		foreach (StringName animation in _spriteFrames.GetAnimationNames().OrderBy(name => name.ToString(), StringComparer.OrdinalIgnoreCase))
		{
			string animationName = animation.ToString();
			string display = _animationLabels.TryGetValue(animationName, out string label)
				? $"{animationName} | {label}"
				: animationName;
			_animationPicker.AddItem(display);
			_animationPicker.SetItemMetadata(_animationPicker.ItemCount - 1, animationName);
		}
	}

	private void PopulateMoves(NormalMoveData select = null)
	{
		_moves.Clear();
		_movePicker.Clear();
		foreach (NormalMoveData state in _stateSet?.Rules ?? Array.Empty<NormalMoveData>())
		{
			if (state == null) continue;
			_moves.Add(new MoveEntry { Move = state, State = true });
			_movePicker.AddItem($"STATE · {state.AttackName.Replace("STATE ", "")} · {state.AnimationName}");
		}
		foreach (NormalMoveData move in _normalSet?.Rules ?? Array.Empty<NormalMoveData>())
		{
			if (move == null) continue;
			_moves.Add(new MoveEntry { Move = move, Special = false });
			_movePicker.AddItem($"NORMAL · {move.AttackName} [{move.Stance}] · {move.AnimationName}");
		}
		foreach (SpecialMoveData move in _specialSet?.Moves ?? Array.Empty<SpecialMoveData>())
		{
			if (move == null) continue;
			_moves.Add(new MoveEntry { Move = move, Special = true });
			_movePicker.AddItem($"SPECIAL · {move.AttackName} [{move.Stance}] · {move.AnimationName}");
		}
		if (select == null) return;
		int index = _moves.FindIndex(entry => entry.Move == select);
		if (index >= 0)
		{
			_movePicker.Select(index);
			SelectMove(index);
		}
	}

	private void OpenSanzoThrow()
	{
		if (_characterPicker.Selected != 1)
		{
			_characterPicker.Select(1);
			LoadSelectedCharacter();
		}
		int index = _moves.FindIndex(entry =>
			string.Equals(entry.Move?.AttackName, "THROW", StringComparison.OrdinalIgnoreCase));
		if (index < 0)
		{
			_status.Text = "Sanzou's THROW rule was not loaded. Reload the character after the C# assembly finishes reloading.";
			return;
		}
		_movePicker.Select(index);
		SelectMove(index);
		_status.Text = "Sanzou THROW selected. Use + THROW VICTIM to place the captured sprite on frames 1-8.";
	}

	private void SelectMove(int index)
	{
		if (index < 0 || index >= _moves.Count) return;
		MoveEntry entry = _moves[index];
		_currentMove = entry.Move;
		_currentMoveIsSpecial = entry.Special;
		_currentMoveIsState = entry.State;
		_selectedBox = -1;
		_preview.SetMove(_currentMove);
		SelectAnimationByName(_currentMove.AnimationName);

		_updatingControls = true;
		_startup.Value = Mathf.Max(0, _currentMove.StartupFrames);
		_active.Value = Mathf.Max(0, _currentMove.ActiveFrames);
		_recovery.Value = Mathf.Max(0, _currentMove.RecoveryFrames);
		_updatingControls = false;
		UpdateTimelineRange();
		SetTimelineFrame(0);
		RefreshBoxList();
		UpdateCoverageWarning();
	}

	private void OnAnimationSelected()
	{
		if (_animationPicker.Selected < 0) return;
		_preview.SetAnimation(GetAnimationName(_animationPicker.Selected));
		UpdateTimelineRange();
		SetTimelineFrame((int)_timeline.Value);
	}

	private string GetAnimationName(int pickerIndex)
	{
		if (pickerIndex < 0 || pickerIndex >= _animationPicker.ItemCount) return "idle";
		Variant metadata = _animationPicker.GetItemMetadata(pickerIndex);
		string animation = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
		return string.IsNullOrEmpty(animation) ? _animationPicker.GetItemText(pickerIndex) : animation;
	}

	private void SelectAnimationByName(string animationName)
	{
		int selected = 0;
		for (int i = 0; i < _animationPicker.ItemCount; i++)
			if (string.Equals(GetAnimationName(i), animationName, StringComparison.Ordinal))
			{
				selected = i;
				break;
			}
		_animationPicker.Select(selected);
		OnAnimationSelected();
	}

	private void UpdateTimelineRange()
	{
		int animationFrames = 1;
		if (_spriteFrames != null && _animationPicker.Selected >= 0)
		{
			StringName animation = GetAnimationName(_animationPicker.Selected);
			if (_spriteFrames.HasAnimation(animation)) animationFrames = GetAnimationTimelineTicks(animation);
		}
		int moveFrames = _currentMove == null ? 1 : Mathf.Max(1,
			Mathf.Max(0, _currentMove.StartupFrames) + Mathf.Max(0, _currentMove.ActiveFrames) + Mathf.Max(0, _currentMove.RecoveryFrames));
		// Sanzou's combat move owns the authoritative 60 Hz editing range. The
		// animation keeps its authored drawing order; no automatic reversal.
		int maximum = (_characterName.StartsWith("Sanzo") && _currentMove != null && !_currentMoveIsState
			? moveFrames : Mathf.Max(animationFrames, moveFrames)) - 1;
		_timeline.MaxValue = maximum;
		_frameNumber.MaxValue = maximum;
	}

	private void SetTimelineFrame(int frame)
	{
		int clamped = Mathf.Clamp(frame, 0, (int)_timeline.MaxValue);
		_updatingControls = true;
		_timeline.Value = clamped;
		_frameNumber.Value = clamped;
		_updatingControls = false;
		_preview.SetFrame(clamped, GetDrawingAtTimelineFrame(clamped));
		UpdateFrameState(clamped);
		RefreshBoxList();
	}

	private void UpdateFrameState(int frame)
	{
		StringName animation = _animationPicker.Selected >= 0 ? GetAnimationName(_animationPicker.Selected) : new StringName();
		int drawing = GetDrawingAtTimelineFrame(frame);
		int drawingCount = _spriteFrames != null && _spriteFrames.HasAnimation(animation) ? _spriteFrames.GetFrameCount(animation) : 0;
		string source = "unknown";
		if (drawingCount > 0)
		{
			Texture2D texture = _spriteFrames.GetFrameTexture(animation, Mathf.Clamp(drawing, 0, drawingCount - 1));
			if (texture != null && !string.IsNullOrEmpty(texture.ResourcePath)) source = Path.GetFileNameWithoutExtension(texture.ResourcePath);
		}
		if (_currentMove == null)
		{
			_frameState.Text = $"Game frame {frame} @ 60 Hz | Drawing {drawing + 1}/{drawingCount} | {source}";
			return;
		}
		int startup = Mathf.Max(0, _currentMove.StartupFrames);
		int active = Mathf.Max(0, _currentMove.ActiveFrames);
		string phase = frame < startup ? "STARTUP" : frame < startup + active ? "ACTIVE" : "RECOVERY";
		_frameState.Text = $"Game frame {frame} @ 60 Hz | Drawing {drawing + 1}/{drawingCount} | {source} | {phase}";
	}

	private int GetAnimationTimelineTicks(StringName animation)
	{
		return AttackDrawingTimeline.GetAuthoredTicks(_spriteFrames, animation);
	}

	private int GetDrawingAtTimelineFrame(int timelineFrame)
	{
		if (_spriteFrames == null || _animationPicker.Selected < 0) return 0;
		StringName animation = GetAnimationName(_animationPicker.Selected);
		if (!_spriteFrames.HasAnimation(animation)) return 0;
		if (_currentMove is SpecialMoveData special)
		{
			int recoveryStart = Mathf.Max(0, special.StartupFrames) + Mathf.Max(0, special.ActiveFrames);
			if (timelineFrame >= recoveryStart && special.LandingAnimationSourceTimeline is { Length: > 0 } landing)
			{
				int sourceTick = landing[Mathf.Clamp(timelineFrame - recoveryStart, 0, landing.Length - 1)];
				return AttackDrawingTimeline.ResolveSourceTick(_spriteFrames, animation, sourceTick);
			}
			if (special.ForceDownwardStartFrame >= 0 && timelineFrame >= special.ForceDownwardStartFrame &&
				special.DescentAnimationSourceCycle is { Length: > 0 } descent)
				return AttackDrawingTimeline.ResolveSourceCycle(_spriteFrames, animation, descent,
					timelineFrame - special.ForceDownwardStartFrame, special.DescentAnimationTicksPerSource);
			if (special.ForceDownwardStartFrame >= 0 && timelineFrame >= Mathf.Max(0, special.StartupFrames) &&
				special.RiseAnimationSourceCycle is { Length: > 0 } rise)
				return AttackDrawingTimeline.ResolveSourceCycle(_spriteFrames, animation, rise,
					timelineFrame - Mathf.Max(0, special.StartupFrames), special.RiseAnimationTicksPerSource);
		}
		return AttackDrawingTimeline.Resolve(_spriteFrames, animation, timelineFrame,
			_currentMove?.StartupFrames ?? 0, _currentMove?.ActiveFrames ?? 0,
			_currentMove?.RecoveryFrames ?? 0, false, _currentMove?.AnimationSourceTimeline);
	}

	private int GetDrawingStartFrame(int targetDrawing)
	{
		if (_spriteFrames == null || _animationPicker.Selected < 0) return 0;
		StringName animation = GetAnimationName(_animationPicker.Selected);
		int start = 0;
		for (int drawing = 0; drawing < targetDrawing; drawing++)
			start += Mathf.Max(1, Mathf.RoundToInt((float)_spriteFrames.GetFrameDuration(animation, drawing)));
		return start;
	}

	private void StepDrawing(int direction)
	{
		if (_spriteFrames == null || _animationPicker.Selected < 0) return;
		StringName animation = GetAnimationName(_animationPicker.Selected);
		int count = _spriteFrames.GetFrameCount(animation);
		if (count <= 0) return;
		int current = GetDrawingAtTimelineFrame((int)_timeline.Value);
		int target = Mathf.Clamp(current + direction, 0, count - 1);
		SetTimelineFrame(GetDrawingStartFrame(target));
	}

	private void TogglePlayback()
	{
		_playing = !_playing;
		_playAccumulator = 0;
		_playButton.Text = _playing ? "Pause" : "Play";
	}

	private void ApplyMoveTiming()
	{
		if (_currentMove == null) return;
		_currentMove.StartupFrames = (int)_startup.Value;
		_currentMove.ActiveFrames = (int)_active.Value;
		_currentMove.RecoveryFrames = (int)_recovery.Value;
		if (_animationPicker.Selected >= 0) _currentMove.AnimationName = GetAnimationName(_animationPicker.Selected);
		SaveCurrentMoveSet("Saved move timing and animation.");
		UpdateTimelineRange();
		UpdateFrameState((int)_timeline.Value);
		UpdateCoverageWarning();
	}

	private void BeginDraw(FighterBoxKind kind)
	{
		if (_currentMove == null)
		{
			_status.Text = $"Select a {_characterName} move before drawing a box.";
			return;
		}
		_pendingDrawKind = kind;
		_preview.SetDrawing(true);
		_status.Text = $"DRAWING {kind.ToString().ToUpperInvariant()}: drag on the sprite preview, then release.";
	}

	private void AddDrawnBox(Rect2 localRect)
	{
		if (_currentMove == null) return;
		int frame = (int)_timeline.Value;
		var box = new FighterBoxFrame
		{
			Kind = _pendingDrawKind,
			StartFrame = frame,
			EndFrame = frame,
			LocalRect = localRect,
			MirrorWithFacing = true,
			Tag = _boxTag.Text,
			ReplacesSameKindWhileActive = false
		};
		AppendBox(box);
		_selectedBox = _currentMove.BoxTimeline.Length - 1;
		_preview.SetDrawing(false);
		SaveCurrentMoveSet($"Added {box.Kind} on frame {frame}.");
		RefreshBoxList();
		SelectBox(_selectedBox);
		UpdateCoverageWarning();
	}

	private void AppendBox(FighterBoxFrame box)
	{
		var boxes = (_currentMove.BoxTimeline ?? Array.Empty<FighterBoxFrame>()).ToList();
		boxes.Add(box);
		_currentMove.BoxTimeline = boxes.ToArray();
		_currentMove.EmitChanged();
	}

	private void RefreshBoxList()
	{
		if (_boxList == null) return;
		int keepSelected = _selectedBox;
		_boxList.Clear();
		FighterBoxFrame[] boxes = _currentMove?.BoxTimeline ?? Array.Empty<FighterBoxFrame>();
		int frame = _timeline == null ? 0 : (int)_timeline.Value;
		for (int i = 0; i < boxes.Length; i++)
		{
			FighterBoxFrame box = boxes[i];
			if (box == null)
			{
				_boxList.AddItem($"{i:00} · missing box");
				continue;
			}
			bool active = box.IsActiveOnFrame(frame);
			string replacement = box.ReplacesSameKindWhileActive ? " · REPLACES" : "";
			_boxList.AddItem($"{(active ? "★" : " ")} {i:00} · {box.Kind} · {box.StartFrame}–{(box.EndFrame < 0 ? "∞" : box.EndFrame)}{replacement} · {box.Tag}");
			_boxList.SetItemCustomFgColor(i, BoxColor(box.Kind, active));
		}
		if (keepSelected >= 0 && keepSelected < boxes.Length)
		{
			_boxList.Select(keepSelected);
			_preview.SetSelectedBox(keepSelected);
		}
	}

	private void SelectBox(int index)
	{
		FighterBoxFrame[] boxes = _currentMove?.BoxTimeline;
		if (boxes == null || index < 0 || index >= boxes.Length || boxes[index] == null) return;
		_selectedBox = index;
		FighterBoxFrame box = boxes[index];
		_updatingControls = true;
		_boxKind.Select((int)box.Kind);
		_boxStart.Value = box.StartFrame;
		_boxEnd.Value = box.EndFrame;
		_boxX.Value = box.LocalRect.Position.X;
		_boxY.Value = box.LocalRect.Position.Y;
		_boxWidth.Value = box.LocalRect.Size.X;
		_boxHeight.Value = box.LocalRect.Size.Y;
		_boxTag.Text = box.Tag;
		_replaceSameKind.ButtonPressed = box.ReplacesSameKindWhileActive;
		_updatingControls = false;
		_preview.SetSelectedBox(index);
	}

	private void ApplySelectedBox()
	{
		FighterBoxFrame box = GetSelectedBox();
		if (box == null) return;
		box.Kind = (FighterBoxKind)_boxKind.Selected;
		box.StartFrame = (int)_boxStart.Value;
		box.EndFrame = (int)_boxEnd.Value;
		box.LocalRect = new Rect2((float)_boxX.Value, (float)_boxY.Value, (float)_boxWidth.Value, (float)_boxHeight.Value);
		box.Tag = _boxTag.Text;
		box.ReplacesSameKindWhileActive = _replaceSameKind.ButtonPressed;
		box.EmitChanged();
		SaveCurrentMoveSet("Saved box changes.");
		RefreshBoxList();
		UpdateCoverageWarning();
		_preview.QueueRedraw();
	}

	private void DuplicateSelectedBoxToNextFrame()
	{
		FighterBoxFrame source = GetSelectedBox();
		if (source == null) return;
		var copy = source.Duplicate(true) as FighterBoxFrame;
		if (copy == null) return;
		copy.StartFrame = Mathf.Max(0, source.EndFrame < 0 ? source.StartFrame + 1 : source.EndFrame + 1);
		copy.EndFrame = copy.StartFrame;
		AppendBox(copy);
		_selectedBox = _currentMove.BoxTimeline.Length - 1;
		SaveCurrentMoveSet($"Copied box to frame {copy.StartFrame}.");
		SetTimelineFrame(copy.StartFrame);
		SelectBox(_selectedBox);
		UpdateCoverageWarning();
	}

	private void DeleteSelectedBox()
	{
		if (_currentMove?.BoxTimeline == null || _selectedBox < 0 || _selectedBox >= _currentMove.BoxTimeline.Length) return;
		var boxes = _currentMove.BoxTimeline.ToList();
		boxes.RemoveAt(_selectedBox);
		_currentMove.BoxTimeline = boxes.ToArray();
		_currentMove.EmitChanged();
		_selectedBox = -1;
		_preview.SetSelectedBox(-1);
		SaveCurrentMoveSet("Deleted box.");
		RefreshBoxList();
		UpdateCoverageWarning();
	}

	private FighterBoxFrame GetSelectedBox()
	{
		FighterBoxFrame[] boxes = _currentMove?.BoxTimeline;
		return boxes != null && _selectedBox >= 0 && _selectedBox < boxes.Length ? boxes[_selectedBox] : null;
	}

	private void UpdateCoverageWarning()
	{
		if (_currentMove == null)
		{
			_coverage.Text = "";
			return;
		}
		int total = Mathf.Max(1, Mathf.Max(0, _currentMove.StartupFrames) + Mathf.Max(0, _currentMove.ActiveFrames) + Mathf.Max(0, _currentMove.RecoveryFrames));
		FighterBoxFrame[] hurtboxes = (_currentMove.BoxTimeline ?? Array.Empty<FighterBoxFrame>())
			.Where(box => box != null && box.Kind == FighterBoxKind.Hurtbox).ToArray();
		if (hurtboxes.Length == 0)
		{
			_coverage.Text = "Hurtbox: the fighter fallback covers every frame. Draw authored boxes only where the pose needs different coverage.";
			_coverage.Modulate = new Color(1f, 0.82f, 0.35f);
		}
		else
		{
			int authoredFrames = Enumerable.Range(0, total).Count(frame => hurtboxes.Any(box => box.IsActiveOnFrame(frame)));
			_coverage.Text = $"Hurtbox coverage: {authoredFrames}/{total} frames authored; fallback safely covers the rest.";
			_coverage.Modulate = new Color(0.35f, 1f, 0.55f);
		}
	}

	private void CreateMove()
	{
		if (_normalSet == null || _specialSet == null)
		{
			_status.Text = "Move sets are not loaded.";
			return;
		}
		string attackName = _newMoveName.Text.Trim().ToUpperInvariant();
		if (string.IsNullOrEmpty(attackName))
		{
			_status.Text = "Enter the exact runtime attack name first.";
			return;
		}
		string animation = _animationPicker.Selected >= 0 ? GetAnimationName(_animationPicker.Selected) : "idle";
		NormalMoveStance stance = (NormalMoveStance)_newMoveStance.Selected;
		MoveEntry existing = _moves.FirstOrDefault(entry =>
			string.Equals(entry.Move.AttackName, attackName, StringComparison.OrdinalIgnoreCase) && entry.Move.Stance == stance);
		if (existing != null)
		{
			PopulateMoves(existing.Move);
			_status.Text = $"{attackName} [{stance}] already exists, so I selected it instead.";
			return;
		}
		var startingHurtbox = new FighterBoxFrame
		{
			Kind = FighterBoxKind.Hurtbox,
			StartFrame = 0,
			EndFrame = 13,
			LocalRect = stance == NormalMoveStance.Crouching
				? new Rect2(-34f, -62f, 68f, 82f)
				: new Rect2(-32f, -92f, 64f, 142f),
			Tag = "body"
		};
		NormalMoveData move;
		if (_newMoveSpecial.ButtonPressed)
		{
			var special = new SpecialMoveData
			{
				AttackName = attackName,
				AnimationName = animation,
				Stance = stance,
				StartupFrames = 4,
				ActiveFrames = 2,
				RecoveryFrames = 8,
				SuppressFallbackHitbox = true,
				BoxTimeline = new[] { startingHurtbox }
			};
			_specialSet.Moves = (_specialSet.Moves ?? Array.Empty<SpecialMoveData>()).Append(special).ToArray();
			move = special;
			SaveResource(_specialSet, _specialSetPath);
		}
		else
		{
			move = new NormalMoveData
			{
				AttackName = attackName,
				AnimationName = animation,
				Stance = stance,
				StartupFrames = 4,
				ActiveFrames = 2,
				RecoveryFrames = 8,
				SuppressFallbackHitbox = true,
				BoxTimeline = new[] { startingHurtbox }
			};
			_normalSet.Rules = (_normalSet.Rules ?? Array.Empty<NormalMoveData>()).Append(move).ToArray();
			SaveResource(_normalSet, _normalSetPath);
		}
		_newMoveName.Text = "";
		PopulateMoves(move);
		_status.Text = $"Created and assigned {attackName}. Draw its boxes now.";
	}

	private void SaveCurrentMoveSet(string successMessage)
	{
		if (_currentMove == null) return;
		ClampHitboxesToActivePhase();
		_currentMove.EmitChanged();
		Error error = _currentMoveIsState
			? SaveResource(_stateSet, _stateSetPath)
			: _currentMoveIsSpecial
				? SaveResource(_specialSet, _specialSetPath)
				: SaveResource(_normalSet, _normalSetPath);
		_status.Text = error == Error.Ok ? successMessage : $"Save failed: {error}.";
	}

	private void ClampHitboxesToActivePhase()
	{
		if (_currentMove?.BoxTimeline == null) return;
		int activeStart = Mathf.Max(0, _currentMove.StartupFrames);
		int activeEnd = activeStart + Mathf.Max(1, _currentMove.ActiveFrames) - 1;
		foreach (FighterBoxFrame box in _currentMove.BoxTimeline)
		{
			if (box == null || box.Kind != FighterBoxKind.Hitbox) continue;
			box.StartFrame = Mathf.Clamp(box.StartFrame, activeStart, activeEnd);
			box.EndFrame = Mathf.Clamp(box.EndFrame < 0 ? activeEnd : box.EndFrame, box.StartFrame, activeEnd);
			box.EmitChanged();
		}
	}

	private static Error SaveResource(Resource resource, string path) =>
		resource == null ? Error.InvalidData : ResourceSaver.Save(resource, path);

	private void AddLabeledControl(string label, Control control)
	{
		_content.AddChild(new Label { Text = label });
		_content.AddChild(control);
	}

	private static SpinBox MakeSpinBox(double minimum, double maximum, double value) => new()
	{
		MinValue = minimum,
		MaxValue = maximum,
		Step = 1,
		Value = value,
		AllowGreater = true,
		AllowLesser = true,
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
	};

	private static SpinBox AddCompactSpin(HBoxContainer row, string label, double minimum = 0, double maximum = 999)
	{
		var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		column.AddChild(new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Center });
		SpinBox spin = MakeSpinBox(minimum, maximum, 0);
		column.AddChild(spin);
		row.AddChild(column);
		return spin;
	}

	private static OptionButton MakeKindPicker()
	{
		var picker = new OptionButton();
		foreach (string name in Enum.GetNames<FighterBoxKind>()) picker.AddItem(name);
		picker.Select((int)FighterBoxKind.Hitbox);
		return picker;
	}

	private static Color BoxColor(FighterBoxKind kind, bool active)
	{
		Color color = kind switch
		{
			FighterBoxKind.Hitbox => new Color(1f, 0.35f, 0.25f),
			FighterBoxKind.Hurtbox => new Color(0.3f, 0.75f, 1f),
			FighterBoxKind.Pushbox => new Color(0.35f, 1f, 0.5f),
			FighterBoxKind.Throwbox => new Color(1f, 0.82f, 0.25f),
			FighterBoxKind.ThrowVictimAnchor => new Color(0.2f, 1f, 0.95f),
			_ => new Color(0.75f, 0.55f, 1f)
		};
		return active ? color : color.Darkened(0.45f);
	}
}
#endif
