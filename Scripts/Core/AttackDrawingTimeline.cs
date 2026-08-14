using Godot;

namespace ModularFighter.Core;

/// <summary>One deterministic 60 Hz drawing resolver shared by gameplay and the box editor.</summary>
public static class AttackDrawingTimeline
{
	public static int Resolve(SpriteFrames frames, StringName animation, int timelineFrame,
		int startupFrames, int activeFrames, int recoveryFrames, bool reverseRecovery,
		int[] animationSourceTimeline = null)
	{
		if (frames == null || !frames.HasAnimation(animation)) return 0;
		int count = frames.GetFrameCount(animation);
		if (count <= 1) return 0;
		int frame = Mathf.Max(0, timelineFrame);
		if (animationSourceTimeline is { Length: > 0 })
		{
			int sourceTick = animationSourceTimeline[Mathf.Clamp(frame, 0, animationSourceTimeline.Length - 1)];
			return ResolveAuthoredDuration(frames, animation, Mathf.Max(0, sourceTick));
		}
		if (!reverseRecovery) return ResolveAuthoredDuration(frames, animation, frame);

		int startup = Mathf.Max(0, startupFrames);
		int active = Mathf.Max(0, activeFrames);
		int recovery = Mathf.Max(1, recoveryFrames);
		int activeDrawing = FindDrawingAtTick(frames, animation, startup);

		if (frame < startup)
		{
			if (startup <= 0 || activeDrawing <= 0) return 0;
			return Mathf.Clamp(frame * activeDrawing / startup, 0, activeDrawing - 1);
		}
		if (frame < startup + active)
		{
			int elapsed = frame - startup;
			int activeDrawingCount = Mathf.Max(1, count - activeDrawing);
			return Mathf.Clamp(activeDrawing + elapsed * activeDrawingCount / Mathf.Max(1, active), activeDrawing, count - 1);
		}

		int recoveryElapsed = frame - startup - active;
		if (recoveryElapsed >= recovery) return 0;
		if (recovery <= 1) return 0;
		int reverseOffset = Mathf.RoundToInt(recoveryElapsed * (count - 1) / (float)(recovery - 1));
		return Mathf.Clamp(count - 1 - reverseOffset, 0, count - 1);
	}

	/// <summary>Resolves one explicitly requested authored 60 Hz source tick.</summary>
	public static int ResolveSourceTick(SpriteFrames frames, StringName animation, int sourceTick) =>
		frames == null || !frames.HasAnimation(animation)
			? 0
			: ResolveAuthoredDuration(frames, animation, Mathf.Max(0, sourceTick));

	public static int ResolveSourceCycle(SpriteFrames frames, StringName animation, int[] sourceCycle,
		int elapsedTicks, int ticksPerSource)
	{
		if (sourceCycle is not { Length: > 0 }) return 0;
		int index = Mathf.Max(0, elapsedTicks) / Mathf.Max(1, ticksPerSource) % sourceCycle.Length;
		return ResolveSourceTick(frames, animation, sourceCycle[index]);
	}

	public static int GetAuthoredTicks(SpriteFrames frames, StringName animation)
	{
		if (frames == null || !frames.HasAnimation(animation)) return 1;
		int ticks = 0;
		for (int drawing = 0; drawing < frames.GetFrameCount(animation); drawing++)
			ticks += GetDrawingTicks(frames, animation, drawing);
		return Mathf.Max(1, ticks);
	}

	private static int ResolveAuthoredDuration(SpriteFrames frames, StringName animation, int frame)
	{
		int elapsed = 0;
		int count = frames.GetFrameCount(animation);
		for (int drawing = 0; drawing < count; drawing++)
		{
			int duration = GetDrawingTicks(frames, animation, drawing);
			if (frame < elapsed + duration) return drawing;
			elapsed += duration;
		}
		return Mathf.Max(0, count - 1);
	}

	private static int FindDrawingAtTick(SpriteFrames frames, StringName animation, int tick) =>
		ResolveAuthoredDuration(frames, animation, Mathf.Max(0, tick));

	private static int GetDrawingTicks(SpriteFrames frames, StringName animation, int drawing)
	{
		float speed = Mathf.Max(0.001f, (float)frames.GetAnimationSpeed(animation));
		return Mathf.Max(1, Mathf.RoundToInt((float)frames.GetFrameDuration(animation, drawing) * (60f / speed)));
	}
}
