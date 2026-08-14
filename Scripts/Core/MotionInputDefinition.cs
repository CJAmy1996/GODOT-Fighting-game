using System;
using Godot;

namespace ModularFighter.Core;

public enum MotionInputKind
{
	DirectionSequence,
	ChargeSequence,
	ButtonMash
}

public enum MotionDirection
{
	Neutral,
	Forward,
	DownForward,
	Down,
	DownBack,
	Back,
	UpBack,
	Up,
	UpForward
}

[Flags]
public enum MotionAttackButton
{
	None = 0,
	LightPunch = 1 << 0,
	HeavyPunch = 1 << 1,
	LightKick = 1 << 2,
	HeavyKick = 1 << 3,
	AnyPunch = LightPunch | HeavyPunch,
	AnyKick = LightKick | HeavyKick,
	AnyAttack = AnyPunch | AnyKick
}

public enum MotionButtonMatchMode
{
	AnySelectedButton,
	AllSelectedButtons
}

/// <summary>
/// Reusable, facing-relative motion recipe. Direction strings use numpad-style tokens:
/// N, F, DF, D, DB, B, UB, U, UF. Multiple strings provide accepted lenient variants.
/// </summary>
[Tool, GlobalClass]
public partial class MotionInputDefinition : Resource
{
	[Export] public string MotionName { get; set; } = "New Motion";
	[Export] public MotionInputKind Kind { get; set; }
	[Export] public string[] DirectionSequences { get; set; } = Array.Empty<string>();
	[Export] public int MotionWindowFrames { get; set; } = 20;
	[Export] public int ButtonLeniencyFrames { get; set; } = 5;
	[Export] public int MaxSkippedDirections { get; set; } = 1;

	[ExportGroup("Charge")]
	[Export] public MotionDirection ChargeDirection { get; set; } = MotionDirection.Back;
	[Export] public int ChargeFrames { get; set; } = 45;
	[Export] public int ChargeReleaseLeniencyFrames { get; set; } = 5;

	[ExportGroup("Mash")]
	[Export] public int RequiredButtonPresses { get; set; } = 5;
	[Export] public int MashWindowFrames { get; set; } = 30;
}
