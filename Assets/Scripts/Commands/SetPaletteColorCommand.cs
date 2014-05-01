using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SetPaletteColorCommand : Command
{
	int _index;
	Color32 _color;

	Color32 _oldColor;
	
	public SetPaletteColorCommand(Color32 color, int index)
	{
		_name = "SetPaletteColorCommand";

		_color = color;
		_index = index;
	}
	
	public override void DoCommand()
	{
		_oldColor = _editor.GetPalette().GetColor(_index);

		_editor.GetPalette().SetPaletteColor(_color, _index);
		_editor.GetPalette().RefreshPaletteTextureColors();
	}
	
	public override void UndoCommand()
	{
		if(_oldColor.a > 0)
			_editor.GetPalette().SetPaletteColor(_oldColor, _index);
		else
			_editor.GetPalette().DeletePaletteColor(_index);

		_editor.GetPalette().RefreshPaletteTextureColors();
	}
}
