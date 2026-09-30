using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;
using Aura3D.Core.Math;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class OutlinePass : RenderPass
{
    private Vector4 outlineColor = new Vector4(0.0f, 0.0f, 0.0f, 1f);

    public float AmbientIntensity = 0.1f;

    /// <summary>
    /// 描边最大宽度（像素）。物体屏占比再大，描边也不会超过该宽度。
    /// </summary>
    public float OutlineWidth { get; set; } = 2f;

    /// <summary>
    /// 描边宽度相对物体屏幕占比的比例。
    /// 物体的屏幕占比（包围盒投到屏幕上的最大跨度，NDC 全范围 2）乘以该值得到描边宽度：
    /// 屏占比越大描边越接近 OutlineWidth 的上限，屏占比越小描边越细。
    /// </summary>
    public float OutlineScreenRatio { get; set; } = 0.03f;

    /// <summary>
    /// 描边最小宽度（像素）。屏占比很小的远处物体会按比例缩到该下限，
    /// 设为 0 可让特别远的物体描边自然淡出。
    /// </summary>
    public float MinOutlineWidth { get; set; } = 0.5f;

    // 由 BeforeRender 按当前相机分辨率换算好的 NDC 宽度上下限
    private float maxWidthNdc;
    private float minWidthNdc;

    public OutlinePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = CelShadingResources.OutlineVertexShader;
        FragmentShader = CelShadingResources.OutlineFragmentShader;
    }
    public override void Setup()
    {
        // Setup logic for the base pass
        // This can include setting up shaders, buffers, etc.
    }
    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Front);

        // 像素宽度换算成 NDC：NDC 全范围是 2
        maxWidthNdc = OutlineWidth * 2f / MathF.Max(camera.Height, 1);
        minWidthNdc = MinOutlineWidth * 2f / MathF.Max(camera.Height, 1);
    }

    public override void Render(Camera camera)
    {
        BindOutputRenderTarget(camera);

        UseShader();
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);
    }

    protected void SetupUniform(Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformVector3("cameraPosition", view.Inverse().Translation);
        UniformVector4("BaseColor", outlineColor);
    }
    public override void AfterRender(Camera camera)
    {
        gl.Disable(EnableCap.CullFace);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        SetupUniform(view, projection);

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        var normalPrjMatrix = (projection * view * mesh.WorldTransform).Inverse();
        normalPrjMatrix = Matrix4x4.Transpose(normalPrjMatrix);
        UniformMatrix4("normalPrjMatrix", normalPrjMatrix);

        UniformFloat("outlineWidthNdc", CalculateOutlineWidthNdc(mesh, view, projection));

        if (mesh.IsSkinnedMesh)
        {
            SyncAndBindBoneMatrixBuffer(mesh);
        }
        base.RenderMesh(mesh, view, projection);
    }

    /// <summary>
    /// 根据物体在屏幕上的占比计算描边宽度（NDC 单位）：
    /// width = clamp(OutlineScreenRatio * 屏幕占比, MinOutlineWidth, OutlineWidth)
    /// 屏幕占比取世界包围盒 8 个角点投影到 NDC 后 x/y 方向的最大跨度。
    /// </summary>
    private float CalculateOutlineWidthNdc(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        var worldBoundingBox = mesh.BoundingBox;
        if (worldBoundingBox == null)
            return maxWidthNdc;

        var min = worldBoundingBox.Min;
        var max = worldBoundingBox.Max;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        int validCorners = 0;

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? min.X : max.X,
                (i & 2) == 0 ? min.Y : max.Y,
                (i & 4) == 0 ? min.Z : max.Z);

            // 与 shader 的 projectionMatrix * viewMatrix * worldPosition 保持同一变换约定
            var clip = Vector4.Transform(new Vector4(corner, 1f), view);
            clip = Vector4.Transform(clip, projection);

            // 近平面之后的角点透视除法不可靠，跳过
            if (clip.W <= 0.0001f)
                continue;

            float x = clip.X / clip.W;
            float y = clip.Y / clip.W;

            minX = MathF.Min(minX, x);
            maxX = MathF.Max(maxX, x);
            minY = MathF.Min(minY, y);
            maxY = MathF.Max(maxY, y);
            validCorners++;
        }

        // 整个包围盒都在近平面之后（正常情况下该 mesh 不会进入渲染列表），回退到最大宽度
        if (validCorners == 0)
            return maxWidthNdc;

        float screenExtentNdc = MathF.Max(maxX - minX, maxY - minY);

        return Math.Clamp(OutlineScreenRatio * screenExtentNdc, minWidthNdc, maxWidthNdc);
    }

}
