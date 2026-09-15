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

    public void PushElement(string name, uint count, VertexBufferElementType type, bool normalized = false)
    {
        Elements.Add(new VertexBufferElement
        {
            Name = name,
            Count = count,
            Type = type,
            Normalized = normalized
        });
        Stride += count * VertexBufferElement.Size(type);
    }

    // Add additional types (PushUInt, PushByte) here as needed later
}