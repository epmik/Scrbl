using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using TextureTarget = Silk.NET.OpenGL.TextureTarget;

namespace Scrbl.Tutorials;

public class FramebufferObject : IDisposable
{
    private readonly GL _gl;

    private bool _isDisposed;

    public uint Width;
    public uint Height;
    public uint Handle;
    public uint ColorAttachmentHandle;
    public uint DepthAttachmentHandle;
    public uint StencilAttachmentHandle;

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
    public event Action<uint, uint>? ResizeAction;

    public static FramebufferObject Default;

    public static void BindDefaultFrameBuffer(GL gl, IWindow window)
    {
        Default = new FramebufferObject(gl)
        {
            Width = (uint)window.FramebufferSize.X,
            Height = (uint)window.FramebufferSize.Y,
            Samples = window.Samples != null ? (uint)window.Samples : 0,
            ColorBits = 24,
            DepthBits = window.PreferredDepthBufferBits != null ? (uint)window.PreferredDepthBufferBits : 8,
            StencilBits = window.PreferredStencilBufferBits != null ? (uint)window.PreferredStencilBufferBits : 8,
        };

        window.Resize += OnDefaultFrameBufferResize;
    }

    private static void OnDefaultFrameBufferResize(Vector2D<int> size)
    {
        Default.Width = (uint)size.X;
        Default.Height = (uint)size.Y;
    }

    public FramebufferObject(GL gl)
    {
        _gl = gl;
    }

    public unsafe void Setup()
    {
        Handle = _gl.GenFramebuffer();

        Resize(Width, Height);
    }

    public GLEnum Status()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);

        return _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
    }

    private void Bind(FramebufferTarget target, bool bindViewport)
    {
        _gl.BindFramebuffer(target, Handle);

        if (bindViewport && Width != 0 && Height != 0)
        {
            _gl.Viewport(0, 0, Width, Height);
        }
    }

    public void Bind(bool bindViewport) => Bind(FramebufferTarget.Framebuffer, bindViewport);
    public void BindRead(bool bindViewport) => Bind(FramebufferTarget.ReadFramebuffer, bindViewport);
    public void BindDraw(bool bindViewport) => Bind(FramebufferTarget.DrawFramebuffer, bindViewport);

    private TextureTarget TextureTarget()
    {
        return Samples < 2 ? Silk.NET.OpenGL.TextureTarget.Texture2D : Silk.NET.OpenGL.TextureTarget.Texture2DMultisample;
    }

    private GLEnum DepthComponentFormat()
    {
        return DepthBits switch
        {
            16 => GLEnum.DepthComponent16,
            24 => GLEnum.DepthComponent24,
            32 => GLEnum.DepthComponent32,
            _ => throw new NotImplementedException($"RenderbufferStorageDepthComponent failed. Unknown DepthBits: {DepthBits}")
        };
    }

    private GLEnum DepthStencilFormat()
    {
        return StencilBits switch
        {
            24 => GLEnum.Depth24Stencil8,
            32 => GLEnum.Depth32fStencil8,
            _ => throw new NotImplementedException($"RenderbufferStorageStencil failed. Unknown StencilBits: {StencilBits}")
        };
    }

    private unsafe void SetupColorAttachment()
    {
        var textureTarget = TextureTarget();
        ColorAttachmentHandle = _gl.GenTexture();
        _gl.BindTexture(textureTarget, ColorAttachmentHandle);

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

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, textureTarget, ColorAttachmentHandle, 0);
    }

    private unsafe void SetupDepthAttachment()
    {
        DepthAttachmentHandle = _gl.GenRenderbuffer();

        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthAttachmentHandle);

        if (Samples <= 1)
        {
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, DepthComponentFormat(), Width, Height);
        }
        else
        {
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, DepthComponentFormat(), Width, Height);
        }
    }

    private unsafe void SetupStencilAttachment()
    {
        StencilAttachmentHandle = _gl.GenRenderbuffer();

        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, StencilAttachmentHandle);

        if (Samples <= 1)
        {
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, DepthStencilFormat(), Width, Height);
        }
        else
        {
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, DepthStencilFormat(), Width, Height);
        }
    }

    public unsafe void Resize(uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Width = width;
        Height = height;

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


    public void BlitTo(in FramebufferObject target)
    {
        Blit(_gl, Handle, target.Handle, 0, 0, (int)Width, (int)Height, 0, 0, (int)target.Width, (int)target.Height);
    }

    public void BlitToScreen()
    {
        BlitToScreen(0, 0, (int)Width, (int)Height);
    }

    public void BlitToScreen(
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight)
    {
        Blit(_gl, Handle, 0, sourceX, sourceY, sourceWidth, sourceHeight, 0, 0, (int)FramebufferObject.Default.Width, (int)FramebufferObject.Default.Height);
    }

    //private static void Blit(
    //    GL gl,
    //    uint sourceHandle,
    //    uint targetHandle,
    //    int sourceWidth, int sourceHeight,
    //    int targetWidth, int targetHeight)
    //{
    //    Blit(gl, sourceHandle, targetHandle, 0, 0, sourceWidth, sourceHeight, 0, 0, targetWidth, targetHeight);
    //}

    private static void Blit(
        GL gl,
        uint sourceHandle,
        uint targetHandle,
        int sourceX, int sourceY,
        int sourceWidth, int sourceHeight,
        int targetX, int targetY,
        int targetWidth, int targetHeight)
    {
        // 1. Bind the offscreen FBO as the READ source
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, sourceHandle);

        // 2. Bind the screen (0) as the DRAW destination
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetHandle);

        // 3. Perform the blit with scaling
        gl.BlitFramebuffer(
            sourceX, sourceY, sourceX + sourceWidth, sourceY + sourceHeight,
            targetX, targetY, targetX + targetWidth, targetY + targetHeight,
            ClearBufferMask.ColorBufferBit,         // Mask specifying which buffers to copy
            BlitFramebufferFilter.Linear            // Filter for scaling (Linear or Nearest)
        );
    }

    //public static void Blit(
    //    GL gl,
    //    uint sourceHandle,
    //    uint targetHandle,
    //    int width, int height)
    //{
    //    Blit(gl, sourceHandle, targetHandle, width, height, width, height);
    //}

    #region Disposal Pattern

    private void DisposeAttachments()
    {
        // Internal clean method—safe to call repeatedly during allocations or disposal
        if (ColorAttachmentHandle != 0)
        {
            _gl.DeleteTexture(ColorAttachmentHandle);
            ColorAttachmentHandle = 0;
        }
        if (DepthAttachmentHandle != 0)
        {
            _gl.DeleteRenderbuffer(DepthAttachmentHandle);
            DepthAttachmentHandle = 0;
        }
        if (StencilAttachmentHandle != 0)
        {
            _gl.DeleteRenderbuffer(StencilAttachmentHandle);
            StencilAttachmentHandle = 0;
        }
    }

    private void Dispose(bool disposing)
    {
        // Note: Since OpenGL calls talk straight to unmanaged driver handles, 
        // we clean up our GPU items regardless of whether disposing is true/false.
        DisposeAttachments();

        if (Handle != 0)
        {
            _gl.DeleteFramebuffer(Handle);
            Handle = 0;
        }

        if (disposing)
        {
            // Clean up managed event links to prevent object retention graph leaks
            ResizeAction = null;
        }
    }

    ~FramebufferObject()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion


    public void SaveAsPng()
    {
        SaveAsPng(this, string.Empty);
    }

    public void SaveAsPng(string filePath)
    {
        SaveAsPng(this, filePath);
    }

    public static void SaveAsPng(FramebufferObject frameBuffer, string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            filePath = $"framebuffer_{DateTime.Now:yyyyMMdd.HHmmss.ffff}.png";
        }
        else if(!filePath.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
        {
            filePath += $".png";
        }

        frameBuffer.Bind(false);

        // 1. Allocate an array to hold the pixel data (4 bytes per pixel for RGBA)
        byte[] pixelData = new byte[frameBuffer.Width * frameBuffer.Height * 4];

        // 2. Ensure alignment matches byte boundaries
        frameBuffer._gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

        // 3. Read the pixels from the currently bound framebuffer
        unsafe
        {
            fixed (byte* ptr = pixelData)
            {
                frameBuffer._gl.ReadPixels(
                    0, 0,
                    frameBuffer.Width, frameBuffer.Height,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr
                );
            }
        }

        // 4. Load the raw bytes directly into an ImageSharp image instance
        using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pixelData, (int)frameBuffer.Width, (int)frameBuffer.Height))
        {
            // 5. OpenGL maps (0,0) to the bottom-left corner, so the image will be upside down.
            // We must flip it vertically to look correct.
            image.Mutate(x => x.Flip(FlipMode.Vertical));

            // 6. Save the processed image to your chosen file path
            image.SaveAsPng(filePath); // Or use a Stream depending on your application
        }
    }

}
