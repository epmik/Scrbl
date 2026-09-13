using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Scrbl.Tutorials
{
    public abstract class AbstractScrbl
    {
        protected struct VertexBufferChunk
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

        // Properties for coordinate spaces
        public float Width { get; protected set; } = 1024f;
        public float Height { get; protected set; } = 1024f;

        // Silk.NET Properties exposed safely to derived classes
        protected IWindow Window { get; private set; }
        protected GL Gl { get; private set; }
        protected IInputContext Input { get; private set; }

        private VertexArrayObject _vao;
        private VertexBufferObject _vbo;
        private Shader _shader;
        private ImGuiController _imGuiController;
        private FramebufferCamera _camera;

        private FramebufferObject _multiSampledFrameBuffer;
        private FramebufferObject _intermediateFrameBuffer;

        private int _windowWidth = 800;
        private int _windowHeight = 600;
        private double _frameBufferScale = 4.0;

        private readonly List<VertexBufferChunk> _chunks = new();

        // Cached delegates for optional overrides bound by reflection
        private Action<double> _onUpdateHook;
        private Action<double> _onRenderHook;
        private Action<Vector2D<int>> _onResizeHook;
        private Action _onCloseHook;

        public const uint VertexElementCount = 7; // X Y Z + R G B A

        private GeometryBuilder _cachedBuilder;

        public void Run(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(_windowWidth, _windowHeight);
            options.Title = GetType().Name;
            options.VSync = false;

            bool isMac = OperatingSystem.IsMacOS();
            options.API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                isMac ? ContextFlags.ForwardCompatible : ContextFlags.Debug,
                isMac ? new APIVersion(4, 1) : new APIVersion(4, 6)
            );

            Window = Silk.NET.Windowing.Window.Create(options);

            Window.Load += OnLoadInternal;
            Window.Render += OnRenderInternal;
            Window.Update += OnUpdateInternal;
            Window.FramebufferResize += OnResizeInternal;
            Window.Closing += OnCloseInternal;

            // Resolve optional implementation hooks by reflection
            BindHookMethods();

            Window.Run();
            Window.Dispose();
        }

        private void BindHookMethods()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = GetType();

            var updateMethod = type.GetMethod("Update", flags, null, new[] { typeof(double) }, null);
            if (updateMethod != null) _onUpdateHook = (Action<double>)Delegate.CreateDelegate(typeof(Action<double>), this, updateMethod);

            var renderMethod = type.GetMethod("Render", flags, null, new[] { typeof(double) }, null);
            if (renderMethod != null) _onRenderHook = (Action<double>)Delegate.CreateDelegate(typeof(Action<double>), this, renderMethod);

            var resizeMethod = type.GetMethod("Resize", flags, null, new[] { typeof(Vector2D<int>) }, null);
            if (resizeMethod != null) _onResizeHook = (Action<Vector2D<int>>)Delegate.CreateDelegate(typeof(Action<Vector2D<int>>), this, resizeMethod);

            var closeMethod = type.GetMethod("Close", flags);
            if (closeMethod != null) _onCloseHook = (Action)Delegate.CreateDelegate(typeof(Action), this, closeMethod);
        }

        private unsafe void OnLoadInternal()
        {
            Gl = GL.GetApi(Window);

            FramebufferObject.BindDefaultFrameBuffer(Gl, Window);

            Input = Window.CreateInput();

            for (int i = 0; i < Input.Keyboards.Count; i++)
            {
                Input.Keyboards[i].KeyDown += KeyDown;
            }

            var mouse = Input.Mice[0];

            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseScroll;

            _vao = new VertexArrayObject(Gl);
            // Generous buffer layout allocation tracking
            _vbo = new VertexBufferObject(Gl, VertexElementCount * sizeof(float), 2048, BufferUsageARB.DynamicDraw);

            var layout = new VertexBufferLayout();
            layout.PushFloat(3); // Pos
            layout.PushFloat(4); // Color
            _vao.AddBuffer(_vbo, layout);
            _vao.Unbind();

            _shader = new Shader(Gl, ".assets/.shaders/m_pos3_col4.vert", ".assets/.shaders/col4.frag");
            _imGuiController = new ImGuiController(Gl, Window, Input);

            _multiSampledFrameBuffer = new FramebufferObject(Gl)
            {
                Width = (uint)(_windowWidth * _frameBufferScale),
                Height = (uint)(_windowHeight * _frameBufferScale),
                Samples = 8
            };
            _multiSampledFrameBuffer.Setup();

            _intermediateFrameBuffer = new FramebufferObject(Gl)
            {
                Width = (uint)(_windowWidth * _frameBufferScale),
                Height = (uint)(_windowHeight * _frameBufferScale),
                Samples = 0,
                DepthBits = 0,
                StencilBits = 0
            };
            _intermediateFrameBuffer.Setup();

            _cachedBuilder = new GeometryBuilder(this);

            _camera = new FramebufferCamera(Input, Window, _intermediateFrameBuffer);
            FramebufferObject.Default.Bind(true);
        }

        private void OnUpdateInternal(double deltaTime)
        {
            _camera.Update();
            _imGuiController.Update((float)deltaTime);
            _onUpdateHook?.Invoke(deltaTime);
        }

        private unsafe void OnRenderInternal(double deltaTime)
        {
            _multiSampledFrameBuffer.Bind(true);
            Gl.ClearColor(0.1f, 0.1f, 0.1f, 0.0f);
            Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            _vbo.Bind();
            _vbo.Clear();
            _chunks.Clear();

            // Fire user rendering commands which load up the dynamic VBO
            _onRenderHook?.Invoke(deltaTime);

            _cachedBuilder.Flush();

            // Render remaining batches stored inside the current pipeline layout step
            FlushCurrentBatches();

            // Swap buffer outputs safely
            FramebufferObject.Default.Bind(true);
            Gl.ClearColor(0.1f, 0.1f, 0.1f, 0.0f);
            Gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            _multiSampledFrameBuffer.BlitTo(_intermediateFrameBuffer);
            _intermediateFrameBuffer.BlitToScreen(_camera.SourceX, _camera.SourceY, (int)_camera.ViewWidth, (int)_camera.ViewHeight);

            _imGuiController.Render();
        }

        // High performance routing for line-stream builder
        internal unsafe void AppendVertexData(PrimitiveType primitiveType, ReadOnlySpan<float> vertexData)
        {
            uint byteSize = (uint)(vertexData.Length * sizeof(float));

            if (_vbo.CanWrite(byteSize))
            {
                FlushCurrentBatches();
                _vbo.Orphan();
                _vbo.Clear();
                _chunks.Clear();
            }

            _vbo.Write(vertexData);

            _chunks.Add(new VertexBufferChunk
            {
                Index = (int)_vbo.UsedElements,
                Count = (uint)(vertexData.Length / VertexElementCount),
                PrimitiveType = primitiveType
            });
        }

        private unsafe void FlushCurrentBatches()
        {
            if (_chunks.Count == 0) return;

            _shader.Use();
            _vao.Bind();

            // Using an Identity Matrix layout for Orthographic view rules matching configuration scale
            //System.Numerics.Matrix4x4 orthoMatrix = System.Numerics.Matrix4x4.CreateOrthographicOffCenter(0, Width, Height, 0, -1f, 1f);

            foreach (var chunk in _chunks)
            {
                _shader.SetUniform("uModel", chunk.Transform.ViewMatrix.ToSpan()); // Or your custom Matrix uniform handling mapping properties directly
                Gl.DrawArrays(chunk.PrimitiveType, chunk.Index, chunk.Count);
            }
        }

        private void OnResizeInternal(Vector2D<int> size)
        {
            _windowWidth = size.X;
            _windowHeight = size.Y;
            _multiSampledFrameBuffer.Resize((uint)(_windowWidth * _frameBufferScale), (uint)(_windowHeight * _frameBufferScale));
            _intermediateFrameBuffer.Resize((uint)(_windowWidth * _frameBufferScale), (uint)(_windowHeight * _frameBufferScale));
            _onResizeHook?.Invoke(size);
        }

        private void OnCloseInternal()
        {
            _onCloseHook?.Invoke();
            _multiSampledFrameBuffer.Dispose();
            _intermediateFrameBuffer.Dispose();
            _vbo.Dispose();
            _vao.Dispose();
            _shader.Dispose();
            Gl?.Dispose();
        }

        // Exposed execution helper for the dynamic Fluent Context
        protected GeometryBuilder Line()
        {
            // Submit whatever line was being built right before this new Line() call
            _cachedBuilder.Flush();
            return _cachedBuilder;
        }
        #region Input Event Handlers

        private void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                Window.Close();
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
    }
}
