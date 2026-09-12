using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;

namespace Scrbl.Tutorials;

class FrameBuffer : IDisposable
{
    private readonly GL _gl;

    private bool _isDisposed;

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
    public uint ColorBits = 32;

    /// <summary>
    /// Default == 24 bits
    /// </summary>
    public uint DepthBits = 24;

    /// <summary>
    /// Default == 0 or no stencil buffer. If you want a stencil buffer, set this to 24 or 32 bits.
    /// </summary>
    public uint StencilBits;

    /// <summary>
    /// Raised when the framebuffer is resized.
    /// </summary>
    event Action<int, int>? ResizeAction;

    public static FrameBuffer Default;

    public static void BindDefaultFrameBuffer(GL gl, IWindow window)
    {
        Default = new FrameBuffer(gl)
        {
            Width = (uint)window.FramebufferSize.X,
            Height = (uint)window.FramebufferSize.Y,
            Samples = window.Samples != null ? (uint)window.Samples : 0,
            ColorBits = 24,
            DepthBits = window.PreferredDepthBufferBits != null ? (uint)window.PreferredDepthBufferBits : 8,
            StencilBits = window.PreferredStencilBufferBits != null ? (uint)window.PreferredStencilBufferBits : 8,
        };
    }

    public FrameBuffer(GL gl)
    {
        _gl = gl;
    }

    //public void Resize(int width, int height)
    //{
    //    Width = (uint)width;
    //    Height = (uint)height;

    //    Bind(false);

    //    // If you are using modern immutable textures (gl.TexStorage2D instead of gl.TexImage2D), deleting and regenerating the texture object
    //    // is absolutely required, because immutable texture memory sizing cannot be reallocated or overwritten.
    //    DisposeFramebufferAttachments(frameBuffer);

    //    if (frameBuffer.ColorBits > 0)
    //    {
    //        SetupFramebufferColorAttachment(ref frameBuffer);
    //    }

    //    if (frameBuffer.DepthBits > 0)
    //    {
    //        SetupFramebufferDepthAttachment(ref frameBuffer);
    //    }

    //    if (frameBuffer.StencilBits > 0)
    //    {
    //        SetupFramebufferStencilAttachment(ref frameBuffer);
    //    }

    //    Console.WriteLine($"Resized Framebuffer status: {FramebufferStatus(frameBuffer)}");
    //}

    public unsafe void Setup()
    {
        Fbo = _gl.GenFramebuffer();

        Resize((int)(Width), (int)(Height));
    }

    public GLEnum Status()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

        return _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
    }

    public void Bind(FramebufferTarget target, bool bindViewport)
    {
        _gl.BindFramebuffer(target, Fbo);

        if (bindViewport && Width != 0 && Height != 0)
        {
            _gl.Viewport(0, 0, Width, Height);
        }
    }

    public void Bind(bool bindViewport) => Bind(FramebufferTarget.Framebuffer, bindViewport);
    public void BindRead(bool bindViewport) => Bind(FramebufferTarget.ReadFramebuffer, bindViewport);
    public void BindDraw(bool bindViewport) => Bind(FramebufferTarget.DrawFramebuffer, bindViewport);

    public TextureTarget TextureTarget()
    {
        return Samples < 2 ? Silk.NET.OpenGL.TextureTarget.Texture2D : Silk.NET.OpenGL.TextureTarget.Texture2DMultisample;
    }

    public GLEnum DepthComponentFormat()
    {
        return DepthBits switch
        {
            16 => GLEnum.DepthComponent16,
            24 => GLEnum.DepthComponent24,
            32 => GLEnum.DepthComponent32,
            _ => throw new NotImplementedException($"RenderbufferStorageDepthComponent failed. Unknown DepthBits: {DepthBits}")
        };
    }

    public GLEnum DepthStencilFormat()
    {
        return StencilBits switch
        {
            24 => GLEnum.Depth24Stencil8,
            32 => GLEnum.Depth32fStencil8,
            _ => throw new NotImplementedException($"RenderbufferStorageStencil failed. Unknown StencilBits: {StencilBits}")
        };
    }

    public unsafe void SetupColorAttachment()
    {
        var textureTarget = TextureTarget();
        FboColorAttachment = _gl.GenTexture();
        _gl.BindTexture(textureTarget, FboColorAttachment);

        if (Samples < 2)
        {
            _gl.TexImage2D(Silk.NET.OpenGL.TextureTarget.Texture2D, 0, InternalFormat.Rgba8, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(textureTarget, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(textureTarget, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(textureTarget, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(textureTarget, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }
        else
        {
            _gl.TexImage2DMultisample(Silk.NET.OpenGL.TextureTarget.Texture2DMultisample, Samples, GLEnum.Rgba8, Width, Height, false);
        }

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, textureTarget, FboColorAttachment, 0);
    }

    public unsafe void SetupDepthAttachment()
    {
        FboDepthAttachment = _gl.GenRenderbuffer();

        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, FboDepthAttachment);

        if (Samples <= 1)
        {
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, DepthComponentFormat(), Width, Height);
        }
        else
        {
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, DepthComponentFormat(), Width, Height);
        }
    }

    public unsafe void SetupStencilAttachment()
    {
        FboStencilAttachment = _gl.GenRenderbuffer();

        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, FboStencilAttachment);

        if (Samples <= 1)
        {
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, DepthStencilFormat(), Width, Height);
        }
        else
        {
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, DepthStencilFormat(), Width, Height);
        }
    }

    public unsafe void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Width = (uint)width;
        Height = (uint)height;

        Bind(false);

        DisposeAttachments();

        if (ColorBits > 0)
        {
            SetupColorAttachment();
        }

        if (DepthBits > 0)
        {
            SetupDepthAttachment();
        }

        if (StencilBits > 0)
        {
            SetupStencilAttachment();
        }

        // Fire event hook notifications to alert pipeline listeners
        ResizeAction?.Invoke(width, height);
    }

    #region Disposal Pattern

    public void DisposeAttachments()
    {
        // Internal clean method—safe to call repeatedly during allocations or disposal
        if (FboColorAttachment != 0)
        {
            _gl.DeleteTexture(FboColorAttachment);
            FboColorAttachment = 0;
        }
        if (FboDepthAttachment != 0)
        {
            _gl.DeleteRenderbuffer(FboDepthAttachment);
            FboDepthAttachment = 0;
        }
        if (FboStencilAttachment != 0)
        {
            _gl.DeleteRenderbuffer(FboStencilAttachment);
            FboStencilAttachment = 0;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            // Note: Since OpenGL calls talk straight to unmanaged driver handles, 
            // we clean up our GPU items regardless of whether disposing is true/false.
            DisposeAttachments();

            if (Fbo != 0)
            {
                _gl.DeleteFramebuffer(Fbo);
                Fbo = 0;
            }

            if (disposing)
            {
                // Clean up managed event links to prevent object retention graph leaks
                ResizeAction = null;
            }

            _isDisposed = true;
        }
    }

    ~FrameBuffer()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}


//public class FrameBufferSettings
//{
//    public uint Width { get; set; }

//    public uint Height { get; set; }

//    /// <summary>
//    /// Default == 0 or no multi-sampling. If you want multi-sampling, set this to 2, 4 or 8.
//    /// </summary>
//    public uint Samples { get; set; }

//    /// <summary>
//    /// Default == 32 bits
//    /// </summary>
//    public uint ColorBits { get; set; } = 32;

//    /// <summary>
//    /// Default == 32 bits
//    /// </summary>
//    public uint DepthBits { get; set; } = 32;

//    /// <summary>
//    /// Default == 0 or no stencil buffer. If you want a stencil buffer, set this to 24 or 32 bits.
//    /// </summary>
//    public uint StencilBits { get; set; }
//}

//public class FrameBuffer : IDisposable
//{
//    private readonly GL _gl;
//    private bool _isDisposed;

//    public uint Width { get; private set; }
//    public uint Height { get; private set; }
//    public uint Fbo { get; private set; }
//    public uint FboColorAttachment { get; private set; }
//    public uint FboDepthAttachment { get; private set; }
//    public uint FboStencilAttachment { get; private set; }

//    /// <summary>
//    /// Default == 0 or no multi-sampling. If you want multi-sampling, set this to 2, 4 or 8.
//    /// </summary>
//    public uint Samples { get; private set; }

//    /// <summary>
//    /// Default == 32 bits
//    /// </summary>
//    public uint ColorBits { get; private set; } = 32;

//    /// <summary>
//    /// Default == 32 bits
//    /// </summary>
//    public uint DepthBits { get; private set; } = 32;

//    /// <summary>
//    /// Default == 0 or no stencil buffer. If you want a stencil buffer, set this to 24 or 32 bits.
//    /// </summary>
//    public uint StencilBits { get; private set; }


//    /// <summary>
//    /// Raised when the framebuffer is resized.
//    /// Safely usable now that FrameBuffer is a reference type.
//    /// </summary>
//    public event Action<int, int>? OnResize;

//    //public static readonly FrameBuffer Default = new FrameBuffer();

//    private FrameBuffer(GL gl)
//    {
//        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
//    }

//    public static FrameBuffer Default;

//    public static void CreateDefaultFrameBuffer(GL gl, IWindow window)
//    {
//        Default = new FrameBuffer(gl)
//        {
//            Width = (uint)window.FramebufferSize.X,
//            Height = (uint)window.FramebufferSize.Y,
//            Samples = window.Samples != null ? (uint)window.Samples : 0,
//            ColorBits = 24,
//            DepthBits = window.PreferredDepthBufferBits != null ? (uint)window.PreferredDepthBufferBits : 8,
//            StencilBits = window.PreferredStencilBufferBits != null ? (uint)window.PreferredStencilBufferBits : 8,
//        };
//    }

//    public static FrameBuffer Create(GL gl, FrameBufferSettings settings)
//    {
//        var frameBuffer = new FrameBuffer(gl)
//        {
//            Width = settings.Width,
//            Height = settings.Height,
//            Samples = settings.Samples,
//            ColorBits = settings.ColorBits,
//            DepthBits = settings.DepthBits,
//            StencilBits = settings.StencilBits,
//            Fbo = gl.GenFramebuffer()
//        };

//        frameBuffer.Resize((int)settings.Width, (int)settings.Height);

//        return frameBuffer;
//    }

//}


