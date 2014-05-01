using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PatternViewer : MonoBehaviour
{
	PixelEditorScreen _editor;

	GameObject _patternPlane;
	Texture2D _patternTexture;

	// BORDER .............................................
	GameObject _patternBorderPlane;
	bool _draggingPatternBorder = false;
	public bool DraggingPatternBorder { get { return _draggingPatternBorder; } }
	
	Vector2 _borderDragWorldOffset;

	// GRID .............................................
	GameObject _gridPlane;
	Texture2D _gridTexture;

	const float GRID_DEPTH = 0.1f;
	const float BORDER_DEPTH = 0.2f;

	const float PATTERN_DEFAULT_SCALE = 0.5f;
	float _zoomTimer = 0.0f;

	bool _activated;

	public void Init(PixelEditorScreen editor)
	{
		_editor = editor;

		CreatePatternPlane();
		CreatePatternTexture();
		RefreshPatternViewer();

		_activated = true;
		int activated = PlayerPrefs.GetInt("patternActivated", 0);
		if(activated == 0)
			TogglePatternViewer();
	}

	public void UpdatePatternViewer()
	{
		// -------------------------------------------------
		// TOGGLE PATTERN VIEWER
		// -------------------------------------------------
		if(!_editor.GetModifierKey() && Input.GetKeyDown(KeyCode.P) && !_editor.GetIsEditingPixels() && _editor.GetTextEnteringMode() == TextEnteringMode.None)
			TogglePatternViewer();

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
			
			// -------------------------------------------------------------------------
			// PLANES
			// -------------------------------------------------------------------------
			if(ray.transform.gameObject == _patternPlane || ray.transform.gameObject == _patternBorderPlane)
			{
				if(leftMouseDown && !_editor.GetIsDragging() && !_editor.GetIsEditingPixels())
				{
					_draggingPatternBorder = true;
					
					Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
					_borderDragWorldOffset = (Vector2)_patternBorderPlane.transform.position - mouseWorldPos;
				}
				
				ZoomPatternPlane();
				zoomedThisFrame = true;
			}
		}
		
		// -------------------------------------------------------------------------
		// ZOOMING
		// -------------------------------------------------------------------------
		if(!zoomedThisFrame && _zoomTimer > 0.0f)
			ZoomPatternPlane();
		
		if(_draggingPatternBorder)
		{
			if(Input.GetMouseButtonUp(0))
			{
				_draggingPatternBorder = false;
				PlayerPrefs.SetFloat("patternPosX", _patternPlane.transform.position.x);
				PlayerPrefs.SetFloat("patternPosY", _patternPlane.transform.position.y);
			}
			else
			{
				Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				_patternPlane.transform.position = new Vector3(mouseWorldPos.x + _borderDragWorldOffset.x, mouseWorldPos.y + _borderDragWorldOffset.y, PixelEditorScreen.PATTERN_PLANE_DEPTH);
			}
		}
	}

	void ZoomPatternPlane()
	{
		if(Input.GetAxis("Mouse ScrollWheel") < 0)
		{
			_patternPlane.transform.localScale *= 1.0f - PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("patternZoom", _patternPlane.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(Input.GetAxis("Mouse ScrollWheel") > 0)
		{
			_patternPlane.transform.localScale *= 1.0f + PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("patternZoom", _patternPlane.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(_zoomTimer > 0.0f)
		{
			_zoomTimer -= Time.deltaTime;
		}
	}

	void CreatePatternPlane()
	{
		_patternPlane = _editor.CreatePlane("PatternPlane", null, PixelEditorScreen.PATTERN_PLANE_DEPTH);
		
		float xPos = PlayerPrefs.GetFloat("patternPosX", 1.25f);
		float yPos = PlayerPrefs.GetFloat("patternPosY", 0.5f);
		_patternPlane.transform.position = new Vector3(xPos, yPos, PixelEditorScreen.PATTERN_PLANE_DEPTH);
		
		// create material
		Material material = (Material)Instantiate(Resources.Load("Materials/UnlitTransparent") as Material);
		_patternPlane.GetComponent<MeshRenderer>().material = material;

		// ---------------------------------------------------------
		// BORDER
		// ---------------------------------------------------------
		_patternBorderPlane = _editor.CreatePlane("Border", _patternPlane.transform, BORDER_DEPTH);
		
		// create border texture
		Texture2D borderTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		borderTexture.filterMode = FilterMode.Point;
		borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR);
		borderTexture.Apply();
		// create border material
		Material borderMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		borderMaterial.mainTexture = borderTexture;
		_patternBorderPlane.GetComponent<MeshRenderer>().material = borderMaterial;

		// ---------------------------------------------------------
		// GRID
		// ---------------------------------------------------------
		_gridPlane = _editor.CreatePlane("Grid", _patternPlane.transform, GRID_DEPTH);
		// create grid material
		Material gridMaterial = (Material)Instantiate(Resources.Load("Materials/Unlit") as Material);
		_gridPlane.GetComponent<MeshRenderer>().material = gridMaterial;
	}

	void CreatePatternTexture()
	{
		int patternPixelWidth = _editor.GetCanvas().pixelWidth * 3;
		int patternPixelHeight = _editor.GetCanvas().pixelHeight * 3;

		float aspect = (float)patternPixelWidth / (float)patternPixelHeight;
		float scale = PlayerPrefs.GetFloat("patternZoom", PATTERN_DEFAULT_SCALE);
		_patternPlane.transform.localScale = new Vector3(scale, scale / aspect, 1.0f);
		
		if(_patternTexture)
			_patternTexture = null;
		
		// create texture
		_patternTexture = new Texture2D(patternPixelWidth, patternPixelHeight, TextureFormat.ARGB32, false);
		_patternTexture.filterMode = FilterMode.Point;
		_patternPlane.renderer.material.mainTexture = _patternTexture;
		
		// -------------------------------------------------------------------------
		// BORDER
		// -------------------------------------------------------------------------
		float xScaleIncrease = 0.10f;
		float yScaleIncrease = xScaleIncrease * aspect;
		_patternBorderPlane.transform.localScale = new Vector3(1.0f + xScaleIncrease, 1.0f + yScaleIncrease, 1.0f);
		
		// -------------------------------------------------------------------------
		// GRID
		// -------------------------------------------------------------------------
		Texture2D gridTexture = new Texture2D(patternPixelWidth, patternPixelHeight, TextureFormat.ARGB32, false);
		gridTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < patternPixelHeight; y++)
		{
			for(int x = 0; x < patternPixelWidth; x++)
			{
				bool yOdd = y % 2 != 0;
				bool xOdd = x % 2 != 0;
				
				byte grey = (yOdd && xOdd || !yOdd && !xOdd) ? PixelEditorScreen.CANVAS_CHECKERS_EVEN_SHADE : PixelEditorScreen.CANVAS_CHECKERS_ODD_SHADE;
				
				gridTexture.SetPixel(x, y, new Color32(grey, grey, grey, 255));
			}
		}
		
		gridTexture.Apply();
		_gridPlane.renderer.material.mainTexture = gridTexture;
	}

	void ResizePatternViewer()
	{
		int patternPixelWidth = _editor.GetCanvas().pixelWidth * 3;
		int patternPixelHeight = _editor.GetCanvas().pixelHeight * 3;
		
		float aspect = (float)patternPixelWidth / (float)patternPixelHeight;
		float scale = PlayerPrefs.GetFloat("patternZoom", PATTERN_DEFAULT_SCALE);
		_patternPlane.transform.localScale = new Vector3(scale, scale / aspect, 1.0f);
		
		if(_patternTexture)
			_patternTexture = null;
		
		// create texture
		_patternTexture = new Texture2D(patternPixelWidth, patternPixelHeight, TextureFormat.ARGB32, false);
		_patternTexture.filterMode = FilterMode.Point;
		_patternPlane.renderer.material.mainTexture = _patternTexture;
		
		// -------------------------------------------------------------------------
		// BORDER
		// -------------------------------------------------------------------------
		float xScaleIncrease = 0.10f;
		float yScaleIncrease = xScaleIncrease * aspect;
		_patternBorderPlane.transform.localScale = new Vector3(1.0f + xScaleIncrease, 1.0f + yScaleIncrease, 1.0f);
		
		// -------------------------------------------------------------------------
		// GRID
		// -------------------------------------------------------------------------
		Texture2D gridTexture = new Texture2D(patternPixelWidth, patternPixelHeight, TextureFormat.ARGB32, false);
		gridTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < patternPixelHeight; y++)
		{
			for(int x = 0; x < patternPixelWidth; x++)
			{
				bool yOdd = y % 2 != 0;
				bool xOdd = x % 2 != 0;
				
				byte grey = (yOdd && xOdd || !yOdd && !xOdd) ? PixelEditorScreen.CANVAS_CHECKERS_EVEN_SHADE : PixelEditorScreen.CANVAS_CHECKERS_ODD_SHADE;
				
				gridTexture.SetPixel(x, y, new Color32(grey, grey, grey, 255));
			}
		}
		
		gridTexture.Apply();
		_gridPlane.renderer.material.mainTexture = gridTexture;
	}
	
	public void NewCanvas()
	{
		if(!_activated)
			return;

		ResizePatternViewer();
		RefreshPatternViewer();
	}

	public void RefreshPatternViewer()
	{
		if(!_activated)
			return;

		int patternPixelWidth = _editor.GetCanvas().pixelWidth * 3;
		int patternPixelHeight = _editor.GetCanvas().pixelHeight * 3;

		Color32[] pixels = new Color32[patternPixelWidth * patternPixelHeight];
		Color32[] canvasPixels = _editor.GetCanvas().GetPixels(_editor.CurrentFrame);

		for(int x = 0; x < patternPixelWidth; x++)
		{
			for(int y = 0; y < patternPixelHeight; y++)
			{
				int index = y * patternPixelWidth + x;

				int canvasX = x % _editor.GetCanvas().pixelWidth;
				int canvasY = y % _editor.GetCanvas().pixelHeight;

				int canvasIndex = canvasY * _editor.GetCanvas().pixelWidth + canvasX;

				pixels[index] = canvasPixels[canvasIndex];
			}
		}

		_patternTexture.SetPixels32(pixels);
		_patternTexture.Apply();
	}

	public void TogglePatternViewer()
	{
		_activated = !_activated;
		PlayerPrefs.SetInt("patternActivated", (_activated) ? 1 : 0);

		if(_activated)
		{
			_patternPlane.SetActive(true);
			NewCanvas();
		}
		else
		{
			_patternPlane.SetActive(false);
		}
	}
}
















