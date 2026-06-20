using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 透明物体渲染通道，渲染半透明物体
/// </summary>
public class TranslucentPass : LightPass
{
    private readonly RenderTargetHandle _baseRenderTarget;

    /// <summary>
    /// 初始化透明物体渲染通道
    /// </summary>
    /// <param name="renderPipeline">渲染管线</param>
    /// <param name="baseRenderTarget">透明物体写入的目标渲染目标。</param>
    public TranslucentPass(RenderPipeline renderPipeline, RenderTargetHandle baseRenderTarget) : base(renderPipeline)
    {
        _baseRenderTarget = baseRenderTarget;
        ShaderName = nameof(TranslucentPass);
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    public override void Render(Camera camera)
    {
        var rt = GetRenderTarget(_baseRenderTarget, camera);

        gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Translucent) && mesh.IsStaticMesh, camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Translucent) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent), camera.View, camera.Projection);
    }
}
