using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Scrbl.Tutorials;

class _012_Static_Lines
{
    private static IWindow window;
    private static GL Gl;

    private static uint Vbo;
    private static uint Vao;
    private static uint Shader;

    //Vertex shaders are run on each vertex.
    private static readonly string VertexShaderSource = @"
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
    private static readonly string FragmentShaderSource = @"
        #version 330 core

        in vec4 fCol;

        out vec4 FragColor;

        void main()
        {
            FragColor = fCol;
        }
        ";

    //Vertex data, uploaded to the VBO.
    private static readonly float[] Vertices =
    {
            //X Y Z                 // R G B A
            // line
             0.0f,  0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
             0.0f, -0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,


            // line strip
             0.0f,  0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
             0.0f, -0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,

            // line loop
             0.0f,  0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, 0.0f,     1.0f, 1.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.0f,     1.0f, 0.0f, 0.0f, 1.0f,
        };

    ////Index data, uploaded to the EBO.
    //private static readonly uint[] Indices =
    //{
    //        0, 1, 3,
    //        1, 2, 3
    //    };


    public void Run(string[] args)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "_012_Static_Lines";
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

        //Initializing a vertex buffer that holds the vertex data.
        Vbo = Gl.GenBuffer(); //Creating the buffer.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo); //Binding the buffer.
        fixed (void* v = &Vertices[0])
        {
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Vertices.Length * sizeof(uint)), v, BufferUsageARB.StaticDraw); //Setting buffer data.
        }


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
    }

    private static unsafe void OnRender(double obj) //Method needs to be unsafe due to draw elements.
    {
        //Clear the color channel.
        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        //Bind the geometry and shader.
        Gl.BindVertexArray(Vao);
        Gl.UseProgram(Shader);

        var i = Matrix4x4.Identity * Matrix4x4.CreateTranslation(0.75f, 0f, 0f);

        int location = Gl.GetUniformLocation(Shader, "uModel");

        Gl.UniformMatrix4(location, 1, false, (float*)&i);

        //Draw the geometry.
        Gl.DrawArrays(PrimitiveType.Lines, 0, 2);


        i = Matrix4x4.Identity * Matrix4x4.CreateTranslation(-0.25f, 0f, 0f);


        Gl.UniformMatrix4(location, 1, false, (float*)&i);

        //Draw the geometry.
        Gl.DrawArrays(PrimitiveType.LineStrip, 2, 4);


        i = Matrix4x4.Identity * Matrix4x4.CreateTranslation(0.0f, 0.25f, 0f);


        Gl.UniformMatrix4(location, 1, false, (float*)&i);

        //Draw the geometry.
        Gl.DrawArrays(PrimitiveType.LineLoop, 6, 3);
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
        Gl.DeleteBuffer(Vbo);
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