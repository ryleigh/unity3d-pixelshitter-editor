using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DuplicateFrameCommand : Command
{
	bool _backward;

	public DuplicateFrameCommand(bool backwards)
	{
		_name = "DuplicateFrameCommand";

		_backward = backwards;
	}
	
	public override void DoCommand()
	{
		_editor.GetFramePreview().DuplicateCurrentFrame(_backward);
	}
	
	public override void UndoCommand()
	{
		if(_backward)
			_editor.GetFramePreview().DeleteFrame(_editor.CurrentFrame - 1);
		else
			_editor.GetFramePreview().DeleteFrame(_editor.CurrentFrame + 1);
	}
}
