using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PasteFrameCommand : Command
{
	Color32[] _pixels;
	Color32[] _originalPixels;
	
	public PasteFrameCommand(Color32[] pixels)
	{
		_name = "PasteFrameCommand";

		_pixels = pixels;
	}
	
	public override void DoCommand()
	{
		_originalPixels = new Color32[_editor.GetCanvas().pixelWidth * _editor.GetCanvas().pixelHeight];
		_editor.GetCanvas().GetPixels(_editor.CurrentFrame).CopyTo(_originalPixels, 0);
		
		_editor.GetCanvas().AddPixels(_pixels);
	}
	
	public override void UndoCommand()
	{
		_editor.GetCanvas().SetPixels(_editor.CurrentFrame, _originalPixels);
	}
}
