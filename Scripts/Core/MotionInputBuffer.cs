using System;
using System.Collections.Generic;

namespace ModularFighter.Core;

/// <summary>Frame-counted command memory for motions that are easier to test outside a live Input event stream.</summary>
public sealed class MotionInputBuffer
{
	private const int ReusableHistoryFrames = 360;

	private readonly struct DirectionEvent
	{
		public DirectionEvent(MotionDirection direction, long frame)
		{
			Direction = direction;
			Frame = frame;
		}

		public MotionDirection Direction { get; }
		public long Frame { get; }
	}

	private readonly struct ButtonEvent
	{
		public ButtonEvent(MotionAttackButton button, long frame)
		{
			Button = button;
			Frame = frame;
		}

		public MotionAttackButton Button { get; }
		public long Frame { get; }
	}

	private readonly List<DirectionEvent> _directionHistory = new();
	private readonly List<ButtonEvent> _buttonHistory = new();
	private readonly Dictionary<ulong, long> _consumedMotionCompletions = new();
	private long _reusableInputFrame;
	private int _framesSinceDown = int.MaxValue;
	private int _downToUpFrames = int.MaxValue;
	private int _downThenUpCommandFramesLeft;
	private int _framesSinceLeftTap = int.MaxValue;
	private int _framesSinceRightTap = int.MaxValue;
	private int _dashCommandFramesLeft;
	private int _dashCommandDirection;
	private int _qcfForwardCommandFramesLeft;
	private int _qcfForwardCommandAgeFrames = int.MaxValue;
	private int _framesSinceJumpPress = int.MaxValue;
	private int _backDashInputLockoutFrames;
	private int _framesSinceForwardTap = int.MaxValue;
	private int _forwardThenDownFramesLeft;
	private int _dragonPunchCommandFramesLeft;
	private int _downChargeFrames;
	private int _downChargeReleaseGraceFrames;
	private int _chargedDownUpCommandFramesLeft;
	private int _backChargeFrames;
	private int _backChargeReleaseGraceFrames;
	private int _chargedBackForwardCommandFramesLeft;
	private int _previousHorizontalDirection;

	public int DashCommandDirection => _dashCommandDirection;
	public bool HasDashCommand => _dashCommandFramesLeft > 0;
	public bool HasQuarterCircleForwardCommand => _qcfForwardCommandFramesLeft > 0;
	public int QuarterCircleForwardCommandAgeFrames => _qcfForwardCommandAgeFrames;
	public bool HasMotionSpecialCommand => HasQuarterCircleForwardCommand;
	public int MotionSpecialCommandAgeFrames => QuarterCircleForwardCommandAgeFrames;
	public int FramesSinceJumpPress => _framesSinceJumpPress;
	public bool HasDragonPunchCommand => _dragonPunchCommandFramesLeft > 0;
	public bool HasChargedDownUpCommand => _chargedDownUpCommandFramesLeft > 0;
	public bool HasChargedBackForwardCommand => _chargedBackForwardCommandFramesLeft > 0;

	/// <summary>Samples reusable motions once per 60 Hz fighter simulation frame.</summary>
	public void RecordReusableInput(FighterInput input, int facing, bool advanceFrame = true)
	{
		if (advanceFrame) _reusableInputFrame++;
		MotionDirection direction = ResolveRelativeDirection(input.Horizontal, input.Vertical, facing);
		if (_directionHistory.Count == 0 || _directionHistory[^1].Direction != direction)
			_directionHistory.Add(new DirectionEvent(direction, _reusableInputFrame));

		RecordPressedButton(input.LightPunchPressed, MotionAttackButton.LightPunch);
		RecordPressedButton(input.HeavyPunchPressed, MotionAttackButton.HeavyPunch);
		RecordPressedButton(input.LightKickPressed, MotionAttackButton.LightKick);
		RecordPressedButton(input.HeavyKickPressed, MotionAttackButton.HeavyKick);
		TrimReusableHistory();
	}

	public bool TryMatchReusableMotion(MotionInputBinding binding, FighterInput buttonInput, out long completionFrame,
		int buttonLeniencyFramesOverride = -1)
	{
		completionFrame = -1;
		MotionInputDefinition definition = binding?.Motion;
		if (definition == null || !MatchesButtonBinding(binding, buttonInput)) return false;

		bool matched = definition.Kind switch
		{
			MotionInputKind.ButtonMash => TryMatchButtonMash(definition, binding.Buttons,
				binding.MashWindowFramesOverride, out completionFrame),
			MotionInputKind.ChargeSequence => TryMatchChargeSequence(definition, buttonLeniencyFramesOverride, out completionFrame),
			_ => TryMatchDirectionSequence(definition, buttonLeniencyFramesOverride, out completionFrame, out _)
		};
		if (!matched) return false;

		ulong id = definition.GetInstanceId();
		return !_consumedMotionCompletions.TryGetValue(id, out long consumedFrame) || completionFrame > consumedFrame;
	}

	public void ConsumeReusableMotion(MotionInputDefinition definition, long completionFrame)
	{
		if (definition == null || completionFrame < 0) return;
		_consumedMotionCompletions[definition.GetInstanceId()] = completionFrame;
	}

	public void Tick()
	{
		if (_framesSinceDown < int.MaxValue) _framesSinceDown++;
		if (_framesSinceLeftTap < int.MaxValue) _framesSinceLeftTap++;
		if (_framesSinceRightTap < int.MaxValue) _framesSinceRightTap++;
		if (_framesSinceJumpPress < int.MaxValue) _framesSinceJumpPress++;
		if (_backDashInputLockoutFrames > 0) _backDashInputLockoutFrames--;
		if (_framesSinceForwardTap < int.MaxValue) _framesSinceForwardTap++;
		if (_forwardThenDownFramesLeft > 0) _forwardThenDownFramesLeft--;
		if (_dragonPunchCommandFramesLeft > 0) _dragonPunchCommandFramesLeft--;
		if (_chargedDownUpCommandFramesLeft > 0)
		{
			_chargedDownUpCommandFramesLeft--;
			if (_chargedDownUpCommandFramesLeft == 0)
			{
				_downChargeFrames = 0;
				_downChargeReleaseGraceFrames = 0;
			}
		}
		if (_chargedBackForwardCommandFramesLeft > 0)
		{
			_chargedBackForwardCommandFramesLeft--;
			if (_chargedBackForwardCommandFramesLeft == 0)
			{
				_backChargeFrames = 0;
				_backChargeReleaseGraceFrames = 0;
			}
		}
		if (_downThenUpCommandFramesLeft > 0) _downThenUpCommandFramesLeft--;
		if (_dashCommandFramesLeft > 0) _dashCommandFramesLeft--;
		TickQuarterCircleForwardCommand();
	}

	public void TickQuarterCircleForwardCommand()
	{
		if (_qcfForwardCommandFramesLeft > 0)
		{
			_qcfForwardCommandFramesLeft--;
			if (_qcfForwardCommandAgeFrames < int.MaxValue) _qcfForwardCommandAgeFrames++;
		}
		else
		{
			_qcfForwardCommandAgeFrames = int.MaxValue;
		}
	}

	public void PressDown()
	{
		_framesSinceDown = 0;
		if (_framesSinceForwardTap <= 16) _forwardThenDownFramesLeft = 16;
	}

	public void PressJump(int inputBufferFrames)
	{
		_framesSinceJumpPress = 0;
		if (_downChargeFrames >= 45) _chargedDownUpCommandFramesLeft = inputBufferFrames;
		if (_framesSinceDown >= int.MaxValue) return;
		_downToUpFrames = _framesSinceDown;
		_downThenUpCommandFramesLeft = inputBufferFrames;
	}

	public void UpdateDownCharge(bool holdingDown)
	{
		if (holdingDown)
		{
			_downChargeFrames++;
			_downChargeReleaseGraceFrames = 5;
		}
		else if (_chargedDownUpCommandFramesLeft <= 0)
		{
			if (_downChargeReleaseGraceFrames > 0)
				_downChargeReleaseGraceFrames--;
			else
				_downChargeFrames = 0;
		}
	}

	public void UpdateBackForwardCharge(float horizontal, int facing, int inputBufferFrames)
	{
		int direction = horizontal > 0.5f ? 1 : horizontal < -0.5f ? -1 : 0;
		int relative = direction * (facing >= 0 ? 1 : -1);
		if (relative < 0)
		{
			_backChargeFrames++;
			// Back may be released for five sampled frames before Forward.
			_backChargeReleaseGraceFrames = 5;
		}
		else if (relative > 0)
		{
			if (_previousHorizontalDirection <= 0 && _backChargeFrames >= 45)
				_chargedBackForwardCommandFramesLeft = inputBufferFrames;
			// The completed motion now has only its short button window.
			if (_backChargeFrames < 45) _backChargeFrames = 0;
		}
		else if (_chargedBackForwardCommandFramesLeft <= 0)
		{
			if (_backChargeReleaseGraceFrames > 0)
				_backChargeReleaseGraceFrames--;
			else
				_backChargeFrames = 0;
		}
		_previousHorizontalDirection = relative;
	}

	public void PressHorizontalTap(int direction, int facing, int inputBufferFrames, int doubleTapWindowFrames,
		int quarterCircleForwardWindowFrames, int quarterCircleForwardLatchFrames, int backDashInputLockoutWindowFrames)
	{
		int normalizedDirection = direction >= 0 ? 1 : -1;
		int normalizedFacing = facing >= 0 ? 1 : -1;
		if (normalizedDirection == normalizedFacing && _framesSinceDown <= quarterCircleForwardWindowFrames)
		{
			_qcfForwardCommandFramesLeft = quarterCircleForwardLatchFrames;
			_qcfForwardCommandAgeFrames = 0;
		}
		if (normalizedDirection == normalizedFacing)
		{
			if (_forwardThenDownFramesLeft > 0) _dragonPunchCommandFramesLeft = quarterCircleForwardLatchFrames;
			_framesSinceForwardTap = 0;
		}

		int framesSinceTap = normalizedDirection < 0 ? _framesSinceLeftTap : _framesSinceRightTap;
		if (framesSinceTap <= doubleTapWindowFrames)
		{
			bool backDashInput = normalizedDirection * normalizedFacing < 0;
			if (!backDashInput || _backDashInputLockoutFrames <= 0)
			{
				_dashCommandDirection = normalizedDirection;
				_dashCommandFramesLeft = inputBufferFrames;
				if (backDashInput) _backDashInputLockoutFrames = backDashInputLockoutWindowFrames;
			}
		}

		if (normalizedDirection < 0)
			_framesSinceLeftTap = 0;
		else
			_framesSinceRightTap = 0;
	}

	public bool IsDownThenUpCommand(int windowFrames) =>
		_downThenUpCommandFramesLeft > 0 && _downToUpFrames <= windowFrames;

	public void ConsumeDashCommand()
	{
		_dashCommandFramesLeft = 0;
		_dashCommandDirection = 0;
	}

	public void ConsumeQuarterCircleForwardCommand()
	{
		_qcfForwardCommandFramesLeft = 0;
		_qcfForwardCommandAgeFrames = int.MaxValue;
	}

	public void ConsumeDragonPunchCommand()
	{
		_dragonPunchCommandFramesLeft = 0;
		_forwardThenDownFramesLeft = 0;
	}

	public void ConsumeDownThenUpCommand()
	{
		_framesSinceDown = int.MaxValue;
		_downToUpFrames = int.MaxValue;
		_downThenUpCommandFramesLeft = 0;
	}

	public void ConsumeChargedDownUpCommand()
	{
		_chargedDownUpCommandFramesLeft = 0;
		_downChargeFrames = 0;
		_downChargeReleaseGraceFrames = 0;
	}

	public void ConsumeChargedBackForwardCommand()
	{
		_chargedBackForwardCommandFramesLeft = 0;
		_backChargeFrames = 0;
		_backChargeReleaseGraceFrames = 0;
	}

	private void RecordPressedButton(bool pressed, MotionAttackButton button)
	{
		if (pressed) _buttonHistory.Add(new ButtonEvent(button, _reusableInputFrame));
	}

	private void TrimReusableHistory()
	{
		long cutoff = _reusableInputFrame - ReusableHistoryFrames;
		// Keep the event that began the current/most recently completed hold so a
		// charge longer than the history window still retains its true duration.
		while (_directionHistory.Count > 1 && _directionHistory[1].Frame < cutoff)
			_directionHistory.RemoveAt(0);
		while (_buttonHistory.Count > 0 && _buttonHistory[0].Frame < cutoff)
			_buttonHistory.RemoveAt(0);
	}

	private bool TryMatchButtonMash(MotionInputDefinition definition, MotionAttackButton acceptedButtons,
		int windowOverride, out long completionFrame)
	{
		completionFrame = -1;
		int required = Math.Max(1, definition.RequiredButtonPresses);
		int windowFrames = Math.Max(1, windowOverride > 0 ? windowOverride : definition.MashWindowFrames);
		long cutoff = _reusableInputFrame - windowFrames + 1;
		int presses = 0;
		for (int index = _buttonHistory.Count - 1; index >= 0; index--)
		{
			ButtonEvent entry = _buttonHistory[index];
			if (entry.Frame < cutoff) break;
			if ((entry.Button & acceptedButtons) == 0) continue;
			if (completionFrame < 0) completionFrame = entry.Frame;
			if (++presses >= required) return true;
		}
		completionFrame = -1;
		return false;
	}

	private bool TryMatchChargeSequence(MotionInputDefinition definition, int buttonLeniencyFramesOverride,
		out long completionFrame)
	{
		completionFrame = -1;
		if (!TryMatchDirectionSequence(definition, buttonLeniencyFramesOverride,
			out long sequenceCompletion, out int firstSequenceIndex)) return false;
		long firstSequenceFrame = _directionHistory[firstSequenceIndex].Frame;
		for (int index = firstSequenceIndex - 1; index >= 0; index--)
		{
			DirectionEvent charge = _directionHistory[index];
			if (!DirectionSatisfiesCharge(charge.Direction, definition.ChargeDirection)) continue;
			long chargeEndFrame = index + 1 < _directionHistory.Count
				? _directionHistory[index + 1].Frame
				: _reusableInputFrame + 1;
			long heldFrames = chargeEndFrame - charge.Frame;
			long releaseGap = Math.Max(0, firstSequenceFrame - chargeEndFrame);
			if (heldFrames < Math.Max(1, definition.ChargeFrames) ||
				releaseGap > Math.Max(0, definition.ChargeReleaseLeniencyFrames)) return false;
			completionFrame = sequenceCompletion;
			return true;
		}
		return false;
	}

	private bool TryMatchDirectionSequence(MotionInputDefinition definition, int buttonLeniencyFramesOverride,
		out long completionFrame,
		out int firstSequenceIndex)
	{
		completionFrame = -1;
		firstSequenceIndex = -1;
		foreach (string notation in definition.DirectionSequences ?? Array.Empty<string>())
		{
			if (!TryParseSequence(notation, out MotionDirection[] sequence) || sequence.Length == 0) continue;
			if (!TryMatchSequenceVariant(sequence, definition, buttonLeniencyFramesOverride,
				out long candidateCompletion, out int candidateFirst)) continue;
			if (candidateCompletion <= completionFrame) continue;
			completionFrame = candidateCompletion;
			firstSequenceIndex = candidateFirst;
		}
		return completionFrame >= 0;
	}

	private bool TryMatchSequenceVariant(MotionDirection[] sequence, MotionInputDefinition definition,
		int buttonLeniencyFramesOverride,
		out long completionFrame, out int firstSequenceIndex)
	{
		completionFrame = -1;
		firstSequenceIndex = -1;
		int buttonLeniencyFrames = buttonLeniencyFramesOverride >= 0
			? buttonLeniencyFramesOverride
			: definition.ButtonLeniencyFrames;
		long completionCutoff = _reusableInputFrame - Math.Max(0, buttonLeniencyFrames);
		for (int endIndex = _directionHistory.Count - 1; endIndex >= 0; endIndex--)
		{
			DirectionEvent end = _directionHistory[endIndex];
			if (end.Frame < completionCutoff) break;
			if (end.Direction != sequence[^1]) continue;

			int step = sequence.Length - 2;
			int skipped = 0;
			int first = endIndex;
			for (int historyIndex = endIndex - 1; historyIndex >= 0 && step >= 0; historyIndex--)
			{
				DirectionEvent entry = _directionHistory[historyIndex];
				if (end.Frame - entry.Frame > Math.Max(1, definition.MotionWindowFrames)) break;
				if (entry.Direction == sequence[step])
				{
					first = historyIndex;
					step--;
					continue;
				}
				if (entry.Direction == MotionDirection.Neutral && sequence[step] != MotionDirection.Neutral) continue;
				if (++skipped > Math.Max(0, definition.MaxSkippedDirections)) break;
			}

			if (step >= 0 || end.Frame - _directionHistory[first].Frame > Math.Max(1, definition.MotionWindowFrames)) continue;
			completionFrame = end.Frame;
			firstSequenceIndex = first;
			return true;
		}
		return false;
	}

	private static bool TryParseSequence(string notation, out MotionDirection[] sequence)
	{
		if (string.IsNullOrWhiteSpace(notation))
		{
			sequence = Array.Empty<MotionDirection>();
			return false;
		}

		string[] tokens = notation.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		sequence = new MotionDirection[tokens.Length];
		for (int index = 0; index < tokens.Length; index++)
		{
			if (!TryParseDirection(tokens[index], out sequence[index]))
			{
				sequence = Array.Empty<MotionDirection>();
				return false;
			}
		}
		return true;
	}

	private static bool TryParseDirection(string token, out MotionDirection direction)
	{
		direction = token.Trim().ToUpperInvariant() switch
		{
			"N" or "5" or "NEUTRAL" => MotionDirection.Neutral,
			"F" or "6" or "FORWARD" => MotionDirection.Forward,
			"DF" or "3" or "DOWNFORWARD" => MotionDirection.DownForward,
			"D" or "2" or "DOWN" => MotionDirection.Down,
			"DB" or "1" or "DOWNBACK" => MotionDirection.DownBack,
			"B" or "4" or "BACK" => MotionDirection.Back,
			"UB" or "7" or "UPBACK" => MotionDirection.UpBack,
			"U" or "8" or "UP" => MotionDirection.Up,
			"UF" or "9" or "UPFORWARD" => MotionDirection.UpForward,
			_ => (MotionDirection)(-1)
		};
		return (int)direction >= 0;
	}

	private static MotionDirection ResolveRelativeDirection(float horizontal, float vertical, int facing)
	{
		int horizontalDirection = horizontal > 0.5f ? 1 : horizontal < -0.5f ? -1 : 0;
		int verticalDirection = vertical > 0.5f ? 1 : vertical < -0.5f ? -1 : 0;
		int relativeHorizontal = horizontalDirection * (facing >= 0 ? 1 : -1);
		if (verticalDirection > 0)
			return relativeHorizontal > 0 ? MotionDirection.DownForward :
				relativeHorizontal < 0 ? MotionDirection.DownBack : MotionDirection.Down;
		if (verticalDirection < 0)
			return relativeHorizontal > 0 ? MotionDirection.UpForward :
				relativeHorizontal < 0 ? MotionDirection.UpBack : MotionDirection.Up;
		return relativeHorizontal > 0 ? MotionDirection.Forward :
			relativeHorizontal < 0 ? MotionDirection.Back : MotionDirection.Neutral;
	}

	private static bool DirectionSatisfiesCharge(MotionDirection actual, MotionDirection required) => required switch
	{
		MotionDirection.Back => actual is MotionDirection.Back or MotionDirection.DownBack or MotionDirection.UpBack,
		MotionDirection.Forward => actual is MotionDirection.Forward or MotionDirection.DownForward or MotionDirection.UpForward,
		MotionDirection.Down => actual is MotionDirection.Down or MotionDirection.DownBack or MotionDirection.DownForward,
		MotionDirection.Up => actual is MotionDirection.Up or MotionDirection.UpBack or MotionDirection.UpForward,
		_ => actual == required
	};

	private static bool MatchesButtonBinding(MotionInputBinding binding, FighterInput input)
	{
		MotionAttackButton pressed = MotionAttackButton.None;
		if (input.LightPunchPressed) pressed |= MotionAttackButton.LightPunch;
		if (input.HeavyPunchPressed) pressed |= MotionAttackButton.HeavyPunch;
		if (input.LightKickPressed) pressed |= MotionAttackButton.LightKick;
		if (input.HeavyKickPressed) pressed |= MotionAttackButton.HeavyKick;
		if (binding.Buttons == MotionAttackButton.None) return false;
		return binding.ButtonMatchMode == MotionButtonMatchMode.AllSelectedButtons
			? (pressed & binding.Buttons) == binding.Buttons
			: (pressed & binding.Buttons) != 0;
	}
}
