using System.Collections.Generic;
using Godot;

namespace ModularFighter.Demo;

/// <summary>Simple temporary hit-spark renderer. Replace with VFX scenes later.</summary>
public partial class HitSparkLayer : Node2D
{
	private const string LightSparkScenePath = "res://Effects/HitSparkLight.tscn";
	private const string HeavySparkScenePath = "res://Effects/HitSparkHeavy.tscn";
	private const int SparkZIndex = 4096;

	private readonly List<Spark> _sparks = new();
	private readonly List<BlockShield> _blockShields = new();
	private PackedScene _lightSparkScene;
	private PackedScene _heavySparkScene;

	private struct Spark
	{
		public Vector2 Position;
		public int FramesLeft;
		public bool Heavy;
	}

	private struct BlockShield
	{
		public Vector2 Position;
		public int FramesLeft;
		public int Facing;
	}

	public void SpawnBlockShield(Vector2 position, int defenderFacing)
	{
		_blockShields.Add(new BlockShield
		{
			Position = position,
			FramesLeft = 11,
			Facing = defenderFacing >= 0 ? 1 : -1
		});
		QueueRedraw();
	}

	public void Spawn(Vector2 position, bool heavy)
	{
		PackedScene scene = heavy ? _heavySparkScene : _lightSparkScene;
		if (scene != null)
		{
			Node instance = scene.Instantiate();
			if (instance is Node2D node)
			{
				node.TopLevel = true;
				node.ZAsRelative = false;
				node.ZIndex = SparkZIndex;
				node.GlobalPosition = position;
				AddChild(node);
				return;
			}
			instance.QueueFree();
		}

		_sparks.Add(new Spark { Position = position, FramesLeft = heavy ? 12 : 8, Heavy = heavy });
		QueueRedraw();
	}

	public override void _Ready()
	{
		TopLevel = true;
		ZAsRelative = false;
		ZIndex = SparkZIndex;
		GlobalPosition = Vector2.Zero;
		if (ResourceLoader.Exists(LightSparkScenePath))
			_lightSparkScene = GD.Load<PackedScene>(LightSparkScenePath);
		if (ResourceLoader.Exists(HeavySparkScenePath))
			_heavySparkScene = GD.Load<PackedScene>(HeavySparkScenePath);
	}

	public override void _Process(double delta)
	{
		for (int i = _sparks.Count - 1; i >= 0; i--)
		{
			Spark spark = _sparks[i];
			spark.FramesLeft--;
			if (spark.FramesLeft <= 0)
				_sparks.RemoveAt(i);
			else
				_sparks[i] = spark;
		}
		for (int i = _blockShields.Count - 1; i >= 0; i--)
		{
			BlockShield shield = _blockShields[i];
			shield.FramesLeft--;
			if (shield.FramesLeft <= 0)
				_blockShields.RemoveAt(i);
			else
				_blockShields[i] = shield;
		}
		if (_sparks.Count > 0 || _blockShields.Count > 0) QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (Spark spark in _sparks)
		{
			float life = spark.FramesLeft / (spark.Heavy ? 12f : 8f);
			float radius = spark.Heavy ? 34f * life : 22f * life;
			Color core = spark.Heavy ? new Color(0.75f, 0.95f, 1f, life) : new Color(1f, 0.95f, 0.25f, life);
			Color edge = spark.Heavy ? new Color(0.2f, 0.55f, 1f, life * 0.8f) : new Color(1f, 0.45f, 0.08f, life * 0.75f);

			DrawCircle(spark.Position, spark.Heavy ? 7f * life : 5f * life, core);
			int rays = spark.Heavy ? 10 : 7;
			for (int i = 0; i < rays; i++)
			{
				float angle = Mathf.Tau * i / rays + (spark.Heavy ? 0.2f : 0f);
				Vector2 dir = Vector2.Right.Rotated(angle);
				DrawLine(spark.Position - dir * radius * 0.25f, spark.Position + dir * radius, i % 2 == 0 ? core : edge, spark.Heavy ? 3f : 2f, true);
			}
			if (spark.Heavy)
			{
				DrawArc(spark.Position, radius * 0.72f, 0f, Mathf.Tau, 18, edge, 2f, true);
			}
		}

		foreach (BlockShield shield in _blockShields)
		{
			float life = shield.FramesLeft / 11f;
			float radius = Mathf.Lerp(38f, 28f, life);
			float startAngle = shield.Facing > 0 ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f;
			float endAngle = startAngle + Mathf.Pi;
			Color fill = new Color(0.28f, 0.78f, 1f, 0.11f * life);
			Color edge = new Color(0.55f, 0.9f, 1f, 0.76f * life);
			Color glint = new Color(1f, 1f, 1f, 0.9f * life);
			DrawCircle(shield.Position, radius, fill);
			DrawArc(shield.Position, radius, startAngle, endAngle, 32, edge, 6f, true);
			DrawArc(shield.Position, radius - 8f, startAngle + 0.18f, endAngle - 0.18f, 28, glint, 2f, true);
		}
	}
}
