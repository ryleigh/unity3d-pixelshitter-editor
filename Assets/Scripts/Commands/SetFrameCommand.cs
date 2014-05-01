using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SetFrameCommand : Command
{
	int _frameNumber;
	int _oldFrameNumber;

	public SetFrameCommand(int frameNumber)
	{
		_name = "SetFrameCommand";

		_frameNumber = frameNumber;
	}
	
	public override void DoCommand()
	{
		_oldFrameNumber = _editor.CurrentFrame;
		_editor.GetFramePreview().SetCurrentFrame(_frameNumber);
	}
	
	public override void UndoCommand()
	{
		_editor.GetFramePreview().SetCurrentFrame(_oldFrameNumber);
	}
}
