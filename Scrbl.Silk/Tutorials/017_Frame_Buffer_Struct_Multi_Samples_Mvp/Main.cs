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

class _017_Frame_Buffer_Struct_Multi_Samples_Mvp
{
    public class Camera : IDisposable
    {
        private readonly IWindow _window;
        private readonly IInputContext _input;

        // --- Input Tracking & Settings ---
        private readonly HashSet<Key> _pressedKeys = new();
        private Vector2 _lastMousePosition;
        private bool _isFirstMousePos = true;
        private Vector2 _mouseDelta;
        private float _scrollDelta;

        // Rebindable Keyboard Controls (Configured for AZERTY by default)
        public Key KeyForward { get; set; } = Key.Z;   // Z instead of W
        public Key KeyBackward { get; set; } = Key.S;  // S
        public Key KeyLeft { get; set; } = Key.Q;      // Q instead of A
        public Key KeyRight { get; set; } = Key.D;     // D
        public Key KeyUp { get; set; } = Key.Space;    // Space
        public Key KeyDown { get; set; } = Key.ControlLeft;

        // Keyboard Roll Keys
        public Key KeyRollLeft { get; set; } = Key.A;  // Custom assignment
        public Key KeyRollRight { get; set; } = Key.E;

        // --- Transform States ---
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;

        private float _pitch;
        private float _yaw;
        private float _roll;

        // Smooth Damping Physics Velocities
        private Vector3 _currentMovementVelocity = Vector3.Zero;
        private float _currentPitchVelocity = 0f;
        private float _currentYawVelocity = 0f;
        private float _currentRollVelocity = 0f;

        // --- Configuration Properties ---

        // Core Vectors Controls (Replaces AllowUpDown, AllowStrafe, etc.)
        public Vector3 MovementAxisLock { get; set; } = Vector3.One; // E.g., new Vector3(1, 0, 1) locks vertical movement

        // Rotational Constraints (Radians)
        public float? MinPitch { get; set; } = null;
        public float? MaxPitch { get; set; } = null;
        public float? MinYaw { get; set; } = null;
        public float? MaxYaw { get; set; } = null;
        public float? MinRoll { get; set; } = null;
        public float? MaxRoll { get; set; } = null;

        // Fine-Grained Linear & Angular Speed Profiles
        public float MoveSpeedForward { get; set; } = 5.0f;
        public float MoveSpeedBackward { get; set; } = 5.0f;
        public float MoveSpeedLeft { get; set; } = 5.0f;
        public float MoveSpeedRight { get; set; } = 5.0f;
        public float MoveSpeedUp { get; set; } = 5.0f;
        public float MoveSpeedDown { get; set; } = 5.0f;

        public float RotateSpeedUp { get; set; } = 1.5f;
        public float RotateSpeedDown { get; set; } = 1.5f;
        public float RotateSpeedLeft { get; set; } = 1.5f;
        public float RotateSpeedRight { get; set; } = 1.5f;
        public float RotateSpeedRollLeft { get; set; } = 1.5f;
        public float RotateSpeedRollRight { get; set; } = 1.5f;

        public float ScrollSensitivity { get; set; } = 0.5f;

        // --- Damping (Smoothing) Settings ---
        public bool UseDamping { get; set; } = true;
        public float DampingFactor { get; set; } = 10.0f; // Higher values = snappier, Lower = driftier/smoother

        // --- Lens & Projection Properties ---
        public bool IsOrthographic { get; set; } = false;
        public float AspectRatio { get; private set; } = 1.0f;
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 1000.0f;
        public float FieldOfView { get; set; } = MathF.PI / 3.0f;
        public float OrthographicSize { get; set; } = 800.0f; // Height of view window in world units for Ortho mode

        public Camera(IWindow window, IInputContext input)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _input = input ?? throw new ArgumentNullException(nameof(input));

            UpdateAspectRatio(_window.Size);
            _window.Resize += OnWindowResize;

            foreach (var keyboard in _input.Keyboards)
            {
                keyboard.KeyDown += OnKeyDown;
                keyboard.KeyUp += OnKeyUp;
            }

            foreach (var mouse in _input.Mice)
            {
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnMouseScroll;
            }
        }

        /// <summary>
        /// Configures mouse capture mode options.
        /// useRawInput = true unlocks boundless hardware frames (best for 3D look).
        /// useRawInput = false locks/hides mouse cursor conventionally.
        /// </summary>
        public void SetMouseCapture(bool enabled, bool useRawInput = true)
        {
            foreach (var mouse in _input.Mice)
            {
                if (enabled)
                {
                    mouse.Cursor.CursorMode = useRawInput ? CursorMode.Raw : CursorMode.Disabled;
                }
                else
                {
                    mouse.Cursor.CursorMode = CursorMode.Normal;
                }
            }
            _isFirstMousePos = true; // Reset mouse baseline tracking on toggle
        }

        public void Update(double deltaTime)
        {
            float dt = (float)deltaTime;

            HandleRotationTick(dt);
            HandleMovementTick(dt);
            HandleZoomTick(dt);
        }

        private void HandleMovementTick(float dt)
        {
            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
            Vector3 right = Vector3.Transform(Vector3.UnitX, Rotation);
            Vector3 up = Vector3.Transform(Vector3.UnitY, Rotation);

            Vector3 targetVelocity = Vector3.Zero;

            // Build target velocities mapped directly to custom bound key structures
            if (_pressedKeys.Contains(KeyForward)) targetVelocity += forward * MoveSpeedForward;
            if (_pressedKeys.Contains(KeyBackward)) targetVelocity -= forward * MoveSpeedBackward;
            if (_pressedKeys.Contains(KeyRight)) targetVelocity += right * MoveSpeedRight;
            if (_pressedKeys.Contains(KeyLeft)) targetVelocity -= right * MoveSpeedLeft;
            if (_pressedKeys.Contains(KeyUp)) targetVelocity += up * MoveSpeedUp;
            if (_pressedKeys.Contains(KeyDown)) targetVelocity -= up * MoveSpeedDown;

            // Enforce structural axis filters globally
            targetVelocity *= MovementAxisLock;

            if (UseDamping)
            {
                // Linear Interpolation over structural time slices
                _currentMovementVelocity = Vector3.Lerp(_currentMovementVelocity, targetVelocity, DampingFactor * dt);
            }
            else
            {
                _currentMovementVelocity = targetVelocity;
            }

            Position += _currentMovementVelocity * dt;
        }

        private void HandleRotationTick(float dt)
        {
            float targetPitchVel = 0f;
            float targetYawVel = 0f;
            float targetRollVel = 0f;

            // 1. Evaluate Target Rotational Velocities from Mouse Look
            if (_mouseDelta != Vector2.Zero)
            {
                float pitchSpeed = _mouseDelta.Y < 0 ? RotateSpeedUp : RotateSpeedDown;
                float yawSpeed = _mouseDelta.X < 0 ? RotateSpeedLeft : RotateSpeedRight;

                targetPitchVel = -_mouseDelta.Y * pitchSpeed;
                targetYawVel = -_mouseDelta.X * yawSpeed;

                _mouseDelta = Vector2.Zero; // Consume immediately
            }

            // 2. Evaluate Target Velocities from Keyboard Roll
            if (_pressedKeys.Contains(KeyRollRight)) targetRollVel = RotateSpeedRollRight;
            if (_pressedKeys.Contains(KeyRollLeft)) targetRollVel = -RotateSpeedRollLeft;

            if (UseDamping)
            {
                // Smoothly damp rotational speeds independently
                _currentPitchVelocity = MathF.IEEERemainder(float.Lerp(_currentPitchVelocity, targetPitchVel, DampingFactor * dt), float.MaxValue);
                _currentYawVelocity = MathF.IEEERemainder(float.Lerp(_currentYawVelocity, targetYawVel, DampingFactor * dt), float.MaxValue);
                _currentRollVelocity = MathF.IEEERemainder(float.Lerp(_currentRollVelocity, targetRollVel, DampingFactor * dt), float.MaxValue);
            }
            else
            {
                _currentPitchVelocity = targetPitchVel;
                _currentYawVelocity = targetYawVel;
                _currentRollVelocity = targetRollVel;
            }

            // Apply velocities to coordinates
            _pitch += _currentPitchVelocity * dt;
            _yaw += _currentYawVelocity * dt;
            _roll += _currentRollVelocity * dt;

            // 3. Enforce Clamps
            if (MinPitch.HasValue) _pitch = MathF.Max(_pitch, MinPitch.Value);
            if (MaxPitch.HasValue) _pitch = MathF.Min(_pitch, MaxPitch.Value);
            if (MinYaw.HasValue) _yaw = MathF.Max(_yaw, MinYaw.Value);
            if (MaxYaw.HasValue) _yaw = MathF.Min(_yaw, MaxYaw.Value);
            if (MinRoll.HasValue) _roll = MathF.Max(_roll, MinRoll.Value);
            if (MaxRoll.HasValue) _roll = MathF.Min(_roll, MaxRoll.Value);

            Rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
        }

        private void HandleZoomTick(float dt)
        {
            if (_scrollDelta != 0)
            {
                if (IsOrthographic)
                {
                    // In Ortho mode, zooming modifies the view window boundaries instead of the lens FOV
                    OrthographicSize -= _scrollDelta * ScrollSensitivity * 50f * dt;
                    OrthographicSize = float.Max(OrthographicSize, 10f); // Guard against inversion
                }
                else
                {
                    FieldOfView -= _scrollDelta * ScrollSensitivity * dt;
                    FieldOfView = float.Clamp(FieldOfView, MathF.PI / 12.0f, MathF.PI / 2.0f);
                }

                _scrollDelta = 0f;
            }
        }

        private void OnKeyDown(IKeyboard keyboard, Key key, int keyCode) => _pressedKeys.Add(key);
        private void OnKeyUp(IKeyboard keyboard, Key key, int keyCode) => _pressedKeys.Remove(key);

        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (_isFirstMousePos)
            {
                _lastMousePosition = position;
                _isFirstMousePos = false;
                return;
            }

            _mouseDelta += new Vector2(position.X - _lastMousePosition.X, position.Y - _lastMousePosition.Y);
            _lastMousePosition = position;
        }

        private void OnMouseScroll(IMouse mouse, ScrollWheel scroll) => _scrollDelta += scroll.Y;

        private void OnWindowResize(Vector2D<int> size) => UpdateAspectRatio(size);

        private void UpdateAspectRatio(Vector2D<int> size)
        {
            AspectRatio = (float)size.X / (float)size.Y;
        }

        public void ConfigurePixelPerfectOrthoMatch(float targetWidth, float targetHeight)
        {
            Rotation = Quaternion.Identity;
            if (IsOrthographic)
            {
                // Match screen heights cleanly
                OrthographicSize = targetHeight;
                Position = new Vector3(0, 0, 10.0f);
                // Arbitrary distance since distance doesn't affect scale in Ortho
            }
            else
            {
                float halfTargetWidth = targetWidth / 2.0f;
                float halfTargetHeight = targetHeight / 2.0f;
                float distanceToFitVertical = halfTargetHeight / MathF.Tau * (MathF.PI / FieldOfView);
                float distanceToFitHorizontal = (halfTargetWidth / AspectRatio) / MathF.Tau * (MathF.PI / FieldOfView);
                Position = new Vector3(0, 0, MathF.Max(distanceToFitVertical, distanceToFitHorizontal));
            }
        }

        public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(Position, Position + Vector3.Transform(-Vector3.UnitZ, Rotation), Vector3.Transform(Vector3.UnitY, Rotation));

        public Matrix4x4 GetProjectionMatrix()
        {
            if (IsOrthographic)
            {
                float orthoWidth = OrthographicSize * AspectRatio;
                return Matrix4x4.CreateOrthographic(orthoWidth, OrthographicSize, NearPlane, FarPlane);
            }
            return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
        }

        public void Dispose()
        {
            _window.Resize -= OnWindowResize;

            foreach (var keyboard in _input.Keyboards)
            {
                keyboard.KeyDown -= OnKeyDown; keyboard.KeyUp -= OnKeyUp;
            }

            foreach (var mouse in _input.Mice)
            {
                mouse.MouseMove -= OnMouseMove;
                mouse.Scroll -= OnMouseScroll;
            }
        }
    }

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

        /// <summary>
        /// Default == 0 or no multi-sampling. If you want multi-sampling, set this to 2, 4 or 8.
        /// </summary>
        public uint Samples;

        /// <summary>
        /// Default == 32 bits
        /// </summary>
        public uint ColorBits;

        /// <summary>
        /// Default == 32 bits
        /// </summary>
        public uint DepthBits;

        /// <summary>
        /// Default == 0 or no stencil buffer. If you want a stencil buffer, set this to 24 or 32 bits.
        /// </summary>
        public uint StencilBits;

        public FrameBuffer()
        {
            ColorBits = 32;
            DepthBits = 32;
        }
    }

    FrameBuffer _multiSampledFrameBuffer;
    FrameBuffer _intermediateFrameBuffer;

    //Vertex shaders are run on each vertex.
    readonly string VertexShaderSource = @"
        #version 330 core //Using version GLSL version 3.3
        
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;
    
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec4 fCol;

        void main()
        {
            //gl_Position =  vec4(vPos, 1.0);
            gl_Position =  uProjection * uView * uModel * vec4(vPos, 1.0);
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

    Camera _camera;

    public _017_Frame_Buffer_Struct_Multi_Samples_Mvp()
    {
        Random = new Random(RandomSeed);

        _multiSampledFrameBuffer = new FrameBuffer();

        _multiSampledFrameBuffer.Width = (uint)(_windowWidth * _frameBufferScale);
        _multiSampledFrameBuffer.Height = (uint)(_windowHeight * _frameBufferScale);
        _multiSampledFrameBuffer.Samples = 0;

        _intermediateFrameBuffer = new FrameBuffer();

        _intermediateFrameBuffer.Width = (uint)(_windowWidth * _frameBufferScale);
        _intermediateFrameBuffer.Height = (uint)(_windowHeight * _frameBufferScale);
        _intermediateFrameBuffer.Samples = 0; // No multi-sampling for the intermediate framebuffer
        _intermediateFrameBuffer.DepthBits = 0; // No depth buffer for the intermediate framebuffer
        _intermediateFrameBuffer.StencilBits = 0; // No depth buffer for the intermediate framebuffer
    }

    public void Run(string[] args)
    {
        var options = WindowOptions.Default;

        options.Size = new Vector2D<int>(_windowWidth, _windowHeight);
        options.Title = "_017_Frame_Buffer_Struct_Multi_Samples_Mvp";
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

        SetupFramebuffer(ref _multiSampledFrameBuffer);

        Console.WriteLine($"Multi Sampled Framebuffer status: {FramebufferStatus(_multiSampledFrameBuffer)}");

        SetupFramebuffer(ref _intermediateFrameBuffer);

        Console.WriteLine($"IntermediateFramebuffer status: {FramebufferStatus(_intermediateFrameBuffer)}");

        #endregion Setup Framebuffer

        BindScreenFramebuffer();

        _camera = new Camera(window, input)
        {
            KeyForward = Key.Z,
            KeyBackward = Key.S,
            KeyLeft = Key.Q,
            KeyRight = Key.D,
        };

    }

    private unsafe void SetupFramebuffer(ref FrameBuffer frameBuffer)
    {
        frameBuffer.Fbo = GL.GenFramebuffer();

        ResizeFrameBuffer(ref frameBuffer, (int)(_windowWidth * _frameBufferScale), (int)(_windowHeight * _frameBufferScale));
    }

    private unsafe void SetupFramebufferColorAttachment(ref FrameBuffer frameBuffer)
    {
        var textureTarget = FramebufferTextureTarget(frameBuffer);

        frameBuffer.FboColorAttachment = GL.GenTexture();

        GL.BindTexture(textureTarget, frameBuffer.FboColorAttachment);

        if (frameBuffer.Samples < 2)
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, frameBuffer.Width, frameBuffer.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

            GL.TexParameter(textureTarget, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(textureTarget, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);

            GL.TexParameter(textureTarget, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(textureTarget, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }
        else
        {
            GL.TexImage2DMultisample(TextureTarget.Texture2DMultisample, frameBuffer.Samples, GLEnum.Rgba8, frameBuffer.Width, frameBuffer.Height, false);
        }

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
        _camera.Update(deltaTime);

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

        BindFramebuffer(_multiSampledFrameBuffer, true);

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

        BlitFramebuffer(_multiSampledFrameBuffer, _intermediateFrameBuffer);

        BlitFramebufferToScreen(_intermediateFrameBuffer);
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

        ResizeFrameBuffer(ref _multiSampledFrameBuffer, (int)(_windowWidth * _frameBufferScale), (int)(_windowHeight * _frameBufferScale));

        ResizeFrameBuffer(ref _intermediateFrameBuffer, (int)(_windowWidth * _frameBufferScale), (int)(_windowHeight * _frameBufferScale));
    }

    private void OnClose()
    {
        //_imGuiController?.Dispose();

        BindScreenFramebuffer();

        _camera.Dispose();

        DisposeFramebuffer(_multiSampledFrameBuffer);
        DisposeFramebuffer(_intermediateFrameBuffer);

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

        if (key == Key.F5)
        {
            SaveFramebuffer(Gl, (int)_multiSampledFrameBuffer.Width, (int)_multiSampledFrameBuffer.Height, $"framebuffer_{DateTime.Now:yyyyMMdd_HHmmss}.png");

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

        int locationModelMatrix = Gl.GetUniformLocation(Shader, "uModel");
        int locationViewMatrix = Gl.GetUniformLocation(Shader, "uView");
        int locationProjectionMatrix = Gl.GetUniformLocation(Shader, "uProjection");

        //var viewMatrix = Matrix4x4.CreateLookAt(new Vector3(0.1f, 0, 0), new Vector3(0.1f, 0, -1), new Vector3(0, 1, 0));
        //var projectionMatrix = Matrix4x4.Identity;

        var viewMatrix = _camera.GetViewMatrix();
        var projectionMatrix = Matrix4x4.Identity;

        foreach (var vertexBufferChunk in VertexBufferChunkList)
        {
            var t = vertexBufferChunk.Transform.ViewMatrix;

            Gl.UniformMatrix4(locationModelMatrix, 1, false, (float*)&t);
            Gl.UniformMatrix4(locationViewMatrix, 1, false, (float*)&viewMatrix);
            Gl.UniformMatrix4(locationProjectionMatrix, 1, false, (float*)&projectionMatrix);

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

    void ResizeFrameBuffer(ref FrameBuffer frameBuffer, int width, int height)
    {
        frameBuffer.Width = (uint)width;
        frameBuffer.Height = (uint)height;

        BindFramebuffer(frameBuffer, false);

        // If you are using modern immutable textures (gl.TexStorage2D instead of gl.TexImage2D), deleting and regenerating the texture object
        // is absolutely required, because immutable texture memory sizing cannot be reallocated or overwritten.
        DisposeFramebufferAttachments(frameBuffer);

        if (frameBuffer.ColorBits > 0)
        {
            SetupFramebufferColorAttachment(ref frameBuffer);
        }

        if (frameBuffer.DepthBits > 0)
        {
            SetupFramebufferDepthAttachment(ref frameBuffer);
        }

        if (frameBuffer.StencilBits > 0)
        {
            SetupFramebufferStencilAttachment(ref frameBuffer);
        }

        Console.WriteLine($"Resized Framebuffer status: {FramebufferStatus(frameBuffer)}");
    }

    GLEnum FrameBufferDepthComponent(in FrameBuffer frameBuffer)
    {
        switch (frameBuffer.DepthBits)
        {
            case 16:
                return GLEnum.DepthComponent16;
            case 24:
                return GLEnum.DepthComponent24;
            case 32:
                return GLEnum.DepthComponent32;
            default:
                throw new NotImplementedException($"RenderbufferStorageDepthComponent() failed. found unknown DepthBits: {frameBuffer.DepthBits}");
        }
    }

    GLEnum FrameBufferDepthStencil(in FrameBuffer frameBuffer)
    {
        switch (frameBuffer.StencilBits)
        {
            case 24:
                return GLEnum.Depth24Stencil8;
            case 32:
                return GLEnum.Depth32fStencil8;
            default:
                throw new NotImplementedException($"RenderbufferStorageStencil() failed. found unknown StencilBits: {frameBuffer.StencilBits}");
        }
    }

    TextureTarget FramebufferTextureTarget(in FrameBuffer frameBuffer)
    {
        return frameBuffer.Samples < 2 ? TextureTarget.Texture2D : TextureTarget.Texture2DMultisample;
    }

    GLEnum FramebufferStatus(in FrameBuffer frameBuffer)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer.Fbo);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

        return status;
    }

    void BindFramebuffer(in FrameBuffer frameBuffer, FramebufferTarget framebufferTarget, bool bindViewport)
    {
        GL.BindFramebuffer(framebufferTarget, frameBuffer.Fbo);

        if (bindViewport)
        {
            GL.Viewport(0, 0, frameBuffer.Width, frameBuffer.Height);
        }
    }

    void BindFramebuffer(in FrameBuffer frameBuffer, bool bindViewport)
    {
        BindFramebuffer(frameBuffer, FramebufferTarget.Framebuffer, bindViewport);
    }

    void BindReadFramebuffer(in FrameBuffer frameBuffer, bool bindViewport)
    {
        BindFramebuffer(frameBuffer, FramebufferTarget.ReadFramebuffer, bindViewport);
    }

    void BindDrawFramebuffer(in FrameBuffer frameBuffer, bool bindViewport)
    {
        BindFramebuffer(frameBuffer, FramebufferTarget.DrawFramebuffer, bindViewport);
    }

    /// <summary>
    /// Make sure the framebuffer is unbound (bind the screen framebuffer) before calling this function, otherwise you will get an OpenGL error.
    /// </summary>
    void DisposeFramebuffer(FrameBuffer frameBuffer)
    {
        DisposeFramebufferAttachments(frameBuffer);

        if(frameBuffer.Fbo != 0)
        {
            GL.DeleteFramebuffer(frameBuffer.Fbo);
        }
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

        //gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    void BlitFramebuffer(
        in FrameBuffer sourceFrameBuffer,
        in FrameBuffer targetFrameBuffer)
    {
        BlitFramebuffer(Gl, sourceFrameBuffer.Fbo, targetFrameBuffer.Fbo, (int)sourceFrameBuffer.Width, (int)sourceFrameBuffer.Height, (int)targetFrameBuffer.Width, (int)targetFrameBuffer.Height);
    }

    void BlitFramebufferToScreen(
        in FrameBuffer frameBuffer)
    {
        BlitFramebufferToScreen(Gl, frameBuffer.Fbo, (int)frameBuffer.Width, (int)frameBuffer.Height);
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
        if (string.IsNullOrEmpty(filePath))
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