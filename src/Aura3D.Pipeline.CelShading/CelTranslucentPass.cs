using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class CelTranslucentPass : LightPass
{
    private readonly RenderTargetHandle _baseRenderTarget;

    public CelTranslucentPass(RenderPipeline renderPipeline, RenderTargetHandle baseRenderTarget) : base(renderPipeline)
    {
        _baseRenderTarget = baseRenderTarget;
        ShaderName = nameof(CelTranslucentPass);
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
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent), camera.View, camera.Projection);

    }
}
