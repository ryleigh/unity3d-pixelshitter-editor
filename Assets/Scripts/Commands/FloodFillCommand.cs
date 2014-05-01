using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FloodFillCommand : Command
{
	Color32 _oldPixelColor;
	Color32 _newColor;

	int _x;
	int _y;

	List<PixelPoint> _changedPixels = new List<PixelPoint>();

	public FloodFillCommand(int x, int y, Color32 color)
	{
		_name = "FloodFillCommand";

		_x = x;
		_y = y;
		_newColor = color;
	}
	
	public override void DoCommand()
	{
		_oldPixelColor = _editor.GetCanvas().GetPixelForCurrentFrame(_x, _y);
		_editor.GetCanvas().FloodFill(_x, _y, _oldPixelColor, _newColor, _changedPixels);
		_editor.GetCanvas().DirtyPixels = true;
	}
	
	public override void UndoCommand()
	{
		foreach(PixelPoint pixelPoint in _changedPixels)
			_editor.GetCanvas().SetPixel(pixelPoint.x, pixelPoint.y, (Color)_oldPixelColor);

		_editor.GetCanvas().DirtyPixels = true;
	}
}
