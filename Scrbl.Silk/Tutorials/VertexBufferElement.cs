using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace Scrbl.Tutorials;

public enum VertexBufferElementType
{
    Float = VertexAttribPointerType.Float,
    UnsignedInt = VertexAttribPointerType.UnsignedInt,
    UnsignedShort = VertexAttribPointerType.UnsignedShort,
    UnsignedByte = VertexAttribPointerType.UnsignedByte,
}

public struct VertexBufferElement
{
    public string Name;
    public uint Count;
    public VertexBufferElementType Type;
    public bool Normalized;

    public static uint Size(VertexBufferElementType type)
    {
        return type switch
        {
            VertexBufferElementType.Float => sizeof(float),
            VertexBufferElementType.UnsignedInt => sizeof(uint),
            VertexBufferElementType.UnsignedByte => sizeof(byte),
            _ => throw new ArgumentException($"Unsupported Attribute Type: {type}")
        };
    }
}
