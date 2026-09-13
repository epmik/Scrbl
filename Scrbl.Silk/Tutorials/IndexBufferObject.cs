using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace Scrbl.Tutorials;

public class IndexBufferObject : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; private set; }
    public uint Count { get; private set; }

    public IndexBufferObject(GL gl, uint[] indices, BufferUsageARB usage)
    {
        _gl = gl;
        Count = (uint)indices.Length;
        Handle = _gl.GenBuffer();

        Bind();
        unsafe
        {
            fixed (void* ptr = indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), ptr, usage);
            }
        }
    }

    public void Bind()
    {
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Handle);
    }

    public void Unbind()
    {
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(Handle);
    }
}
