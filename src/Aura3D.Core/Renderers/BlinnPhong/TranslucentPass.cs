using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 透明物体渲染通道，渲染半透明物体
/// </summary>
public class TranslucentPass : LightPass
{
    /// <summary>
    /// 初始化透明物体渲染通道
    /// </summary>
    /// <param name="renderPipeline">渲染管线</param>
    public TranslucentPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
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

        var rt = GetRenderTarget("BaseRenderTarget",
            new System.Drawing.Size((int)camera.Width, (int)camera.Height));

        gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Translucent) && mesh.IsStaticMesh, camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Translucent) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent), camera.View, camera.Projection);
    }
}
