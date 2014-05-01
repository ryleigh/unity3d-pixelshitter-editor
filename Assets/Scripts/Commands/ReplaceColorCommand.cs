using UnityEngine;
using System.Collections;
using System.Collections.Generic;

struct ReplacedPixelData
{
	public int frameNumber;
	public int pixelIndex;

	public ReplacedPixelData(int frameNumber, int pixelIndex)
	{
		this.frameNumber = frameNumber;
		this.pixelIndex = pixelIndex;
	}
}

public class ReplaceColorCommand : Command
{
	Color32 _newColor;
	Color32 _oldColor;

	List<ReplacedPixelData> _replacedPixelData = new List<ReplacedPixelData>();
	
	public ReplaceColorCommand(Color32 oldColor, Color32 newColor)
	{
		_name = "ReplaceColorCommand";

		_oldColor = oldColor;
		_newColor = newColor;
	}
	
	public override void DoCommand()
	{
		for(int frameNum = 0; frameNum < _editor.GetFramePreview().GetNumFrames(); frameNum++)
		{
			Color32[] pixels = _editor.GetCanvas().GetPixels(frameNum);

			for(int i = 0; i < pixels.Length; i++)
			{
				if(pixels[i].Equals(_oldColor))
				{
					_replacedPixelData.Add(new ReplacedPixelData(frameNum, i));
					pixels[i] = _newColor;
				}
			}
		}

		_editor.GetCanvas().DirtyPixels = true;
	}
	
	public override void UndoCommand()
	{
		foreach(ReplacedPixelData data in _replacedPixelData)
			_editor.GetCanvas().SetPixelForFrame(data.frameNumber, data.pixelIndex, _oldColor);

		_editor.GetCanvas().DirtyPixels = true;
	}
}
