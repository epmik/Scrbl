using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Scrbl.Tutorials;

class _014_Dynamic_Lines_Orphaning_Buffer
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
    private static DebugProc _debugCallback;

    private static ImGuiController _imGuiController;

    // FPS Tracker State
    private static int _frameCount = 0;
    private static double _fpsTimer = 0.0;
    private static string _fpsText = "FPS: ...";

    int MinLineChunkCount = 4;
    int MaxLineChunkCount = 6;

    int LineChunkCount = 0;

    Random random = new Random();

    private double NextUpdateTimeDelta = 0;
    private double NextUpdateTimeout = 4.0;    // 4 seconds

    private static IWindow window;
    private static GL Gl;

    private static uint Vbo;
    private static uint Vao;
    private static uint Shader;


    //Vertex shaders are run on each vertex.
    private readonly string VertexShaderSource = @"
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
    private readonly string FragmentShaderSource = @"
        #version 330 core

        in vec4 fCol;

        out vec4 FragColor;

        void main()
        {
            FragColor = fCol;
        }
        ";

    ////Vertex data, uploaded to the VBO.
    //private readonly float[] Vertices =
    //{
    //    //X Y Z                 // R G B A
    //    // line
    //    0.0f,  0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
    //    0.0f, -0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,


    //    // line strip
    //    0.0f,  0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
    //    0.0f, -0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,
    //    -0.5f, -0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
    //    -0.5f,  0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,

    //    // line loop
    //    0.0f,  0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
    //    0.5f, -0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,
    //    -0.5f, -0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
    //};

    private uint VertexElementCount = (3 + 4); // X Y Z + R G B A
    private uint VertexElementByteSize = (3 + 4) * sizeof(float); // X Y Z + R G B A

    private uint VertexBufferByteTotalSize = 1024 * (3 + 4) * sizeof(float);
    private uint VertexBufferBytesUsedCount = 0;

    private List<VertexBufferChunk> VertexBufferChunkList = new List<VertexBufferChunk>();

    IInputContext input;

    public void Run(string[] args)
    {
        var options = WindowOptions.Default;

        options.Size = new Vector2D<int>(800, 600);
        options.Title = "_014_Dynamic_Lines_Orphaning_Buffer";
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
        Gl = GL.GetApi(window);

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
            _debugCallback = OnOpenGLDebugMessage;
            Gl.DebugMessageCallback(_debugCallback, null);

            // (Optional) Filter out less important notifications if your console gets spammy
            Gl.DebugMessageControl(DebugSource.DontCare, DebugType.DontCare, DebugSeverity.DebugSeverityNotification, 0, null, false);
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

    }

    private unsafe void OnRender(double timeDelta) //Method needs to be unsafe due to draw elements.
    {
        //Clear the color channel.
        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        //Bind the geometry and shader.
        Gl.BindVertexArray(Vao);
        Gl.UseProgram(Shader);

        FlushVertexBufferChunkList();

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

        if (NextUpdateTimeDelta <= 0)
        {
            VertexBufferChunk chunk;

            VertexBufferChunkList.Clear();
            VertexBufferBytesUsedCount = 0;

            LineChunkCount = RandomInt(MinLineChunkCount, MaxLineChunkCount);

            var vertexBufferIndex = 0;

            for (var i = 0; i < LineChunkCount; i++)
            {
                chunk = new VertexBufferChunk()
                {
                    Index = vertexBufferIndex,
                    Count = 2,
                    PrimitiveType = PrimitiveType.Lines,
                };

                AddVertexBufferChunkToListAndConditionalyFlush(chunk, new[]
                {
                     // X Y Z                                                       R G B
                     RandomFloat(-1.0f, 1.0f),  RandomFloat(0.5f, 1.0f), 0.0f,      1.0f, 0.0f, 0.0f, 1.0f,
                     RandomFloat(-1.0f, 1.0f),  RandomFloat(-0.5f, -1.0f), 0.0f,    1.0f, 1.0f, 0.0f, 1.0f,
                });

                vertexBufferIndex += (int)chunk.Count;
            }

            // ------------

            var lineLoopVertexCount = RandomInt(3, 7);

            chunk = new VertexBufferChunk()
            {
                Index = vertexBufferIndex,
                Count = (uint)lineLoopVertexCount,
                PrimitiveType = PrimitiveType.LineLoop,
            };

            VertexBufferChunkList.Add(chunk);

            vertexBufferIndex += lineLoopVertexCount;

            for (var i = 0; i < lineLoopVertexCount; i++)
            {
                AddVertexBufferChunkToListAndConditionalyFlush(chunk, new[]
                {
                     // X Y Z                                                       R G B
                     RandomFloat(-1.0f, 1.0f),  RandomFloat(1.0f, -1.0f), 0.0f,     0.0f, 1.0f, 0.0f, 1.0f,
                });
            }


            // ------------

            var lineStripVertexCount = RandomInt(3, 7);

            chunk = new VertexBufferChunk()
            {
                Index = vertexBufferIndex,
                Count = (uint)lineStripVertexCount,
                PrimitiveType = PrimitiveType.LineStrip,
            };

            float[] vertices = new float[lineStripVertexCount * VertexElementCount];

            var j = 0;

            for (var i = 0; i < lineStripVertexCount; i++)
            {
                vertices[j++] = RandomFloat(-1.0f, 1.0f);
                vertices[j++] = RandomFloat(1.0f, -1.0f);
                vertices[j++] = 0.0f;
                vertices[j++] = 0.0f;
                vertices[j++] = 1.0f;
                vertices[j++] = 0.0f;
            }

            AddVertexBufferChunkToListAndConditionalyFlush(chunk, vertices);

            // ------------

            NextUpdateTimeDelta += NextUpdateTimeout;
        }

        NextUpdateTimeDelta -= deltaTime;

        if (OperatingSystem.IsMacOS())
        {
            DebugMacOsOpenGlErrors();
        }
    }

    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        Gl.Viewport(newSize);
    }

    private void OnClose()
    {
        //_imGuiController?.Dispose();

        //Remember to delete the buffers.
        Gl.DeleteBuffer(Vbo);
        Gl.DeleteVertexArray(Vao);
        Gl.DeleteProgram(Shader);
        Gl?.Dispose();
    }

    private void KeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        if (arg2 == Key.Escape)
        {
            window.Close();
        }
    }


    private unsafe void FlushVertexBufferChunkList()
    {
        int location = Gl.GetUniformLocation(Shader, "uModel");

        foreach (var vertexBufferChunk in VertexBufferChunkList)
        {
            var t = vertexBufferChunk.Transform.ViewMatrix;

            Gl.UniformMatrix4(location, 1, false, (float*)&t);

            //Draw the geometry.
            Gl.DrawArrays(vertexBufferChunk.PrimitiveType, vertexBufferChunk.Index, vertexBufferChunk.Count);
        }
    }

    private unsafe void AddVertexBufferChunkToListAndConditionalyFlush(VertexBufferChunk chunk, ReadOnlySpan<float> data)
    {
        var bytes = chunk.Count * VertexElementByteSize;

        if (VertexBufferBytesUsedCount + bytes > VertexBufferByteTotalSize)
        {
            FlushVertexBufferChunkList();

            VertexBufferBytesUsedCount = 0;
            VertexBufferChunkList.Clear();
        }

        fixed (void* ptr = data)
        {
            Gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)VertexBufferBytesUsedCount, (nuint)bytes, ptr);
        }

        VertexBufferBytesUsedCount += bytes;
    }


    //private unsafe void AddBufferData(ReadOnlySpan<float> data)
    //{
    //    int incomingBytes = data.Length * sizeof(float);

    //    var vertexCount = VertexBufferBytesUsedCount / VertexElementByteSize;

    //    if (VertexBufferBytesUsedCount + incomingBytes > VertexBufferByteTotalSize)
    //    {
    //        // VBO full: Draw what we have, then wrap back to 0. Do NOT orphan here!
    //        FlushVertexBuffer((uint)vertexCount);

    //        VertexBufferBytesUsedCount = 0;
    //    }

    //    fixed (void* ptr = data)
    //    {
    //        Gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)VertexBufferBytesUsedCount, (nuint)incomingBytes, ptr);
    //    }

    //    VertexBufferBytesUsedCount += incomingBytes;
    //}

    //private unsafe void FlushVertexBuffer(int vertexOffset, uint vertexCount)
    //{
    //    if (vertexCount > 0)
    //    {
    //        Gl.DrawArrays(PrimitiveType.Points, vertexOffset, vertexCount);
    //    }
    //}

    //private unsafe void FlushVertexBuffer(uint vertexCount)
    //{
    //    FlushVertexBuffer(0, (uint)vertexCount);
    //}

    //private unsafe void FlushVertexBuffer()
    //{
    //    FlushVertexBuffer(0, (uint)VertexBufferBytesUsedCount / VertexElementByteSize);
    //}

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

        Console.WriteLine($"[GL DEBUG] {severity} | Type: {type} | Source: {source} | ID: {id}");
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
        return random.Next(min, max);
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
        return min + (max - min) * random.NextSingle();
    }
}