using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Palette : MonoBehaviour
{
	PixelEditorScreen _editor;

	GameObject _palettePlane;
	Texture2D _paletteTexture;
	const int PALETTE_NUM_X = 3;
	const int PALETTE_NUM_Y = 9;
	Color32[] _paletteColors;
	public Color32 GetColor(int index) { return _paletteColors[index]; }
	public bool GetColorExists(int index) { return _paletteColors[index].a > 0; }

	public int GetNumPaletteColors() { return _paletteColors.Length; }
	public void SetPaletteColor(Color32 color, int index) { _paletteColors[index] = color; }

	GameObject _paletteBorderPlane;
	bool _draggingPaletteBorder = false;
	public bool DraggingPaletteBorder { get { return _draggingPaletteBorder; } }
	Vector2 _borderDragWorldOffset;

	GameObject _paletteGridPlane;
	float _zoomTimer = 0.0f;

	bool _draggingCurrentPaletteColor = false;

	int _paletteColorEditIndex;
	public int PaletteColorEditIndex { get { return _paletteColorEditIndex; } }

	const float GRID_DEPTH = 0.1f;
	const float BORDER_DEPTH = 0.2f;

	public void Init(PixelEditorScreen editor)
	{
		_editor = editor;

		CreatePalette();
	}

	public void UpdatePalette()
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

		RaycastHit ray = new RaycastHit();
		if(Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out ray))
		{
			Texture2D textureMap = (Texture2D)ray.transform.renderer.material.mainTexture;
			Vector2 pixelUV = ray.textureCoord;
			pixelUV.x *= textureMap.width;
			pixelUV.y *= textureMap.height;
			
			int x = (int)pixelUV.x;
			int y = (int)pixelUV.y;
			
			// -------------------------------------------------------------------------
			// PALETTE PLANE
			// -------------------------------------------------------------------------
			if(ray.transform.gameObject == _palettePlane)
			{
				if(!_editor.GetIsDragging())
				{
					int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;
					
					if(GetColorExists(index))
					{
						if(leftMouse)
						{
							Color32 color = _paletteColors[index];
							if(!color.Equals(_editor.GetCurrentColor()))
							{
								if(_editor.GetToolMode() == ToolMode.Eraser)
									_editor.SetToolMode(ToolMode.Brush);

								_editor.SetCurrentColor(_paletteColors[index]);
							}
						}
						else if(rightMouse && _editor.GetModifierKey() && !_editor.GetIsEditingPixels())
						{
							_editor.AddNewCommand(new DeletePaletteColorCommand(index));
							_editor.SetTextEnteringMode(TextEnteringMode.None);
						}
						else if(rightMouse && !_editor.GetModifierKey())
						{
							_editor.SetTextEnteringMode(TextEnteringMode.PaletteColor);
							
							Color32 color = _paletteColors[index];
							_editor.PaletteColorString = color.r.ToString() + "," + color.g.ToString() + "," + color.b.ToString() + ((color.a < 255) ? ("," + color.a.ToString()) : "");
							_paletteColorEditIndex = index;

							_editor.PaletteInputScreenPosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); // gotta flip the y-position;
						}
						
						// -----------------------------------------------------------
						// COLOR SWAPPING
						// -----------------------------------------------------------
						if(leftMouseDown)
						{
							_paletteColorEditIndex = index;
							_draggingCurrentPaletteColor = true;
						}
						else if(leftMouseUp && _draggingCurrentPaletteColor && index != _paletteColorEditIndex && _editor.GetModifierKey() && !_editor.GetIsEditingPixels())
						{
							if(_editor.GetModifierKey())
								_editor.AddNewCommand(new SetPaletteColorCommand(_paletteColors[_paletteColorEditIndex], index)); // duplicate color
							else
								_editor.AddNewCommand(new SwapPaletteColorsCommand(index, _paletteColorEditIndex));

							_draggingCurrentPaletteColor = false;
						}
					}
					else
					{
						if(leftMouseDown)
						{
							_draggingPaletteBorder = true;
							
							Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
							_borderDragWorldOffset = (Vector2)_paletteBorderPlane.transform.position - mouseWorldPos;
						}
						else if(rightMouse)
						{
							_editor.SetTextEnteringMode(TextEnteringMode.PaletteColor);
							
							_editor.PaletteColorString = "...";
							
							_editor.PaletteInputScreenPosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); // gotta flip the y-position;
							_paletteColorEditIndex = index;
						}
						else if(leftMouseUp && _draggingCurrentPaletteColor && index != _paletteColorEditIndex && _editor.GetModifierKey() && !_editor.GetIsEditingPixels())
						{
							_editor.AddNewCommand(new SwapPaletteColorsCommand(index, _paletteColorEditIndex));

							_draggingCurrentPaletteColor = false;
						}
					}
				}
				
				ZoomPalettePlane();
				zoomedThisFrame = true;
			}
			// -------------------------------------------------------------------------
			// PALETTE BORDER PLANE
			// -------------------------------------------------------------------------
			else if(ray.transform.gameObject == _paletteBorderPlane)
			{
				if(leftMouseDown && !_editor.GetIsDragging() && !_editor.GetIsEditingPixels())
				{
					_draggingPaletteBorder = true;
					
					Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
					_borderDragWorldOffset = (Vector2)_paletteBorderPlane.transform.position - mouseWorldPos;
				}
				
				ZoomPalettePlane();
				zoomedThisFrame = true;
			}
		}

		// -------------------------------------------------------------------------
		// ZOOMING
		// -------------------------------------------------------------------------
		if(!zoomedThisFrame && _zoomTimer > 0.0f)
			ZoomPalettePlane();

		// -------------------------------------------------
		// WRAP UP SOME PALETTE MOUSE SHIT
		// -------------------------------------------------
		if(leftMouseUp && _draggingCurrentPaletteColor)
			_draggingCurrentPaletteColor = false;
		
		if(leftMouse && _editor.GetTextEnteringMode() == TextEnteringMode.PaletteColor)
			_editor.SetTextEnteringMode(TextEnteringMode.None);

		if(_draggingPaletteBorder)
		{
			if(Input.GetMouseButtonUp(0))
			{
				_draggingPaletteBorder = false;
				PlayerPrefs.SetFloat("palettePosX", _palettePlane.transform.position.x);
				PlayerPrefs.SetFloat("palettePosY", _palettePlane.transform.position.y);
			}
			else
			{
				Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				_palettePlane.transform.position = new Vector3(mouseWorldPos.x + _borderDragWorldOffset.x, mouseWorldPos.y + _borderDragWorldOffset.y, PixelEditorScreen.PALETTE_PLANE_DEPTH);
			}
		}
	}

	void ZoomPalettePlane()
	{
		if(Input.GetAxis("Mouse ScrollWheel") < 0)
		{
			_palettePlane.transform.localScale *= 1.0f - PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("paletteZoom", _palettePlane.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(Input.GetAxis("Mouse ScrollWheel") > 0)
		{
			_palettePlane.transform.localScale *= 1.0f + PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("paletteZoom", _palettePlane.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(_zoomTimer > 0.0f)
		{
			_zoomTimer -= Time.deltaTime;
		}
	}

	void CreatePalette()
	{
		_palettePlane = _editor.CreatePlane("PalettePlane", null, PixelEditorScreen.PALETTE_PLANE_DEPTH);
		
		float aspect = (float)PALETTE_NUM_X / (float)PALETTE_NUM_Y;
		
		float xPos = PlayerPrefs.GetFloat("palettePosX", -1.1f);
		float yPos = PlayerPrefs.GetFloat("palettePosY", -0.5f);
		_palettePlane.transform.position = new Vector3(xPos, yPos, PixelEditorScreen.PALETTE_PLANE_DEPTH);
		float scale = PlayerPrefs.GetFloat("paletteZoom", 0.16f);
		_palettePlane.transform.localScale = new Vector3(scale, scale / aspect, 1.0f);
		
		_paletteTexture = new Texture2D(PALETTE_NUM_X, PALETTE_NUM_Y, TextureFormat.ARGB32, false);
		_paletteTexture.filterMode = FilterMode.Point;
		
		_paletteColors = new Color32[PALETTE_NUM_X * PALETTE_NUM_Y];

		for(int y = 0; y < PALETTE_NUM_Y; y++)
		{
			for(int x = 0; x < PALETTE_NUM_X; x++)
			{
				int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;
				
				string colorString = PlayerPrefs.GetString("PaletteColor" + index.ToString(), "");
				
				if(colorString != "")
				{
					string[] vals = colorString.Split(',');
					if(vals.Length == 3 || vals.Length == 4)
					{
						Color32 color;
						if(vals.Length == 3)
							color = new Color32(byte.Parse(vals[0]), byte.Parse(vals[1]), byte.Parse(vals[2]), 255);
						else
							color = new Color32(byte.Parse(vals[0]), byte.Parse(vals[1]), byte.Parse(vals[2]), byte.Parse(vals[3]));

						_paletteColors[index] = color;
					}
				}
				else
				{
					_paletteColors[index] = new Color32(0, 0, 0, 0);
				}
			}
		}
		
		RefreshPaletteTextureColors();
		
		// Create material
		Material paletteMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitTransparent") as Material);
		paletteMaterial.mainTexture = _paletteTexture;
		_palettePlane.GetComponent<MeshRenderer>().material = paletteMaterial;
		
		// ---------------------------------------------------------
		// BORDER
		// ---------------------------------------------------------
		_paletteBorderPlane = _editor.CreatePlane("Border", _palettePlane.transform, BORDER_DEPTH);
		
		// create border texture
		Texture2D borderTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		borderTexture.filterMode = FilterMode.Point;
		borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR);
		borderTexture.Apply();
		
		// create border material
		Material borderMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		borderMaterial.mainTexture = borderTexture;
		_paletteBorderPlane.GetComponent<MeshRenderer>().material = borderMaterial;
		
		float xScaleIncrease = 0.15f;
		float yScaleIncrease = xScaleIncrease * aspect;
		_paletteBorderPlane.transform.localScale = new Vector3(1.0f + xScaleIncrease, 1.0f + yScaleIncrease, 1.0f);

		// ---------------------------------------------------------
		// GRID
		// ---------------------------------------------------------
		_paletteGridPlane = _editor.CreatePlane("Grid", _palettePlane.transform, GRID_DEPTH);
		_paletteGridPlane.transform.localScale = new Vector3(1, 1, 1);
		
		// create border texture
		Texture2D gridTexture = new Texture2D(PALETTE_NUM_X, PALETTE_NUM_Y, TextureFormat.ARGB32, false);
		gridTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < PALETTE_NUM_Y; y++)
		{
			for(int x = 0; x < PALETTE_NUM_X; x++)
			{
				bool yOdd = y % 2 != 0;
				bool xOdd = x % 2 != 0;
				
				byte grey = (yOdd && xOdd || !yOdd && !xOdd) ? PixelEditorScreen.CANVAS_CHECKERS_EVEN_SHADE : PixelEditorScreen.CANVAS_CHECKERS_ODD_SHADE;
				
				gridTexture.SetPixel(x, y, new Color32(grey, grey, grey, 255));
			}
		}
		
		gridTexture.Apply();
		
		// create border material
		Material gridMaterial = (Material)Instantiate(Resources.Load("Materials/Unlit") as Material);
		gridMaterial.mainTexture = gridTexture;
		_paletteGridPlane.GetComponent<MeshRenderer>().material = gridMaterial;
	}

	public void RefreshPaletteTextureColors()
	{
		for(int y = 0; y < PALETTE_NUM_Y; y++)
		{
			for(int x = 0; x < PALETTE_NUM_X; x++)
			{
				int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;
				_paletteTexture.SetPixel(x, y, (Color)_paletteColors[index]);
			}
		}
		
		_paletteTexture.Apply();
	}
	
	public void DeletePaletteColor(int x, int y)
	{
		int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;
		
		if(index > _paletteColors.Length - 1)
			return;

		_paletteColors[index] = new Color32(0, 0, 0, 0);
	}
	
	public void DeletePaletteColor(int index)
	{
		if(index > _paletteColors.Length - 1)
			return;
		
		PixelPoint coords = GetPaletteCoordsForIndex(_paletteColorEditIndex);

		_paletteColors[index] = new Color32(0, 0, 0, 0);
	}
	
	PixelPoint GetPaletteCoordsForIndex(int index)
	{
		int yCoord = Mathf.FloorToInt(index / PALETTE_NUM_X) - 1;
		int xCoord = index % PALETTE_NUM_X;
		
		return new PixelPoint(xCoord, PALETTE_NUM_Y - yCoord);
	}

	public void AddColorToFirstEmptySpot(Color32 color)
	{
		for(int y = PALETTE_NUM_Y - 1; y >= 0; y--)
		{
			for(int x = 0; x < PALETTE_NUM_X; x++)
			{
				int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;

				if(_paletteColors[index].a == 0)
				{
					_editor.AddNewCommand(new SetPaletteColorCommand(color, index));
					return;
				}
			}
		}
	}

	public void ClearAllColors()
	{
		for(int y = PALETTE_NUM_Y - 1; y >= 0; y--)
		{
			for(int x = 0; x < PALETTE_NUM_X; x++)
			{
				int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;
				
				if(_paletteColors[index].a != 0)
				{
					_editor.AddNewCommand(new DeletePaletteColorCommand(index));
				}
			}
		}

		RefreshPaletteTextureColors();
	}

	public void OnQuit()
	{
		for(int y = 0; y < PALETTE_NUM_Y; y++)
		{
			for(int x = 0; x < PALETTE_NUM_X; x++)
			{
				int index = (PALETTE_NUM_Y - y - 1) * PALETTE_NUM_X + x;

				Color32 color = _paletteColors[index];
				string colorString = color.r.ToString() + "," + color.g.ToString() + "," + color.b.ToString() + "," + color.a.ToString();
				PlayerPrefs.SetString("PaletteColor" + index.ToString(), colorString);
			}
		}
	}
}


























