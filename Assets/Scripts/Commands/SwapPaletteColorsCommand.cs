using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SwapPaletteColorsCommand : Command
{
	int _indexA;
	int _indexB;

	public SwapPaletteColorsCommand(int indexA, int indexB)
	{
		_name = "SwapPaletteColorsCommand";

		_indexA = indexA;
		_indexB = indexB;
	}
	
	public override void DoCommand()
	{
		Swap();
	}
	
	public override void UndoCommand()
	{
		Swap();
	}

	void Swap()
	{
		Palette palette = _editor.GetPalette();
		
		Color32 tempColor = palette.GetColor(_indexA);
		
		palette.SetPaletteColor(palette.GetColor(_indexB), _indexA);
		palette.SetPaletteColor(tempColor, _indexB);
		
		palette.RefreshPaletteTextureColors(); 
	}
}
