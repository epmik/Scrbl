using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace Scrbl.Tutorials;

public struct VertexBufferElement
{
    public uint Count;
    public VertexAttribPointerType Type;
    public bool Normalized;

    public static uint GetSizeOfType(VertexAttribPointerType type)
    {
        return type switch
        {
            VertexAttribPointerType.Float => sizeof(float),
            VertexAttribPointerType.UnsignedInt => sizeof(uint),
            VertexAttribPointerType.UnsignedByte => sizeof(byte),
            _ => throw new ArgumentException($"Unsupported Attribute Type: {type}")
        };
    }
}
