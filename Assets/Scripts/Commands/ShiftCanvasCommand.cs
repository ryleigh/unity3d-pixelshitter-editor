using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShiftCanvasCommand : Command
{
	Direction _direction;
	
	public ShiftCanvasCommand(Direction direction)
	{
		_name = "ShiftCanvasCommand";

		_direction = direction;
	}
	
	public override void DoCommand()
	{
		_editor.GetCanvas().ShiftPixels(_direction);
	}
	
	public override void UndoCommand()
	{
		_editor.GetCanvas().ShiftPixels(Globals.GetOppositeDirection(_direction));
	}
}
