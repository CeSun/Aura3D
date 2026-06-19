using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class CelTranslucentPass : LightPass
{
    public CelTranslucentPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
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

        var rt = GetRenderTarget("BaseRenderTarget",
            new System.Drawing.Size((int)camera.Width, (int)camera.Height));


        gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent), camera.View, camera.Projection);

    }
}
