using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum LoopMode { Loops, PlayOnce, PingPong, RandomFrame };

public class FramePreview : MonoBehaviour
{
	PixelEditorScreen _editor;

	int _currentFrame = 0;
	public int CurrentFrame { get { return _currentFrame; } set { _currentFrame = value; } }

	public void SetCurrentFrame(int frameNumber)
	{
		_currentFrame = frameNumber;
		HighlightCurrentFramePreview();
		_editor.GetCanvas().DirtyPixels = true;
		_editor.GetCanvas().RefreshOnionSkin();
	}
	
	GameObject _frameContainer;
	List<GameObject> _framePlanes = new List<GameObject>();
	List<Texture2D> _frameTextures = new List<Texture2D>();

	public void SetPixels(Color32[] pixels, int frameNumber)
	{
		_frameTextures[frameNumber].SetPixels32(pixels);
		_frameTextures[frameNumber].Apply();
	}

	List<GameObject> _frameBorderPlanes = new List<GameObject>();
	List<GameObject> _frameGridPlanes = new List<GameObject>();
	List<float> _frameTimes = new List<float>();
	public float GetFrameTime(int frameNumber) { return _frameTimes[frameNumber]; }
	public float GetCurrentFrameTime() { return _frameTimes[_currentFrame]; }
	public void SetFrameTime(float time, int frameNumber) { _frameTimes[frameNumber] = time; }
	public void SetCurrentFrameTime(float time) { _frameTimes[_currentFrame] = time; }

	bool _draggingPreviewFrames = false;
	public bool DraggingPreviewFrames { get { return _draggingPreviewFrames; } }

	bool _selectingFrame = false;
	public int GetNumFrames() { return _framePlanes.Count; }

	float _zoomTimer = 0.0f;

	//-----------------------------------------------------
	// ANIMATION
	//-----------------------------------------------------
	const float DEFAULT_FRAME_TIME = 0.1f;
	bool _isPlayingAnimation = false;
	float _animTimer = 0.0f;
	public float AnimTimer { get { return _animTimer; } set { _animTimer = value; } }
	LoopMode _loopMode;
	public LoopMode LoopMode { get { return _loopMode; } }
	bool _pingPongForward = true;

	Vector2 _borderDragWorldOffset;

	const float GRID_DEPTH = 0.1f;
	const float BORDER_DEPTH = 0.2f;

	public void Init(PixelEditorScreen editor)
	{
		_editor = editor;

		CreateFrameContainer();
		CreateFramePlane();

		SetLoopMode((LoopMode)PlayerPrefs.GetInt("loopMode", 0));
	}

	public void UpdateFramePreview()
	{
		// -------------------------------------------------
		// SWITCHING FRAMES
		// -------------------------------------------------
		if(Input.GetKeyDown(KeyCode.A) && !_editor.GetIsEditingPixels())// || Input.GetKeyDown(KeyCode.LeftArrow))
		{
			if(_editor.GetModifierKey())
				_editor.AddNewCommand(new DuplicateFrameCommand(true));
			else
				GoToPreviousFrame();
			
			_isPlayingAnimation = false;
		}
		else if(Input.GetKeyDown(KeyCode.D) && !_editor.GetIsEditingPixels())// || Input.GetKeyDown(KeyCode.RightArrow))
		{
			if(_editor.GetModifierKey())
				_editor.AddNewCommand(new DuplicateFrameCommand(false));
			else
				GoToNextFrame();
			
			_isPlayingAnimation = false;
		}
		else if(_editor.GetModifierKey() && Input.GetKeyUp(KeyCode.Space) && !_editor.GetIsEditingPixels())
		{
			_editor.AddNewCommand(new AddNewBlankFrameCommand());
			_isPlayingAnimation = false;
		}
		
		// -------------------------------------------------
		// ANIMATION
		// -------------------------------------------------
		if(_isPlayingAnimation)
		{
			_animTimer += Time.deltaTime;
			if(_animTimer >= _frameTimes[_currentFrame])
			{
				// ----------------------------------------------------------------
				// LOOPS
				// ----------------------------------------------------------------
				if(_loopMode == LoopMode.Loops)
		        {
					GoToNextFrame();
				}
				// ----------------------------------------------------------------
				// PLAY ONCE
				// ----------------------------------------------------------------
				else if(_loopMode == LoopMode.PlayOnce)
				{
					if(_currentFrame < GetNumFrames() - 1)
						GoToNextFrame();
					else
						_isPlayingAnimation = false;
				}
				// ----------------------------------------------------------------
				// PING PONG
				// ----------------------------------------------------------------
				else if(_loopMode == LoopMode.PingPong)
				{
					if(_pingPongForward)
					{
						if(_currentFrame < GetNumFrames() - 1)
						{
							GoToNextFrame();
						}
						else
						{
							GoToPreviousFrame();
							_pingPongForward = false;
						}
					}
					else
					{
						if(_currentFrame > 0)
						{
							GoToPreviousFrame();
						}
						else
						{
							GoToNextFrame();
							_pingPongForward = true;
						}
					}
				}
				// ----------------------------------------------------------------
				// RANDOM
				// ----------------------------------------------------------------
				else if(_loopMode == LoopMode.RandomFrame)
				{
					if(GetNumFrames() > 1)
					{
						int newFrame = Random.Range(0, GetNumFrames());
						while(newFrame == _currentFrame)
							newFrame = Random.Range(0, GetNumFrames());

						_editor.AddNewCommand(new SetFrameCommand(newFrame));
						
						_editor.GetCanvas().DirtyPixels = true;
						_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
					}
				}

				_animTimer = 0.0f;
			}
		}
		
		if(!_editor.GetModifierKey() && Input.GetKeyDown(KeyCode.Space))
		{
			if(_isPlayingAnimation)
			{
				_isPlayingAnimation = false;
			}
			else if(!_isPlayingAnimation && GetNumFrames() > 1)
			{
				if(_loopMode == LoopMode.PlayOnce && _currentFrame == GetNumFrames() - 1)
				{
					_editor.AddNewCommand(new SetFrameCommand(0));
					_editor.GetCanvas().DirtyPixels = true;
				}
				
				_animTimer = 0.0f;
				_isPlayingAnimation = true;
			}
		}
		
		if(Input.GetKeyDown(KeyCode.L))
		{
			ToggleLoopMode();
		}
		
		if(_editor.GetModifierKey() && (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Q)) && !_editor.GetIsEditingPixels())
		{
			_editor.AddNewCommand(new DeleteFrameCommand());

			HighlightCurrentFramePreview();
		}

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
			// FRAME PREVIEW PLANES
			// -------------------------------------------------------------------------
			if(ray.transform.gameObject.layer == LayerMask.NameToLayer("PreviewFrame"))
			{
				if(leftMouse)
				{
					// SELECT THE FRAME
					for(int i = 0; i < GetNumFrames(); i++)
					{
						GameObject framePlane = _framePlanes[i];
						GameObject frameBorderPlane = _frameBorderPlanes[i];
						
						if(ray.transform.gameObject == framePlane || ray.transform.gameObject == frameBorderPlane)
						{
							if(!_editor.GetIsDragging() && !_editor.GetIsEditingPixels())
							{
								if(leftMouseDown && _currentFrame == i)
								{
									_draggingPreviewFrames = true;
									
									Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
									_borderDragWorldOffset = (Vector2)_frameContainer.transform.position - mouseWorldPos;
								}
								else if(leftMouse && _currentFrame != i && !_editor.GetIsEditingPixels())
								{
									_editor.AddNewCommand(new SetFrameCommand(i));
									_editor.GetCanvas().DirtyPixels = true;
									_isPlayingAnimation = false;
									_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
								}
							}
						}
					}
				}
				
				ZoomFramePreviews();
				zoomedThisFrame = true;
			}
		}

		// -------------------------------------------------------------------------
		// ZOOMING
		// -------------------------------------------------------------------------
		if(!zoomedThisFrame && _zoomTimer > 0.0f)
			ZoomFramePreviews();

		// -------------------------------------------------
		// BORDERS
		// -------------------------------------------------
		if(_draggingPreviewFrames)
		{
			if(Input.GetMouseButtonUp(0))
			{
				_draggingPreviewFrames = false;
				PlayerPrefs.SetFloat("previewFramesPosX", _frameContainer.transform.position.x);
				PlayerPrefs.SetFloat("previewFramesPosY", _frameContainer.transform.position.y);
			}
			else
			{
				Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				_frameContainer.transform.position = new Vector3(mouseWorldPos.x + _borderDragWorldOffset.x, mouseWorldPos.y + _borderDragWorldOffset.y, PixelEditorScreen.FRAME_PREVIEW_DEPTH);
			}
		}
	}

	void ZoomFramePreviews()
	{
		if(Input.GetAxis("Mouse ScrollWheel") < 0)
		{
			_frameContainer.transform.localScale *= 1.0f - PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("previewFramesZoom", _frameContainer.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(Input.GetAxis("Mouse ScrollWheel") > 0)
		{
			_frameContainer.transform.localScale *= 1.0f + PixelEditorScreen.CAMERA_ZOOM_SPEED;
			PlayerPrefs.SetFloat("previewFramesZoom", _frameContainer.transform.localScale.x);
			_zoomTimer = PixelEditorScreen.ZOOM_TIMER_LENIENCY;
		}
		else if(_zoomTimer > 0.0f)
		{
			_zoomTimer -= Time.deltaTime;
		}
	}

	void CreateFrameContainer()
	{
		_frameContainer = new GameObject("PreviewFrames");
		float xPos = PlayerPrefs.GetFloat("previewFramesPosX", -0.2f);
		float yPos = PlayerPrefs.GetFloat("previewFramesPosY", 0.6f);
		_frameContainer.transform.position = new Vector3(xPos, yPos, PixelEditorScreen.FRAME_PREVIEW_DEPTH);
		float scale = PlayerPrefs.GetFloat("previewFramesZoom", 1.0f);
		_frameContainer.transform.localScale = new Vector3(scale, scale, 1.0f);
	}
	
	public void CreateFramePlane()
	{
		int i = GetNumFrames();
		GameObject framePreviewPlane = _editor.CreatePlane("FramePreview: " + i, _frameContainer.transform, 0.0f);
		
		float aspect = (float)_editor.GetCanvas().pixelWidth / (float)_editor.GetCanvas().pixelHeight;
		float PREVIEW_SCALE = 0.1f;
		float xScaleFrameIncrease = 0.16f;
		float yScaleFrameIncrease = xScaleFrameIncrease * aspect;
		framePreviewPlane.transform.localScale = new Vector3(aspect * PREVIEW_SCALE, PREVIEW_SCALE, 1);
		
		_framePlanes.Add(framePreviewPlane);
		framePreviewPlane.layer = LayerMask.NameToLayer("PreviewFrame");
		
		Material framePreviewMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		framePreviewPlane.GetComponent<MeshRenderer>().material = framePreviewMaterial;

		// ---------------------------------------------------------
		// BORDER
		// ---------------------------------------------------------
		GameObject borderPlane = _editor.CreatePlane("Frame Border " + i, framePreviewPlane.transform, BORDER_DEPTH);
		_frameBorderPlanes.Add(borderPlane);
		borderPlane.transform.localScale = new Vector3(1.0f + xScaleFrameIncrease, 1.0f + yScaleFrameIncrease, 1.0f);
		borderPlane.layer = LayerMask.NameToLayer("PreviewFrame");
		
		// create border texture
		Texture2D borderTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		borderTexture.filterMode = FilterMode.Point;
		borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR_NONCURRENT_FRAME);
		borderTexture.Apply();
		
		// create border material
		Material borderMaterial = (Material)Instantiate(Resources.Load("Materials/UnlitAlphaWithFade") as Material);
		borderMaterial.mainTexture = borderTexture;
		borderPlane.GetComponent<MeshRenderer>().material = borderMaterial;

		// ---------------------------------------------------------
		// GRID
		// ---------------------------------------------------------
		GameObject gridPlane = _editor.CreatePlane("Grid", framePreviewPlane.transform, GRID_DEPTH);
		_frameGridPlanes.Add(gridPlane);
		gridPlane.transform.localScale = new Vector3(1, 1, 1);
		
		// create border texture
		Texture2D gridTexture = new Texture2D(_editor.GetCanvas().pixelWidth, _editor.GetCanvas().pixelHeight, TextureFormat.ARGB32, false);
		gridTexture.filterMode = FilterMode.Point;
		
		for(int y = 0; y < _editor.GetCanvas().pixelHeight; y++)
		{
			for(int x = 0; x < _editor.GetCanvas().pixelWidth; x++)
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
		gridPlane.GetComponent<MeshRenderer>().material = gridMaterial;


		framePreviewPlane.renderer.material.mainTexture = CreateFrameTexture();
		
		_frameTimes.Add(DEFAULT_FRAME_TIME);
		_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
		
		RepositionFramePlanes();
		HighlightCurrentFramePreview();
	}

	// ONLY CALL FROM A COMMAND
	public void AddNewBlankFrame()
	{
		CreateFramePlane();
		SetCurrentFrame(_currentFrame + 1);
		_editor.GetCanvas().AddNewBlankFrame();
		_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
		HighlightCurrentFramePreview();
	}

	public void DuplicateCurrentFrame(bool backward = true)
	{
		CreateFramePlane();
		
		_editor.GetCanvas().DuplicateCurrentFrame();
		
		if(backward)
		{
			_currentFrame++;
			HighlightCurrentFramePreview();
			SetCurrentFrameTime(GetFrameTime(_currentFrame - 1));
			_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
		}
		else
		{
			SetFrameTime(GetCurrentFrameTime(), _currentFrame + 1);
		}
	}

	// CALL FROM FRAME PREVIEW METHOD
	public void AddNewFrame(int frameNumber, Color32[] pixels)
	{
		CreateFramePlane();
		_currentFrame = frameNumber;
		_editor.GetCanvas().AddNewFrame(frameNumber, pixels);
		_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
		HighlightCurrentFramePreview();
	}

	// ONLY CALL FROM A COMMAND
	public void DeleteFrame(int frameNumber)
	{
		if(frameNumber >= GetNumFrames() || GetNumFrames() <= 1)
			return;

		if(_currentFrame > 0 && ((_currentFrame >= frameNumber) || (_currentFrame == frameNumber && _currentFrame == GetNumFrames() - 1)))
			SetCurrentFrame(_currentFrame - 1);

		Destroy(_framePlanes[frameNumber]);
		_framePlanes.RemoveAt(frameNumber);
		_frameTextures.RemoveAt(frameNumber);
		_frameBorderPlanes.RemoveAt(frameNumber);
		_frameGridPlanes.RemoveAt(frameNumber);
		_frameTimes.RemoveAt(frameNumber);
		
		_editor.GetCanvas().DeleteFrame(frameNumber);
		RepositionFramePlanes();
		HighlightCurrentFramePreview();
	}
	
	void RepositionFramePlanes()
	{
		for(int i = 0; i < GetNumFrames(); i++)
		{
			GameObject framePlane = _framePlanes[i];
			GameObject borderPlane = _frameBorderPlanes[i];
			
			float xOffset = i * (borderPlane.transform.lossyScale.x * 2.0f) - (i * (borderPlane.transform.lossyScale.x - framePlane.transform.lossyScale.x));
			framePlane.transform.position = new Vector3(_frameContainer.transform.position.x + xOffset, _frameContainer.transform.position.y, _frameContainer.transform.position.z);
		}
	}
	
	Texture2D CreateFrameTexture()
	{
		// create texture
		Texture2D framePreviewTexture = new Texture2D(_editor.GetCanvas().pixelWidth, _editor.GetCanvas().pixelHeight, TextureFormat.ARGB32, false);
		framePreviewTexture.filterMode = FilterMode.Point;
		framePreviewTexture.SetPixels32(_editor.GetCanvas().GetPixels(CurrentFrame));
		framePreviewTexture.Apply();
		
		_frameTextures.Add(framePreviewTexture);
		
		return framePreviewTexture;
	}

	public void ResizeFramePreviews()
	{
		_frameTextures.Clear();
		
		float aspect = (float)_editor.GetCanvas().pixelWidth / (float)_editor.GetCanvas().pixelHeight;
		float PREVIEW_SCALE = 0.1f;
		float xScaleFrameIncrease = 0.16f;
		float yScaleFrameIncrease = xScaleFrameIncrease * aspect;

		for(int i = 0; i < GetNumFrames(); i++)
		{
			GameObject framePreviewPlane = _framePlanes[i];
			framePreviewPlane.transform.localScale = new Vector3(aspect * PREVIEW_SCALE, PREVIEW_SCALE, 1);
			
			framePreviewPlane.renderer.material.mainTexture = CreateFrameTexture();
			
			GameObject borderPlane = _frameBorderPlanes[i];
			borderPlane.transform.localScale = new Vector3(1.0f + xScaleFrameIncrease, 1.0f + yScaleFrameIncrease, 1.0f);
			
			framePreviewPlane.transform.localPosition = new Vector3(i * (borderPlane.transform.lossyScale.x * 2.0f) - (i * (borderPlane.transform.lossyScale.x - framePreviewPlane.transform.lossyScale.x)), 0.0f, 0.0f);
		}
	}

	void HighlightCurrentFramePreview()
	{
		for(int i = 0; i < GetNumFrames(); i++)
		{
			GameObject framePlane = _framePlanes[i];
			Texture2D borderTexture = (Texture2D)_frameBorderPlanes[i].renderer.material.mainTexture;
			
			if(_currentFrame == i)
			{
				framePlane.transform.localPosition = new Vector3(framePlane.transform.localPosition.x, framePlane.transform.localPosition.y, 0.0f);
				borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR_CURRENT_FRAME);
			}
			else
			{
				framePlane.transform.localPosition = new Vector3(framePlane.transform.localPosition.x, framePlane.transform.localPosition.y, 0.1f);
				borderTexture.SetPixel(0, 0, PixelEditorScreen.BORDER_COLOR_NONCURRENT_FRAME);
			}
			
			borderTexture.Apply();
		}
	}

	void GoToPreviousFrame()
	{
		if(GetNumFrames() == 1)
			return;

		if(_currentFrame > 0)
			_editor.AddNewCommand(new SetFrameCommand(_currentFrame - 1));
		else
			_editor.AddNewCommand(new SetFrameCommand(GetNumFrames() - 1));

		_editor.GetCanvas().DirtyPixels = true;
		_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
	}
	
	void GoToNextFrame()
	{
		if(GetNumFrames() == 1)
			return;

		if(_currentFrame < GetNumFrames() - 1)
			_editor.AddNewCommand(new SetFrameCommand(_currentFrame + 1));
		else
			_editor.AddNewCommand(new SetFrameCommand(0));

		_editor.GetCanvas().DirtyPixels = true;
		_editor.CurrentFrameTimeString = _frameTimes[_currentFrame].ToString();
	}

	public void NewCanvas()
	{
		_currentFrame = 0;
		
		foreach(GameObject framePreviewPlane in _framePlanes)
			Destroy(framePreviewPlane);
		_framePlanes.Clear();
		_frameTextures.Clear();
		_frameBorderPlanes.Clear();
		_frameGridPlanes.Clear();
		_frameTimes.Clear();
	}

	public void ToggleLoopMode()
	{
		if(_loopMode == LoopMode.Loops)
			SetLoopMode(LoopMode.PlayOnce);
		else if(_loopMode == LoopMode.PlayOnce)
			SetLoopMode(LoopMode.PingPong);
		else if(_loopMode == LoopMode.PingPong)
			SetLoopMode(LoopMode.RandomFrame);
		else if(_loopMode == LoopMode.RandomFrame)
			SetLoopMode(LoopMode.Loops);
	}

	public void SetLoopMode(LoopMode loopType)
	{
		_loopMode = loopType;
		PlayerPrefs.SetInt("loopMode", (int)_loopMode);
		
		if(_loopMode == LoopMode.Loops)
		{
			_editor.LoopTypeString = "-->LOOP-->";
		}
		else if(_loopMode == LoopMode.PlayOnce)
		{
			_editor.LoopTypeString = "PLAY ONCE-->";
		}
		else if(_loopMode == LoopMode.PingPong)
		{
			_editor.LoopTypeString = "<--PING PONG-->";
			_pingPongForward = true;
		}
		else if(_loopMode == LoopMode.RandomFrame)
		{
			_editor.LoopTypeString = "?--RANDOM--?";
		}
	}
}








