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
    private readonly Random _random;

    public _020_AbstractScrbl()
    {
        Width = 1024f;
        Height = 1024f;
        _random = new Random(10241024);
    }

    // Optional Update Lifecycle Hook
    void Update(double deltaTime)
    {
        // Compute game logic, modify coordinates or shapes over time here
    }
    float RandomFloat(float min, float max)
    {
        return min + (max - min) * _random.NextSingle();
    }

    // Dynamic Render Execution Step Loop Context 
    void Render(double deltaTime)
    {
        // 1. Draw a basic multi-colored line pair segment
        Line()
            .Color(1.0f, 0.0f, 0.0f, 1.0f)
            .From(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .Color(1.0f, 1.0f, 0.0f, 1.0f)
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1));

        // 2. Chained lines auto-fall back directly into standard high performance LineStrips
        Line()
            .Color(0.0f, 1.0f, 0.0f, 1.0f)
            .From(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1));

        // 3. Appending a explicit .Close() commands converts it cleanly to a complete LineLoop structure
        Line()
            .Color(0.0f, 0.5f, 1.0f, 1.0f)
            .From(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .To(RandomFloat(-1, 1), RandomFloat(-1, 1))
            .Close();
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
}