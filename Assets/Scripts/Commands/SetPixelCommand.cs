using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SetPixelCommand : Command
{
	int _x;
	int _y;
	Color32 _color;

	Color32 _oldPixelColor;
//	bool _oldPixelFlag;


	public SetPixelCommand(int x, int y, Color32 color)
	{
		_name = "SetPixelCommand";

		_x = x;
		_y = y;
		_color = color;
	}

	public override void DoCommand()
	{
//		_oldPixelFlag = _editor.GetCanvas().GetPixelFlagForCurrentFrame(_x, _y);

		_oldPixelColor = _editor.GetCanvas().GetPixelForCurrentFrame(_x, _y);

		if(_color.a == 255)
			_editor.GetCanvas().SetPixel(_x, _y, _color);
		else
			_editor.GetCanvas().AddPixel(_x, _y, _color);
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
