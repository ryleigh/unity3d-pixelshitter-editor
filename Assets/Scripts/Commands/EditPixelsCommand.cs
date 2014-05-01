using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EditPixelsCommand : Command
{
	Dictionary<PixelPoint, Color32> _replacedPixels;
	Dictionary<PixelPoint, Color32> _newPixels;
	
	public EditPixelsCommand(Dictionary<PixelPoint, Color32> replacedPixels, Dictionary<PixelPoint, Color32> newPixels)
	{
		_name = "EditPixelsCommand";

		_replacedPixels = new Dictionary<PixelPoint, Color32>(replacedPixels);
		_newPixels = new Dictionary<PixelPoint, Color32>(newPixels);
	}
	
	public override void DoCommand()
	{
		// do nothing for this command - the pixels have already been set individually
		// the command just exists to facilitate the undo
	}

	public override void RedoCommand()
	{
		foreach(KeyValuePair<PixelPoint, Color32> pair in _newPixels)
		{
			PixelPoint pixelPoint = pair.Key;
			Color32 color = pair.Value;
			_editor.GetCanvas().SetPixel(pixelPoint.x, pixelPoint.y, color);
		}
		
		_editor.GetCanvas().DirtyPixels = true;
	}

	public override void UndoCommand()
	{
		foreach(KeyValuePair<PixelPoint, Color32> pair in _replacedPixels)
		{
			PixelPoint pixelPoint = pair.Key;
			Color32 color = pair.Value;
			_editor.GetCanvas().SetPixel(pixelPoint.x, pixelPoint.y, color);
		}

		_editor.GetCanvas().DirtyPixels = true;
	}
}
