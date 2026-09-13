using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Scrbl.Tutorials
{
    public class GeometryBuilder
    {
        private readonly AbstractScrbl _context;

        // Reusable heap buffer (32 vertices * 7 elements = 224 floats). No GC pressure after creation.
        private readonly float[] _buffer;
        private int _vertexCount;

        // State properties
        private float _r, _g, _b, _a;
        private PrimitiveType _forcedType;
        private bool _hasForcedType;

        public GeometryBuilder(AbstractScrbl context)
        {
            _context = context;
            _buffer = new float[32 * (int)AbstractScrbl.VertexElementCount];
            Reset();
        }

        /// <summary>
        /// Resets the builder state for a brand new line sequence.
        /// </summary>
        private void Reset()
        {
            _vertexCount = 0;
            _r = 1.0f; _g = 1.0f; _b = 1.0f; _a = 1.0f; // Default White
            _hasForcedType = false;
            _forcedType = PrimitiveType.Lines;
        }

        public GeometryBuilder Color(float r, float g, float b, float a = 1.0f)
        {
            _r = r; _g = g; _b = b; _a = a;
            return this;
        }

        public GeometryBuilder From(float x, float y, float z = 0.0f)
        {
            return AddVertex(x, y, z);
        }

        public GeometryBuilder To(float x, float y, float z = 0.0f)
        {
            return AddVertex(x, y, z);
        }

        public void Close()
        {
            _forcedType = PrimitiveType.LineLoop;
            _hasForcedType = true;
            // Explicitly calling Close() means the line is done, we can flush early!
            Flush();
        }

        private GeometryBuilder AddVertex(float x, float y, float z)
        {
            int offset = _vertexCount * (int)AbstractScrbl.VertexElementCount;

            // Dynamic safety check: if we hit buffer capacity limits, flush immediately
            if (offset >= _buffer.Length)
            {
                Flush();
                offset = 0;
            }

            _buffer[offset + 0] = x;
            _buffer[offset + 1] = y;
            _buffer[offset + 2] = z;
            _buffer[offset + 3] = _r;
            _buffer[offset + 4] = _g;
            _buffer[offset + 5] = _b;
            _buffer[offset + 6] = _a;

            _vertexCount++;
            return this;
        }

        /// <summary>
        /// Pushes current accumulated vertices to the central VBO batching queue.
        /// </summary>
        public void Flush()
        {
            if (_vertexCount < 2)
            {
                Reset();
                return;
            }

            PrimitiveType targetType = _hasForcedType ? _forcedType : (_vertexCount == 2 ? PrimitiveType.Lines : PrimitiveType.LineStrip);

            // Pass a slice of our clean buffer without copying arrays
            ReadOnlySpan<float> dataSpan = new ReadOnlySpan<float>(_buffer, 0, _vertexCount * (int)AbstractScrbl.VertexElementCount);
            _context.AppendVertexData(targetType, dataSpan);

            // Prepare state clean properties for next line configuration tracking phase
            Reset();
        }
    }
}
