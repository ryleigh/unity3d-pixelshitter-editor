using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum OnionSkinMode { None, Previous, Next };
public enum MirroringMode { None, Horizontal, Vertical, Both };

public class Canvas : MonoBehaviour
{
	public int pixelWidth;
	public int pixelHeight;

	PixelEditorScreen _editor;

	List<Color32[]> _pixels = new List<Color32[]>();
	public Color32[] GetPixels(int frameNumber) { return _pixels[frameNumber]; }
	public void SetPixels(int frameNumber, Color32[] newPixels)
	{
		newPixels.CopyTo(_pixels[frameNumber], 0);
		_dirtyPixels = true;
	}

	public Color32 GetPixel(int frameNumber, int index)
	{
		return _pixels[frameNumber][index];
	}

	public Color32 GetPixelForCurrentFrame(int x, int y)
	{
		int index = y * pixelWidth + x;
		return _pixels[_editor.CurrentFrame][index];
	}

	public bool GetPixelExistsForCurrentFrame(int x, int y)
	{
		int index = y * pixelWidth + x;
		return _pixels[_editor.CurrentFrame][index].a > 0;
	}

	public bool GetPixelExistsForCurrentFrame(int index)
	{
		return _pixels[_editor.CurrentFrame][index].a > 0;
	}

	public bool GetPixelExists(int frameNumber, int index)
	{
		return _pixels[frameNumber][index].a > 0;
	}

	public void SetPixelForFrame(int frameNumber, int index, Color32 color)
	{
		_pixels[frameNumber][index] = color;
	}

	public void SetPixelForFrame(int frameNumber, int x, int y, Color32 color)
	{
		int index = y * pixelWidth + x;
		_pixels[frameNumber][index] = color;
	}

	bool _isCurrentlyEditingPixels = false;
	public bool CurrentlyEditingPixels { get { return _isCurrentlyEditingPixels; } }
	Dictionary<PixelPoint, Color32> _currentlyReplacedPixels = new Dictionary<PixelPoint, Color32>();
	Dictionary<PixelPoint, Color32> _currentlyAddedPixels = new Dictionary<PixelPoint, Color32>();

	// don't draw the same pixels again until mouse-up
	// works with flood-fill too, that's why it's a bit redundant
	List<int> _pixelsDrawnThisStroke = new List<int>();

	PixelPoint _lastDrawnPoint; // used for lines

	bool _dirtyPixels = true;
	public bool DirtyPixels { get { return _dirtyPixels; } set { _dirtyPixels = value; } }

	GameObject _canvasPlane;
	public GameObject GetCanvasPlane() { return _canvasPlane; }

	Texture2D _canvasTexture;
	const float CANVAS_DEFAULT_SCALE = 0.45f;
	float _zoomTimer = 0.0f;

	// BORDER .............................................
	GameObject _canvasBorderPlane;
	bool _draggingCanvasBorder = false;
	public bool DraggingCanvasBorder { get { return _draggingCanvasBorder; } }

	Vector2 _borderDragWorldOffset;

	// GRID .............................................
	GameObject _gridPlane;
	Texture2D _gridTexture;

	// ONION SKIN .............................................
	GameObject _onionSkinPlane;
	Texture2D _onionSkinTexture;
	OnionSkinMode _onionSkinMode;
	float _onionSkinOpacity;
	float ONION_SKIN_OPACITY_DEFAULT = 0.25f;
	float ONION_SKIN_OPACITY_MIN = 0.10f;
	float ONION_SKIN_OPACITY_MAX = 0.50f;
	float ONION_SKIN_OPACITY_INCREMENT = 0.05f;

	// MIRRORING .............................................
	MirroringMode _mirroringMode;

	// HITBOX .............................................
	GameObject _hitboxPlane;
	Texture2D _hitboxTexture;
	float HITBOX_OPACITY = 0.25f;

	const float HITBOX_DEPTH = -0.1f;
	const float ONION_SKIN_DEPTH = 0.1f;
	const float GRID_DEPTH = 0.2f;
	const float BORDER_DEPTH = 0.3f;

	public void Init(PixelEditorScreen editor)
	{
		_editor = editor;

		pixelWidth = PlayerPrefs.GetInt("pixelWidth", 16);
		pixelHeight = PlayerPrefs.GetInt("pixelHeight", 16);
		pixelWidth = Mathf.Clamp(pixelWidth, 1, 128);
		pixelHeight = Mathf.Clamp(pixelHeight, 1, 128);

		CreateCanvasPlane();
		InitPixels();
		CreateCanvasTexture();
	}

	public void UpdateTexture()
	{
		_canvasTexture.SetPixels32(_pixels[_editor.CurrentFrame]);
		_canvasTexture.Apply();
		
		_dirtyPixels = false;
	}

	public void UpdateCanvas()
	{
		// -------------------------------------------------
		// MOUSE INPUT
		// -------------------------------------------------
		bool leftMouse = Input.GetMouseButton(0);
		bool rightMouse = Input.GetMouseButton(1);
		bool leftMouseDown = Input.GetMouseButtonDown(0);
		bool leftMouseUp = Input.GetMouseButtonUp(0);
		bool rightMouseDown = Input.GetMouseButtonDown(1);
		bool rightMouseUp = Input.GetMouseButtonUp(1);

		bool zoomedThisFrame = false;

		// -------------------------------------------------
		// MOUSE INPUT
		// -------------------------------------------------
		RaycastHit ray = new RaycastHit();
		if(Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out ray))
		{
			Texture2D textureMap = (Texture2D)ray.transform.renderer.material.mainTexture;
			Vector2 pixelUV = ray.textureCoord;
			pixelUV.x *= textureMap.width;
			pixelUV.y *= textureMap.height;
			
			int x = (int)pixelUV.x;
			int y = (int)pixelUV.y;

			_editor.GridIndexString = "(" + x.ToString() + "," + y.ToString() + ")";

			// -------------------------------------------------------------------------
			// CANVAS PLANE
			// -------------------------------------------------------------------------
			if(ray.transform.gameObject == _canvasPlane)
			{
				int index = y * pixelWidth + x;
				bool pixelExists = _pixels[_editor.CurrentFrame][index].a > 0;
				
				if(leftMouse && !_editor.GetIsDragging())
				{
					if(_editor.GetToolMode() == ToolMode.Brush)
					{
						Color32 indexColor = _pixels[_editor.CurrentFrame][index];
						if(!indexColor.Equals(_editor.GetCurrentColor()) || indexColor.a != 255)
						{
							if(_editor.GetModifierKey())
							{
								DrawLine(_lastDrawnPoint.x, _lastDrawnPoint.y, x, y);
								if(_mirroringMode == MirroringMode.Horizontal || _mirroringMode == MirroringMode.Both)
									DrawLine(pixelWidth - 1 - _lastDrawnPoint.x, _lastDrawnPoint.y, pixelWidth - 1 - x, y);
								if(_mirroringMode == MirroringMode.Vertical || _mirroringMode == MirroringMode.Both)
									DrawLine(_lastDrawnPoint.x, pixelHeight - 1 - _lastDrawnPoint.y, x, pixelHeight - 1 - y);
								if(_mirroringMode == MirroringMode.Both)
									DrawLine(pixelWidth - 1 - _lastDrawnPoint.x, pixelHeight - 1 - _lastDrawnPoint.y, pixelWidth - 1 - x, pixelHeight - 1 - y);
							}
							else
							{
								ToolDrawPixel(x, y);
								if(_mirroringMode == MirroringMode.Horizontal || _mirroringMode == MirroringMode.Both)
									ToolDrawPixel(pixelWidth - 1 - x, y);
								if(_mirroringMode == MirroringMode.Vertical || _mirroringMode == MirroringMode.Both)
									ToolDrawPixel(x, pixelHeight - 1 - y);
								if(_mirroringMode == MirroringMode.Both)
									ToolDrawPixel(pixelWidth - 1 - x, pixelHeight - 1 - y);
							}

							_lastDrawnPoint = new PixelPoint(x, y);
						}
					}
					else if(_editor.GetToolMode() == ToolMode.Eraser)
					{
						ToolErasePixel(x, y);
						if(_mirroringMode == MirroringMode.Horizontal || _mirroringMode == MirroringMode.Both)
							ToolErasePixel(pixelWidth - 1 - x, y);
						if(_mirroringMode == MirroringMode.Vertical || _mirroringMode == MirroringMode.Both)
							ToolErasePixel(x, pixelHeight - 1 - y);
						if(_mirroringMode == MirroringMode.Both)
							ToolErasePixel(pixelWidth - 1 - x, pixelHeight - 1 - y);
					}
					else if(_editor.GetToolMode() == ToolMode.Bucket)
					{
						Color32 indexColor = _pixels[_editor.CurrentFrame][index];
						if(!indexColor.Equals(_editor.GetCurrentColor()) || indexColor.a != 255)
						{
							if(!_pixelsDrawnThisStroke.Contains(index))
							{
								_editor.AddNewCommand(new FloodFillCommand(x, y, _editor.GetCurrentColor()));
								_pixelsDrawnThisStroke.Add(index);
							}
						}
					}
					else if(_editor.GetToolMode() == ToolMode.Dropper)
					{
						Color32 indexColor = _pixels[_editor.CurrentFrame][index];
						if(!indexColor.Equals(_editor.GetCurrentColor()))
						{
							if(pixelExists)
								_editor.SetCurrentColor(_canvasTexture.GetPixel(x, y));
						}
					}
				}
				else if(leftMouseUp && _editor.GetToolMode() == ToolMode.Dropper && !_editor.GetIsDragging())
				{
					Color32 indexColor = _pixels[_editor.CurrentFrame][index];
					if(pixelExists)
					{
						_editor.SetCurrentColor(_canvasTexture.GetPixel(x, y));
						_editor.SetToolMode(ToolMode.Brush);
					}
					else
					{
						_editor.SetToolMode(ToolMode.Eraser);
					}
				}
				else if(_editor.GetModifierKey() && rightMouseDown && pixelExists)
				{
					_editor.GetPalette().AddColorToFirstEmptySpot(_canvasTexture.GetPixel(x, y));
				}
				else if(rightMouse)
				{
					_editor.SetToolMode(ToolMode.Dropper);
					
					// check to see if there is any color at this pixel
					// if so, eyedropper it
					if(pixelExists)
						_editor.SetCurrentColor(_canvasTexture.GetPixel(x, y));
				}
				else if(rightMouseUp)
				{
					if(pixelExists)
						_editor.SetToolMode(ToolMode.Brush);
					else
						_editor.SetToolMode(ToolMode.Eraser);
				}

				ZoomCanvasPlane();
				zoomedThisFrame = true;
			}
			// -------------------------------------------------------------------------
			// CANVAS BORDER PLANE
			// -------------------------------------------------------------------------
			else if(ray.transform.gameObject == _canvasBorderPlane)
			{
				if(leftMouseDown && !_editor.GetIsDragging() && !CurrentlyEditingPixels)
				{
					_draggingCanvasBorder = true;
					
					Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
					_borderDragWorldOffset = (Vector2)_canvasBorderPlane.transform.position - mouseWorldPos;
				}
				
				ZoomCanvasPlane();
				zoomedThisFrame = true;
			}
		}

		// -------------------------------------------------------------------------
		// ZOOMING
		// -------------------------------------------------------------------------
		if(!zoomedThisFrame && _zoomTimer > 0.0f)
			ZoomCanvasPlane();

		if(_draggingCanvasBorder)
		{
			if(Input.GetMouseButtonUp(0))
			{
				_draggingCanvasBorder = false;
				PlayerPrefs.SetFloat("canvasPosX", _canvasPlane.transform.position.x);
				PlayerPrefs.SetFloat("canvasPosY", _canvasPlane.transform.position.y);
			}
			else
			{
				Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				_canvasPlane.transform.position = new Vector3(mouseWorldPos.x + _borderDragWorldOffset.x, mouseWorldPos.y + _borderDragWorldOffset.y, PixelEditorScreen.CANVAS_PLANE_DEPTH);
			}
		}

		// -------------------------------------------------------------------------
		// CURRENTLY EDITING STUFF
		// -------------------------------------------------------------------------
		if(Input.GetMouseButtonUp(0))
		{
			if(_isCurrentlyEditingPixels)
			{
				_isCurrentlyEditingPixels = false;

				_editor.AddNewCommand(new EditPixelsCommand(_currentlyReplacedPixels, _currentlyAddedPixels));
				_currentlyReplacedPixels.Clear();
				_currentlyAddedPixels.Clear();
			}
		}

		if(_pixelsDrawnThisStroke.Count > 0 && leftMouseUp)
			_pixelsDrawnThisStroke.Clear();

		// -------------------------------------------------
		// CLEARING FRAME
		// -------------------------------------------------
		if(!_editor.GetModifierKey() && (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Q)) && !_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
		{
			_editor.AddNewCommand(new ClearCurrentFrameCommand());
		}

		// -------------------------------------------------
		// SHIFTING PIXELS
		// -------------------------------------------------
		if(!_editor.GetModifierKey() && !_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
		{
			if(Input.GetKeyDown(KeyCode.UpArrow))
				_editor.AddNewCommand(new ShiftCanvasCommand(Direction.Up));
			else if(Input.GetKeyDown(KeyCode.DownArrow))
				_editor.AddNewCommand(new ShiftCanvasCommand(Direction.Down));
			else if(Input.GetKeyDown(KeyCode.RightArrow))
				_editor.AddNewCommand(new ShiftCanvasCommand(Direction.Right));
			else if(Input.GetKeyDown(KeyCode.LeftArrow))
				_editor.AddNewCommand(new ShiftCanvasCommand(Direction.Left));
		}

		// -------------------------------------------------
		// FLIPPING CANVAS
		// -------------------------------------------------
		if(_editor.GetModifierKey() && !_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
		{
			if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
				_editor.AddNewCommand(new FlipCanvasCommand(false));
			else if(Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
				_editor.AddNewCommand(new FlipCanvasCommand(true));
		}

		// -------------------------------------------------
		// ONION SKIN
		// -------------------------------------------------
		if(!_editor.GetModifierKey() && Input.GetKeyDown(KeyCode.O) && !_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
			ToggleOnionSkinMode();

		if(!_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
		{
			if(Input.GetKeyDown(KeyCode.Minus))
				AdjustOnionSkinOpacity(false);
			else if(Input.GetKeyDown(KeyCode.Equals))
				AdjustOnionSkinOpacity(true);
		}

		// -------------------------------------------------
		// MIRRORING
		// -------------------------------------------------
		if(!_editor.GetModifierKey() && Input.GetKeyDown(KeyCode.M) && !_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
			ToggleMirroringMode();
	}

	void ToolDrawPixel(int x, int y)
	{
		int index = y * pixelWidth + x;
		bool pixelExists = _pixels[_editor.CurrentFrame][index].a > 0;
		if(!pixelExists || !_pixelsDrawnThisStroke.Contains(index))
		{
			if(!_isCurrentlyEditingPixels)
				_isCurrentlyEditingPixels = true;
			
			_currentlyReplacedPixels.Add(new PixelPoint(x, y), GetPixelForCurrentFrame(x, y));
			
			Color32 color = _editor.GetCurrentColor();
			if(color.a == 255)
				SetPixel(x, y, color);
			else
				AddPixel(x, y, color);
			
			_currentlyAddedPixels.Add(new PixelPoint(x, y), GetPixelForCurrentFrame(x, y));
			_pixelsDrawnThisStroke.Add(index);
		}
	}

	public void DrawLine(int x0, int y0, int x1, int y1)
	{
		bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
		if (steep) { Utils.Swap<int>(ref x0, ref y0); Utils.Swap<int>(ref x1, ref y1); }
		if (x0 > x1) { Utils.Swap<int>(ref x0, ref x1); Utils.Swap<int>(ref y0, ref y1); }
		int dX = (x1 - x0), dY = Mathf.Abs(y1 -y0), err = (dX / 2), ystep = (y0 < y1 ? 1 : -1), y = y0;
		
		for (int x = x0; x <= x1; ++x)
		{
			if(steep)
				ToolDrawPixel(y, x);
			else
				ToolDrawPixel(x, y);
			
			err = err - dY;
			if (err < 0) { y += ystep;  err += dX; }
		}
	}

	void ToolErasePixel(int x, int y)
	{
		int index = y * pixelWidth + x;
		bool pixelExists = _pixels[_editor.CurrentFrame][index].a > 0;
		if(pixelExists && !_pixelsDrawnThisStroke.Contains(index))
		{
			if(!_isCurrentlyEditingPixels)
				_isCurrentlyEditingPixels = true;
			
			if(_editor.GetModifierKey())
			{
				// *** create a combineable flood fill so one undo will undo mirrored flood-erases?
				_editor.AddNewCommand(new FloodFillCommand(x, y, new Color32(0, 0, 0, 0)));
			}
			else
			{
				_currentlyReplacedPixels.Add(new PixelPoint(x, y), GetPixelForCurrentFrame(x, y));
				ErasePixel(x, y);
				_currentlyAddedPixels.Add(new PixelPoint(x, y), GetPixelForCurrentFrame(x, y));
			}
		}
	}

	void CreateCanvasPlane()
	{
		_canvasPlane = _editor.CreatePlane("CanvasPlane", null, PixelEditorScreen.CANVAS_PLANE_DEPTH);

		float xPos = PlayerPrefs.GetFloat("canvasPosX", 0.0f);
		float yPos = PlayerPrefs.GetFloat("canvasPosY", 0.0f);
		_canvasPlane.transform.position = new Vector3(xPos, yPos, PixelEditorScreen.CANVAS_PLANE_DEPTH);
		
		// create material
		Material material = (Material)Instantiate(Resources.Load("Materials/UnlitTransparent") as Material);
		_canvasPlane.GetComponent<MeshRenderer>().material = material;
		
		// ---------------------------------------------------------
		// BORDER
		// ---------------------------------------------------------
		_canvasBorderPlane = _editor.CreatePlane("Border", _canvasPlane.transform, BORDER_DEPTH);
		
		// create border texture
		Texture2D borderTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		borderTexture.filterMode = FilterMode.Point;
		borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR);
		borderTexture.Apply();
		// create border material
		Material borderMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		borderMaterial.mainTexture = borderTexture;
		_canvasBorderPlane.GetComponent<MeshRenderer>().material = borderMaterial;

		// ---------------------------------------------------------
		// GRID
		// ---------------------------------------------------------
		_gridPlane = _editor.CreatePlane("Grid", _canvasPlane.transform, GRID_DEPTH);
		// create grid material
		Material gridMaterial = (Material)Instantiate(Resources.Load("Materials/Unlit") as Material);
		_gridPlane.GetComponent<MeshRenderer>().material = gridMaterial;

		// ---------------------------------------------------------
		// ONION SKIN
		// ---------------------------------------------------------
		_onionSkinPlane = _editor.CreatePlane("OnionSkin", _canvasPlane.transform, ONION_SKIN_DEPTH);
		// create onion skin material
		Material onionSkinMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		onionSkinMaterial.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_onionSkinPlane.GetComponent<MeshRenderer>().material = onionSkinMaterial;
		Destroy(_onionSkinPlane.GetComponent<MeshCollider>());

		_onionSkinOpacity = PlayerPrefs.GetFloat("onionSkinOpacity", ONION_SKIN_OPACITY_DEFAULT);

		// ---------------------------------------------------------
		// HITBOX
		// ---------------------------------------------------------
		_hitboxPlane = _editor.CreatePlane("Hitbox", _canvasPlane.transform, HITBOX_DEPTH);
		// create hitbox material
		Material hitboxMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		hitboxMaterial.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_hitboxPlane.GetComponent<MeshRenderer>().material = hitboxMaterial;
		Destroy(_hitboxPlane.GetComponent<MeshCollider>());
	}

	void InitPixels()
	{
		_pixels.Clear();
		
		_pixels.Add(new Color32[pixelWidth * pixelHeight]);
		
		for(int y = 0; y < pixelHeight; y++)
			for(int x = 0; x < pixelWidth; x++)
				ErasePixel(x, y);
	}

	void CreateCanvasTexture()
	{
		float aspect = (float)pixelWidth / (float)pixelHeight;
		float scale = PlayerPrefs.GetFloat("canvasZoom", CANVAS_DEFAULT_SCALE);
		_canvasPlane.transform.localScale = new Vector3(scale, scale / aspect, 1.0f);
		
		if(_canvasTexture)
			_canvasTexture = null;
		
		// create texture
		_canvasTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		_canvasTexture.filterMode = FilterMode.Point;
		_canvasPlane.renderer.material.mainTexture = _canvasTexture;
		
		_canvasTexture.SetPixels32(_pixels[_editor.CurrentFrame]);
		_canvasTexture.Apply();
		
		// -------------------------------------------------------------------------
		// BORDER
		// -------------------------------------------------------------------------
		float xScaleIncrease = 0.10f;
		float yScaleIncrease = xScaleIncrease * aspect;
		_canvasBorderPlane.transform.localScale = new Vector3(1.0f + xScaleIncrease, 1.0f + yScaleIncrease, 1.0f);

		// -------------------------------------------------------------------------
		// GRID
		// -------------------------------------------------------------------------
		Texture2D gridTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		gridTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < pixelHeight; y++)
		{
			for(int x = 0; x < pixelWidth; x++)
			{
				bool yOdd = y % 2 != 0;
				bool xOdd = x % 2 != 0;
				
				byte grey = (yOdd && xOdd || !yOdd && !xOdd) ? PixelEditorScreen.CANVAS_CHECKERS_EVEN_SHADE : PixelEditorScreen.CANVAS_CHECKERS_ODD_SHADE;
				
				gridTexture.SetPixel(x, y, new Color32(grey, grey, grey, 255));
			}
		}
		
		gridTexture.Apply();
		_gridPlane.renderer.material.mainTexture = gridTexture;

		// -------------------------------------------------------------------------
		// ONION SKIN
		// -------------------------------------------------------------------------
		_onionSkinTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		_onionSkinTexture.filterMode = FilterMode.Point;

		for(int y = 0; y < pixelHeight; y++)
			for(int x = 0; x < pixelWidth; x++)
				_onionSkinTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
		
		_onionSkinTexture.Apply();
		_onionSkinPlane.renderer.material.mainTexture = _onionSkinTexture;

		// -------------------------------------------------------------------------
		// HITBOX
		// -------------------------------------------------------------------------
		_hitboxTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		_hitboxTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < pixelHeight; y++)
			for(int x = 0; x < pixelWidth; x++)
				_hitboxTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
		
		_hitboxTexture.Apply();
		_hitboxPlane.renderer.material.mainTexture = _hitboxTexture;
	}

	void ResizeCanvas()
	{
		float aspect = (float)pixelWidth / (float)pixelHeight;
		_canvasPlane.transform.localScale = new Vector3(aspect * CANVAS_DEFAULT_SCALE, CANVAS_DEFAULT_SCALE, 1);
		
		PlayerPrefs.SetInt("pixelWidth", pixelWidth);
		PlayerPrefs.SetInt("pixelHeight", pixelHeight);
		
		if(_canvasTexture)
			_canvasTexture = null;

		// create texture
		_canvasTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		_canvasTexture.filterMode = FilterMode.Point;
		_canvasPlane.renderer.material.mainTexture = _canvasTexture;
		
		_canvasTexture.SetPixels32(_pixels[_editor.CurrentFrame]);
		_canvasTexture.Apply();
		
		// -------------------------------------------------------------------------
		// BORDER
		// -------------------------------------------------------------------------
		float xScaleIncrease = 0.10f;
		float yScaleIncrease = xScaleIncrease * aspect;
		_canvasBorderPlane.transform.localScale = new Vector3(1.0f + xScaleIncrease, 1.0f + yScaleIncrease, 1.0f);

		// -------------------------------------------------------------------------
		// GRID
		// -------------------------------------------------------------------------
		Texture2D gridTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		gridTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < pixelHeight; y++)
		{
			for(int x = 0; x < pixelWidth; x++)
			{
				bool yOdd = y % 2 != 0;
				bool xOdd = x % 2 != 0;
				
				byte grey = (yOdd && xOdd || !yOdd && !xOdd) ? PixelEditorScreen.CANVAS_CHECKERS_EVEN_SHADE : PixelEditorScreen.CANVAS_CHECKERS_ODD_SHADE;
				
				gridTexture.SetPixel(x, y, new Color32(grey, grey, grey, 255));
			}
		}
		
		gridTexture.Apply();
		_gridPlane.renderer.material.mainTexture = gridTexture;

		// -------------------------------------------------------------------------
		// ONION SKIN
		// -------------------------------------------------------------------------
		_onionSkinTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		_onionSkinTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < pixelHeight; y++)
			for(int x = 0; x < pixelWidth; x++)
				_onionSkinTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
		
		_onionSkinTexture.Apply();
		_onionSkinPlane.renderer.material.mainTexture = _onionSkinTexture;

		// -------------------------------------------------------------------------
		// HITBOX
		// -------------------------------------------------------------------------
		_hitboxTexture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.ARGB32, false);
		_hitboxTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < pixelHeight; y++)
			for(int x = 0; x < pixelWidth; x++)
				_hitboxTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
		
		_hitboxTexture.Apply();
		_hitboxPlane.renderer.material.mainTexture = _hitboxTexture;
	}

	public void NewCanvas()
	{
		_pixelsDrawnThisStroke.Clear();
		InitPixels();
		ResizeCanvas();
	}

	public void AddNewBlankFrame()
	{
		_pixels.Insert(_editor.CurrentFrame, new Color32[pixelWidth * pixelHeight]);
		
		for(int y = 0; y < pixelHeight; y++)
			for(int x = 0; x < pixelWidth; x++)
				ErasePixel(x, y);
		
		_dirtyPixels = true;
	}

	public void DuplicateCurrentFrame()
	{
		Color32[] newPixels = new Color32[pixelWidth * pixelHeight];
		_pixels[_editor.CurrentFrame].CopyTo(newPixels, 0);

		_pixels.Insert(_editor.CurrentFrame, newPixels);

		_dirtyPixels = true;
	}

	// CALL FROM FRAME PREVIEW METHOD
	public void AddNewFrame(int frameNumber, Color32[] pixels)
	{
		_pixels.Insert(frameNumber, pixels);
		_dirtyPixels = true;
	}

	public void DeleteFrame(int frameNumber)
	{
		_pixels.RemoveAt(frameNumber);
		_dirtyPixels = true;
	}

	void ZoomCanvasPlane()
	{
		if(Input.GetAxis("Mouse ScrollWheel") < 0)
		{
			_canvasPlane.transform.localScale *= 1.0f - PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("canvasZoom", _canvasPlane.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(Input.GetAxis("Mouse ScrollWheel") > 0)
		{
			_canvasPlane.transform.localScale *= 1.0f + PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("canvasZoom", _canvasPlane.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(_zoomTimer > 0.0f)
		{
			_zoomTimer -= Time.deltaTime;
		}
	}

	// ONLY CALL FROM A COMMAND
	public void ShiftPixels(Direction direction)
	{
		if(direction != Direction.None)
		{
			Color32[] tempPixels = new Color32[pixelWidth * pixelHeight];

			_pixels[_editor.CurrentFrame].CopyTo(tempPixels, 0);

			for(int y = pixelHeight - 1; y >= 0; y--)
			{
				for(int x = 0; x < pixelWidth; x++)
				{
					int currentIndex = y * pixelWidth + x;
					int copyIndex = 0;

					if(direction == Direction.Up)
					{
						if(y > 0)
							copyIndex = (y - 1) * pixelWidth + x;
						else
							copyIndex = (pixelHeight - 1) * pixelWidth + x;
					}
					else if(direction == Direction.Down)
					{
						if(y < pixelHeight - 1)
							copyIndex = (y + 1) * pixelWidth + x;
						else
							copyIndex = (0) * pixelWidth + x;
					}
					else if(direction == Direction.Right)
					{
						if(x > 0)
							copyIndex = y * pixelWidth + (x - 1);
						else
							copyIndex = y * pixelWidth + (pixelWidth - 1);
					}
					else if(direction == Direction.Left)
					{
						if(x < pixelWidth - 1)
							copyIndex = y * pixelWidth + (x + 1);
						else
							copyIndex = y * pixelWidth + (0);
					}

					_pixels[_editor.CurrentFrame][currentIndex] = tempPixels[copyIndex];

					if(_pixels[_editor.CurrentFrame][currentIndex].a == 0)
						ErasePixel(x, y);
				}
			}
			
			_dirtyPixels = true;
		}
	}

	public void AddColor(Color color)
	{
		for(int i = 0; i < _pixels[_editor.CurrentFrame].Length; i++)
			_pixels[_editor.CurrentFrame][i] = (Color32)((Color)_pixels[_editor.CurrentFrame][i] + color);
		
		_dirtyPixels = true;
	}
	
	public void SubtractColor(Color color)
	{
		for(int i = 0; i < _pixels[_editor.CurrentFrame].Length; i++)
			_pixels[_editor.CurrentFrame][i] = (Color32)((Color)_pixels[_editor.CurrentFrame][i] - color);
		
		_dirtyPixels = true;
	}
	
	public void SetPixel(int x, int y, Color color)
	{
		if(x < 0 || x > pixelWidth - 1 ||
		   y < 0 || y > pixelHeight - 1)
			return;
		
		int index = y * pixelWidth + x;
		_pixels[_editor.CurrentFrame][index] = (Color32)color;

		_dirtyPixels = true;
	}
	
	public void ErasePixel(int x, int y)
	{
		if(x < 0 || x > pixelWidth - 1 ||
		   y < 0 || y > pixelHeight - 1)
			return;
		
		int index = y * pixelWidth + x;
		_pixels[_editor.CurrentFrame][index] = new Color32(0, 0, 0, 0);
		
		_dirtyPixels = true;
	}

	public void AddPixel(int x, int y, Color newColor)
	{
		if(x < 0 || x > pixelWidth - 1 ||
		   y < 0 || y > pixelHeight - 1)
			return;
		
		int index = y * pixelWidth + x;

		Color originalColor = (Color)_pixels[_editor.CurrentFrame][index];
		Color resultingColor = Utils.AddColors(originalColor, newColor);

		_pixels[_editor.CurrentFrame][index] = (Color32)resultingColor;

		_dirtyPixels = true;
	}

	public void AddPixels(Color32[] pixels)
	{
		for(int i = 0; i < pixels.Length; i++)
		{
			Color32 color = pixels[i];
			if(color.a > 0)
			{
				if(color.a == 255)
				{
					_pixels[_editor.CurrentFrame][i] = color;
				}
				else
				{
					Color originalColor = (Color)_pixels[_editor.CurrentFrame][i];
					Color resultingColor = Utils.AddColors(originalColor, color);
					_pixels[_editor.CurrentFrame][i] = (Color32)resultingColor;
				}
			}
		}

		_dirtyPixels = true;
	}

	public void FloodFill(int x, int y, Color32 targetColor, Color32 newColor, List<PixelPoint> changedPixels)
	{
		int index = y * pixelWidth + x;
		Color32 existingColor = _pixels[_editor.CurrentFrame][index];

		// If the color of node is not equal to target-color, return.
		if(!existingColor.Equals(targetColor))
		   return;

		if(newColor.a == 255)
		{
			_pixels[_editor.CurrentFrame][index] = newColor;
		}
		else if(newColor.a == 0) // flood erase
		{
			_pixels[_editor.CurrentFrame][index] = new Color32(0, 0, 0, 0);
		}
		else
		{
			Color resultingColor = Utils.AddColors((Color)existingColor, (Color)newColor);
			_pixels[_editor.CurrentFrame][index] = (Color32)resultingColor;

			// if resulting color equals the target, return (to prevent infinite loop)
			if(((Color32)resultingColor).Equals(targetColor))
				return;
		}

		changedPixels.Add(new PixelPoint(x, y));

		if(x > 0)
			FloodFill(x - 1, y, targetColor, newColor, changedPixels);
		if(x < pixelWidth - 1)
			FloodFill(x + 1, y, targetColor, newColor, changedPixels);
		if(y > 0)
			FloodFill(x, y - 1, targetColor, newColor, changedPixels);
		if(y < pixelHeight - 1)
			FloodFill(x, y + 1, targetColor, newColor, changedPixels);
	}

	public void SubtractPixel(int x, int y, Color color)
	{
		if(x < 0 || x > pixelWidth - 1 ||
		   y < 0 || y > pixelHeight - 1)
			return;
		
		int index = y * pixelWidth + x;
		_pixels[_editor.CurrentFrame][index] = (Color32)((Color)_pixels[_editor.CurrentFrame][index] - color);
		_dirtyPixels = true;
	}

	public void ToggleOnionSkinMode()
	{
		if(_onionSkinMode == OnionSkinMode.None)
			SetOnionSkinMode(OnionSkinMode.Previous);
		else if(_onionSkinMode == OnionSkinMode.Previous)
			SetOnionSkinMode(OnionSkinMode.Next);
		else if(_onionSkinMode == OnionSkinMode.Next)
			SetOnionSkinMode(OnionSkinMode.None);

		RefreshOnionSkin();
	}

	public void SetOnionSkinMode(OnionSkinMode onionSkinMode)
	{
		_onionSkinMode = onionSkinMode;
		PlayerPrefs.SetInt("onionSkinMode", (int)_onionSkinMode);

		if(_onionSkinMode == OnionSkinMode.None)
			_editor.OnionSkinString = "";
		else if(_onionSkinMode == OnionSkinMode.Previous)
			_editor.OnionSkinString = "<--ONION";
		else if(_onionSkinMode == OnionSkinMode.Next)
			_editor.OnionSkinString = "ONION-->";
	}

	public void ToggleMirroringMode()
	{
		if(_mirroringMode == MirroringMode.None)
			SetMirroringMode(MirroringMode.Horizontal);
		else if(_mirroringMode == MirroringMode.Horizontal)
			SetMirroringMode(MirroringMode.Vertical);
		else if(_mirroringMode == MirroringMode.Vertical)
			SetMirroringMode(MirroringMode.Both);
		else if(_mirroringMode == MirroringMode.Both)
			SetMirroringMode(MirroringMode.None);
	}

	public void SetMirroringMode(MirroringMode mirroringMode)
	{
		_mirroringMode = mirroringMode;
		PlayerPrefs.SetInt("mirroringMode", (int)_mirroringMode);
		
		if(_mirroringMode == MirroringMode.None)
			_editor.MirroringString = "";
		else if(_mirroringMode == MirroringMode.Horizontal)
			_editor.MirroringString = "-MIRROR HORZ-";
		else if(_mirroringMode == MirroringMode.Vertical)
			_editor.MirroringString = "|MIRROR VERT|";
		else if(_mirroringMode == MirroringMode.Both)
			_editor.MirroringString = "/MIRROR BOTH/";
	}

	public void RefreshOnionSkin()
	{
		if(_onionSkinMode == OnionSkinMode.None)
		{
			_onionSkinPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		}
		else if(_editor.GetFramePreview().GetNumFrames() > 1)
		{
			_onionSkinPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, _onionSkinOpacity);
			int onionFrameNum = -1;

			if(_onionSkinMode == OnionSkinMode.Previous)
			{
				if(_editor.CurrentFrame > 0)
					onionFrameNum = _editor.CurrentFrame - 1;
				else
					onionFrameNum = _editor.GetFramePreview().GetNumFrames() - 1;
			}
			else if(_onionSkinMode == OnionSkinMode.Next)
			{
				if(_editor.CurrentFrame < _editor.GetFramePreview().GetNumFrames() - 1)
					onionFrameNum = _editor.CurrentFrame + 1;
				else
					onionFrameNum = 0;
			}

			if(onionFrameNum != -1)
			{
				_onionSkinTexture.SetPixels32(_pixels[onionFrameNum]);
				_onionSkinTexture.Apply();
			}
		}
	}

	void AdjustOnionSkinOpacity(bool increase)
	{
		if(increase)
			_onionSkinOpacity = Mathf.Clamp(_onionSkinOpacity + ONION_SKIN_OPACITY_INCREMENT, ONION_SKIN_OPACITY_MIN, ONION_SKIN_OPACITY_MAX);
		else
			_onionSkinOpacity = Mathf.Clamp(_onionSkinOpacity - ONION_SKIN_OPACITY_INCREMENT, ONION_SKIN_OPACITY_MIN, ONION_SKIN_OPACITY_MAX);

		PlayerPrefs.SetFloat("onionSkinOpacity", _onionSkinOpacity);
		RefreshOnionSkin();
	}

	public void RefreshHitbox()
	{
		if(_editor.ShowingHitbox)
		{
			_hitboxPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, HITBOX_OPACITY);

			PixelRect hitbox = _editor.Hitbox;
			for(int x = 0; x < pixelWidth; x++)
			{
				for(int y = 0; y < pixelHeight; y++)
				{
					if(hitbox.Contains(x, y))
						_hitboxTexture.SetPixel(x, y, new Color32(255, 0, 0, 255));
					else
						_hitboxTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
				}
			}

			_hitboxTexture.Apply();
		}
		else
		{
			_hitboxPlane.renderer.material.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		}
	}
}




















