using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace Scrbl.Tutorials;

public class VertexBufferLayout
{
    public List<VertexBufferElement> Elements { get; private set; } = new();
    public uint Stride { get; private set; } = 0;

    public void PushFloat(uint count)
    {
        Elements.Add(new VertexBufferElement
        {
            Count = count,
            Type = VertexAttribPointerType.Float,
            Normalized = false
        });
        Stride += count * VertexBufferElement.GetSizeOfType(VertexAttribPointerType.Float);
    }

    // Add additional types (PushUInt, PushByte) here as needed later
}