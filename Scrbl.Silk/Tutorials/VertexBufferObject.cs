using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;

namespace Scrbl.Tutorials;

public class VertexBufferObject : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; private set; }
    public uint SizeInBytes { get; private set; }
    public uint ElementSizeInBytes { get; private set; }
    public BufferUsageARB Usage { get; private set; }

    public uint UsedBytes { get; private set; }
    public uint UsedElements { get; private set; }

    private bool _isBound;

    public unsafe VertexBufferObject(GL gl, uint elementSizeInBytes, uint elementCount, BufferUsageARB usage)
    {
        _gl = gl;
        ElementSizeInBytes = elementSizeInBytes;
        SizeInBytes = elementSizeInBytes * elementCount;
        Usage = usage;

        Handle = _gl.GenBuffer();
        Bind();
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)SizeInBytes, null, Usage);
    }

    public void Bind()
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Handle);
        _isBound = true;
    }

    public void Unbind()
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _isBound = false;
    }

    public unsafe void Write(ReadOnlySpan<float> data)
    {
        AssertIsBound();

        var byteSize = (uint)(data.Length * sizeof(float));

        if (byteSize % ElementSizeInBytes != 0)
        {
            throw new ArgumentException($"{data.Length} * sizeof(float) {data.Length} is not a multiple of ElementSizeInBytes {ElementSizeInBytes}.", nameof(data));
        }

        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)UsedBytes, data);
        
        UsedBytes += byteSize;
        UsedElements += (uint)(byteSize / ElementSizeInBytes);
    }

    public unsafe void Orphan()
    {
        AssertIsBound();

        Clear();

        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)SizeInBytes, null, Usage);
    }

    public void Dispose()
    {
        Unbind();
        _gl.DeleteBuffer(Handle);
    }

    public bool CanWrite(uint bytes)
    {
        return UsedBytes + bytes > SizeInBytes;
    }

    public void Clear()
    {
        UsedBytes = 0;
        UsedElements = 0;
    }

    [Conditional("DEBUG")]
    private void AssertIsBound()
    {
        if (!_isBound)
        {
            throw new InvalidOperationException("Cannot perform operation: The VertexBufferObject is NOT bound.");
        }
    }

    //[Conditional("DEBUG")]
    //private void AssertIsNotBound()
    //{
    //    if (_isBound)
    //    {
    //        throw new InvalidOperationException("Cannot perform operation: The VertexBufferObject is bound.");
    //    }
    //}
}