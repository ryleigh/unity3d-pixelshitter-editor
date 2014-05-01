using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FlipCanvasCommand : Command
{
	bool _horizontal;
	
	public FlipCanvasCommand(bool horizontal)
	{
		_name = "FlipCanvasCommand";
		
		_horizontal = horizontal;
	}
	
	public override void DoCommand()
	{
		Flip(_horizontal);
	}
	
	public override void UndoCommand()
	{
		Flip(_horizontal);
	}

	void Flip(bool horizontal)
	{
		int pixelWidth = _editor.GetCanvas().pixelWidth;
		int pixelHeight = _editor.GetCanvas().pixelHeight;

		Color32[] pixels = new Color32[pixelWidth * pixelHeight];
		Color32[] currentCanvasPixels = _editor.GetCanvas().GetPixels(_editor.CurrentFrame);

		for(int x = 0; x < pixelWidth; x++)
		{
			for(int y = 0; y < pixelHeight; y++)
			{
				int index = y * pixelWidth + x;

				int flippedIndex;
				if(_horizontal)
					flippedIndex = y * pixelWidth + (pixelWidth - 1 - x);
				else
					flippedIndex = (pixelHeight - 1 - y) * pixelWidth + x;

				pixels[index] = currentCanvasPixels[flippedIndex];
			}
		}

		_editor.GetCanvas().SetPixels(_editor.CurrentFrame, pixels);
	}
}
