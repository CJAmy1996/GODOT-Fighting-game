namespace ModularFighter.Core;

/// <summary>Frame-counted command memory for motions that are easier to test outside a live Input event stream.</summary>
public sealed class MotionInputBuffer
{
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
}
