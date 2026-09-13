using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Scrbl.Tutorials
{
    public class Shader : IDisposable
    {
        private uint _handle;
        private GL _gl;

        // 1. The Uniform Cache Map
        private readonly Dictionary<string, int> _uniformLocationCache = new();

        public Shader(GL gl, string vertexPath, string fragmentPath)
        {
            _gl = gl;

            uint vertex = LoadShader(ShaderType.VertexShader, vertexPath);
            uint fragment = LoadShader(ShaderType.FragmentShader, fragmentPath);
            _handle = _gl.CreateProgram();
            _gl.AttachShader(_handle, vertex);
            _gl.AttachShader(_handle, fragment);
            _gl.LinkProgram(_handle);
            _gl.GetProgram(_handle, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(_handle)}");
            }
            _gl.DetachShader(_handle, vertex);
            _gl.DetachShader(_handle, fragment);
            _gl.DeleteShader(vertex);
            _gl.DeleteShader(fragment);
        }

        public void Use()
        {
            _gl.UseProgram(_handle);
        }

        public void SetUniform(string name, ReadOnlySpan<Matrix4x4> matrix)
        {
            SetUniform(UniformLocation(name), matrix);
        }

        public void SetUniform(int location, ReadOnlySpan<Matrix4x4> matrix)
        {
            _gl.UniformMatrix4(location, 1, false, matrix.ToFloatArraySpan());
        }

        public void SetUniform(string name, in Matrix4x4 value)
        {
            SetUniform(UniformLocation(name), value);
        }

        public void SetUniform(int location, in Matrix4x4 value)
        {
            _gl.UniformMatrix4(location, 1, false, value.ToFloatArraySpan());
        }

        public void SetUniform(string name, int value)
        {
            int location = UniformLocation(name);
            if (location != -1) _gl.Uniform1(location, value);
        }

        //public unsafe void SetUniform(string name, Matrix4x4 value)
        //{
        //    int location = UniformLocation(name);
        //    if (location != -1) _gl.UniformMatrix4(location, 1, false, (float*)&value);
        //}

        //public unsafe void SetUniform(string name, in Matrix4x4 value)
        //{
        //    int location = UniformLocation(name);
        //    if (location == -1) return;

        //    // Unsafe.AsPointer bypasses the 'in' read-only restriction cleanly 
        //    // by turning the reference back into a raw void pointer.
        //    void* ptr = Unsafe.AsPointer(ref Unsafe.AsRef(in value));

        //    _gl.UniformMatrix4(location, 1, false, (float*)ptr);
        //}

        public void SetUniform(string name, Vector3 value)
        {
            int location = UniformLocation(name);
            if (location != -1) _gl.Uniform3(location, value.X, value.Y, value.Z);
        }

        public void SetUniform(string name, float value)
        {
            int location = UniformLocation(name);
            if (location != -1) _gl.Uniform1(location, value);
        }

        // 3. Keep the location-based overloads for manual binding if preferred
        public void SetUniform(int location, int value)
        {
            if (location != -1) _gl.Uniform1(location, value);
        }

        public unsafe void SetUniform(int location, Matrix4x4 value)
        {
            if (location != -1) _gl.UniformMatrix4(location, 1, false, (float*)&value);
        }

        public void SetUniform(int location, in Vector3 value)
        {
            if (location != -1) _gl.Uniform3(location, value.X, value.Y, value.Z);
        }

        public void SetUniform(int location, float value)
        {
            if (location != -1) _gl.Uniform1(location, value);
        }

        // 4. Cached String Lookup with Silent Fallback for Driver Optimizations
        public int UniformLocation(string name)
        {
            // If we have looked up this string before, return the integer location instantly
            if (_uniformLocationCache.TryGetValue(name, out int location))
            {
                return location;
            }

            // Otherwise, query the GPU driver once
            location = _gl.GetUniformLocation(_handle, name);

            // NOTE: OpenGL returns -1 if a uniform is spelled wrong OR if it is completely
            // unused in the GLSL code (the compiler stripes out unused variables).
            // Instead of throwing an exception and crashing, we log a warning or simply ignore it,
            // allowing your engine to keep running smoothly.
            if (location == -1)
            {
                Console.WriteLine($"[GL WARNING] Uniform '{name}' was not found or is inactive in the shader program.");
            }

            // Cache it (even if it's -1) so we never check the driver for this string again
            _uniformLocationCache[name] = location;
            return location;
        }

        public void Dispose()
        {
            _gl.DeleteProgram(_handle);
        }

        private uint LoadShader(ShaderType type, string path)
        {
            string src = File.ReadAllText(path);
            uint handle = _gl.CreateShader(type);
            _gl.ShaderSource(handle, src);
            _gl.CompileShader(handle);
            string infoLog = _gl.GetShaderInfoLog(handle);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                throw new Exception($"Error compiling shader of type {type}, failed with error {infoLog}");
            }

            return handle;
        }
    }
}
