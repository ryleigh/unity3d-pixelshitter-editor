using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Command
{
	protected PixelEditorScreen _editor;
	public void SetEditor(PixelEditorScreen editor) { _editor = editor; }

	protected string _name;
	public string Name { get { return _name; } }

	public virtual void DoCommand()
	{
	
	}

	public virtual void RedoCommand()
	{
		DoCommand();
	}
	
	public virtual void UndoCommand()
	{
	
	}
}
