using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SetAnimNameCommand : Command
{
	string _animName;
	string _oldAnimName;
	
	public SetAnimNameCommand(string animName)
	{
		_name = "SetAnimNameCommand";

		_animName = animName;
	}
	
	public override void DoCommand()
	{
		_oldAnimName = _editor.AnimName;
		_editor.AnimName = _animName;
	}
	
	public override void UndoCommand()
	{
		_editor.AnimName = _oldAnimName;
	}
}
