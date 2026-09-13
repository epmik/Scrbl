using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace Scrbl.Tutorials;

public class VertexArrayObject : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; private set; }

    public VertexArrayObject(GL gl)
    {
        _gl = gl;
        Handle = _gl.GenVertexArray();
    }

    public void Bind()
    {
        _gl.BindVertexArray(Handle);
    }

    public void Unbind()
    {
        _gl.BindVertexArray(0);
    }

    public unsafe void AddBuffer(VertexBufferObject vbo, VertexBufferLayout layout)
    {
        Bind();
        vbo.Bind(); // Links this VBO to this VAO's upcoming attribute assignments!

        var elements = layout.Elements;
        nint offset = 0;

        for (uint i = 0; i < elements.Count; i++)
        {
            var element = elements[(int)i];

            _gl.VertexAttribPointer(
                i,
                (int)element.Count,
                element.Type,
                element.Normalized,
                layout.Stride,
                (void*)offset
            );

            _gl.EnableVertexAttribArray(i);

            offset += (nint)(element.Count * VertexBufferElement.GetSizeOfType(element.Type));
        }
    }

    public void SetIndexBuffer(IndexBufferObject ebo)
    {
        Bind();
        ebo.Bind(); // Attaches the EBO directly to the VAO state
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(Handle);
    }
}
