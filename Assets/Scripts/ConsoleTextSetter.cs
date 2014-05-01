using UnityEngine;
using System.Collections;

public class ConsoleTextSetter : MonoBehaviour
{
	public float lineLength = 120;
	public float lineLengthHard = 150;
	
	string[] words;
	ArrayList wordsList;
	string result = "";
	Rect TextSize;
	
	int numberOfLines = 1;
	
	void Awake()
	{
		FormatString(guiText.text);	
	}
	
	public void SetText( string text )
	{
		FormatString(text);
	}
	
	void FormatString (string text)
	{
		int charCount = 0;
		string displayString = "";
		for(int i = 0; i < text.Length; i++)
		{
			displayString += text[i];
			charCount++;

			if(charCount >= lineLengthHard || (charCount > lineLength && (text[i] == ')' || text[i] == ']' || text[i] == '}' || text[i] == '>')))
			{
				displayString += "\n";
				charCount = 0;
			}
		}

		guiText.text = displayString;
	}
}
