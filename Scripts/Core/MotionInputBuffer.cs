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
	private int _backDashInputLockoutFrames;

	public int DashCommandDirection => _dashCommandDirection;
	public bool HasDashCommand => _dashCommandFramesLeft > 0;
	public bool HasQuarterCircleForwardCommand => _qcfForwardCommandFramesLeft > 0;

	public void Tick()
	{
		if (_framesSinceDown < int.MaxValue) _framesSinceDown++;
		if (_framesSinceLeftTap < int.MaxValue) _framesSinceLeftTap++;
		if (_framesSinceRightTap < int.MaxValue) _framesSinceRightTap++;
		if (_backDashInputLockoutFrames > 0) _backDashInputLockoutFrames--;
		if (_downThenUpCommandFramesLeft > 0) _downThenUpCommandFramesLeft--;
		if (_dashCommandFramesLeft > 0) _dashCommandFramesLeft--;
		if (_qcfForwardCommandFramesLeft > 0) _qcfForwardCommandFramesLeft--;
	}

	public void PressDown() => _framesSinceDown = 0;

	public void PressJump(int inputBufferFrames)
	{
		if (_framesSinceDown >= int.MaxValue) return;
		_downToUpFrames = _framesSinceDown;
		_downThenUpCommandFramesLeft = inputBufferFrames;
	}

	public void PressHorizontalTap(int direction, int facing, int inputBufferFrames, int doubleTapWindowFrames,
		int quarterCircleForwardWindowFrames, int backDashInputLockoutWindowFrames)
	{
		int normalizedDirection = direction >= 0 ? 1 : -1;
		int normalizedFacing = facing >= 0 ? 1 : -1;
		if (normalizedDirection == normalizedFacing && _framesSinceDown <= quarterCircleForwardWindowFrames)
			_qcfForwardCommandFramesLeft = quarterCircleForwardWindowFrames;

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

	public void ConsumeQuarterCircleForwardCommand() => _qcfForwardCommandFramesLeft = 0;

	public void ConsumeDownThenUpCommand()
	{
		_framesSinceDown = int.MaxValue;
		_downToUpFrames = int.MaxValue;
		_downThenUpCommandFramesLeft = 0;
	}
}
