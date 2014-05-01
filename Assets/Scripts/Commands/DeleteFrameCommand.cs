using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DeleteFrameCommand : Command
{
	int _frameNumber;
	Color32[] _pixels;

	public DeleteFrameCommand()
	{
		_name = "DeleteFrameCommand";
	}
	
	public override void DoCommand()
	{
		_frameNumber = _editor.CurrentFrame;
		_pixels = _editor.GetCanvas().GetPixels(_editor.CurrentFrame);

		_editor.GetFramePreview().DeleteFrame(_editor.CurrentFrame);
	}
	
	public override void UndoCommand()
	{
		_editor.GetFramePreview().AddNewFrame(_frameNumber, _pixels);
	}
}
