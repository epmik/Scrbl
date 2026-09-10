using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Scrbl.Tutorials;

class _016_Vao_Vbo_DrawArrays_Tryout
{
    //struct VertexBufferChunk
    //{
    //    public Transform Transform;

    //    public int Index;
    //    public uint Count;

    //    public PrimitiveType PrimitiveType;

    //    public VertexBufferChunk()
    //    {
    //        Transform = new Transform();
    //        PrimitiveType = PrimitiveType.Triangles;
    //    }
    //}

    // We must hold a reference to the delegate so the Garbage Collector doesn't destroy it
    //private static DebugProc _debugCallback;

    //private static ImGuiController _imGuiController;

    //// FPS Tracker State
    //private static int _frameCount = 0;
    //private static double _fpsTimer = 0.0;
    //private static string _fpsText = "FPS: ...";

    //int MinLineChunkCount = 24;
    //int MaxLineChunkCount = 68;

    //int MinLineLoopChunkCount = 2;

    //int MaxLineLoopChunkCount = 5;

    //int MinLineStripChunkCount = 2;

    //int MaxLineStripChunkCount = 5;

    //int LineChunkCount = 0;
    //int LineLoopChunkCount = 0;
    //int LineStripChunkCount = 0;

    Random random = new Random();

    private double NextUpdateTimeDelta = 0;
    private double NextUpdateTimeout = 4.0;    // 4 seconds

    private static IWindow window;
    private static GL Gl;

    private static uint Vbo1;
    private static uint Vbo2;
    private static uint Vao1;
    private static uint Vao2;
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

    //private uint VertexElementCount = (3 + 4); // X Y Z + R G B A

    //private uint VertexElementByteSize = (3 + 4) * sizeof(float); // X Y Z + R G B A

    private const uint VertexBufferElementSize = (3 + 4) * sizeof(float);

    private const uint VertexBufferTotalElementCount = 128;

    private const uint VertexBufferByteTotalSize = VertexBufferTotalElementCount * VertexBufferElementSize;

    //private uint VertexBufferBytesUsedCount = 0;

    //private uint VertexBufferElementsUsedCount = 0;

    //private List<VertexBufferChunk> VertexBufferChunkList = new List<VertexBufferChunk>();

    IInputContext input;

    public void Run(string[] args)
    {
        var options = WindowOptions.Default;

        options.Size = new Vector2D<int>(800, 600);
        options.Title = "_016_Vao_Vbo_DrawArrays_Tryout";
        options.VSync = false;

        bool isMac = OperatingSystem.IsMacOS();

        // Apple macOS Limitations: If you intend to run your application on macOS, do not target version 4.6.
        // Apple officially deprecated OpenGL and limits their native drivers strictly to OpenGL 4.1 Core Profile.
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,        
            ContextProfile.Core,     
            isMac
                ? ContextFlags.ForwardCompatible
                : ContextFlags.Debug,
            isMac
                ? new APIVersion(4, 1)
                : new APIVersion(4, 6)  
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
            Gl.DebugMessageCallback(OnOpenGLDebugMessage, null);

            // (Optional) Filter out less important notifications if your console gets spammy
            Gl.DebugMessageControl(DebugSource.DontCare, DebugType.DontCare, DebugSeverity.DebugSeverityNotification, 0, null, false);

            // Trigger a debug message to ensure the callback is working
            //Gl.Enable((EnableCap)9999);
        }

        //Creating a vertex array.
        Vao1 = Gl.GenVertexArray();
        Gl.BindVertexArray(Vao1);

        //Initializing a vertex buffer that holds the vertex data.
        Vbo1 = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo1); //Binding the buffer.
        Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)VertexBufferByteTotalSize, null, BufferUsageARB.DynamicDraw); //Setting buffer data.

        //Tell opengl how to give the data to the shaders.
        Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexBufferElementSize, null);
        Gl.EnableVertexAttribArray(0);

        Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, VertexBufferElementSize, (void*)(3 * sizeof(float)));
        Gl.EnableVertexAttribArray(1);



        Vao2 = Gl.GenVertexArray();
        Gl.BindVertexArray(Vao2);

        //Initializing a vertex buffer that holds the vertex data.
        Vbo2 = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo2); //Binding the buffer.
        Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)VertexBufferByteTotalSize, null, BufferUsageARB.DynamicDraw); //Setting buffer data.

        //Tell opengl how to give the data to the shaders.
        Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexBufferElementSize, null);
        Gl.EnableVertexAttribArray(0);

        Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, VertexBufferElementSize, (void*)(3 * sizeof(float)));
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
    }

    private unsafe void OnUpdate(double deltaTime)
    {
   
        if (OperatingSystem.IsMacOS())
        {
            DebugMacOsOpenGlErrors();
        }
    }

    private unsafe void OnRender(double deltaTime) //Method needs to be unsafe due to draw elements.
    {
        if (NextUpdateTimeDelta <= 0)
        {
            UpdateVertexBuffer(Vao1, Vbo1, deltaTime, new Vector4(1.0f, 0.0f, 0.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
            UpdateVertexBuffer(Vao2, Vbo2, deltaTime, new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f));

            NextUpdateTimeDelta += NextUpdateTimeout;
        }

        NextUpdateTimeDelta -= deltaTime;

        //Clear the color channel.
        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);


        // works correctly
        DrawVertexBuffer(Vao1, Vbo1);
        DrawVertexBuffer(Vao2, Vbo2);


        // works correctly
        DrawVertexBuffer(Vao1, Vbo2);
        DrawVertexBuffer(Vao2, Vbo1);

        // draws only Vbo1
        //DrawVertexBuffer(Vao1, Vbo2);
        //DrawVertexBuffer(Vao1, Vbo1);

        // draws only Vbo2
        //DrawVertexBuffer(Vao2, Vbo2);
        //DrawVertexBuffer(Vao2, Vbo1);
    }

    /// <summary>
    /// </summary>
    /// <param name="deltaTime"></param>
    private unsafe void UpdateVertexBuffer(uint vao, uint vbo, double deltaTime, Vector4 colorA, Vector4 colorB)
    {
        // No need to bind the VAO here since we are only updating the VBO data.
        //Gl.BindVertexArray(vao);

        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        uint offset = 0;

        for (var i = 0; i < VertexBufferTotalElementCount; i += 2)
        {

            var data = new float[]
            { 
                    // X Y Z                                                       R G B
                    RandomFloat(-1.0f, 1.0f),  RandomFloat(0.5f, 1.0f), 0.0f,      colorA.X, colorA.Y, colorA.Z, colorA.W,
                    RandomFloat(-1.0f, 1.0f),  RandomFloat(-0.5f, -1.0f), 0.0f,    colorB.X, colorB.Y, colorB.Z, colorB.W,
            };


            fixed (void* ptr = data)
            {
                Gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)offset, (nuint)(VertexBufferElementSize + VertexBufferElementSize), ptr);
            }

            offset += VertexBufferElementSize + VertexBufferElementSize;

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
        Gl.DeleteBuffer(Vbo1);
        Gl.DeleteBuffer(Vbo2);
        Gl.DeleteVertexArray(Vao1);
        Gl.DeleteVertexArray(Vao2);
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

    private unsafe void DrawVertexBuffer(uint vao, uint vbo)
    {
        Gl.UseProgram(Shader);

        Gl.BindVertexArray(vao);

        //Gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        int location = Gl.GetUniformLocation(Shader, "uModel");

        var t = Matrix4x4.Identity;

        Gl.UniformMatrix4(location, 1, false, (float*)&t);

        //Draw the geometry.
        Gl.DrawArrays(PrimitiveType.Lines, 0, VertexBufferTotalElementCount);
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