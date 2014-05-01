using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public enum ToolMode { Brush, Eraser, Dropper, Bucket };
public enum TextEnteringMode { None, AnimName, PixelDimensions, Hitbox, CurrentFrameTime, PaletteColor, LoadAnimation };

[RequireComponent(typeof(Camera))]
public class PixelEditorScreen : MonoBehaviour
{
	private static PixelEditorScreen _instance;
	public static PixelEditorScreen shared { get{ return _instance; } }

	bool _initialized = false;

	Vector2 _borderDragWorldOffset;
	//-----------------------------------------------------
	// GUI TEXT BOXES
	//-----------------------------------------------------
	string _animName = "...";
	public string AnimName { get { return _animName; } set { _animName = value; } }
	string _animNameTempString;
	string _pixelDimensionsString = "";

	PixelRect _hitbox;
	public PixelRect Hitbox { get { return _hitbox; } }
	string _hitboxString;
	bool _showingHitbox;
	public bool ShowingHitbox { get { return _showingHitbox; } }

	string _loopTypeString = "";
	public string LoopTypeString { get { return _loopTypeString; } set { _loopTypeString = value; } }
	string _onionSkinString = "";
	public string OnionSkinString { get { return _onionSkinString; } set { _onionSkinString = value; } }
	string _mirroringString = "";
	public string MirroringString { get { return _mirroringString; } set { _mirroringString = value; } }
	string _currentFrameTimeString = "";
	public string CurrentFrameTimeString { get { return _currentFrameTimeString; } set { _currentFrameTimeString = value; } }
	string _paletteColorString = "";
	public string PaletteColorString { get { return _paletteColorString; } set { _paletteColorString = value; } }
	string _gridIndexString = "(4,0)";
	public string GridIndexString { get { return _gridIndexString; } set { _gridIndexString = value; } }

	string _loadAnimString = "";

	GUIStyle _textStyle;
	GUIStyle _textStyleRed;
	GUIStyle _textStyleFocused;
	TextEnteringMode _textEnteringMode = TextEnteringMode.None;
	public TextEnteringMode GetTextEnteringMode() { return _textEnteringMode; }
	public void SetTextEnteringMode(TextEnteringMode mode) { _textEnteringMode = mode; }
	bool _firstChar = true;

	Vector2 _paletteInputScreenPosition;
	public Vector2 PaletteInputScreenPosition { get { return _paletteInputScreenPosition; } set { _paletteInputScreenPosition = value; } }

	Color32 _currentColor;
	public Color32 GetCurrentColor() { return _swatch.GetCurrentColor(); }

	Canvas _canvas;
	public Canvas GetCanvas() { return _canvas; }
	Swatch _swatch;
	Palette _palette;
	public Palette GetPalette() { return _palette; }
	FramePreview _framePreview;
	public FramePreview GetFramePreview() { return _framePreview; }
	PatternViewer _patternViewer;
	public PatternViewer GetPatternViewer() { return _patternViewer; }

	int _lastCanvasWidth;
	int _lastCanvasHeight;

	const int MAX_CANVAS_WIDTH = 128;
	const int MAX_CANVAS_HEIGHT = 128;

	public int CurrentFrame
	{
		get
		{
			if(_framePreview == null)
			{
				return 0;
			}
			return _framePreview.CurrentFrame;
		}
	}

	//-----------------------------------------------------
	// CURSOR STUFF
	//-----------------------------------------------------
	ToolMode _toolMode;
	public ToolMode GetToolMode() { return _toolMode; }

	public Texture2D cursorBrush;
	public Texture2D cursorEraser;
	public Texture2D cursorDropper;
	public Texture2D cursorPaint;
	//-----------------------------------------------------
	// COPY & PASTE EFFECTS
	//-----------------------------------------------------
	Color32[] _copiedPixels;
	GameObject _effectPlane;
	const float EFFECT_ZOOM_TIME = 0.25f;
	const float EFFECT_EXTRA_SCALE = 0.15f;

	public static float CAMERA_ZOOM_SPEED = 0.08f;
	public static float ZOOM_TIMER_LENIENCY = 0.2f;
	//-----------------------------------------------------
	// PLANE DEPTHS
	//-----------------------------------------------------
	public static float EFFECT_PLANE_DEPTH = 1.0f;
	public static float CANVAS_PLANE_DEPTH = 2.0f;
	public static float PALETTE_PLANE_DEPTH = 3.0f;
	public static float FRAME_PREVIEW_DEPTH = 4.0f;
	public static float SWATCH_PLANE_DEPTH = 5.0f;
	public static float PATTERN_PLANE_DEPTH = 6.0f;
	//-----------------------------------------------------
	// COLORS
	//-----------------------------------------------------
//	public static Color BORDER_COLOR = new Color(0.0f, 0.0f, 0.0f, 0.3f);
	public static Color BORDER_COLOR = new Color(0.7f, 0.7f, 0.7f, 1.0f);
	public static Color BORDER_COLOR_CURRENT_FRAME = new Color(0.45f, 0.45f, 0.45f, 1.0f);
	public static Color BORDER_COLOR_NONCURRENT_FRAME = new Color(0.8f, 0.8f, 0.8f, 1.0f);
	public static byte CANVAS_CHECKERS_EVEN_SHADE = 255;
	public static byte CANVAS_CHECKERS_ODD_SHADE = 249;

	public bool GetIsDragging() { return _canvas.DraggingCanvasBorder || _palette.DraggingPaletteBorder || _framePreview.DraggingPreviewFrames || _swatch.DraggingSwatchBorder || _patternViewer.DraggingPatternBorder; }
	public bool GetIsEditingPixels() { return _canvas.CurrentlyEditingPixels; }
	public bool GetModifierKey() { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftControl); }

	PseudoStack<Command> _commandUndoStack = new PseudoStack<Command>();
	PseudoStack<Command> _commandRedoStack = new PseudoStack<Command>();
	const int MAX_COMMANDS = 50;

	GUIText _consoleText;
	ConsoleTextSetter _consoleTextWrap;
	bool _isShowingConsoleText = false;

	public void AddNewCommand(Command command)
	{
		Debug.Log("AddNewCommand: " + command.Name);

		if(_commandUndoStack.Count >= MAX_COMMANDS)
			_commandUndoStack.Remove(0);

		command.SetEditor(this);
		command.DoCommand();
		_commandUndoStack.Push(command);
		_commandRedoStack.Clear();

	}

	public void UndoLastCommand()
	{
		if(_commandUndoStack.Count > 0)
		{
			Command lastCommand = _commandUndoStack.Pop();
			lastCommand.UndoCommand();
			_commandRedoStack.Push(lastCommand);
//			Debug.Log("Undo: " + lastCommand.Name);
		}
	}

	public void RedoLastCommand()
	{
		if(_commandRedoStack.Count > 0)
		{
			Command lastCommand = _commandRedoStack.Pop();
			lastCommand.RedoCommand();
			_commandUndoStack.Push(lastCommand);
//			Debug.Log("Redo: " + lastCommand.Name);
		}
	}

	void Awake()
	{
		_instance = this;
	}
	
	void Start()
	{
		Init();
	}
	
	public bool Init()
	{
		if(!_initialized)
		{
			// these seem to return 0 sometimes if we check them too early
			// so we won't consider the pixel screen initialized until we can get back a non-zero value for these
			if(Screen.width == 0 || Screen.height == 0)
				return false;

			int screenWidth = PlayerPrefs.GetInt("screenWidth", 800);
			int screenHeight = PlayerPrefs.GetInt("screenHeight", 500);
			screenWidth = Mathf.Max(screenHeight, 300);
			screenHeight = Mathf.Max(screenHeight, 300);		

			Screen.SetResolution(screenWidth, screenHeight, false);

			Camera camera = GetComponent<Camera>();
			camera.orthographic = true;
			camera.orthographicSize = 1;

			// ----------------------------------------------
			// CANVAS
			// ----------------------------------------------
			_canvas = gameObject.AddComponent<Canvas>();
			_canvas.Init(this);

			_lastCanvasWidth = _canvas.pixelWidth;
			_lastCanvasHeight = _canvas.pixelHeight;

			_copiedPixels = new Color32[_canvas.pixelWidth * _canvas.pixelHeight];
			_canvas.GetPixels(CurrentFrame).CopyTo(_copiedPixels, 0);

			_pixelDimensionsString = _canvas.pixelWidth.ToString() + "," + _canvas.pixelHeight.ToString();
			_hitbox = new PixelRect(0, 0, _canvas.pixelWidth, _canvas.pixelHeight);
			RefreshHitboxString();

			// ----------------------------------------------
			// SWATCH
			// ----------------------------------------------
			_swatch = gameObject.AddComponent<Swatch>();
			_swatch.Init(this); 
			SetCurrentColor(Color.black);

			// ----------------------------------------------
			// PALETTE
			// ----------------------------------------------
			_palette = gameObject.AddComponent<Palette>();
			_palette.Init(this); 

			CreateEffectPlane();

			// ----------------------------------------------
			// FRAME PREVIEW
			// ----------------------------------------------
			_framePreview = gameObject.AddComponent<FramePreview>();
			_framePreview.Init(this);

			// ----------------------------------------------
			// PATTERN VIEWER
			// ----------------------------------------------
			_patternViewer = gameObject.AddComponent<PatternViewer>();
			_patternViewer.Init(this);

			_initialized = true;

			_textStyle = new GUIStyle();
			_textStyle.font = Resources.Load("Fonts/04b03") as Font;
			_textStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
			_textStyle.fontSize = 20;

			_textStyleRed = new GUIStyle(_textStyle);
			_textStyleRed.normal.textColor = new Color(0.8f, 0.2f, 0.2f);

			_textStyleFocused = new GUIStyle();
			_textStyleFocused.font = Resources.Load("Fonts/04b03") as Font;
			_textStyleFocused.normal.textColor = Color.white;
			Texture2D black = new Texture2D(1, 1);
			black.SetPixel(0, 0, Color.black);
			black.Apply();
			_textStyleFocused.normal.background = black;
			_textStyleFocused.fontSize = 20;

			_toolMode = ToolMode.Brush;
//			Cursor.SetCursor(cursorBrush, Vector2.zero, CursorMode.Auto);

			_canvas.SetOnionSkinMode((OnionSkinMode)PlayerPrefs.GetInt("onionSkinMode", 0));
			_canvas.RefreshOnionSkin();

			_canvas.SetMirroringMode((MirroringMode)PlayerPrefs.GetInt("mirroringMode", 0));

			_consoleText = gameObject.AddComponent<GUIText>();
			_consoleText.color = Color.black;
			_consoleText.font = Resources.Load("Fonts/04b03") as Font;
			_consoleText.fontSize = 12;
			_consoleText.transform.position = new Vector2(0.0025f, 0);
			_consoleText.anchor = TextAnchor.LowerLeft;

			_consoleTextWrap = gameObject.AddComponent<ConsoleTextSetter>();

			string animData = PlayerPrefs.GetString("animData", "");
			Debug.Log("animData: " + animData);
			if(animData != "")
				LoadAnimationString(animData);
		}

		return _initialized;
	}

	void ShowConsoleText(string text)
	{
		_consoleTextWrap.SetText(text);
		_isShowingConsoleText = true;
	}

	// create the plane that will be animated for copy/paste effects
	// (only do this once)
	void CreateEffectPlane()
	{
		GameObject canvasPlane = _canvas.GetCanvasPlane();

		_effectPlane = (GameObject)Instantiate(canvasPlane);
		Destroy(_effectPlane.transform.FindChild("Border").gameObject); // get rid of border
		Destroy(_effectPlane.transform.FindChild("Grid").gameObject); // get rid of grid
		Destroy(_effectPlane.transform.FindChild("OnionSkin").gameObject); // get rid of onion skin
		_effectPlane.transform.parent = canvasPlane.transform.parent;
		_effectPlane.name = "EffectPlane";

		Material effectMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		_effectPlane.renderer.material = effectMaterial;

		_effectPlane.SetActive(false);
	}

	// used each time we want to play the copy effect
	// resets the orientation of the copy frame, fills with current pixel data, and starts the copy animation coroutine
	void PlayCopyEffect()
	{
		GameObject canvasPlane = _canvas.GetCanvasPlane();

		StopCoroutine("CopyEffectCoroutine");
		StopCoroutine("PasteEffectCoroutine");

		_effectPlane.renderer.material.mainTexture = (Texture2D)Instantiate(canvasPlane.renderer.material.mainTexture);
		_effectPlane.transform.localPosition = new Vector3(canvasPlane.transform.localPosition.x, canvasPlane.transform.localPosition.y, EFFECT_PLANE_DEPTH);
		_effectPlane.transform.localScale = canvasPlane.transform.localScale;
		_effectPlane.renderer.material.color = Color.white;
		_effectPlane.SetActive(true);

		StartCoroutine("CopyEffectCoroutine");
	}

	IEnumerator CopyEffectCoroutine()
	{
		GameObject canvasPlane = _canvas.GetCanvasPlane();
		float elapsedTime = 0.0f;

		while(elapsedTime < EFFECT_ZOOM_TIME)
		{
			float extraScale = Utils.Map(elapsedTime, 0.0f, EFFECT_ZOOM_TIME, 0.0f, EFFECT_EXTRA_SCALE, true, EASING_TYPE.CUBIC_EASE_OUT);
			float opacity = Utils.Map(elapsedTime, 0.0f, EFFECT_ZOOM_TIME, 1.0f, 0.0f, true, EASING_TYPE.CUBIC_EASE_OUT);

			_effectPlane.transform.localScale = new Vector3(canvasPlane.transform.localScale.x * (1.0f + extraScale), canvasPlane.transform.localScale.y * (1.0f + extraScale), 1.0f);
			_effectPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, opacity);

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		_effectPlane.SetActive(false);
		_effectPlane.renderer.material.mainTexture = null;
	}

	// used each time we want to play the paste effect
	// sets the effect frame to the zoomed-out orientation, fills with current copied pixel data, and starts the paste animation coroutine
	void PlayPasteEffect()
	{
		GameObject canvasPlane = _canvas.GetCanvasPlane();

		StopCoroutine("CopyEffectCoroutine");
		StopCoroutine("PasteEffectCoroutine");

		_effectPlane.renderer.material.mainTexture = (Texture2D)Instantiate(canvasPlane.renderer.material.mainTexture);
		((Texture2D)_effectPlane.renderer.material.mainTexture).SetPixels32(_copiedPixels);
		((Texture2D)_effectPlane.renderer.material.mainTexture).Apply();

		_effectPlane.transform.localPosition = new Vector3(canvasPlane.transform.localPosition.x, canvasPlane.transform.localPosition.y, EFFECT_PLANE_DEPTH);
		_effectPlane.transform.localScale = new Vector3(canvasPlane.transform.localScale.x * (1.0f + EFFECT_EXTRA_SCALE), canvasPlane.transform.localScale.y * (1.0f + EFFECT_EXTRA_SCALE), 1.0f);
		_effectPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_effectPlane.SetActive(true);
		
		StartCoroutine("PasteEffectCoroutine");
	}
	
	IEnumerator PasteEffectCoroutine()
	{
		GameObject canvasPlane = _canvas.GetCanvasPlane();
		float elapsedTime = 0.0f;
		
		while(elapsedTime < EFFECT_ZOOM_TIME)
		{
			float extraScale = Utils.Map(elapsedTime, 0.0f, EFFECT_ZOOM_TIME, EFFECT_EXTRA_SCALE, 0.0f, true, EASING_TYPE.SINE_EASE_IN);
			float opacity = Utils.Map(elapsedTime, 0.0f, EFFECT_ZOOM_TIME, 0.0f, 1.0f, true, EASING_TYPE.SINE_EASE_IN);
			
			_effectPlane.transform.localScale = new Vector3(canvasPlane.transform.localScale.x * (1.0f + extraScale), canvasPlane.transform.localScale.y * (1.0f + extraScale), 1.0f);
			_effectPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, opacity);
			
			elapsedTime += Time.deltaTime;
			yield return null;
		}
		
		_effectPlane.SetActive(false);
		_effectPlane.renderer.material.mainTexture = null;

		AddNewCommand(new PasteFrameCommand(_copiedPixels));
	}

	void NewCanvas()
	{
		_framePreview.NewCanvas();

		_commandUndoStack.Clear();
		_commandRedoStack.Clear();

		_canvas.NewCanvas();

		// re-init copied pixels so the array is the right size/dimensions
		if(_canvas.pixelWidth != _lastCanvasWidth || _canvas.pixelHeight != _lastCanvasHeight)
			_copiedPixels = new Color32[_canvas.pixelWidth * _canvas.pixelHeight];

		_hitbox = new PixelRect(0, 0, _canvas.pixelWidth, _canvas.pixelHeight);
		RefreshHitboxString();
		_canvas.RefreshHitbox();

		_framePreview.ResizeFramePreviews();
		_framePreview.CreateFramePlane();

		_lastCanvasWidth = _canvas.pixelWidth;
		_lastCanvasHeight = _canvas.pixelHeight;

		_patternViewer.NewCanvas();
	}

	void Update()
	{
		// -------------------------------------------------
		// UNDO
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.Z) && !GetIsEditingPixels())
			UndoLastCommand();

		// -------------------------------------------------
		// REDO
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.Y) && !GetIsEditingPixels())
			RedoLastCommand();

		// -------------------------------------------------
		// TOGGLE SHOWING HITBOX
		// -------------------------------------------------
		if(Input.GetKeyDown(KeyCode.H) && _textEnteringMode == TextEnteringMode.None)
		{
			_showingHitbox = !_showingHitbox;
			_canvas.RefreshHitbox();
		}

		// -------------------------------------------------
		// LOADING ANIMATIONS
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.O) && !GetIsEditingPixels())
		{
			_loadAnimString = "***PASTE ANIM DATA***";
			_textEnteringMode = TextEnteringMode.LoadAnimation;
		}

		// -------------------------------------------------
		// SWITCHING TOOLS
		// -------------------------------------------------
		if(_textEnteringMode == TextEnteringMode.None)
		{
			if(Input.GetKeyDown(KeyCode.B))
				SetToolMode(ToolMode.Brush);
			else if(Input.GetKeyDown(KeyCode.E))
				SetToolMode(ToolMode.Eraser);
			else if(Input.GetKeyDown(KeyCode.G))
				SetToolMode(ToolMode.Bucket);
			else if(Input.GetKeyDown(KeyCode.F))
				SetToolMode(ToolMode.Dropper);
		}

		// -------------------------------------------------
		// CANVAS
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.N) && !GetIsEditingPixels() && _textEnteringMode == TextEnteringMode.None)
		{
			NewCanvas();
		}

		_canvas.UpdateCanvas();
		_swatch.UpdateSwatch();
		_palette.UpdatePalette();
		_framePreview.UpdateFramePreview();
		_patternViewer.UpdatePatternViewer();

		// -------------------------------------------------
		// COPYING FRAMES
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.C) && !GetIsEditingPixels())
		{
			_canvas.GetPixels(CurrentFrame).CopyTo(_copiedPixels, 0);
			_canvas.DirtyPixels = true;

			PlayCopyEffect();
		}

		// -------------------------------------------------
		// PASTING FRAMES
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.V) && !GetIsEditingPixels())
		{
			PlayPasteEffect();
		}

		// -------------------------------------------------
		// SAVING DATA
		// -------------------------------------------------
		if(GetModifierKey() && Input.GetKeyDown(KeyCode.S))
		{
			ClipboardHelper.clipBoard = GetAnimData();
			Debug.Log("clipboard: " + ClipboardHelper.clipBoard);
			ShowConsoleText(ClipboardHelper.clipBoard);
		}

		// -------------------------------------------------
		// CONSOLE TEXT
		// -------------------------------------------------
		if(_isShowingConsoleText && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
		{
			_consoleTextWrap.SetText("");
			_isShowingConsoleText = false;
		}
		// -------------------------------------------------
		// TEXT INPUT
		// -------------------------------------------------
		if(_textEnteringMode == TextEnteringMode.None)
		{
			if(Input.GetKeyDown(KeyCode.Return))
			{
				_textEnteringMode = TextEnteringMode.CurrentFrameTime;
				_firstChar = true;
			}
		}
		else
		{
			HandleInputString(Input.inputString);

			// -------------------------------------------------------------------------------------
			// ENTERING ANIM NAME
			// -------------------------------------------------------------------------------------
			if(_textEnteringMode == TextEnteringMode.AnimName)
			{
				if(Input.GetKeyDown(KeyCode.Return))
				{
					if(_animNameTempString != "")
					{
						AddNewCommand(new SetAnimNameCommand(_animNameTempString));
						_textEnteringMode = TextEnteringMode.None;
					}
				}
				else if(Input.GetKeyDown(KeyCode.Tab))
				{
					if(GetModifierKey())
					{
						// edit frame time
						_textEnteringMode = TextEnteringMode.CurrentFrameTime;
					}
					else
					{
						// edit dimensions
						_textEnteringMode = TextEnteringMode.PixelDimensions;
					}

					_animNameTempString = _animName;
					_firstChar = true;
				}
			}
			// -------------------------------------------------------------------------------------
			// ENTERING PIXEL DIMENSTIONS
			// -------------------------------------------------------------------------------------
			else if(_textEnteringMode == TextEnteringMode.PixelDimensions)
			{
				if(Input.GetKeyDown(KeyCode.Return))
				{
					string[] vals = _pixelDimensionsString.Split(',');
					if(vals.Length == 2)
					{
						int pw = 0;
						int.TryParse(vals[0], out pw);
						int ph = 0;
						int.TryParse(vals[1], out ph);
						if((pw != 0 && ph != 0) &&
						   !(pw == _canvas.pixelWidth && ph == _canvas.pixelHeight) &&
						   (pw <= MAX_CANVAS_WIDTH && ph <= MAX_CANVAS_HEIGHT))
						{
							_canvas.pixelWidth = pw;
							_canvas.pixelHeight = ph;
							NewCanvas();
						}

						_pixelDimensionsString = _canvas.pixelWidth.ToString() + "," + _canvas.pixelHeight.ToString();
						_textEnteringMode = TextEnteringMode.None;
					}
				}
				else if(Input.GetKeyDown(KeyCode.Tab))
				{
					if(GetModifierKey())
					{
						// edit anim name
						_textEnteringMode = TextEnteringMode.AnimName;
					}
					else
					{
						// edit hitbox
						_textEnteringMode = TextEnteringMode.Hitbox;
					}

					_pixelDimensionsString = _canvas.pixelWidth.ToString() + "," + _canvas.pixelHeight.ToString();
					_firstChar = true;
				}
			}
			// -------------------------------------------------------------------------------------
			// ENTERING HITBOX
			// -------------------------------------------------------------------------------------
			else if(_textEnteringMode == TextEnteringMode.Hitbox)
			{
				if(Input.GetKeyDown(KeyCode.Return))
				{
					string[] vals = _hitboxString.Split(',');
					if(vals.Length == 4)
					{
						int left = -1;
						int.TryParse(vals[0], out left);
						int bottom = -1;
						int.TryParse(vals[1], out bottom);
						int width = -1;
						int.TryParse(vals[2], out width);
						int height = -1;
						int.TryParse(vals[3], out height);
						if(left >= 0 && bottom >= 0 && width > 0 && height > 0 &&
						   width <= _canvas.pixelWidth && height <= _canvas.pixelHeight &&
						   left + width <= _canvas.pixelWidth && bottom + height <= _canvas.pixelHeight)
						{
							_hitbox = new PixelRect(left, bottom, width, height);
							_canvas.RefreshHitbox();
						}

						RefreshHitboxString();
						_textEnteringMode = TextEnteringMode.None;
					}
				}
				else if(Input.GetKeyDown(KeyCode.Tab))
				{
					if(GetModifierKey())
					{
						// edit dimensions
						_textEnteringMode = TextEnteringMode.PixelDimensions;
					}
					else
					{
						// edit frame time
						_textEnteringMode = TextEnteringMode.CurrentFrameTime;
					}

					RefreshHitboxString();
					_firstChar = true;
				}
			}
			// -------------------------------------------------------------------------------------
			// ENTERING CURRENT FRAME TIME
			// -------------------------------------------------------------------------------------
			else if(_textEnteringMode == TextEnteringMode.CurrentFrameTime)
			{
				if(Input.GetKeyDown(KeyCode.Return))
				{
					float time = 0.0f;
					bool valid = float.TryParse(_currentFrameTimeString, out time);

					if(valid && time > 0.0f)
					{
						AddNewCommand(new SetCurrentFrameTimeCommand(time));
						_textEnteringMode = TextEnteringMode.None;
					}
					else
					{
						_currentFrameTimeString = _framePreview.GetCurrentFrameTime().ToString();
						_textEnteringMode = TextEnteringMode.None;
					}
				}
				else if(Input.GetKeyDown(KeyCode.Tab))
				{
					if(GetModifierKey())
					{
						// edit hitbox
						_textEnteringMode = TextEnteringMode.Hitbox;
					}
					else
					{
						// edit anim name
						_textEnteringMode = TextEnteringMode.AnimName;
					}

					_currentFrameTimeString = _framePreview.GetCurrentFrameTime().ToString();
					_firstChar = true;
				}
			}
		}
	}

	void HandleInputString(string inputString)
	{
		if(_textEnteringMode == TextEnteringMode.AnimName)
		{
			foreach (char c in Input.inputString)
			{
				if(c == "\b"[0])
				{
					if(_firstChar)
					{
						_animNameTempString = "";
						_firstChar = false;
					}
					else if(_animNameTempString.Length != 0)
					{
						_animNameTempString = _animNameTempString.Substring(0, _animNameTempString.Length - 1);
					}
				}   
				else if(c != "\n"[0] && c != "\r"[0] && c != "\t"[0])
				{
					if(_firstChar)
					{
						_animNameTempString = "";
						_firstChar = false;
					}

					if(_animNameTempString.Length < 16)
						_animNameTempString += c;
				}
			}
		}
		else if(_textEnteringMode == TextEnteringMode.PixelDimensions)
		{
			foreach (char c in Input.inputString)
			{
				if(c == "\b"[0])
				{
					if(_firstChar)
					{
						_pixelDimensionsString = "";
						_firstChar = false;
					}
					else if(_pixelDimensionsString.Length != 0)
					{
						_pixelDimensionsString = _pixelDimensionsString.Substring(0, _pixelDimensionsString.Length - 1);
					}
				}   
				else if(c != "\n"[0] && c != "\r"[0] && c != "\t"[0])
				{
					if(_firstChar)
					{
						_pixelDimensionsString = "";
						_firstChar = false;
					}

					if(_pixelDimensionsString.Length < 16)
						_pixelDimensionsString += c;
				}
			}
		}
		else if(_textEnteringMode == TextEnteringMode.Hitbox)
		{
			foreach (char c in Input.inputString)
			{
				if(c == "\b"[0])
				{
					if(_firstChar)
					{
						_hitboxString = "";
						_firstChar = false;
					}
					else if(_hitboxString.Length != 0)
					{
						_hitboxString = _hitboxString.Substring(0, _hitboxString.Length - 1);
					}
				}   
				else if(c != "\n"[0] && c != "\r"[0] && c != "\t"[0])
				{
					if(_firstChar)
					{
						_hitboxString = "";
						_firstChar = false;
					}
					
					if(_hitboxString.Length < 24)
						_hitboxString += c;
				}
			}
		}
		else if(_textEnteringMode == TextEnteringMode.CurrentFrameTime)
		{
			foreach (char c in Input.inputString)
			{
				if(c == "\b"[0])
				{
					if(_firstChar)
					{
						_currentFrameTimeString = "";
						_firstChar = false;
					}
					else if(_currentFrameTimeString.Length != 0)
					{
						_currentFrameTimeString = _currentFrameTimeString.Substring(0, _currentFrameTimeString.Length - 1);
					}
				}   
				else if(c != "\n"[0] && c != "\r"[0] && c != "\t"[0])
				{
					if(_firstChar)
					{
						_currentFrameTimeString = "";
						_firstChar = false;
					}
					
					if(_currentFrameTimeString.Length < 16)
						_currentFrameTimeString += c;
				}
			}
		}
	}

	void OnGUI()
	{
		float SPACING = 20;

		GUILayout.BeginHorizontal();
		GUILayout.Space(4);
		GUILayout.Label((_textEnteringMode == TextEnteringMode.AnimName) ? "\"" + _animNameTempString + "\"" : "\"" + _animName + "\"", (_textEnteringMode == TextEnteringMode.AnimName) ? _textStyleFocused : _textStyle);
		GUILayout.Space(SPACING);
		GUILayout.Label("SIZE: " + _pixelDimensionsString, (_textEnteringMode == TextEnteringMode.PixelDimensions) ? _textStyleFocused : _textStyle);
		GUILayout.Space(SPACING);
		GUILayout.Label("HITBOX: " + _hitboxString, (_textEnteringMode == TextEnteringMode.Hitbox) ? _textStyleFocused : (_showingHitbox) ? _textStyleRed : _textStyle);
		GUILayout.Space(SPACING * 5);
		GUILayout.Label("TIME: " + _currentFrameTimeString + ((_textEnteringMode == TextEnteringMode.CurrentFrameTime) ? "" : "s"), (_textEnteringMode == TextEnteringMode.CurrentFrameTime) ? _textStyleFocused : _textStyle);
		GUILayout.Space(SPACING * 3);
		GUILayout.Label(_loopTypeString, _textStyle);
		GUILayout.Space(SPACING * 3);
		GUILayout.Label(_onionSkinString, _textStyle);
		GUILayout.Space(SPACING * 3);
		GUILayout.Label(_mirroringString, _textStyle);
		GUILayout.EndHorizontal();

		if(Input.GetKey(KeyCode.T))
		{
			Vector2 pos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); // gotta flip the y-position;
			float width = _textStyleFocused.CalcSize(new GUIContent(_gridIndexString)).x;
			GUI.TextField(new Rect(pos.x - 2, pos.y - 26, width, 20), _gridIndexString, _textStyleFocused);
		}

		if(_textEnteringMode == TextEnteringMode.LoadAnimation)
		{
			GUI.SetNextControlName("LoadAnim");
			
			float width = _textStyleFocused.CalcSize(new GUIContent(_loadAnimString)).x;
			_loadAnimString = GUI.TextField(new Rect(30, 30, width, 20), _loadAnimString, _textStyleFocused);
			
			GUI.FocusControl("LoadAnim");

			if(Event.current.keyCode == KeyCode.Tab)
			{
				_textEnteringMode = TextEnteringMode.None;
			}
			else if(Event.current.keyCode == KeyCode.Return)
			{
				if(_loadAnimString.Length > 0)
					LoadAnimationString(_loadAnimString);

				_textEnteringMode = TextEnteringMode.None;
			}
		}
		else if(_textEnteringMode == TextEnteringMode.PaletteColor)
		{
			GUI.SetNextControlName("PaletteColor");

			float width = _textStyleFocused.CalcSize(new GUIContent(_paletteColorString)).x;
			_paletteColorString = _paletteColorString.Replace(System.Environment.NewLine, "*");
			_paletteColorString = _paletteColorString.Replace(" ", string.Empty);
			_paletteColorString = _paletteColorString.Replace("\t", string.Empty);
			_paletteColorString = GUI.TextField(new Rect(_paletteInputScreenPosition.x - 2, _paletteInputScreenPosition.y - 26, width, 20), _paletteColorString, _textStyleFocused);

			GUI.FocusControl("PaletteColor");

			if(Event.current.keyCode == KeyCode.Tab)
			{
				_textEnteringMode = TextEnteringMode.None;
			}
			else if(Event.current.keyCode == KeyCode.Return)
			{
				if(_paletteColorString == "x all" || _paletteColorString == "xall")
				{
					_palette.ClearAllColors();
					_textEnteringMode = TextEnteringMode.None;
					return;
				}
				else if(_paletteColorString == "x")
				{
					AddNewCommand(new DeletePaletteColorCommand(_palette.PaletteColorEditIndex));

					_palette.RefreshPaletteTextureColors();
					_textEnteringMode = TextEnteringMode.None;
					return;
				}

				bool firstColor = true;
				bool replaceAllInstances = false;
				if(_paletteColorString.Length > 0 && _paletteColorString[0] == '=')
				{
					_paletteColorString = _paletteColorString.Substring(1);
					replaceAllInstances = true;
				}

				List<Color32> colorsToAdd = new List<Color32>();

				string[] colors = _paletteColorString.Split('*');
				for(int i = 0; i < colors.Length; i++)
				{
					string[] vals = colors[i].Split(',');
					if(vals.Length == 3 || vals.Length == 4)
					{
						byte r = 0;
						bool rSet = byte.TryParse(vals[0], out r);
						byte g = 0;
						bool gSet = byte.TryParse(vals[1], out g);
						byte b = 0;
						bool bSet = byte.TryParse(vals[2], out b);
						byte a = 0;
						bool aSet = false;
						if(vals.Length == 4)
							aSet = byte.TryParse(vals[3], out a);

						if(vals.Length == 3 && rSet && gSet && bSet)
							colorsToAdd.Add(new Color32(r, g, b, 255));
						else if(vals.Length == 4 && rSet && gSet && bSet && aSet)
							colorsToAdd.Add(new Color32(r, g, b, a));
					}
				}

				int currentIndex = _palette.PaletteColorEditIndex;
				foreach(Color32 color in colorsToAdd)
				{
					if(currentIndex > _palette.GetNumPaletteColors() - 1)
						break;

					if(firstColor)
					{
						// set swatch to the new color
						SetCurrentColor(color);
						firstColor = false;
					}

					if(replaceAllInstances)
					{
						Color32 oldColor = _palette.GetColor(currentIndex);
						AddNewCommand(new ReplaceColorCommand(oldColor, color));
						replaceAllInstances = false;
					}

					AddNewCommand(new SetPaletteColorCommand(color, currentIndex));
					currentIndex++;
				}

				_palette.RefreshPaletteTextureColors();
				_textEnteringMode = TextEnteringMode.None;
			}
		}
	}

	void RefreshHitboxString()
	{
		_hitboxString = _hitbox.left.ToString() + "," + _hitbox.bottom.ToString() + "," + _hitbox.width + "," + _hitbox.height;
	}

	public void SetCurrentColor(Color color)
	{
		SetCurrentColor((Color32)color);
	}

	public void SetCurrentColor(Color32 color)
	{
		_swatch.SetColor(color);
	}

	string GetAnimData()
	{
		string str = "";
		
		// NAME
		str += "*" + ((_animName == "" || _animName == "...") ? "NAME" : _animName) + "*";
		// ANIM SIZE
		str += "!" + _canvas.pixelWidth.ToString() + "," + _canvas.pixelHeight.ToString() + "!";
		// HITBOX
		RefreshHitboxString();
		str += "<" + _hitboxString + ">";
		
		// FRAMES
		for(int i = 0; i < _framePreview.GetNumFrames(); i++)
			str += GetFrameData(i);
		
		// LOOPS
		str += "&" + ((int)_framePreview.LoopMode).ToString() + "&";

		return str;
	}

	string GetFrameData(int frameNumber)
	{
		Color32[] currentFramePixels = _canvas.GetPixels(frameNumber);

		// figure out if we should use 'differences from last frame' mode or not
		bool differencesMode = false;
		if(frameNumber > 0)
		{
			Color32[] lastFramePixels = _canvas.GetPixels(frameNumber - 1);

			int numSamePixels = 0;
			for(int x = 0; x < _canvas.pixelWidth; x++)
			{
				for(int y = 0; y < _canvas.pixelHeight; y++)
				{
					int index = y * _canvas.pixelWidth + x;
					if(currentFramePixels[index].a > 0 && currentFramePixels[index].Equals(lastFramePixels[index]))
						numSamePixels++;
				}
			}

			int numTotalPixels = _canvas.pixelWidth * _canvas.pixelHeight;
			if(numSamePixels > (int)(numTotalPixels / 2))
				differencesMode = true;
		}

		// START FRAME
		string str = "{";

		if(differencesMode)
			str += "^";
		else
			str += "-";

		// COLOR AND PIXELS
		Dictionary<Color32, List<PixelPoint>> pixels = new Dictionary<Color32, List<PixelPoint>>();
		for(int x = 0; x < _canvas.pixelWidth; x++)
		{
			for(int y = 0; y < _canvas.pixelHeight; y++)
			{
				int index = y * _canvas.pixelWidth + x;

				if(differencesMode)
				{
					Color32[] lastFramePixels = _canvas.GetPixels(frameNumber - 1);

					if(!currentFramePixels[index].Equals(lastFramePixels[index]))
					{
						Color32 color = currentFramePixels[index];
						PixelPoint pixelPoint = new PixelPoint(x, y);
						
						if(pixels.ContainsKey(color))
							pixels[color].Add(pixelPoint);
						else
							pixels[color] = new List<PixelPoint>() { pixelPoint };
					}
				}
				else
				{
					if(_canvas.GetPixelExists(frameNumber, index))
					{
						Color32 color = currentFramePixels[index];
						PixelPoint pixelPoint = new PixelPoint(x, y);
						
						if(pixels.ContainsKey(color))
							pixels[color].Add(pixelPoint);
						else
							pixels[color] = new List<PixelPoint>() { pixelPoint };
					}
				}
			}
		}
		
		foreach(KeyValuePair<Color32, List<PixelPoint>> pair in pixels)
		{
			List<PixelPoint> pixelPoints = pair.Value;
			if(pixelPoints.Count > 0)
			{
				Dictionary<int, List<int>> coordinates = new Dictionary<int, List<int>>(); // y-value is the key, and has list of x-values (for that y) for the value
				foreach(PixelPoint point in pixelPoints)
				{
					if(coordinates.ContainsKey(point.y))
						coordinates[point.y].Add(point.x);
					else
					coordinates[point.y] = new List<int>() { point.x };
				}
				
				Color32 col = pair.Key;
				if(col.a == 0) // a blank pixel
				{
					str += "[]";
				}
				else if(col.r == col.g && col.r == col.b) // a shade of grey
				{
					if(col.a == 255)
						str += "[" + col.r.ToString() + "]"; // solid grey
					else
						str += "[" + col.r.ToString() + "," + col.a.ToString() + "]";
				}
				else if(col.a == 255) // a solid color
				{
					str += "[" + col.r.ToString() + "," + col.g.ToString() + "," + col.b.ToString() + "]";
				}
				else // a transparent color
				{
					str += "[" + col.r.ToString() + "," + col.g.ToString() + "," + col.b.ToString() + "," + col.a.ToString() + "]";
				}
				
				foreach(KeyValuePair<int, List<int>> coordPair in coordinates)
				{
					int yPos = coordPair.Key;
					List<int> xPositions = coordPair.Value;
					
					str += "(" + yPos.ToString() + "-";
					
					bool first = true;
					foreach(int xPos in xPositions)
					{
						if(first)
							first = false;
						else
							str += ",";
						
						str += xPos.ToString();
					}
					
					str += ")";
				}
			}
		}
		
		// FRAME TIME
		str += "#" + _framePreview.GetFrameTime(frameNumber).ToString() + "#";
		// END FRAME
		str += "}";
		
		return str;
	}

	void LateUpdate()
	{
		if(!_initialized)
			return;

		if(_canvas.DirtyPixels)
		{
			// ------------------------------------------------------------
			// UPDATE CANVAS
			// ------------------------------------------------------------
			_canvas.UpdateTexture();

			// ------------------------------------------------------------
			// UPDATE FRAME PREVIEWS
			// ------------------------------------------------------------
			for(int i = 0; i < _framePreview.GetNumFrames(); i++)
				_framePreview.SetPixels(_canvas.GetPixels(i), i);

			// ------------------------------------------------------------
			// UPDATE PATTERN VIEWER
			// ------------------------------------------------------------
			_patternViewer.RefreshPatternViewer();
		}
	}

//	void HandleSwatchColorChanging()
//	{
//		float COLOR_CHANGE_AMOUNT = 0.99f;
//
//		if(GetModifierKey())
//		{
//			if(Input.GetAxis("Mouse ScrollWheel") < 0)
//			{
//				Color color = (Color)_currentColor;
//				Color newColor = new Color(color.r * COLOR_CHANGE_AMOUNT, color.g * COLOR_CHANGE_AMOUNT, color.b * COLOR_CHANGE_AMOUNT);
//				SetCurrentColor((Color32)newColor);
//			}
//			else if(Input.GetAxis("Mouse ScrollWheel") > 0)
//			{
//				Color color = (Color)_currentColor;
//				Color newColor = new Color(color.r * (1.0f - COLOR_CHANGE_AMOUNT), color.g * (1.0f - COLOR_CHANGE_AMOUNT), color.b * (1.0f - COLOR_CHANGE_AMOUNT));
//				SetCurrentColor((Color32)newColor);
//			}
//		}
//	}

	public GameObject CreatePlane(string planeName, Transform parentTransform, float localDepth)
	{
		GameObject plane = new GameObject(planeName);
		plane.AddComponent(typeof(MeshRenderer));
		plane.AddComponent(typeof(MeshFilter));
		plane.transform.parent = parentTransform;
		plane.transform.localPosition = new Vector3(0, 0, localDepth);
		
		Mesh mesh = new Mesh();
		mesh.vertices = new Vector3[] { new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0) };
		mesh.triangles = new int[] {0, 1, 2, 1, 3, 2};
		mesh.uv = new Vector2[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
		
		plane.GetComponent<MeshFilter>().mesh = mesh;
		plane.AddComponent(typeof(MeshCollider));
		
		return plane;
	}

	public void SetToolMode(ToolMode toolMode)
	{
		if(_toolMode == toolMode)
			return;
		
		_toolMode = toolMode;
		
		switch(_toolMode)
		{
		case ToolMode.Brush:
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
//			Cursor.SetCursor(cursorBrush, Vector2.zero, CursorMode.Auto);
			break;
		case ToolMode.Eraser:
			Cursor.SetCursor(cursorEraser, Vector2.zero, CursorMode.Auto);
			break;
		case ToolMode.Dropper:
			Cursor.SetCursor(cursorDropper, Vector2.zero, CursorMode.Auto);
			break;
		case ToolMode.Bucket:
			Cursor.SetCursor(cursorPaint, Vector2.zero, CursorMode.Auto);
			break;
		}
	}

	void OnApplicationQuit()
	{
		_palette.OnQuit();

		PlayerPrefs.SetInt("screenWidth", Screen.width);
		PlayerPrefs.SetInt("screenHeight", Screen.height);

		PlayerPrefs.SetString("animData", GetAnimData());

		PlayerPrefs.Save();
	}

	// ** name
	// !! size
	// {} frame
	// [] color
	// () pixel
	// <> hitbox
	// ## time
	// && loops
	void LoadAnimationString(string animString)
	{
		AnimationData animData = ParseAnimationString(animString);
		if(animData == null)
			return;

		_animName = animData.name;
		_framePreview.SetLoopMode(animData.loopMode);
		_canvas.pixelWidth = animData.animSize.x;
		_canvas.pixelHeight = animData.animSize.y;

		NewCanvas();

		int frameNumber = 0;
		foreach(FrameData frameData in animData.frames)
		{
			// create a new frame
			if(frameNumber >= _framePreview.GetNumFrames())
				_framePreview.AddNewBlankFrame();

			_framePreview.SetFrameTime(frameData.animTime, frameNumber);

			foreach(PixelData pixelData in frameData.pixels)
				_canvas.SetPixelForFrame(frameNumber, pixelData.position.x, pixelData.position.y, pixelData.color);

			frameNumber++;
		}

		// NewCanvas() resets hitbox to anim size so gotta set it again
		_hitbox = animData.hitbox;
		RefreshHitboxString();

		_canvas.DirtyPixels = true;
	}
	
	AnimationData ParseAnimationString(string animString)
	{
		Stack<ParseAnimState> animStates = new Stack<ParseAnimState>();
		string currentString = "";
		
		// ANIMATION DATA
		string name = "";
		List<FrameData> frames = new List<FrameData>();
		LoopMode loopMode = LoopMode.Loops;
		
		// CURRENT FRAME DATA
		Color32 currentColor = new Color32(0, 0, 0, 0);
		List<PixelData> currentPixels = new List<PixelData>();
		List<PixelData> previousFramePixels = new List<PixelData>();
		PixelPoint animSize = new PixelPoint(0, 0);
		PixelRect hitbox = new PixelRect(0, 0, 0, 0);
		float currentAnimTime = 0.0f;
		int currentPixelYPos = 0;

		bool differencesMode = false;
		
		foreach(char c in animString)
		{
			if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.DifferencesMode)
			{
				if(c == '^')
					differencesMode = true;
				else
					differencesMode = false;

				animStates.Pop();
				animStates.Push(ParseAnimState.Frame);
			}
			else
			{
				if(c == '*')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.Name)
					{
						name = currentString;
						currentString = "";
						animStates.Pop();
					}
					else
					{
						animStates.Push(ParseAnimState.Name);
						currentString = "";
					}
				}
				else if(c == '!')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.Size)
					{
						// parse current string for animation rect and save it
						string[] vals = currentString.Split(',');
						if(vals.Length == 2)
						{
							int xSize = 0;
							int ySize = 0;
							if(!(int.TryParse(vals[0], out xSize) && int.TryParse(vals[1], out ySize)))
							{
								ShowConsoleText(currentString + " is not properly formatted anim size data!");
								return null;
							}
							animSize = new PixelPoint(xSize, ySize);
						}
						else
						{
							ShowConsoleText(currentString + " is not properly formatted anim size data!");
							return null;
						}
						
						currentString = "";
						animStates.Pop();
					}
					else
					{
						animStates.Push(ParseAnimState.Size);
					}
				}
				else if(c == '<')
				{
					animStates.Push(ParseAnimState.Hitbox);
				}
				else if(c == '>')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.Hitbox)
					{
						// parse current string for hitbox info and save it
						string[] vals = currentString.Split(',');
						if(vals.Length == 4)
						{
							int left = 0;
							int bottom = 0;
							int width = 0;
							int height = 0;

							if(!(int.TryParse(vals[0], out left) &&
							     int.TryParse(vals[1], out bottom) &&
							     int.TryParse(vals[2], out width) &&
							     int.TryParse(vals[3], out height)))
							{
								ShowConsoleText(currentString + " is not properly formatted hitbox data!");
								return null;
							}

							hitbox = new PixelRect(left, bottom, width, height);
						}
						else
						{
							ShowConsoleText(currentString + " is not properly formatted hitbox data!");
							return null;
						}
						
						currentString = "";
						animStates.Pop();
					}
				}
				else if(c == '{')
				{
					animStates.Push(ParseAnimState.DifferencesMode);
				}
				else if(c == '}')
				{
					if(differencesMode)
					{
						// currentPixels holds pixels that are DIFFERENT than the previous frame
						List<PixelData> pixels = new List<PixelData>();
						foreach(PixelData prevPixelData in previousFramePixels)
						{
							// only add the previous frame pixels that AREN'T being overriden by the new frame data
							bool allowPixel = true;
							foreach(PixelData pixel in currentPixels)
							{
								if(pixel.position == prevPixelData.position)
								{
									allowPixel = false;
									break;
								}
							}

							if(allowPixel)
								pixels.Add(new PixelData(prevPixelData.position, prevPixelData.color));
						}

						// add in the new, different pixels
						foreach(PixelData newPixel in currentPixels)
						{
							pixels.Add(newPixel);
						}

						currentPixels.Clear();
						
						frames.Add(new FrameData(pixels, currentAnimTime));
						
						previousFramePixels.Clear();
						previousFramePixels.AddRange(pixels);
					}
					else
					{
						// save current frame data
						List<PixelData> pixels = new List<PixelData>();
						pixels.AddRange(currentPixels);
						currentPixels.Clear();
						
						frames.Add(new FrameData(pixels, currentAnimTime));

						previousFramePixels.Clear();
						previousFramePixels.AddRange(pixels);
					}

					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.Frame)
						animStates.Pop();
				}
				else if(c == '[')
				{
					animStates.Push(ParseAnimState.PixelColor);
				}
				else if(c == ']')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.PixelColor)
					{
						// parse current string for pixel color
						if(currentString.Length == 0)
						{
							currentColor = new Color32(0, 0, 0, 0);
						}
						else
						{
							string[] vals = currentString.Split(',');
							if(vals.Length == 1)
							{
								byte grey = 0;
								if(!byte.TryParse(vals[0], out grey))
								{
									ShowConsoleText(currentString + " is not properly formatted color data!");
									return null;
								}
								currentColor = new Color32(grey, grey, grey, 255);
							}
							else if(vals.Length == 2)
							{
								byte grey = 0;
								byte opacity = 0;
								if(!(byte.TryParse(vals[0], out grey) && byte.TryParse(vals[1], out opacity)))
								{
									ShowConsoleText(currentString + " is not properly formatted color data!");
									return null;
								}

								currentColor = new Color32(grey, grey, grey, opacity);
							}
							else if(vals.Length == 3)
							{
								byte red = 0;
								byte green = 0;
								byte blue = 0;
								if(!(byte.TryParse(vals[0], out red) && byte.TryParse(vals[1], out green) && byte.TryParse(vals[2], out blue)))
								{
									ShowConsoleText(currentString + " is not properly formatted color data!");
									return null;
								}

								currentColor = new Color32(red, green, blue, 255);
							}
							else if(vals.Length == 4)
							{
								byte red = 0;
								byte green = 0;
								byte blue = 0;
								byte opacity = 0;
								if(!(byte.TryParse(vals[0], out red) && byte.TryParse(vals[1], out green) && byte.TryParse(vals[2], out blue) && byte.TryParse(vals[3], out opacity)))
								{
									ShowConsoleText(currentString + " is not properly formatted color data!");
									return null;
								}
								
								currentColor = new Color32(red, green, blue, opacity);
							}
							else
							{
								ShowConsoleText(currentString + " is not properly formatted color data!");
							}
						}
						
						currentString = "";
						animStates.Pop();
					}
				}
				else if(c == '(')
				{
					animStates.Push(ParseAnimState.PixelYPos);
				}
				else if(c == '-')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.PixelYPos)
					{
						if(!int.TryParse(currentString, out currentPixelYPos))
						{
							ShowConsoleText(currentString + " is not a properly formatted y-position!");
							return null;
						}

						currentString = "";
						animStates.Pop();
						
						animStates.Push(ParseAnimState.PixelXPos);
					}
				}
				else if(c == ')')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.PixelXPos)
					{
						// parse current string for x pixel positions (for the current pixel y pos)
						// add a pixel of current color to a list
						string[] vals = currentString.Split(',');
						for(int i = 0; i < vals.Length; i++)
						{
							int xPos = 0;
							if(!int.TryParse(vals[i], out xPos))
							{
								ShowConsoleText(currentString + " is not a properly formatted x-position!");
								return null;
							}

							currentPixels.Add(new PixelData(new PixelPoint(xPos, currentPixelYPos), currentColor));
						}
						
						animStates.Pop();
					}
					currentString = "";
				}
				else if(c == '#')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.AnimTime)
					{
						// parse current string for animation time and save it
						if(!float.TryParse(currentString, out currentAnimTime))
						{
							ShowConsoleText(currentString + " is not a properly formatted frame time!");
							return null;
						}
						
						currentString = "";
						animStates.Pop();
					}
					else
					{
						animStates.Push(ParseAnimState.AnimTime);
					}
				}
				else if(c == '&')
				{
					if(animStates.Count > 0 && animStates.Peek() == ParseAnimState.Loops)
					{
						// parse current string for loop mode and save it
						int loopModeInt = 0;
						if(!int.TryParse(currentString, out loopModeInt))
						{
							ShowConsoleText(currentString + " is not a properly formatted loop mode!");
							return null;
						}
						
						loopMode = (LoopMode)loopModeInt;
						
						currentString = "";
						animStates.Pop();
					}
					else
					{
						animStates.Push(ParseAnimState.Loops);
					}
				}
				else
				{
					currentString += c;
				}
			}
		}
		
		AnimationData animData = new AnimationData(name, frames, animSize, hitbox, loopMode);
		return animData;
	}
}

public enum ParseAnimState { Name, Size, Hitbox, Frame, DifferencesMode, PixelColor, PixelYPos, PixelXPos, AnimTime, Loops };

public class AnimationData
{
	public string name;
	public List<FrameData> frames;
	public PixelPoint animSize;
	public PixelRect hitbox;
	public LoopMode loopMode;
	
	public AnimationData(string name, List<FrameData> frames, PixelPoint animSize, PixelRect hitbox, LoopMode loopMode)
	{
		this.name = name;
		this.frames = frames;
		this.animSize = animSize;
		this.hitbox = hitbox;
		this.loopMode = loopMode;
	}
}

public struct FrameData
{
	public List<PixelData> pixels;
	public float animTime;
	
	public FrameData(List<PixelData> pixels, float animTime = 0.0f)
	{
		this.pixels = pixels;
		this.animTime = animTime;
	}
}

public struct PixelData
{
	public PixelPoint position;
	public Color32 color;
	
	public PixelData(PixelPoint position, Color32 color)
	{
		this.position = position;
		this.color = color;
	}
}





















