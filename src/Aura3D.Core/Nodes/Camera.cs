using System.Drawing;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core.Math;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Nodes;

public class Camera : Node
{
    public float NearPlane { get; set; } = 1f; // 近裁剪面

    public float FarPlane { get; set; } = 100f; // 远裁剪面

    public float FieldOfView { get; set; } = 75f; // 视野角度（度数）

    public float OrthographicSize { get; set; } = 5f; // 正交投影时的大小

    public Matrix4x4 View
    {
        get
        {

            var worldTransform = WorldTransform;

            return Matrix4x4.CreateLookAt(worldTransform.Translation, worldTransform.Translation + worldTransform.ForwardVector(), worldTransform.UpVector());

        }
    }

    public Matrix4x4 Projection
    {
        get
        {
            if (ProjectionType == ProjectionType.Perspective)
            {
                return Matrix4x4.CreatePerspectiveFieldOfView(
                    FieldOfView.DegreeToRadians(),
                    RenderTarget.Width / (float)RenderTarget.Height,
                    NearPlane,
                    FarPlane);
            }
            else // Orthographic
            {
                float aspectRatio = RenderTarget.Width / (float)RenderTarget.Height;
                return Matrix4x4.CreateOrthographic(
                    OrthographicSize * aspectRatio, // 宽度
                    OrthographicSize, // 高度
                    NearPlane,
                    FarPlane);
            }
        }
    }

    public Matrix4x4 ViewProjection => View * Projection;

    public Color ClearColor { get; set; } = Color.FromArgb(0, 0, 0, 0); // 清除颜色

    public ProjectionType ProjectionType { get; set; } = ProjectionType.Perspective; // 投影类型

    public IRenderTarget RenderTarget { get; set; } = new ControlRenderTarget();

    public ClearType ClearType { get; set; } = ClearType.Color; // 清除类型


    private CubeTexture? skyboxTexture = null;

    public  CubeTexture? SkyboxTexture
    {
        get => skyboxTexture;
        set
        {
            if (value != null && CurrentScene != null)
            {
                CurrentScene.RenderPipeline.AddGpuResource(value);
            }
            skyboxTexture = value;
        }
    }

    private Texture? backgroundTexture = null;
    public Texture? BackgroundTexture 
    { 
        get => backgroundTexture;
        set
        {
            if (value != null && CurrentScene != null)
            {
                CurrentScene.RenderPipeline.AddGpuResource(value);
            }
            backgroundTexture = value;
        }
    }

    public override List<IGpuResource> GetGpuResources()
    {
        var list = new List<IGpuResource>();

        if (SkyboxTexture != null)
        {
            list.Add(SkyboxTexture);
        }
        if (BackgroundTexture != null)
        {
            list.Add(BackgroundTexture);
        }
        return list;
    }
}

public enum ProjectionType
{
    Perspective, // 透视投影
    Orthographic // 正交投影
}

public enum ClearType
{
    OnlyDepth, // 仅清除颜色缓冲区
    Color,
    Skybox,
    Texture
}