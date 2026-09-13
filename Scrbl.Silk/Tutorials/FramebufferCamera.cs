using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System;

namespace Scrbl.Tutorials;

public class FramebufferCamera
{
    private readonly IInputContext _input;

    private Vector2D<int> _windowSize;
    private uint _fbWidth;
    private uint _fbHeight;

    private Vector2D<float> _cameraPos;
    private float _zoomLevel = 1.0f;
    private bool _isDragging = false;

    private Vector2D<float> _dragStartMousePos;
    private Vector2D<float> _dragStartCameraPos;

    // Public properties to access active camera matrices/view options
    public Vector2D<float> Position => _cameraPos;
    public float ZoomLevel => _zoomLevel;
    public bool IsDragging => _isDragging;

    /// <summary>
    /// Calculated bounding properties representing what portion of the FBO texture to source during blits
    /// </summary>
    public int SourceX => (int)(_cameraPos.X - ViewWidth / 2.0f);
    public int SourceY => (int)(_cameraPos.Y - ViewHeight / 2.0f);
    public float ViewWidth => _windowSize.X * _zoomLevel;
    public float ViewHeight => _windowSize.Y * _zoomLevel;

    public FramebufferCamera(IInputContext input, IWindow window, FramebufferObject frameBuffer)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _windowSize = window.Size;
        _fbWidth = frameBuffer.Width;
        _fbHeight = frameBuffer.Height;

        // Default: Start centered relative to the overall backing Framebuffer texture bounds
        _cameraPos = new Vector2D<float>(_fbWidth / 2f, _fbHeight / 2f);

        window.Resize += OnWindowResize;
        frameBuffer.ResizeAction += OnFrameBufferResize;

        var mouse = _input.Mice[0];
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.Scroll += OnMouseScroll;
    }

    private void OnWindowResize(Vector2D<int> size)
    {
        _windowSize = size;

        ClampPosition();
    }

    private void OnFrameBufferResize(uint width, uint height)
    {
        _fbWidth = width;
        _fbHeight = height;

        ClampPosition();
    }

    //public void Resize(Vector2D<int> windowSize, uint fbWidth, uint fbHeight)
    //{
    //    _windowSize = windowSize;
    //    _fbWidth = fbWidth;
    //    _fbHeight = fbHeight;

    //    ClampPosition();
    //}

    public void Update()
    {
        if (!_isDragging) return;

        // Get the absolute live mouse position for this current frame loop
        var currentMouse = new Vector2D<float>(_input.Mice[0].Position.X, _input.Mice[0].Position.Y);

        // Calculate total distance traveled since the drag started
        Vector2D<float> totalDeltaScreen = currentMouse - _dragStartMousePos;

        // Apply the delta back to the original snapshot position 
        float newCamX = _dragStartCameraPos.X - (totalDeltaScreen.X * _zoomLevel);
        float newCamY = _dragStartCameraPos.Y + (totalDeltaScreen.Y * _zoomLevel); // Invert Y for GL space

        _cameraPos = new Vector2D<float>(newCamX, newCamY);

        ClampPosition();
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isDragging = true;
            _dragStartMousePos = new Vector2D<float>(mouse.Position.X, mouse.Position.Y);
            _dragStartCameraPos = _cameraPos;
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isDragging = false;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        // 1. Get the current cursor position in Screen coordinates
        Vector2D<float> mouseScreenPos = new Vector2D<float>(mouse.Position.X, mouse.Position.Y);

        // 2. Convert Screen coordinates to current Framebuffer (World) coordinates
        // Normalise screen space (-0.5 to 0.5 relative to window center)
        float ndcX = (mouseScreenPos.X / _windowSize.X) - 0.5f;
        // Flip Y because Screen 0 is top, OpenGL FBO 0 is bottom
        float ndcY = 0.5f - (mouseScreenPos.Y / _windowSize.Y);

        // Find the exact FBO pixel under the mouse before zooming
        float mouseFboX = _cameraPos.X + (ndcX * ViewWidth);
        float mouseFboY = _cameraPos.Y + (ndcY * ViewHeight);

        // 3. Calculate the new zoom level
        float zoomFactor = scroll.Y > 0 ? 0.9f : 1.1f;
        float newZoomLevel = Math.Clamp(_zoomLevel * zoomFactor, 0.5f, 4.0f);

        // 4. Calculate the new viewport dimensions
        float viewWidthAfter = _windowSize.X * newZoomLevel;
        float viewHeightAfter = _windowSize.Y * newZoomLevel;

        // 5. Shift the camera position so the FBO pixel stays under the cursor
        _cameraPos.X = mouseFboX - (ndcX * viewWidthAfter);
        _cameraPos.Y = mouseFboY - (ndcY * viewHeightAfter);

        // Update zoom level state
        _zoomLevel = newZoomLevel;

        // 6. If you are currently dragging while zooming, reset the drag anchors
        if (_isDragging)
        {
            _dragStartMousePos = mouseScreenPos;
            _dragStartCameraPos = _cameraPos;
        }

        ClampPosition();
    }

    private void ClampPosition()
    {
        float halfWidth = ViewWidth / 2.0f;
        float halfHeight = ViewHeight / 2.0f;

        _cameraPos.X = Math.Clamp(_cameraPos.X, halfWidth, _fbWidth - halfWidth);
        _cameraPos.Y = Math.Clamp(_cameraPos.Y, halfHeight, _fbHeight - halfHeight);
    }
}
