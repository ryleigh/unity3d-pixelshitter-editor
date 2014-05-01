using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ClearCurrentFrameCommand : Command
{
	int _frameNumber;
	Color32[] _pixels;
	
	public ClearCurrentFrameCommand()
	{
		_name = "ClearCurrentFrameCommand";
	}
	
	public override void DoCommand()
	{
		_pixels = new Color32[_editor.GetCanvas().pixelWidth * _editor.GetCanvas().pixelHeight];
		_editor.GetCanvas().GetPixels(_editor.CurrentFrame).CopyTo(_pixels, 0);
		
		for(int y = 0; y < _editor.GetCanvas().pixelHeight; y++)
		{
			for(int x = 0; x < _editor.GetCanvas().pixelWidth; x++)
			{
				int index = y * _editor.GetCanvas().pixelWidth + x;
				_editor.GetCanvas().SetPixelForFrame(_editor.CurrentFrame, index, new Color32(0, 0, 0, 0));
			}
		}

		_editor.GetCanvas().DirtyPixels = true;
	}
	
	public override void UndoCommand()
	{
		_editor.GetCanvas().SetPixels(_editor.CurrentFrame, _pixels);
	}
}
