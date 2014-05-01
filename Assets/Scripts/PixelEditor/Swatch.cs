using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Swatch : MonoBehaviour
{
	PixelEditorScreen _editor;

	GameObject _swatchPlane1;
	GameObject _swatchPlane2;

	Texture2D _swatchTexture1;
	Texture2D _swatchTexture2;

	float _zoomTimer = 0.0f;

	// BORDER .............................................
	GameObject _swatchBorderPlane1;
	GameObject _swatchBorderPlane2;

	Vector2 _borderDragWorldOffset;
	bool _draggingSwatchBorder = false;
	public bool DraggingSwatchBorder { get { return _draggingSwatchBorder; } }
	const float BORDER_DEPTH = 0.1f;

	Color32 _color1;
	public Color32 GetCurrentColor() { return _color1; }
	Color32 _color2;
	public Color32 GetOtherColor() { return _color2; }

	public void Init(PixelEditorScreen editor)
	{
		_editor = editor;

		CreateSwatch(out _swatchPlane1, out _swatchTexture1, out _swatchBorderPlane1);
		CreateSwatch(out _swatchPlane2, out _swatchTexture2, out _swatchBorderPlane2);

		_swatchPlane2.transform.parent = _swatchPlane1.transform;
		_swatchPlane2.transform.localPosition = new Vector3(0.5f, -0.5f, 1.0f);

		_color1 = new Color32(0, 0, 0, 255);
		_color2 = new Color32(255, 255, 255, 255);
	}

	public void UpdateSwatch()
	{
		// -------------------------------------------------
		// SWAPPING COLORS
		// -------------------------------------------------
		if(!_editor.GetModifierKey() && Input.GetKeyDown(KeyCode.X))
			SwapColors();

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
			// SWATCH PLANE
			// -------------------------------------------------------------------------
			if(ray.transform.gameObject.layer == LayerMask.NameToLayer("Swatch"))
			{
				if(leftMouseDown && !_editor.GetIsDragging() && !_editor.GetIsEditingPixels())
				{
					_draggingSwatchBorder = true;
					
					Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
					_borderDragWorldOffset = (Vector2)_swatchBorderPlane1.transform.position - mouseWorldPos;
				}
				
				ZoomSwatchPlane();
				zoomedThisFrame = true;
			}
		}

		// -------------------------------------------------------------------------
		// ZOOMING
		// -------------------------------------------------------------------------
		if(!zoomedThisFrame && _zoomTimer > 0.0f)
			ZoomSwatchPlane();

		if(_draggingSwatchBorder)
		{
			if(Input.GetMouseButtonUp(0))
			{
				_draggingSwatchBorder = false;
				PlayerPrefs.SetFloat("swatchPosX", _swatchPlane1.transform.position.x);
				PlayerPrefs.SetFloat("swatchPosY", _swatchPlane1.transform.position.y);
			}
			else
			{
				Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				_swatchPlane1.transform.position = new Vector3(mouseWorldPos.x + _borderDragWorldOffset.x, mouseWorldPos.y + _borderDragWorldOffset.y, PixelEditorScreen.SWATCH_PLANE_DEPTH);
			}
		}
	}

	public void RefreshColors()
	{
		_swatchTexture1.SetPixel(0, 0, _color1);
		_swatchTexture1.Apply();

		_swatchTexture2.SetPixel(0, 0, _color2);
		_swatchTexture2.Apply();
	}

	public void SetColor(Color32 color)
	{
		_color1 = color;
		RefreshColors();
	}

	public void SwapColors()
	{
		Color32 tempColor = _color1;
		_color1 = _color2;
		_color2 = tempColor;

		RefreshColors();
	}

	void ZoomSwatchPlane()
	{
		if(!_editor.GetModifierKey())
		{
			if(Input.GetAxis("Mouse ScrollWheel") < 0)
			{
				_swatchPlane1.transform.localScale *= 1.0f - PixelEditorScreen.CAMERA_ZOOM_SPEED;
				PlayerPrefs.SetFloat("swatchZoom", _swatchPlane1.transform.localScale.x);
				_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
			}
			else if(Input.GetAxis("Mouse ScrollWheel") > 0)
			{
				_swatchPlane1.transform.localScale *= 1.0f + PixelEditorScreen.CAMERA_ZOOM_SPEED;
				PlayerPrefs.SetFloat("swatchZoom", _swatchPlane1.transform.localScale.x);
				_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
			}
			else if(_zoomTimer > 0.0f)
			{
				_zoomTimer -= Time.deltaTime;
			}
		}
	}

	void CreateSwatch(out GameObject plane, out Texture2D texture, out GameObject borderPlane)
	{
		plane = _editor.CreatePlane("SwatchPlane", null, PixelEditorScreen.PALETTE_PLANE_DEPTH);
		
		float xPos = PlayerPrefs.GetFloat("swatchPosX", -1.1f);
		float yPos = PlayerPrefs.GetFloat("swatchPosY", 0.8f);
		plane.transform.position = new Vector3(xPos, yPos, PixelEditorScreen.SWATCH_PLANE_DEPTH);
		float scale = PlayerPrefs.GetFloat("swatchZoom", 0.16f);
		plane.transform.localScale = new Vector3(scale, scale, 1.0f);
		plane.layer = LayerMask.NameToLayer("Swatch");

		texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		texture.filterMode = FilterMode.Point;
		
		// Create material
		Material swatchMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitTransparent") as Material);
		swatchMaterial.mainTexture = texture;
		plane.GetComponent<MeshRenderer>().material = swatchMaterial;
		
		// ---------------------------------------------------------
		// BORDER
		// ---------------------------------------------------------
		borderPlane = _editor.CreatePlane("Border", plane.transform, 0.2f);
		
		// create border texture
		Texture2D borderTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		borderTexture.filterMode = FilterMode.Point;
		borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR);
		borderTexture.Apply();
		
		// create border material
		Material borderMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		borderMaterial.mainTexture = borderTexture;
		borderPlane.GetComponent<MeshRenderer>().material = borderMaterial;
		borderPlane.layer = LayerMask.NameToLayer("Swatch");
		
		float scaleIncrease = 0.15f;
		borderPlane.transform.localScale = new Vector3(1.0f + scaleIncrease, 1.0f + scaleIncrease, 1.0f);

		// ---------------------------------------------------------
		// BACKGROUND
		// ---------------------------------------------------------
		GameObject backgroundPlane = _editor.CreatePlane("Background", plane.transform, BORDER_DEPTH);
		backgroundPlane.transform.localScale = new Vector3(1, 1, 1);
		
		// create border texture
		Texture2D backgroundTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		backgroundTexture.filterMode = FilterMode.Point;
		byte grey = PixelEditorScreen.CANVAS_CHECKERS_EVEN_SHADE;
		backgroundTexture.SetPixel(0, 0, new Color32(grey, grey, grey, 255));
		backgroundTexture.Apply();
		
		// create border material
		Material backgroundMaterial = (Material)Instantiate(Resources.Load("Materials/Unlit") as Material);
		backgroundMaterial.mainTexture = backgroundTexture;
		backgroundPlane.GetComponent<MeshRenderer>().material = backgroundMaterial;
	}
}















