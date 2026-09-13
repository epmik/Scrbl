//
// 2 VBO's, switching between VBP's one one is full
//
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace Scrbl.Tutorials;

class _019_Frame_Vertex_Index_BufferObjects
{
    struct VertexBufferChunk
    {
        public Transform Transform;

        public int Index;
        public uint Count;

        public PrimitiveType PrimitiveType;

        public VertexBufferChunk()
        {
            Transform = new Transform();
            PrimitiveType = PrimitiveType.Triangles;
        }
    }

    // We must hold a reference to the delegate so the Garbage Collector doesn't destroy it
    //private static DebugProc _debugCallback;

    ImGuiController _imGuiController;

    // FPS Tracker State
    int _frameCount = 0;
    double _fpsTimer = 0.0;
    string _fpsText = "FPS: ...";

    int MinLineChunkCount = 24;
    int MaxLineChunkCount = 68;

    int MinLineLoopChunkCount = 2;

    int MaxLineLoopChunkCount = 5;

    int MinLineStripChunkCount = 2;

    int MaxLineStripChunkCount = 5;

    int LineChunkCount = 0;
    int LineLoopChunkCount = 0;
    int LineStripChunkCount = 0;

    int RandomSeed = 10241024;

    Random Random;

    double NextUpdateTimeDelta = 12.0;
    double NextUpdateTimeout = 12.0;    // 4 seconds

    IWindow window;
    GL Gl;

    private VertexArrayObject _vao;
    private VertexBufferObject _vbo;

    int _windowWidth = 800;
    int _windowHeight = 600;

    double _frameBufferScale = 4.0;

    FramebufferObject _multiSampledFrameBuffer;
    FramebufferObject _intermediateFrameBuffer;

    Shader _shader;

    const uint VertexElementCount = (3 + 4); // X Y Z + R G B A

    const uint VertexElementByteSize = VertexElementCount * sizeof(float);
    const uint TotalVertexElement = 128;

    //const uint VertexBufferByteTotalSize = 128 * VertexElementByteSize;

    //uint VertexBufferBytesUsedCount = 0;
    //uint PreviousVertexBufferBytesUsedCount;

    //uint VertexBufferElementsUsedCount = 0;
    //uint PreviousVertexBufferElementsUsedCount = 0;

    List<VertexBufferChunk> VertexBufferChunkList = new List<VertexBufferChunk>();

    IInputContext input;

    FramebufferCamera _camera;

    public void Run(string[] args)
    {
        Random = new Random(RandomSeed);

        var options = WindowOptions.Default;

        options.Size = new Vector2D<int>(_windowWidth, _windowHeight);
        options.Title = "_019_Frame_Vertex_Index_BufferObjects";
        options.VSync = false;

        bool isMac = OperatingSystem.IsMacOS();

        // Apple macOS Limitations: If you intend to run your application on macOS, do not target version 4.6.
        // Apple officially deprecated OpenGL and limits their native drivers strictly to OpenGL 4.1 Core Profile.

        // 2. Configure the graphics context to explicitly target OpenGL 4.6 Core
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,              // Desktop OpenGL
            ContextProfile.Core,            // Core Profile (removes deprecated fixed-pipeline functions)
            isMac
                ? ContextFlags.ForwardCompatible
                : ContextFlags.Debug,       // Removes features deprecated in your target version
            isMac
                ? new APIVersion(4, 1)
                : new APIVersion(4, 6)                      // Target Version: Major 4, Minor 6
        );


        window = Window.Create(options);

        window.Load += OnLoad;
        window.Render += OnRender;
        window.Update += OnUpdate;
        window.FramebufferResize += OnFramebufferResize;
        window.Closing += OnClose;

        window.Run();

        window.Dispose();
    }

    private unsafe void OnLoad()
    {
        Gl = GL.GetApi(window);

        FramebufferObject.BindDefaultFrameBuffer(Gl, window);

        input = window.CreateInput();

        for (int i = 0; i < input.Keyboards.Count; i++)
        {
            input.Keyboards[i].KeyDown += KeyDown;
        }

        var mouse = input.Mice[0];
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnMouseScroll;

        string version = Gl.GetStringS(StringName.Version);
        string vendor = Gl.GetStringS(StringName.Vendor);
        string renderer = Gl.GetStringS(StringName.Renderer);

        Console.WriteLine($"OpenGL Version:  {version}");
        Console.WriteLine($"Graphics Card:   {renderer}");
        Console.WriteLine($"Driver Vendor:   {vendor}");

        if (!OperatingSystem.IsMacOS())
        {
            // 2. Enable Debug Outputs in OpenGL
            Gl.Enable(EnableCap.DebugOutput);

            // Ensures messages are sent synchronously exactly when the error happens
            Gl.Enable(EnableCap.DebugOutputSynchronous);

            // 3. Assign the callback delegate
            Gl.DebugMessageCallback(OnOpenGLDebugMessage, null);

            // (Optional) Filter out less important notifications if your console gets spammy
            Gl.DebugMessageControl(DebugSource.DontCare, DebugType.DontCare, DebugSeverity.DebugSeverityNotification, 0, null, false);

            // Trigger a debug message to ensure the callback is working
            //Gl.Enable((EnableCap)9999);
        }

        _vao = new VertexArrayObject(Gl);

        // 2. Instantiate memory sizing properties via VBO Wrapper
        _vbo = new VertexBufferObject(Gl, VertexElementByteSize, TotalVertexElement, BufferUsageARB.DynamicDraw);

        // 3. Define the layout specifications
        VertexBufferLayout layout = new VertexBufferLayout();
        layout.PushFloat(3); // Position Vector: X Y Z
        layout.PushFloat(4); // Color Vector: R G B A

        // 4. Attach layout configurations securely inside our VAO object instance
        _vao.AddBuffer(_vbo, layout);
        _vao.Unbind();     

        _shader = new Shader(Gl, ".assets/.shaders/m_pos3_col4.vert", ".assets/.shaders/col4.frag");

        _imGuiController = new ImGuiController(Gl, window, input);

        #region Setup Framebuffer

        _multiSampledFrameBuffer = new FramebufferObject(Gl)
        {
            Width = (uint)(_windowWidth * _frameBufferScale),
            Height = (uint)(_windowHeight * _frameBufferScale),
            Samples = 8
        };

        _intermediateFrameBuffer = new FramebufferObject(Gl)
        {
            Width = (uint)(_windowWidth * _frameBufferScale),
            Height = (uint)(_windowHeight * _frameBufferScale),
            Samples = 0, // No multi-sampling for the intermediate framebuffer
            DepthBits = 0, // No depth buffer for the intermediate framebuffer
            StencilBits = 0 // No depth buffer for the intermediate framebuffer
        };

        _multiSampledFrameBuffer.Setup();

        Console.WriteLine($"Multi Sampled Framebuffer status: {_multiSampledFrameBuffer.Status()}");

        _intermediateFrameBuffer.Setup();

        Console.WriteLine($"IntermediateFramebuffer status: {_intermediateFrameBuffer.Status()}");

        #endregion Setup Framebuffer

        _camera = new FramebufferCamera(input, window, _intermediateFrameBuffer);

        FramebufferObject.Default.Bind(true);

    }

    private unsafe void OnUpdate(double deltaTime)
    {
        _camera.Update();

        // 1. Make sure to feed ImGui the latest time step input
        _imGuiController.Update((float)deltaTime);

        // 2. Increment frame counts and track accumulated delta time
        _frameCount++;
        _fpsTimer += deltaTime;

        // 3. Throttle the string updates to maximum once per second
        if (_fpsTimer >= 1.0)
        {
            double calculatedFps = _frameCount / _fpsTimer;
            _fpsText = $"FPS: {calculatedFps:F1}"; // Formats to 1 decimal place

            // Reset trackers for the next 1-second phase
            _frameCount = 0;
            _fpsTimer = 0.0;
        }

        if (OperatingSystem.IsMacOS())
        {
            DebugMacOsOpenGlErrors();
        }
    }

    private unsafe void OnRender(double deltaTime) //Method needs to be unsafe due to draw elements.
    {
        if (NextUpdateTimeDelta <= 0)
        {
            RandomSeed = Guid.NewGuid().GetHashCode();

            NextUpdateTimeDelta += NextUpdateTimeout;
        }

        NextUpdateTimeDelta -= deltaTime;

        Random = new Random(RandomSeed);

        _multiSampledFrameBuffer.Bind(true);

        Gl.ClearColor(0.1f, 0.1f, 0.1f, 0.0f);

        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        GenerateAndDrawVertexBufferChunkList(deltaTime);

        FramebufferObject.Default.Bind(true);

        Gl.ClearColor(0.1f, 0.1f, 0.1f, 0.0f);

        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        _multiSampledFrameBuffer.BlitTo(_intermediateFrameBuffer);

        _intermediateFrameBuffer.BlitToScreen(_camera.SourceX, _camera.SourceY, (int)_camera.ViewWidth, (int)_camera.ViewHeight);

        FramebufferObject.Default.Bind(true);

        // 1. Declare your UI components using standard immediate-mode patterns
        // We set up a subtle, borderless debugging canvas in the top-left corner
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.35f); // Semi-transparent overlay background

        var windowFlags = ImGuiWindowFlags.NoDecoration |
                          ImGuiWindowFlags.AlwaysAutoResize |
                          ImGuiWindowFlags.NoSavedSettings |
                          ImGuiWindowFlags.NoFocusOnAppearing |
                          ImGuiWindowFlags.NoNav |
                          ImGuiWindowFlags.NoMove;

        if (ImGui.Begin("FPS Overlay", windowFlags))
        {
            // 2. Render text onto the active ImGui frame layer
            ImGui.TextColored(new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f), _fpsText); // Green FPS Text
            ImGui.End();
        }

        // 3. Command the controller wrapper to render structures to the backbuffer
        _imGuiController.Render();

    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _windowWidth = size.X;
        _windowHeight = size.Y;

        _multiSampledFrameBuffer.Resize((uint)(_windowWidth * _frameBufferScale), (uint)(_windowHeight * _frameBufferScale));

        _intermediateFrameBuffer.Resize((uint)(_windowWidth * _frameBufferScale), (uint)(_windowHeight * _frameBufferScale));
    }

    private void OnClose()
    {
        //_imGuiController?.Dispose();

        FramebufferObject.Default.Bind(false);

        _multiSampledFrameBuffer.Dispose();
        _intermediateFrameBuffer.Dispose();

        _vbo.Dispose();
        _vao.Dispose();

        //Gl.DeleteBuffer(Vbo);
        //Gl.DeleteVertexArray(Vao);

        _shader.Dispose();

        Gl?.Dispose();
    }

    #region Input Event Handlers

    private void KeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if (key == Key.Escape)
        {
            window.Close();
        }

        if (key == Key.S)
        {
            _intermediateFrameBuffer.SaveAsPng();
        }
    }
    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
    }

    // We no longer manipulate the camera directly in OnMouseMove!
    private void OnMouseMove(IMouse mouse, Vector2 _position)
    {
        // Leave this empty or use it for non-drag related features
    }


    private void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
    }

    #endregion Input Event Handlers

    /// <summary>
    /// </summary>
    /// <param name="deltaTime"></param>
    private void GenerateAndDrawVertexBufferChunkList(double deltaTime)
    {
        _vbo.Bind();

        ResetVertexBufferChunkList();

        LineChunkCount = RandomInt(MinLineChunkCount, MaxLineChunkCount);

        for (var i = 0; i < LineChunkCount; i++)
        {

            if (AddVertexBufferChunkToListAndBufferData(PrimitiveType.Lines, new[] {
                    // X Y Z                                                       R G B
                    RandomFloat(-1.0f, 1.0f),  RandomFloat(0.5f, 1.0f), 0.0f,      1.0f, 0.0f, 0.0f, 1.0f,
                    RandomFloat(-1.0f, 1.0f),  RandomFloat(-0.5f, -1.0f), 0.0f,    1.0f, 1.0f, 0.0f, 1.0f,
            }))
            {
                //Console.WriteLine($"Chunks flushed while adding lines");
            }
        }

        // ------------

        LineLoopChunkCount = RandomInt(MinLineLoopChunkCount, MaxLineLoopChunkCount);

        for (var k = 0; k < LineLoopChunkCount; k++)
        {
            var lineLoopVertexCount = RandomInt(3, 7);

            float[] vertices = new float[lineLoopVertexCount * VertexElementCount];

            var j = 0;

            for (var i = 0; i < lineLoopVertexCount; i++)
            {
                vertices[j++] = RandomFloat(-1.0f, 1.0f); // X
                vertices[j++] = RandomFloat(-1.0f, 1.0f); // Y
                vertices[j++] = 0; // Z

                vertices[j++] = 0f; // R
                vertices[j++] = RandomFloat(0.85f, 1.0f); // G
                vertices[j++] = 0f; // B
                vertices[j++] = 1f; // A
            }

            if (AddVertexBufferChunkToListAndBufferData(PrimitiveType.LineLoop, vertices))
            {
                //Console.WriteLine($"Chunks flushed while adding line loops");
            }
        }


        // ------------

        LineStripChunkCount = RandomInt(MinLineStripChunkCount, MaxLineStripChunkCount);

        for (var k = 0; k < LineStripChunkCount; k++)
        {
            var lineStripVertexCount = RandomInt(3, 7);

            var vertices = new float[lineStripVertexCount * VertexElementCount];

            var j = 0;

            for (var i = 0; i < lineStripVertexCount; i++)
            {
                vertices[j++] = RandomFloat(-1.0f, 1.0f); // X
                vertices[j++] = RandomFloat(-1.0f, 1.0f); // Y
                vertices[j++] = 0; // Z

                vertices[j++] = 0f; // R
                vertices[j++] = RandomFloat(0.50f, 1.0f); // G
                vertices[j++] = RandomFloat(0.85f, 1.0f); // B
                vertices[j++] = 1f; // A
            }

            if (AddVertexBufferChunkToListAndBufferData(PrimitiveType.LineStrip, vertices))
            {
                //Console.WriteLine($"Chunks flushed while adding line strips");
            }
        }

        DrawVertexBufferChunkList();
    }

    private unsafe bool AddVertexBufferChunkToListAndBufferData(PrimitiveType primitiveType, ReadOnlySpan<float> data)
    {
        if (data.Length % VertexElementCount != 0)
        {
            throw new ArgumentException($"Data length {data.Length} is not a multiple of VertexElementCount {VertexElementCount}.", nameof(data));
        }

        var flushed = false;

        //var bytes = chunk.Count * VertexElementByteSize;
        var bytes = (uint)(data.Length * sizeof(float));
        var count = (uint)(data.Length / VertexElementCount);

        //if (VertexBufferBytesUsedCount + bytes > VertexBufferByteTotalSize)
        if (_vbo.CanWrite(bytes))
        {
            //Console.WriteLine("Vertex buffer full. Orphaning current buffer and starting a new one.");

            DrawVertexBufferChunkList();

            _vbo.Orphan();

            ResetVertexBufferChunkList();

            flushed = true;
        }

        _vbo.Write(data);

        var chunk = new VertexBufferChunk()
        {
            //Index = (int)VertexBufferElementsUsedCount,
            Index = (int)_vbo.UsedElements,
            Count = count,
            PrimitiveType = primitiveType,
        };

        VertexBufferChunkList.Add(chunk);

        return flushed;
    }

    private unsafe void DrawVertexBufferChunkList()
    {
        _shader.Use();

        _vao.Bind();

        foreach (var vertexBufferChunk in VertexBufferChunkList)
        {
            _shader.SetUniform("uModel", vertexBufferChunk.Transform.ViewMatrix.ToSpan());

            //Draw the geometry.
            Gl.DrawArrays(vertexBufferChunk.PrimitiveType, vertexBufferChunk.Index, vertexBufferChunk.Count);
        }

        //if (PreviousVertexBufferBytesUsedCount != VertexBufferBytesUsedCount || PreviousVertexBufferElementsUsedCount != VertexBufferElementsUsedCount)
        //{
        //    PreviousVertexBufferBytesUsedCount = VertexBufferBytesUsedCount;
        //    PreviousVertexBufferElementsUsedCount = VertexBufferElementsUsedCount;

        //    //Console.WriteLine($"{VertexBufferBytesUsedCount} bytes used of {VertexBufferByteTotalSize} total bytes ({VertexBufferElementsUsedCount} elements)");
        //}
    }

    private void ResetVertexBufferChunkList()
    {
        _vbo.Clear();
        VertexBufferChunkList.Clear();
    }

    #region OpenGL Debug Functions

    private static unsafe void OnOpenGLDebugMessage(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam)
    {
        // Convert the raw native char pointer to a clean C# string
        string msgStr = Marshal.PtrToStringAnsi(message, length);

        Console.ForegroundColor = severity switch
        {
            GLEnum.DebugSeverityHigh => ConsoleColor.Red,
            GLEnum.DebugSeverityMedium => ConsoleColor.DarkYellow,
            GLEnum.DebugSeverityLow => ConsoleColor.Yellow,
            _ => ConsoleColor.Gray
        };

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [GL DEBUG] {severity} | Type: {type} | Source: {source} | ID: {id}");
        Console.WriteLine($"Message: {msgStr}\n");
        Console.ResetColor();
    }

    private void DebugMacOsOpenGlErrors()
    {
        GLEnum error;

        // Keep querying until the error queue is empty (returns ErrorCode.NoError)
        while ((error = Gl.GetError()) != GLEnum.NoError)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[macOS GL ERROR] Code triggered: {error}");
            Console.ResetColor();
        }
    }

    #endregion OpenGL Debug Functions

    #region Framebuffer Functions

    #endregion Framebuffer Functions

    #region Random Functions

    int RandomInt()
    {
        return RandomInt(0, int.MaxValue);
    }

    int RandomInt(int max)
    {
        return RandomInt(0, max);
    }

    int RandomInt(int min, int max)
    {
        return Random.Next(min, max);
    }

    float RandomFloat()
    {
        return RandomFloat(0f, 1f);
    }

    float RandomFloat(float max)
    {
        return RandomFloat(0f, max);
    }

    float RandomFloat(float min, float max)
    {
        return min + (max - min) * Random.NextSingle();
    }

    #endregion Random Functions
}