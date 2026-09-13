//
// 2 VBO's, switching between VBP's one one is full
//
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace Scrbl.Tutorials;

class _020_AbstractScrbl : AbstractScrbl
{
    private int _randomSeed = 10241024;
    private Random _random;
    int MinLineChunkCount = 24;
    int MaxLineChunkCount = 68;

    int MinLineLoopChunkCount = 2;

    int MaxLineLoopChunkCount = 5;

    int MinLineStripChunkCount = 2;

    int MaxLineStripChunkCount = 5;

    double NextUpdateTimeDelta = 12.0;
    double NextUpdateTimeout = 12.0;    // 4 seconds

    public _020_AbstractScrbl()
    {
        Width = 1024f;
        Height = 1024f;
        _random = new Random(_randomSeed);
    }

    // Optional Update Lifecycle Hook
    public void Update(double deltaTime)
    {
        if (NextUpdateTimeDelta <= 0)
        {
            _randomSeed = Guid.NewGuid().GetHashCode();

            NextUpdateTimeDelta += NextUpdateTimeout;
        }

        NextUpdateTimeDelta -= deltaTime;

        _random = new Random(_randomSeed);
    }

    // Dynamic Render Execution Step Loop Context 
    public void Render(double deltaTime)
    {
        var lineChunkCount = RandomInt(MinLineChunkCount, MaxLineChunkCount);

        for (var i = 0; i < lineChunkCount; i++)
        {
            Line()
                .Color(1.0f, 0.0f, 0.0f, 1.0f)
                .From(RandomFloat(-1, 1), RandomFloat(0.5f, 1.0f))
                .Color(1.0f, 1.0f, 0.0f, 1.0f)
                .To(RandomFloat(-1, 1), RandomFloat(-0.5f, -1.0f));
        }

        //var lineLoopChunkCount = RandomInt(MinLineLoopChunkCount, MaxLineLoopChunkCount);

        //for (var k = 0; k < lineLoopChunkCount; k++)
        //{
        //    var lineLoopVertexCount = RandomInt(3, 7);

        //    for (var i = 0; i < lineLoopVertexCount; i++)
        //    {
        //        Line()
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .From(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.50f, 1.0f), RandomFloat(0.50f, 1.0f), 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Close();
        //    }
        //}

        //var lineStripChunkCount = RandomInt(MinLineStripChunkCount, MaxLineStripChunkCount);

        //for (var k = 0; k < lineStripChunkCount; k++)
        //{
        //    var lineStripVertexCount = RandomInt(3, 7);

        //    for (var i = 0; i < lineStripVertexCount; i++)
        //    {
        //        Line()
        //            .Color(0.0f, RandomFloat(0.85f, 1.0f), 1.0f, 1.0f)
        //            .From(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.85f, 1.0f), 1.0f, 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.85f, 1.0f), 1.0f, 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.85f, 1.0f), 1.0f, 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
        //            .Color(0.0f, RandomFloat(0.85f, 1.0f), 1.0f, 1.0f)
        //            .To(RandomFloat(-1, 1), RandomFloat(-1, 1));
        //    }
        //}
    }

    // Optional Event Callbacks managed smoothly via engine reflection tracking layers
    void Resize(Vector2D<int> size)
    {
        Console.WriteLine($"Window surface updated size properties context: {size.X}x{size.Y}");
    }

    void Close()
    {
        Console.WriteLine("Cleaning custom application configurations.");
    }

    #region Random Functions

    int RandomInt()
    {
        return RandomInt(0, int.MaxValue);
    }

    int RandomInt(int max)
    {
        return RandomInt(0, max);
    }

    int RandomInt(int min, int max)
    {
        return _random.Next(min, max);
    }

    float RandomFloat()
    {
        return RandomFloat(0f, 1f);
    }

    float RandomFloat(float max)
    {
        return RandomFloat(0f, max);
    }

    float RandomFloat(float min, float max)
    {
        return min + (max - min) * _random.NextSingle();
    }

    #endregion Random Functions
}