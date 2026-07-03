using System.Collections.Generic;
using Godot;

namespace ModularFighter.Demo;

/// <summary>Simple temporary hit-spark renderer. Replace with VFX scenes later.</summary>
public partial class HitSparkLayer : Node2D
{
	private const string LightSparkScenePath = "res://Effects/HitSparkLight.tscn";
	private const string HeavySparkScenePath = "res://Effects/HitSparkHeavy.tscn";

	private readonly List<Spark> _sparks = new();
	private PackedScene _lightSparkScene;
	private PackedScene _heavySparkScene;

	private struct Spark
	{
		public Vector2 Position;
		public int FramesLeft;
		public bool Heavy;
	}

	public void Spawn(Vector2 position, bool heavy)
	{
		PackedScene scene = heavy ? _heavySparkScene : _lightSparkScene;
		if (scene != null)
		{
			Node instance = scene.Instantiate();
			if (instance is Node2D node)
			{
				node.Position = position;
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
		if (_sparks.Count > 0) QueueRedraw();
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
	}
}
