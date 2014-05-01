using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AddNewBlankFrameCommand : Command
{
	public AddNewBlankFrameCommand()
	{
		_name = "AddNewBlankFrameCommand";
	}
	
	public override void DoCommand()
	{
		_editor.GetFramePreview().AddNewBlankFrame();
	}
	
	public override void UndoCommand()
	{
		_editor.GetFramePreview().DeleteFrame(_editor.CurrentFrame);
	}
}
