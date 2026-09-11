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

class _017_Frame_Buffer_Struct
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

    double NextUpdateTimeDelta = 4.0;
    double NextUpdateTimeout = 4.0;    // 4 seconds

    IWindow window;
    GL Gl;
    GL GL;

    uint Vbo;


    /// <summary>
    /// On VAO's: https://stackoverflow.com/a/26559063/527843
    /// The key thing to understand is that a VAO is a collection of state. It does not own any data. It's VBOs that own vertex data. 
    /// A VAO, on the other hand, contains all the state used to describe where a draw call gets its vertex attributes from.
    /// </summary>
    static uint Vao;
    static uint Shader;
    int _windowWidth = 800;
    int _windowHeight = 600;

    double _frameBufferScale = 4.0;

    struct FrameBuffer
    {
        public uint Width;
        public uint Height;
        public uint Fbo;
        public uint FboColorAttachment;
        public uint FboDepthAttachment;
        public uint FboStencilAttachment;
        public uint Samples;
        public uint FboColorBits;
        public uint FboDepthBits;
        public uint FboStencilBits;

        public FrameBuffer()
        {
            FboColorBits = 32;
            FboDepthBits = 32;
        }
    }

    FrameBuffer _frameBuffer = new FrameBuffer();

    //uint _frameBufferWidth;
    //uint _frameBufferHeight;

    //uint _frameBufferHandle;
    //uint _frameBufferColorAttachmentHandle;
    //uint _frameBufferDepthAttachmentHandle;
    //uint _frameBufferStencilAttachmentHandle;

    //uint _frameBufferStencilBits = 0;
    //uint _frameBufferColorBits = 32;
    //uint _frameBufferDepthBits = 32;
    //uint _frameBufferSamples = 0;

    //Vertex shaders are run on each vertex.
    readonly string VertexShaderSource = @"
        #version 330 core //Using version GLSL version 3.3
        
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;
    
        uniform mat4 uModel;    

        out vec4 fCol;

        void main()
        {
            //gl_Position =  vec4(vPos, 1.0);
            gl_Position =  uModel * vec4(vPos, 1.0);
            fCol = vCol;
        }
        ";

    //Fragment shaders are run on each fragment/pixel of the geometry.
    readonly string FragmentShaderSource = @"
        #version 330 core

        in vec4 fCol;

        out vec4 FragColor;

        void main()
        {
            FragColor = fCol;
        }
        ";

    const uint VertexElementCount = (3 + 4); // X Y Z + R G B A

    const uint VertexElementByteSize = VertexElementCount * sizeof(float);

    const uint VertexBufferByteTotalSize = 128 * VertexElementByteSize;

    uint VertexBufferBytesUsedCount = 0;
    uint PreviousVertexBufferBytesUsedCount;

    uint VertexBufferElementsUsedCount = 0;
     uint PreviousVertexBufferElementsUsedCount = 0;

    List<VertexBufferChunk> VertexBufferChunkList = new List<VertexBufferChunk>();

    IInputContext input;

    public _017_Frame_Buffer_Struct()
    {
        Random = new Random(RandomSeed);

        _frameBuffer.Width = (uint)(_windowWidth * _frameBufferScale);
        _frameBuffer.Height = (uint)(_windowHeight * _frameBufferScale);
    }

    public void Run(string[] args)
    {
        var options = WindowOptions.Default;

        options.Size = new Vector2D<int>(_windowWidth, _windowHeight);
        options.Title = "_017_Frame_Buffer_Struct";
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
        input = window.CreateInput();

        for (int i = 0; i < input.Keyboards.Count; i++)
        {
            input.Keyboards[i].KeyDown += KeyDown;
        }

        //Getting the opengl api for drawing to the screen.
        GL = Gl = GL.GetApi(window);

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

        //Creating a vertex array.
        Vao = Gl.GenVertexArray();
        Gl.BindVertexArray(Vao);


        //Initializing a vertex buffer that holds the vertex data.
        Vbo = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo); //Binding the buffer.
        Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)VertexBufferByteTotalSize, null, BufferUsageARB.DynamicDraw); //Setting buffer data.

        //Tell opengl how to give the data to the shaders.
        Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), null);
        Gl.EnableVertexAttribArray(0);

        Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        Gl.EnableVertexAttribArray(1);



        //Creating a vertex shader.
        uint vertexShader = Gl.CreateShader(ShaderType.VertexShader);
        Gl.ShaderSource(vertexShader, VertexShaderSource);
        Gl.CompileShader(vertexShader);

        //Checking the shader for compilation errors.
        string infoLog = Gl.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Error compiling vertex shader {infoLog}");
        }

        //Creating a fragment shader.
        uint fragmentShader = Gl.CreateShader(ShaderType.FragmentShader);
        Gl.ShaderSource(fragmentShader, FragmentShaderSource);
        Gl.CompileShader(fragmentShader);

        //Checking the shader for compilation errors.
        infoLog = Gl.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Error compiling fragment shader {infoLog}");
        }

        //Combining the shaders under one shader program.
        Shader = Gl.CreateProgram();
        Gl.AttachShader(Shader, vertexShader);
        Gl.AttachShader(Shader, fragmentShader);
        Gl.LinkProgram(Shader);

        //Checking the linking for errors.
        Gl.GetProgram(Shader, GLEnum.LinkStatus, out var status);
        if (status == 0)
        {
            Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(Shader)}");
        }

        //Delete the no longer useful individual shaders;
        Gl.DetachShader(Shader, vertexShader);
        Gl.DetachShader(Shader, fragmentShader);
        Gl.DeleteShader(vertexShader);
        Gl.DeleteShader(fragmentShader);

        _imGuiController = new ImGuiController(Gl, window, input);

        #region Setup Framebuffer

        SetupFramebuffer(ref _frameBuffer);

        Console.WriteLine($"Framebuffer status: {FramebufferStatus(_frameBuffer)}");

        #endregion Setup Framebuffer

        BindScreenFramebuffer();

    }

    private unsafe void SetupFramebuffer(ref FrameBuffer frameBuffer)
    {
        frameBuffer.Fbo = GL.GenFramebuffer();

        BindFramebuffer(frameBuffer);

        if (frameBuffer.FboColorBits > 0)
        {
            SetupFramebufferColorAttachment(ref frameBuffer);
        }

        if (frameBuffer.FboDepthBits > 0)
        {
            SetupFramebufferDepthAttachment(ref frameBuffer);
        }

        if (frameBuffer.FboStencilBits > 0)
        {
            SetupFramebufferStencilAttachment(ref frameBuffer);
        }

    }

    private unsafe void SetupFramebufferColorAttachment(ref FrameBuffer frameBuffer)
    {
        var textureTarget = FramebufferTextureTarget(frameBuffer);

        frameBuffer.FboColorAttachment = GL.GenTexture();

        GL.BindTexture(textureTarget, frameBuffer.FboColorAttachment);

        if (frameBuffer.Samples <= 1)
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, frameBuffer.Width, frameBuffer.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }
        else
        {
            GL.TexImage2DMultisample(TextureTarget.Texture2DMultisample, frameBuffer.Samples, GLEnum.Rgba8, frameBuffer.Width, frameBuffer.Height, false);
        }

        GL.TexParameter(textureTarget, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(textureTarget, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);

        GL.TexParameter(textureTarget, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(textureTarget, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        //GL.BindTexture(FramebufferTextureTarget(), 0);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, textureTarget, frameBuffer.FboColorAttachment, 0);
    }

    private unsafe void SetupFramebufferDepthAttachment(ref FrameBuffer frameBuffer)
    {
        frameBuffer.FboDepthAttachment = GL.GenRenderbuffer();

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, frameBuffer.FboDepthAttachment);

        if (frameBuffer.Samples <= 1)
        {
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, FrameBufferDepthComponent(frameBuffer), frameBuffer.Width, frameBuffer.Height);
        }
        else
        {
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, frameBuffer.Samples, FrameBufferDepthComponent(frameBuffer), frameBuffer.Width, frameBuffer.Height);
        }
    }

    private unsafe void SetupFramebufferStencilAttachment(ref FrameBuffer frameBuffer)
    {
        frameBuffer.FboStencilAttachment = GL.GenRenderbuffer();

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, frameBuffer.FboStencilAttachment);

        if (frameBuffer.Samples <= 1)
        {
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, FrameBufferDepthStencil(frameBuffer), frameBuffer.Width, frameBuffer.Height);
        }
        else
        {
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, frameBuffer.Samples, FrameBufferDepthStencil(frameBuffer), frameBuffer.Width, frameBuffer.Height);
        }
    }

    private unsafe void OnUpdate(double deltaTime)
    {
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

        BindFramebuffer(_frameBuffer);

        Gl.ClearColor(0.1f, 0.1f, 0.1f, 0.0f);

        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        GenerateAndDrawVertexBufferChunkList(deltaTime);

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

        BlitFramebufferToScreen(Gl, _frameBuffer.Fbo, (int)_frameBuffer.Width, (int)_frameBuffer.Height);
    }

    /// <summary>
    /// </summary>
    /// <param name="deltaTime"></param>
    private void GenerateAndDrawVertexBufferChunkList(double deltaTime)
    {
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

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _windowWidth = size.X;
        _windowHeight = size.Y;

        BindScreenFramebuffer();

        Gl.Viewport(size);

        _frameBuffer.Width = (uint)(_windowWidth * _frameBufferScale);
        _frameBuffer.Height = (uint)(_windowHeight * _frameBufferScale);

        ResizeFrameBuffer(ref _frameBuffer);
    }

    private void OnClose()
    {
        //_imGuiController?.Dispose();

        BindScreenFramebuffer();

        DisposeFramebuffer(_frameBuffer);

        Gl.DeleteBuffer(Vbo);
        Gl.DeleteVertexArray(Vao);
        Gl.DeleteProgram(Shader);
        Gl?.Dispose();
    }

    private void KeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if (key == Key.Escape)
        {
            window.Close();
        }

        if (key == Key.S)
        {
            BindFramebuffer(_frameBuffer);

            SaveFramebuffer(Gl, (int)_frameBuffer.Width, (int)_frameBuffer.Height, $"framebuffer_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            BindScreenFramebuffer();
        }
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

        if (VertexBufferBytesUsedCount + bytes > VertexBufferByteTotalSize)
        {
            //Console.WriteLine("Vertex buffer full. Orphaning current buffer and starting a new one.");

            DrawVertexBufferChunkList();

            OrphanVertexBuffer();

            ResetVertexBufferChunkList();

            flushed = true;     
        }


        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);

        fixed (void* ptr = data)
        {
            Gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)VertexBufferBytesUsedCount, (nuint)bytes, ptr);
        }

        var chunk = new VertexBufferChunk()
        {
            Index = (int)VertexBufferElementsUsedCount,
            Count = count,
            PrimitiveType = primitiveType,
        };

        VertexBufferChunkList.Add(chunk);

        VertexBufferBytesUsedCount += bytes;
        VertexBufferElementsUsedCount += count;

        return flushed;
    }

    private unsafe void DrawVertexBufferChunkList()
    {
        Gl.UseProgram(Shader);

        Gl.BindVertexArray(Vao);

        //Gl.BindBuffer(BufferTargetARB.ArrayBuffer, ActiveVbo);

        int location = Gl.GetUniformLocation(Shader, "uModel");

        foreach (var vertexBufferChunk in VertexBufferChunkList)
        {
            var t = vertexBufferChunk.Transform.ViewMatrix;

            Gl.UniformMatrix4(location, 1, false, (float*)&t);

            //Draw the geometry.
            Gl.DrawArrays(vertexBufferChunk.PrimitiveType, vertexBufferChunk.Index, vertexBufferChunk.Count);
        }

        if (PreviousVertexBufferBytesUsedCount != VertexBufferBytesUsedCount || PreviousVertexBufferElementsUsedCount != VertexBufferElementsUsedCount)
        {
            PreviousVertexBufferBytesUsedCount = VertexBufferBytesUsedCount;
            PreviousVertexBufferElementsUsedCount = VertexBufferElementsUsedCount;

            //Console.WriteLine($"{VertexBufferBytesUsedCount} bytes used of {VertexBufferByteTotalSize} total bytes ({VertexBufferElementsUsedCount} elements)");
        }
    }

    private unsafe void OrphanVertexBuffer()
    {
        // buffer orphaning is a performance optimization technique used to avoid CPU-GPU synchronization stalls when updating data in a Buffer Object (like a VBO or UBO).
        // It works by telling the OpenGL driver to abandon the current memory allocation under the hood and replace it with a brand-new, clean block of memory.
        // This allows the GPU to continue using the old buffer while we prepare a new one.
        // we request a new memory buffer by calling glBufferData with the same size and usage flag and a null pointer for the data.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)VertexBufferByteTotalSize, null, BufferUsageARB.DynamicDraw);

        // TODO: it might be quicker to use Persistent Mapped Buffers https://www.cppstories.com/2015/01/persistent-mapped-buffers-in-opengl/
    }

    private void ResetVertexBufferChunkList()
    {
        VertexBufferBytesUsedCount = 0;
        VertexBufferElementsUsedCount = 0;
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

    void ResizeFrameBuffer(ref FrameBuffer frameBuffer)
    {
        BindFramebuffer(frameBuffer, false);

        // If you are using modern immutable textures (gl.TexStorage2D instead of gl.TexImage2D), deleting and regenerating the texture object
        // is absolutely required, because immutable texture memory sizing cannot be reallocated or overwritten.
        DisposeFramebufferAttachments(frameBuffer);

        if (frameBuffer.FboColorBits > 0)
        {
            SetupFramebufferColorAttachment(ref frameBuffer);
        }

        if (frameBuffer.FboDepthBits > 0)
        {
            SetupFramebufferDepthAttachment(ref frameBuffer);
        }

        if (frameBuffer.FboStencilBits > 0)
        {
            SetupFramebufferStencilAttachment(ref frameBuffer);
        }

        Console.WriteLine($"Resized Framebuffer status: {FramebufferStatus(frameBuffer)}");

        GL.Viewport(0, 0, frameBuffer.Width, frameBuffer.Height);
    }

    //void BindFrameBufferViewport(FrameBuffer frameBuffer)
    //{
    //    GL.Viewport(0, 0, frameBuffer.Width, frameBuffer.Height);
    //}

    GLEnum FrameBufferDepthComponent(FrameBuffer frameBuffer)
    {
        switch (frameBuffer.FboDepthBits)
        {
            case 16:
                return GLEnum.DepthComponent16;
            case 24:
                return GLEnum.DepthComponent24;
            case 32:
                return GLEnum.DepthComponent32;
            default:
                throw new NotImplementedException($"RenderbufferStorageDepthComponent() failed. found unknown DepthBits: {frameBuffer.FboDepthBits}");
        }
    }

    GLEnum FrameBufferDepthStencil(FrameBuffer frameBuffer)
    {
        switch (frameBuffer.FboStencilBits)
        {
            case 24:
                return GLEnum.Depth24Stencil8;
            case 32:
                return GLEnum.Depth32fStencil8;
            default:
                throw new NotImplementedException($"RenderbufferStorageStencil() failed. found unknown StencilBits: {frameBuffer.FboStencilBits}");
        }
    }

    TextureTarget FramebufferTextureTarget(FrameBuffer frameBuffer)
    {
        return frameBuffer.Samples < 2 ? TextureTarget.Texture2D : TextureTarget.Texture2DMultisample;
    }

    GLEnum FramebufferStatus(FrameBuffer frameBuffer)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer.Fbo);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

        return status;
    }

    void BindFramebuffer(FrameBuffer frameBuffer, bool bindViewport = true)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer.Fbo);

        if (bindViewport)
        {
            GL.Viewport(0, 0, frameBuffer.Width, frameBuffer.Height);
        }
    }

    /// <summary>
    /// Make sure the framebuffer is unbound (bind the screen framebuffer) before calling this function, otherwise you will get an OpenGL error.
    /// </summary>
    void DisposeFramebuffer(FrameBuffer frameBuffer)
    {
        DisposeFramebufferAttachments(frameBuffer);

        GL.DeleteFramebuffer(frameBuffer.Fbo);
    }

    void DisposeFramebufferAttachments(FrameBuffer frameBuffer)
    {
        if (frameBuffer.FboColorAttachment != 0)
        {
            GL.DeleteTexture(frameBuffer.FboColorAttachment);
        }

        if (frameBuffer.FboDepthAttachment != 0)
        {
            GL.DeleteRenderbuffer(frameBuffer.FboDepthAttachment);
        }

        if (frameBuffer.FboStencilAttachment != 0)
        {
            GL.DeleteRenderbuffer(frameBuffer.FboStencilAttachment);
        }
    }

    public void BindScreenFramebuffer()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public static void BlitFramebuffer(
        GL gl,
        uint sourceHandle,
        uint targetHandle,
        int sourceWidth, int sourceHeight,
        int targetWidth, int targetHeight)
    {
        // 1. Bind the offscreen FBO as the READ source
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, sourceHandle);

        // 2. Bind the screen (0) as the DRAW destination
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetHandle);

        // 3. Perform the blit with scaling
        gl.BlitFramebuffer(
            0, 0, sourceWidth, sourceHeight,  // Source rectangle (Bottom-Left to Top-Right)
            0, 0, targetWidth, targetHeight,        // Destination rectangle (Bottom-Left to Top-Right)
            ClearBufferMask.ColorBufferBit,         // Mask specifying which buffers to copy
            BlitFramebufferFilter.Linear            // Filter for scaling (Linear or Nearest)
        );

        // 4. Unbind/Restore default target state (optional but good practice)
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void BlitFramebufferToScreen(
        GL gl,
        uint sourceHandle,
        int width, int height)
    {
        BlitFramebuffer(gl, sourceHandle, 0, width, height, _windowWidth, _windowHeight);
    }

    public static void BlitFramebuffer(
        GL gl,
        uint sourceHandle,
        uint targetHandle,
        int width, int height)
    {
        BlitFramebuffer(gl, sourceHandle, targetHandle, width, height, width, height);
    }

    public static void SaveFramebuffer(GL gl, int width, int height)
    {
        SaveFramebuffer(gl, width, height, string.Empty);
    }

    public static void SaveFramebuffer(GL gl, int width, int height, string filePath)
    {
        if(string.IsNullOrEmpty(filePath))
        {
            filePath = $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        }

        // 1. Allocate an array to hold the pixel data (4 bytes per pixel for RGBA)
        byte[] pixelData = new byte[width * height * 4];

        // 2. Ensure alignment matches byte boundaries
        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

        // 3. Read the pixels from the currently bound framebuffer
        unsafe
        {
            fixed (byte* ptr = pixelData)
            {
                gl.ReadPixels(
                    0, 0,
                    (uint)width, (uint)height,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr
                );
            }
        }

        // 4. Load the raw bytes directly into an ImageSharp image instance
        using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pixelData, width, height))
        {
            // 5. OpenGL maps (0,0) to the bottom-left corner, so the image will be upside down.
            // We must flip it vertically to look correct.
            image.Mutate(x => x.Flip(FlipMode.Vertical));

            // 6. Save the processed image to your chosen file path
            image.SaveAsPng(filePath); // Or use a Stream depending on your application
        }
    }


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