using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Scrbl.Tutorials
{
    public static class Matrix4x4Extensions
    {
        public static ReadOnlySpan<Matrix4x4> ToSpan(this Matrix4x4 matrix)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in matrix), 1);
        }

        public static ReadOnlySpan<float> ToFloatArraySpan(this Matrix4x4 matrix)
        {
            return ToFloatArraySpan(matrix.ToSpan());
        }

        public static ReadOnlySpan<float> ToFloatArraySpan(this ReadOnlySpan<Matrix4x4> matrix)
        {
            return MemoryMarshal.Cast<Matrix4x4, float>(matrix);
        }
    }
}
