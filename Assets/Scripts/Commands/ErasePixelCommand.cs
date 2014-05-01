using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ErasePixelCommand : Command
{
	int _x;
	int _y;
	
	Color32 _oldPixelColor;
//	bool _oldPixelFlag;
	
	
	public ErasePixelCommand(int x, int y)
	{
		_name = "ErasePixelCommand";

		_x = x;
		_y = y;
	}
	
	public override void DoCommand()
	{
//		_oldPixelFlag = _editor.GetCanvas().GetPixelFlagForCurrentFrame(_x, _y);
		_oldPixelColor = _editor.GetCanvas().GetPixelForCurrentFrame(_x, _y);
		
		_editor.GetCanvas().ErasePixel(_x, _y);
	}
	
	public override void UndoCommand()
	{
		if(_oldPixelColor.a > 0)
		{
			_editor.GetCanvas().SetPixel(_x, _y, _oldPixelColor);
		}
		else
		{
			_editor.GetCanvas().ErasePixel(_x, _y);
		}
	}
}
