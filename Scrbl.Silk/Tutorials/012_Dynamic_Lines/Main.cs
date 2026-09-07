using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Color = System.Drawing.Color;

namespace Scrbl.Tutorials;

class _012_Dynamic_Lines
{
    enum BufferEntryType
    {
        Lines = PrimitiveType.Lines,
        LineLoop = PrimitiveType.LineLoop,
        LineStrip = PrimitiveType.LineStrip,

        Triangles = PrimitiveType.Triangles,

        TriangleFan = PrimitiveType.TriangleFan,

        TriangleStrip = PrimitiveType.TriangleStrip,
    }

    struct BufferEntry
    {
        public uint Index;
        public uint Count;

        public BufferEntryType Type;

        public BufferEntry()
        {
            Type = BufferEntryType.Triangles;
        }
    }


    private static IWindow _window;
    private static GL _gl;

    private static uint _vao;
    private static uint _vbo;
    private static uint _ebo;

    private static uint _program;

    private static uint _texture;

    private GLEnum FrontFace = GLEnum.Ccw;

    private GLEnum CullFace = GLEnum.Back;

    private static readonly uint[] Indices =
    {
        // counter clockwise winding order
        0, 3, 1,    // top right / bottom right / top left
        3, 2, 1,     // bottom right / bottom left / top left

        // clockwise winding order
        //0, 1, 3,    // right top / right bottom / left top 
        //3, 1, 2,    // right bottom / left top / left bottom 
    };

    private Random _random = new Random();

    //private static Transform[] Transforms = new Transform[1];

    private double NextSpawnDelta = 0;
    private double NextSpawnSpeed = 1.0;    // 1 second

    //private double NextCullFaceDelta = 0;
    //private double NextCullFaceSpeed = 4.0;    // 4 seconds

    private List<BufferEntry> _bufferEntries = new List<BufferEntry>();

    private int _bufferSize = 6 * 1024 * sizeof(float);

    public void Run(string[] args)
    {
        WindowOptions options = WindowOptions.Default;

        options.Size = new Vector2D<int>(800, 600);
        options.Title = "_011_FrontFace_And_CullFace";
        options.VSync = true;

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Run();

        _window.Dispose();
    }

    private unsafe void OnLoad()
    {
        IInputContext input = _window.CreateInput();
        for (int i = 0; i < input.Keyboards.Count; i++)
        {
            input.Keyboards[i].KeyDown += KeyDown;
        }

        _gl = _window.CreateOpenGL();

        _gl.ClearColor(Color.CornflowerBlue);

        // Create the VAO.
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Create the VBO.
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // Upload the vertices data to the VBO.
        //fixed (float* buf = vertices)
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)_bufferSize, (void*)null, BufferUsageARB.DynamicDraw);

        // Create the EBO.
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        // Upload the indices data to the EBO.
        fixed (uint* buf = Indices)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(uint)), buf, BufferUsageARB.StaticDraw);

        // The vertex shader code.
        const string vertexCode = @"
    #version 330 core
layout (location = 0) in vec3 vPos;
layout (location = 1) in vec2 vUv;
layout (location = 2) in vec3 vColor;

uniform mat4 uModel;

out vec2 fUv;
out vec3 fColor;

void main()
{
    //Multiplying our uniform with the vertex position, the multiplication order here does matter.
    gl_Position =  uModel * vec4(vPos, 1.0);
    fUv = vUv;
    fColor = vColor;
}";

        // The fragment shader code.
        const string fragmentCode = @"
    #version 330 core
in vec2 fUv;
in vec3 fColor;

//uniform sampler2D uTexture0;

out vec4 FragColor;

void main()
{
    //FragColor = texture(uTexture0, fUv) * vec4(fColor, 1.0);
    FragColor = vec4(fColor, 1.0);
}";

        // Create our vertex shader, and give it our vertex shader source code.
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexCode);

        // Attempt to compile the shader.
        _gl.CompileShader(vertexShader);

        // Check to make sure that the shader has successfully compiled.
        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Vertex shader failed to compile: " + _gl.GetShaderInfoLog(vertexShader));

        // Repeat this process for the fragment shader.
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentCode);

        _gl.CompileShader(fragmentShader);

        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int)GLEnum.True)
            throw new Exception("Fragment shader failed to compile: " + _gl.GetShaderInfoLog(fragmentShader));

        // Create our shader program, and attach the vertex & fragment shaders.
        _program = _gl.CreateProgram();

        _gl.AttachShader(_program, vertexShader);
        _gl.AttachShader(_program, fragmentShader);

        // Attempt to "link" the program together.
        _gl.LinkProgram(_program);

        // Similar to shader compilation, check to make sure that the shader program has linked properly.
        _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int lStatus);
        if (lStatus != (int)GLEnum.True)
            throw new Exception("Program failed to link: " + _gl.GetProgramInfoLog(_program));

        // Detach and delete our shaders. Once a program is linked, we no longer need the individual shader objects.
        _gl.DetachShader(_program, vertexShader);
        _gl.DetachShader(_program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Set up our vertex attributes! These tell the vertex array (VAO) how to process the vertex data we defined
        // earlier. Each vertex array contains attributes. 

        // Our stride constant. The stride must be in bytes, so we take the first attribute (a vec3), multiply it
        // by the size in bytes of a float, and then take our second attribute (a vec2), and do the same.
        const uint stride = ((3 * sizeof(float))) + (2 * sizeof(float) + (3 * sizeof(float)));

        // Enable the "aPosition" attribute in our vertex array, providing its size and stride too.
        const uint positionLoc = 0;
        _gl.EnableVertexAttribArray(positionLoc);
        _gl.VertexAttribPointer(positionLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

        // Now we need to enable our texture coordinates! We've defined that as location 1 so that's what we'll use
        // here. The code is very similar to above, but you must make sure you set its offset to the **size in bytes**
        // of the attribute before.
        const uint textureLoc = 1;
        _gl.EnableVertexAttribArray(textureLoc);
        _gl.VertexAttribPointer(textureLoc, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        // Now we need to enable our texture coordinates! We've defined that as location 1 so that's what we'll use
        // here. The code is very similar to above, but you must make sure you set its offset to the **size in bytes**
        // of the attribute before.
        const uint colorLoc = 2;
        _gl.EnableVertexAttribArray(colorLoc);
        _gl.VertexAttribPointer(colorLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));



        // Unbind everything as we don't need it.
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);


        // Now we begin the process of creating our texture!
        // First, we create the texture handle. Then, we must set an active texture unit. Each texture unit is a
        // separate bindable texture that we can use in a shader. GPUs have a maximum number of texture units they
        // can use, however the OpenGL spec states there MUST be at least 32 units available.
        // Much like buffers, we then bind the texture to the Texture2D target.
        _texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        // Use ImageSharp to load an image from our PNG file.
        using (Image<Rgba32> image = Image.Load<Rgba32>("silk.png"))
        {
            // Now, let's create the texture itself.
            // Much like buffers, the texture is not created until you call glTexImage2D, where you define some
            // parameters to describe the texture. Let's go over each parameter used here:
            // 1. Tell OpenGL that we want to use the texture bound in the Texture2D target.
            // 2. We are creating the "base" texture level, therefore this value should be 0. You don't need to
            //    worry about texture levels for now.
            // 3. We tell OpenGL that we want the GPU to store this data as RGBA formatted data on the GPU itself.
            // 4. The image's width.
            // 5. The image's height.
            // 6. This is the image's border. This value MUST be 0. It is a leftover component from legacy OpenGL,
            //    and it serves no purpose.
            // 7. Our image data is formatted as RGBA data, therefore we must tell OpenGL we are uploading RGBA data.
            // 8. The Rgba32 struct contains four color channels stored as bytes. Therefore we must tell OpenGL we
            //    each color channel will be an unsigned byte.
            // 9. This parameter is a pointer to an array of texture data. ImageSharp doesn't provide a single array
            //    of data, so we just pass in null here, and upload our texture data below instead.
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height,
                0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

            // Upload our texture data line-by-line.
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    fixed (void* data = accessor.GetRowSpan(y))
                    {
                        // glTexSubImage2D allows us to upload data to arbitrary locations within the texture.
                        // This can be used to upload to sub-regions of the texture.
                        // In this case, we use it to upload each line of our texture data to the texture.
                        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1,
                            PixelFormat.Rgba, PixelType.UnsignedByte, data);
                    }
                }
            });
        }

        // Now let's set some texture parameters!
        // This tells the GPU how it should sample the texture.

        // Set the texture wrap mode to repeat.
        // The texture wrap mode defines what should happen when the texture coordinates go outside of the 0-1 range.
        // In this case, we set it to repeat. The texture will just repeatedly tile over and over again.
        // You'll notice we're using S and T wrapping here. This is OpenGL's version of the standard UV mapping you
        // may be more used to, where S is on the X-axis, and T is on the Y-axis.
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        // The min and mag filters define how the texture should be sampled as it resized.
        // The min, or minification filter, is used when the texture is reduced in size.
        // The mag, or magnification filter, is used when the texture is increased in size.
        // We're using bilinear filtering here, as it produces a generally nice result.
        // You can also use nearest (point) filtering, or anisotropic filtering, which is only available on the min
        // filter.
        // You may notice that the min filter defines a "mipmap" filter as well. We'll go over mipmaps below.
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        // Generate mipmaps for this texture.
        // Note: We MUST do this or the texture will appear as black (this is an option you can change but this is
        // out of scope for this tutorial).
        // What is a mipmap?
        // A mipmap is essentially a smaller version of the existing texture. When generating mipmaps, the texture
        // size is continuously halved, generally stopping once it reaches a size of 1x1 pixels. (Note: there are
        // exceptions to this, for example if the GPU reaches its maximum level of mipmaps, which is both a hardware
        // limitation, and a user defined value. You don't need to worry about this for now, so just assume that
        // the mips will be generated all the way down to 1x1 pixels).
        // Mipmaps are used when the texture is reduced in size, to produce a much nicer result, and to reduce moire
        // effect patterns.
        _gl.GenerateMipmap(TextureTarget.Texture2D);

        // Unbind the texture as we no longer need to update it any further.
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        // Get our texture uniform, and set it to 0.
        // We can easily do this by using glGetUniformLocation and giving it a name.
        // Setting it to 0 tells it that you want it to use the 0th texture unit.
        // Generally, OpenGL should automatically initialize all uniform values to their default value (which is
        // almost always 0), however you should get into the practice of initializing all uniform values to a known
        // value, before you use them in your shader.
        int location = _gl.GetUniformLocation(_program, "uTexture");
        _gl.Uniform1(location, 0);

        // Finally a bit of blending!
        // If you disable blending, you'll notice a black border around the texture.
        // The texture is partially transparent, however OpenGL doesn't know how to handle this by default.
        // By enabling blending, and giving it a blend function, you can tell OpenGL how to handle transparency.
        // In this case, it removes the black background and just leaves the texture on its own.
        // The blend function is out of scope for this tutorial, so don't worry if you don't understand it too much.
        // The program will function just fine without blending!
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        //Unlike in the transformation, because of our abstraction, order doesn't matter here.
        //Translation.
        //Transforms[0] = new Transform();
        //Transforms[0].Position = new Vector3(0.5f, 0.5f, 0f);
        //Rotation.
        //Transforms[1] = new Transform();
        //Transforms[1].Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1f);
        ////Scaling.
        //Transforms[2] = new Transform();
        //Transforms[2].Scale = 0.5f;
        ////Mixed transformation.
        //Transforms[3] = new Transform();
        //Transforms[3].Position = new Vector3(-0.5f, 0.5f, 0f);
        //Transforms[3].Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1f);
        //Transforms[3].Scale = 0.5f;

        _bufferEntries.Add(new BufferEntry() { Index = 0, Count = 2, Type = BufferEntryType.Lines });

        Span<BufferEntry> bufferEntrySpan = CollectionsMarshal.AsSpan<BufferEntry>(_bufferEntries);

        bufferEntrySpan[0].Type = BufferEntryType.Lines;
        bufferEntrySpan[0].Count = 2;
    }

    private float RandomFloat()
    {
        return RandomFloat(0.0f, 1.0f);
    }

    private float RandomFloat(float max)
    {
        return RandomFloat(0.0f, max);
    }

    private float RandomFloat(float min, float max)
    {
        return (float)(_random.NextDouble() * (max - min) + min);
    }

    private unsafe void OnUpdate(double dt) 
    {
        if (NextSpawnDelta <= 0) 
        {
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            // The quad vertices data.
            // You may have noticed an addition - texture coordinates!
            // Texture coordinates are a value between 0-1 (see more later about this) which tell the GPU which part
            // of the texture to use for each vertex.

            var top = (float)_random.NextDouble();
            var bottom = (float)_random.NextDouble() - 1.0f;
            var right = (float)_random.NextDouble();
            var left = (float)_random.NextDouble() - 1.0f;

            float[] lines =
            {
                 // X Y Z                U V             R G B
                 RandomFloat(-1, 1),  RandomFloat(-1, 1), 0.0f,  RandomFloat(0.5f, 1.0f), 0.0f, 0.0f,  // top right vertex (black)
                 RandomFloat(-1, 1), RandomFloat(-1, 1), 0.0f,   RandomFloat(0.5f, 1.0f), RandomFloat(0.5f, 1.0f), 0.0f,  // bottom right vertex (yellow)
            };

            // Upload the vertices data to the VBO.
            fixed (float* buf = lines)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(lines.Length * sizeof(float)), buf);


            // Unbind everything as we don't need it.
            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            //Transforms[0].Position = new Vector3((float)_random.NextDouble(), (float)_random.NextDouble(), 0f);

            FrontFace = FrontFace == GLEnum.Ccw ? GLEnum.CW : GLEnum.Ccw;

            Console.WriteLine($"FrontFace: {FrontFace} | CullFace: {CullFace}");

            NextSpawnDelta += NextSpawnSpeed;
        }

        NextSpawnDelta -= dt;
    }

    private unsafe void OnRender(double dt)
    {
        // Clear the window to the color we set earlier.
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        // Bind our VAO, then the program.
        _gl.BindVertexArray(_vao);
        _gl.UseProgram(_program);

        _gl.FrontFace(FrontFace);
        _gl.CullFace(CullFace);
        _gl.Enable(GLEnum.CullFace);

        //_gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Line);

        // Much like our texture creation earlier, we must first set our active texture unit, and then bind the
        // texture to use it during draw!
        //_gl.ActiveTexture(TextureUnit.Texture0);
        //_gl.BindTexture(TextureTarget.Texture2D, _texture);


        //for (int i = 0; i < Transforms.Length; i++)
        //{
        //    var m = Transforms[i].ViewMatrix;

        //    int location = _gl.GetUniformLocation(_program, "uModel");
        //    _gl.UniformMatrix4(location, 1, false, (float*)&m);

        //    _gl.DrawElements(PrimitiveType.Triangles, (uint)Indices.Length, DrawElementsType.UnsignedInt, null);
        //}

        foreach(var bufferEntry in _bufferEntries)
        {
            int location = _gl.GetUniformLocation(_program, "uModel");
            _gl.DrawElements((PrimitiveType)bufferEntry.Type, bufferEntry.Count, DrawElementsType.UnsignedInt, (void*)(bufferEntry.Index * sizeof(uint)));
        }
    }

    private static void OnFramebufferResize(Vector2D<int> newSize)
    {
        _gl.Viewport(newSize);
    }

    private static void OnClose()
    {
        //Remember to delete the buffers.
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_program);
        _gl.DeleteTexture(_texture);
    }

    private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        if (arg2 == Key.Escape)
        {
            _window.Close();
        }
    }
}