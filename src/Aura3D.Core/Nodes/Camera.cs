using System.Drawing;
using System.Numerics;
using Aura3D.Core.Math;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the camera type.
/// </summary>
public class Camera : Node
{
    /// <summary>
    /// Gets or sets the near plane.
    /// </summary>
    public float NearPlane
    {
        get => _nearPlane;
        set
        {
            ValidatePositiveFinite(value, nameof(NearPlane));
            if (value >= _farPlane)
                throw new ArgumentOutOfRangeException(nameof(NearPlane), "Near plane must be less than the far plane.");
            _nearPlane = value;
        }
    }

    private float _nearPlane = 1f;

    /// <summary>
    /// Gets or sets the far plane.
    /// </summary>
    public float FarPlane
    {
        get => _farPlane;
        set
        {
            ValidatePositiveFinite(value, nameof(FarPlane));
            if (value <= _nearPlane)
                throw new ArgumentOutOfRangeException(nameof(FarPlane), "Far plane must be greater than the near plane.");
            _farPlane = value;
        }
    }

    private float _farPlane = 100f;

    /// <summary>
    /// Gets or sets the field of view.
    /// </summary>
    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            if (!float.IsFinite(value) || value <= 0f || value >= 180f)
                throw new ArgumentOutOfRangeException(nameof(FieldOfView), "Field of view must be finite and between 0 and 180 degrees.");
            _fieldOfView = value;
        }
    }

    private float _fieldOfView = 75f;

    /// <summary>
    /// Gets or sets the orthographic size.
    /// </summary>
    public float OrthographicSize
    {
        get => _orthographicSize;
        set
        {
            ValidatePositiveFinite(value, nameof(OrthographicSize));
            _orthographicSize = value;
        }
    }

    private float _orthographicSize = 5f;

    /// <summary>
    /// Gets the view.
    /// </summary>
    public Matrix4x4 View
    {
        get
        {
            var worldTransform = WorldTransform;

            return Matrix4x4.CreateLookAt(worldTransform.Translation, worldTransform.Translation + worldTransform.ForwardVector(), worldTransform.UpVector());

        }
    }

    /// <summary>
    /// Gets the projection.
    /// </summary>
    public Matrix4x4 Projection
    {
        get
        {
            if (ProjectionType == ProjectionType.Perspective)
            {
                var fovRadians = FieldOfView.DegreeToRadians();

                var aspectRatio = Width / (float)Height;

                var projection =  Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, NearPlane, FarPlane);

                return projection;
            }
            else // Orthographic
            {
                float aspectRatio = Width / (float)Height;
                return Matrix4x4.CreateOrthographic(
                    OrthographicSize * aspectRatio, // 宽度
                    OrthographicSize, // 高度
                    NearPlane,
                    FarPlane);
            }
        }
    }

    /// <summary>
    /// Gets the view projection.
    /// </summary>
    public Matrix4x4 ViewProjection => View * Projection;

    /// <summary>
    /// Performs the world to screen operation.
    /// </summary>
    public Vector2? WorldToScreen(Vector3 worldPos)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1), ViewProjection);
        if (clip.W <= 0) return null;

        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;

        float screenX = (ndcX + 1f) * 0.5f * Width / ScreenScale;
        float screenY = (1f - ndcY) * 0.5f * Height / ScreenScale;

        return new Vector2(screenX, screenY);
    }

    /// <summary>
    /// Gets or sets the projection type.
    /// </summary>
    public ProjectionType ProjectionType
    {
        get => _projectionType;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(ProjectionType), value, "Unsupported projection type.");
            _projectionType = value;
        }
    }

    private ProjectionType _projectionType = ProjectionType.Perspective;

    /// <summary>
    /// Gets the width.
    /// </summary>
    public uint Width => OutputTexture != null
        ? OutputTexture.Width
        : GetDefaultOutputSurfaceOrThrow().Width;

    /// <summary>
    /// Gets the height.
    /// </summary>
    public uint Height => OutputTexture != null
        ? OutputTexture.Height
        : GetDefaultOutputSurfaceOrThrow().Height;

    /// <summary>
    /// Gets the screen scale.
    /// </summary>
    public float ScreenScale => OutputTexture != null ? 1f : GetDefaultOutputSurfaceOrThrow().Scale;

    /// <summary>
    /// Gets the output texture.
    /// </summary>
    public WritableTexture? OutputTexture
    {
        get => _outputTexture;
        set => _outputTexture = value;
    }

    private WritableTexture? _outputTexture;

    private RenderSurface? DefaultOutputSurface => CurrentScene?.DefaultOutputSurface;

    private RenderSurface GetDefaultOutputSurfaceOrThrow()
    {
        if (CurrentScene == null)
        {
            throw Aura3D.Core.Exceptions.NodeErrors.CameraMustBelongToScene();
        }

        return CurrentScene.DefaultOutputSurface
               ?? throw Aura3D.Core.Exceptions.RendererErrors.DefaultOutputSurfaceNotSet();
    }

    /// <summary>
    /// Gets a value indicating whether the object is render background.
    /// </summary>
    public bool IsRenderBackground { get; set; } = true;

    /// <summary>
    /// Performs the look at operation.
    /// </summary>
    public void LookAt(Vector3 target)
    {
        var camera = this;

        Vector3 cameraPos = camera.Position;

        Vector3 forward = Vector3.Normalize(target - cameraPos);

        Vector3 up = Vector3.UnitY; // 假设世界上方向为Y轴

        // 计算右向量
        Vector3 right = Vector3.Cross(forward, up);
        // 重新计算正交上向量
        up = Vector3.Cross(right, forward);

        // 构建旋转矩阵
        Matrix4x4 rotation = Matrix4x4.Identity;
        rotation.M11 = right.X;
        rotation.M21 = right.Y;
        rotation.M31 = right.Z;
        rotation.M12 = up.X;
        rotation.M22 = up.Y;
        rotation.M32 = up.Z;
        rotation.M13 = -forward.X;
        rotation.M23 = -forward.Y;
        rotation.M33 = -forward.Z;

        // 从旋转矩阵提取欧拉角（弧度）
        float pitch = MathF.Asin(-rotation.M23);
        float yaw = MathF.Atan2(rotation.M13, rotation.M33);
        float roll = MathF.Atan2(rotation.M21, rotation.M22);

        // 转换为角度并设置
        camera.RotationDegrees = new Vector3(
            pitch.RadiansToDegree(),
            yaw.RadiansToDegree(),
            roll.RadiansToDegree()
        );
    }

    /// <summary>
    /// Performs the fit to bounding box operation.
    /// </summary>
    public void FitToBoundingBox(BoundingBox aabb, float padding = 0.1f)
    {
        var camera = this;
        ArgumentNullException.ThrowIfNull(aabb);
        if (!float.IsFinite(padding) || padding < 0 || padding > 1)
            throw new ArgumentOutOfRangeException(nameof(padding), "Padding must be finite and between 0 and 1.");

        Vector3 boxCenter = aabb.Center;
        Vector3 boxSize = aabb.Size;

        float fovRadians = camera.FieldOfView.DegreeToRadians();
        float aspectRatio = camera.Width / (float)camera.Height;

        float maxExtent = MathF.Max(boxSize.X, MathF.Max(boxSize.Y, boxSize.Z)) / 2f;

        float distance = maxExtent / MathF.Sin(fovRadians / 2f) * (1 + padding);
        distance = MathF.Max(distance, maxExtent / (MathF.Sin(fovRadians / 2f) * aspectRatio) * (1 + padding));
       

        Vector3 cameraDirection = camera.Forward;
        camera.Position = boxCenter - cameraDirection * distance;

        float boxDiagonal = boxSize.Length();

        float nearPlane = distance - boxDiagonal * 0.6f;
        float farPlane = distance + boxDiagonal * 1.2f;

        if (nearPlane <= 0)
        {
            nearPlane = MathF.Max(float.Epsilon, -nearPlane);
            farPlane += 2 * nearPlane;
        }

        camera.SetClippingPlanes(nearPlane, farPlane);

        camera.LookAt(boxCenter);
    }

    /// <summary>
    /// Atomically updates the clipping planes after validating their relationship.
    /// </summary>
    public void SetClippingPlanes(float nearPlane, float farPlane)
    {
        ValidatePositiveFinite(nearPlane, nameof(nearPlane));
        ValidatePositiveFinite(farPlane, nameof(farPlane));
        if (nearPlane >= farPlane)
            throw new ArgumentOutOfRangeException(nameof(nearPlane), "Near plane must be less than the far plane.");

        _nearPlane = nearPlane;
        _farPlane = farPlane;
    }

    private static void ValidatePositiveFinite(float value, string paramName)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(paramName, "Value must be finite and greater than zero.");
    }
}

/// <summary>
/// Specifies values for projection type.
/// </summary>
public enum ProjectionType
{
    /// <summary>
    /// Specifies perspective.
    /// </summary>
    Perspective, // 透视投影
    /// <summary>
    /// Specifies orthographic.
    /// </summary>
    Orthographic // 正交投影
}

/// <summary>
/// Specifies values for clear type.
/// </summary>
public enum ClearType
{
    /// <summary>
    /// Specifies only depth.
    /// </summary>
    OnlyDepth, // 仅清除颜色缓冲区
    /// <summary>
    /// Specifies color.
    /// </summary>
    Color,
    /// <summary>
    /// Specifies skybox.
    /// </summary>
    Skybox,
    /// <summary>
    /// Specifies texture.
    /// </summary>
    Texture
}
