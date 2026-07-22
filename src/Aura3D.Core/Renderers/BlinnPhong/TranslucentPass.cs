using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the translucent pass type.
/// </summary>
public class TranslucentPass : LightPass
{
    private readonly RenderTargetHandle _baseRenderTarget;

    /// <summary>
    /// Initializes a new instance of the translucent pass type.
    /// </summary>
    public TranslucentPass(RenderPipeline renderPipeline, RenderTargetHandle baseRenderTarget) : base(renderPipeline)
    {
        _baseRenderTarget = baseRenderTarget;
        ShaderName = nameof(TranslucentPass);
    }

    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    /// <summary>
    /// Renders the associated data.
    /// </summary>
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
