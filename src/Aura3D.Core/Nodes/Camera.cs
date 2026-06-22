using System.Drawing;
using System.Numerics;
using Aura3D.Core.Math;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 相机节点，负责场景的拍摄与渲染，支持透视与正交两种投影模式。
/// </summary>
public class Camera : Node
{
    /// <summary>
    /// 获取或设置近裁剪面距离。
    /// </summary>
    public float NearPlane { get; set; } = 1f; // 近裁剪面

    /// <summary>
    /// 获取或设置远裁剪面距离。
    /// </summary>
    public float FarPlane { get; set; } = 100f; // 远裁剪面

    /// <summary>
    /// 获取或设置视野角度（度数）。
    /// </summary>
    public float FieldOfView { get; set; } = 75f; // 视野角度（度数）

    /// <summary>
    /// 获取或设置正交投影时的大小。
    /// </summary>
    public float OrthographicSize { get; set; } = 5f; // 正交投影时的大小

    /// <summary>
    /// 获取观察矩阵。
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
    /// 获取投影矩阵。
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
    /// 获取视图投影矩阵（View * Projection）。
    /// </summary>
    public Matrix4x4 ViewProjection => View * Projection;

    /// <summary>
    /// 将世界空间坐标映射到屏幕控件坐标。返回 null 表示目标在相机后方。
    /// </summary>
    /// <param name="worldPos">世界空间位置。</param>
    /// <returns>控件像素坐标，原点在控件左上角；如果目标在相机后方则返回 null。</returns>
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
    /// 获取或设置投影类型。
    /// </summary>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.Perspective; // 投影类型

    /// <summary>
    /// 获取当前相机输出纹理的宽度。
    /// </summary>
    public uint Width => OutputTexture != null
        ? OutputTexture.Width
        : GetDefaultOutputSurfaceOrThrow().Width;

    /// <summary>
    /// 获取当前相机输出纹理的高度。
    /// </summary>
    public uint Height => OutputTexture != null
        ? OutputTexture.Height
        : GetDefaultOutputSurfaceOrThrow().Height;

    /// <summary>
    /// 获取屏幕空间与像素空间的缩放比。
    /// </summary>
    public float ScreenScale => OutputTexture != null ? 1f : GetDefaultOutputSurfaceOrThrow().Scale;

    /// <summary>
    /// 获取或设置相机最终颜色输出对应的可写纹理。
    /// 该纹理是相机默认渲染路径的唯一颜色输出。
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
            throw new InvalidOperationException("Camera must be added to a scene before querying Width or Height when OutputTexture is not set.");
        }

        return CurrentScene.DefaultOutputSurface
               ?? throw new InvalidOperationException("Scene default output surface is not set.");
    }

    /// <summary>
    /// 获取或设置是否渲染背景。
    /// </summary>
    public bool IsRenderBackground { get; set; } = true;

    /// <summary>
    /// 使相机朝向指定目标点。
    /// </summary>
    /// <param name="target">目标位置。</param>
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
    /// 调整相机位置与裁剪面，使其完整包围指定的轴对齐包围盒。
    /// </summary>
    /// <param name="aabb">包围盒。</param>
    /// <param name="padding">边距比例，范围为 0 到 1。</param>
    public void FitToBoundingBox(BoundingBox aabb, float padding = 0.1f)
    {
        var camera = this;
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        if (aabb == null) throw new ArgumentNullException(nameof(aabb));
        if (padding < 0 || padding > 1) throw new ArgumentOutOfRangeException(nameof(padding));

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

        camera.NearPlane = distance - boxDiagonal * 0.6f;
        camera.FarPlane = distance + boxDiagonal * 1.2f;

        if (camera.NearPlane < 0)
        {
            camera.NearPlane = -camera.NearPlane;

            camera.FarPlane = camera.FarPlane + 2 * camera.NearPlane;
        }

        camera.LookAt(boxCenter);
    }
}

/// <summary>
/// 投影类型。
/// </summary>
public enum ProjectionType
{
    Perspective, // 透视投影
    Orthographic // 正交投影
}

/// <summary>
/// 清除缓冲区类型。
/// </summary>
public enum ClearType
{
    OnlyDepth, // 仅清除颜色缓冲区
    Color,
    Skybox,
    Texture
}
