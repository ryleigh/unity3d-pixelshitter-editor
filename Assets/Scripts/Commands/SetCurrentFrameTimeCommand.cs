using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SetCurrentFrameTimeCommand : Command
{
	float _time;
	float _oldTime;

	public SetCurrentFrameTimeCommand(float time)
	{
		_name = "SetCurrentFrameTimeCommand";

		_time = time;
	}
	
	public override void DoCommand()
	{
		_oldTime = _editor.GetFramePreview().GetCurrentFrameTime();
		_editor.GetFramePreview().SetCurrentFrameTime(_time);
		_editor.GetFramePreview().AnimTimer = 0.0f;
		_editor.CurrentFrameTimeString = _editor.GetFramePreview().GetCurrentFrameTime().ToString();
	}
	
	public override void UndoCommand()
	{
		_editor.GetFramePreview().SetCurrentFrameTime(_oldTime);
		_editor.GetFramePreview().AnimTimer = 0.0f;
		_editor.CurrentFrameTimeString = _editor.GetFramePreview().GetCurrentFrameTime().ToString();
	}
}
