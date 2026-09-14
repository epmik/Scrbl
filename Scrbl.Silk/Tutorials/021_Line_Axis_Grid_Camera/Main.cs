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

class _021_Line_Axis_Grid_Camera : AbstractScrbl
{
    public class Camera
    {
        // Position and Rotation
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;

        // Movement Restrictions & Toggles
        public bool AllowForwardBackward { get; set; } = true;
        public bool AllowStrafe { get; set; } = true;
        public bool AllowUpDown { get; set; } = true;
        public Vector3 MovementAxisLock { get; set; } = Vector3.One; // Set to Vector3.UnitY to lock movement purely to Y

        // Rotation Restrictions & Constraints (in Radians)
        public float? MinPitch { get; set; } = null;
        public float? MaxPitch { get; set; } = null;
        public float? MinYaw { get; set; } = null;
        public float? MaxYaw { get; set; } = null;
        public float? MinRoll { get; set; } = null;
        public float? MaxRoll { get; set; } = null;

        // Current Euler angles (tracked internally to enforce min/max constraints safely)
        private float _pitch;
        private float _yaw;
        private float _roll;

        // Projection Properties
        public float AspectRatio { get; set; } = 1.0f;
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 1000.0f;
        public float FieldOfView { get; set; } = MathF.PI / 3.0f; // ~60 degrees default

        /// <summary>
        /// Moves the camera based on its local orientation vectors, obeying all locks and constraints.
        /// </summary>
        public void Move(Vector3 localDirection, float speed, float deltaTime)
        {
            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
            Vector3 right = Vector3.Transform(Vector3.UnitX, Rotation);
            Vector3 up = Vector3.Transform(Vector3.UnitY, Rotation);

            Vector3 moveDir = Vector3.Zero;

            if (AllowForwardBackward) moveDir += forward * localDirection.Z;
            if (AllowStrafe) moveDir += right * localDirection.X;
            if (AllowUpDown) moveDir += up * localDirection.Y;

            // Apply global per-axis structural filters/locks
            moveDir *= MovementAxisLock;

            if (moveDir != Vector3.Zero)
            {
                Position += Vector3.Normalize(moveDir) * speed * deltaTime;
            }
        }

        /// <summary>
        /// Rotates the camera securely using Pitch, Yaw, and Roll offsets while honoring constraints.
        /// </summary>
        public void Rotate(float deltaPitch, float deltaYaw, float deltaRoll)
        {
            _pitch += deltaPitch;
            _yaw += deltaYaw;
            _roll += deltaRoll;

            // Apply clamping constraints where assigned
            if (MinPitch.HasValue) _pitch = MathF.Max(_pitch, MinPitch.Value);
            if (MaxPitch.HasValue) _pitch = MathF.Min(_pitch, MaxPitch.Value);
            if (MinYaw.HasValue) _yaw = MathF.Max(_yaw, MinYaw.Value);
            if (MaxYaw.HasValue) _yaw = MathF.Min(_yaw, MaxYaw.Value);
            if (MinRoll.HasValue) _roll = MathF.Max(_roll, MinRoll.Value);
            if (MaxRoll.HasValue) _roll = MathF.Min(_roll, MaxRoll.Value);

            // Reconstruct Quaternion directly from Euler angles to sidestep Gimbal Lock completely
            Rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
        }

        /// <summary>
        /// Automatically aligns the camera over the Z axis so a specific target area maps pixel-perfect.
        /// </summary>
        public void ConfigurePixelPerfectOrthoMatch(float targetWidth, float targetHeight, float screenWidth, float screenHeight)
        {
            AspectRatio = screenWidth / screenHeight;

            // Face perfectly down the negative Z-axis toward the origin
            Rotation = Quaternion.Identity;

            // Target bounds centered at 0,0 require mapping matching ortho bounds
            float halfTargetWidth = targetWidth / 2.0f;
            float halfTargetHeight = targetHeight / 2.0f;

            // Calculate distance required based on the FOV safely mapping the system boundaries
            float distanceToFitVertical = halfTargetHeight / MathF.Tau * (MathF.PI / FieldOfView);
            float distanceToFitHorizontal = (halfTargetWidth / AspectRatio) / MathF.Tau * (MathF.PI / FieldOfView);

            // Position camera back far enough to map the most critical bounding edge
            float optimalDistance = MathF.Max(distanceToFitVertical, distanceToFitHorizontal);
            Position = new Vector3(0, 0, optimalDistance);
        }

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Position + Vector3.Transform(-Vector3.UnitZ, Rotation), Vector3.Transform(Vector3.UnitY, Rotation));
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
        }
    }

    private int _randomSeed = 10241024;
    private Random _random;

    private Camera _camera;

    public _021_Line_Axis_Grid_Camera()
    {
        Width = 1024f;
        Height = 1024f;
        _random = new Random(_randomSeed);
    }

    // Optional Update Lifecycle Hook
    public void Update(double deltaTime)
    {
    }

    // Dynamic Render Execution Step Loop Context 
    public void Render(double deltaTime)
    {
        Line()
            .From(0, 0, 0)
            .To(1, 0, 0);

        Line()
            .From(0, 0, 0)
            .To(0, 1, 0);

        Line()
            .From(0, 0, 0)
            .To(0, 0, 1);
    }

    // Optional Event Callbacks managed smoothly via engine reflection tracking layers
    void Resize(Vector2D<int> size)
    {
    }

    void Close()
    {
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