using System;
using System.IO;
using System.Linq;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

/// <summary>Locks Sanzou's live 60 Hz animations to his reviewable CSV without benching moves.</summary>
public partial class SanzouAnimationCatalogRegressionTest : Node
{
	private const string FramesPath = "res://Assets/TestFighter/Sanzo/sanzo_sprite_frames.tres";
	private const string CatalogPath = "res://Assets/TestFighter/Sanzo/animation_catalog.csv";
	private const string DefinitionPath = "res://Data/Characters/Sanzo/sanzo_kongoumaru.tres";

	public override void _Ready()
	{
		int failures = 0;
		SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(FramesPath);
		FighterDefinition definition = ResourceLoader.Load<FighterDefinition>(DefinitionPath);
		string systemCatalog = ProjectSettings.GlobalizePath(CatalogPath);
		if (frames == null || definition == null || !File.Exists(systemCatalog))
		{
			GD.PushError("Sanzou catalog regression: required resources did not load");
			GetTree().Quit(1);
			return;
		}

		string[] rows = File.ReadLines(systemCatalog).Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
		if (rows.Length != frames.GetAnimationNames().Length)
		{
			GD.PushError($"Sanzou catalog regression: catalog has {rows.Length} rows but SpriteFrames has {frames.GetAnimationNames().Length} animations");
			failures++;
		}

		int sourceGroups = 0;
		foreach (string row in rows)
		{
			string[] columns = row.Split(',');
			if (columns.Length < 13 || !int.TryParse(columns[5], out int expectedDrawings))
			{
				GD.PushError("Sanzou catalog regression: malformed catalog row");
				failures++;
				continue;
			}
			StringName animation = columns[0].Trim().TrimStart('\ufeff');
			if (!frames.HasAnimation(animation) || frames.GetFrameCount(animation) != expectedDrawings)
			{
				GD.PushError($"Sanzou catalog regression: {animation} drawing count does not match");
				failures++;
				continue;
			}
			int catalogTicks = columns[9].Split(' ', StringSplitOptions.RemoveEmptyEntries).Sum(int.Parse);
			int liveTicks = Enumerable.Range(0, frames.GetFrameCount(animation))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(animation, index)));
			if (catalogTicks != liveTicks)
			{
				GD.PushError($"Sanzou catalog regression: {animation} is {liveTicks} ticks, expected {catalogTicks}");
				failures++;
			}
			if (animation.ToString().StartsWith("group_", StringComparison.Ordinal))
			{
				sourceGroups++;
				if (columns[4] != "SOURCE_POOL") failures++;
			}
			else if (columns[4] == "BENCHED")
			{
				GD.PushError($"Sanzou catalog regression: {animation} was unexpectedly benched");
				failures++;
			}
		}

		if (sourceGroups != 32)
		{
			GD.PushError($"Sanzou catalog regression: expected 32 source groups, found {sourceGroups}");
			failures++;
		}
		if ((definition.NormalMoves?.Rules?.Length ?? 0) == 0 || (definition.SpecialMoves?.Moves?.Length ?? 0) == 0)
		{
			GD.PushError("Sanzou catalog regression: current move assignments were lost");
			failures++;
		}

		if (failures == 0)
			GD.Print($"SANZOU_ANIMATION_CATALOG_PASS animations={rows.Length} source_groups={sourceGroups}");
		GetTree().Quit(failures == 0 ? 0 : 1);
	}
}
