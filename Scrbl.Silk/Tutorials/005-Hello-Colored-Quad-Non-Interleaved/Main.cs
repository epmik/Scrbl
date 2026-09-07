using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Scrbl.Tutorials;

//public unsafe class NonInterleavedRenderer
//{
//    private GL _gl;
//    private uint _vao;
//    private uint _positionVbo;
//    private uint _colorVbo;

//    public void SetupMesh(GL gl)
//    {
//        _gl = gl;

//        // 1. Define distinct data sets (separate client-side arrays)
//        float[] positions = new float[]
//        {
//            // X,    Y,    Z
//             0.0f,  0.5f, 0.0f,  // Top vertex
//            -0.5f, -0.5f, 0.0f,  // Bottom-left vertex
//             0.5f, -0.5f, 0.0f   // Bottom-right vertex
//        };

//        float[] colors = new float[]
//        {
//            // R,    G,    B
//            1.0f, 0.0f, 0.0f,    // Red for top vertex
//            0.0f, 1.0f, 0.0f,    // Green for bottom-left vertex
//            0.0f, 0.0f, 1.0f     // Blue for bottom-right vertex
//        };

//        // 2. Generate and bind the Vertex Array Object (VAO)
//        _vao = _gl.GenVertexArray();
//        _gl.BindVertexArray(_vao);






//        // 5. Unbind to safe-keep state changes
//        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
//        _gl.BindVertexArray(0);
//    }

//    public void Render()
//    {
//        // When drawing, you only need to bind the single VAO.
//        // It remembers which buffer to grab for each attribute layout slot.
//        _gl.BindVertexArray(_vao);
//        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
//        _gl.BindVertexArray(0);
//    }

//    public void CleanUp()
//    {
//        _gl.DeleteBuffer(_positionVbo);
//        _gl.DeleteBuffer(_colorVbo);
//        _gl.DeleteVertexArray(_vao);
//    }
//}

class _005_Hello_Colored_Quad_Non_Interleaved
{
    private static IWindow window;
    private static GL Gl;

    private static uint VboPositions;
    private static uint VboColors;
    private static uint Ebo;
    private static uint Vao;
    private static uint Shader;

    //Vertex shaders are run on each vertex.
    private static readonly string VertexShaderSource = @"
        #version 330 core //Using version GLSL version 3.3
        layout (location = 0) in vec4 vPos;
        layout (location = 1) in vec3 vColor;

        out vec3 oColor; // output a color to the fragment shader

        void main()
        {
            gl_Position = vec4(vPos.x, vPos.y, vPos.z, 1.0);
            oColor = vColor;
        }
        ";

    //Fragment shaders are run on each fragment/pixel of the geometry.
    private static readonly string FragmentShaderSource = @"
        #version 330 core
        in vec3 oColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = vec4(oColor, 1.0f);
        }
        ";

    private const uint VertexElementCount = 6;  // 6 elements per vertex (3 for position, 3 for color)

    //Vertex data, uploaded to the VBO.
    private static readonly float[] Positions =
    {
        //X    Y      Z
         0.5f,  0.5f, 0.0f,      // top right vertex (black)
         0.5f, -0.5f, 0.0f,      // bottom right vertex (yellow)
        -0.5f, -0.5f, 0.0f,      // bottom left vertex (white)
        -0.5f,  0.5f, 0.5f,      // top left vertex (red)
    };
    private static readonly float[] Colors =
    {
        //R     G     B
        1.0f, 1.0f, 1.0f,      // top right vertex (white)
        1.0f, 1.0f, 0.0f,      // bottom right vertex (yellow)
        0.0f, 0.0f, 0.0f,      // bottom left vertex (black)
        1.0f, 0.0f, 0.0f,      // top left vertex (red)
    };

    //Index data, uploaded to the EBO.

    private static readonly uint[] Indices =
    {
        // counter clockwise winding order
        0, 3, 1,    // top right / bottom right / top left
        3, 2, 1,     // bottom right / bottom left / top left

        // clockwise winding order
        //0, 1, 3,    // right top / right bottom / left top 
        //3, 1, 2,    // right bottom / left top / left bottom 
    };


    public void Run(string[] args)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "LearnOpenGL with Silk.NET";
        window = Window.Create(options);

        window.Load += OnLoad;
        window.Render += OnRender;
        window.Update += OnUpdate;
        window.FramebufferResize += OnFramebufferResize;
        window.Closing += OnClose;

        window.Run();

        window.Dispose();
    }


    private static unsafe void OnLoad()
    {
        IInputContext input = window.CreateInput();
        for (int i = 0; i < input.Keyboards.Count; i++)
        {
            input.Keyboards[i].KeyDown += KeyDown;
        }

        //Getting the opengl api for drawing to the screen.
        Gl = GL.GetApi(window);

        //Creating a vertex array.
        Vao = Gl.GenVertexArray();
        Gl.BindVertexArray(Vao);

        //        // ==========================================
        //        // 3. SET UP POSITION BUFFER
        //        // ==========================================
        //        _positionVbo = _gl.GenBuffer();
        //        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _positionVbo);

        //        fixed (void* pData = positions)
        //        {
        //            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(positions.Length * sizeof(float)), pData, BufferUsageARB.StaticDraw);
        //        }

        //        // Configure attribute location 0 (Position)
        //        const uint positionLocation = 0;
        //        _gl.EnableVertexAttribArray(positionLocation);
        //        // Stride is 0 (or 3 * sizeof(float)): Tells OpenGL data is tightly packed
        //        // Pointer offset is 0: Starts at the very beginning of this buffer
        //        _gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 0, (void*)0);



        //Initializing a vertex buffer that holds the vertex data.
        VboPositions = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, VboPositions); //Binding the buffer.
        fixed (void* v = &Positions[0])
        {
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Positions.Length * sizeof(float)), v, BufferUsageARB.StaticDraw); //Setting buffer data.
        }

        //Tell opengl how to give the data to the shaders.
        // Stride is 0 (or 3 * sizeof(float)): Tells OpenGL data is tightly packed
        Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        Gl.EnableVertexAttribArray(0);


        //        // ==========================================
        //        // 4. SET UP COLOR BUFFER
        //        // ==========================================
        //        _colorVbo = _gl.GenBuffer();
        //        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _colorVbo);

        //        fixed (void* cData = colors)
        //        {
        //            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(colors.Length * sizeof(float)), cData, BufferUsageARB.StaticDraw);
        //        }

        //        // Configure attribute location 1 (Color)
        //        const uint colorLocation = 1;
        //        _gl.EnableVertexAttribArray(colorLocation);
        //        // Stride is 0: Tells OpenGL data is tightly packed
        //        // Pointer offset is 0: Starts at the beginning of THIS newly bound buffer
        //        _gl.VertexAttribPointer(colorLocation, 3, VertexAttribPointerType.Float, false, 0, (void*)0);

        VboColors = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, VboColors); //Binding the buffer.
        fixed (void* c = &Colors[0])
        {
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Colors.Length * sizeof(float)), c, BufferUsageARB.StaticDraw); //Setting buffer data.
        }

        //Tell opengl how to give the data to the shaders.
        // Stride is 0 (or 3 * sizeof(float)): Tells OpenGL data is tightly packed
        Gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        Gl.EnableVertexAttribArray(1);


        //Initializing a element buffer that holds the index data.
        Ebo = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo); //Binding the buffer.
        fixed (void* i = &Indices[0])
        {
            Gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw); //Setting buffer data.
        }

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

    private static unsafe void OnRender(double obj) //Method needs to be unsafe due to draw elements.
    {
        //Clear the color channel.
        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        //Bind the geometry and shader.
        Gl.BindVertexArray(Vao);
        Gl.UseProgram(Shader);

        //Draw the geometry.
        Gl.DrawElements(PrimitiveType.Triangles, (uint)Indices.Length, DrawElementsType.UnsignedInt, null);
    }

    private static void OnUpdate(double obj)
    {

    }

    private static void OnFramebufferResize(Vector2D<int> newSize)
    {
        Gl.Viewport(newSize);
    }

    private static void OnClose()
    {
        //Remember to delete the buffers.
        Gl.DeleteBuffer(VboPositions);
        Gl.DeleteBuffer(VboColors);
        Gl.DeleteBuffer(Ebo);
        Gl.DeleteVertexArray(Vao);
        Gl.DeleteProgram(Shader);
    }

    private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        if (arg2 == Key.Escape)
        {
            window.Close();
        }
    }
}