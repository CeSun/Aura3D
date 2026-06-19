using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

internal class TranslucentConstantAmbientPass : RenderPass<PBRDeferredPipeline>
{
    Core.Resources.Texture defaultBaseColor => RenderPipeline.DefaultBaseColor;

    Core.Resources.Texture defaultOcclusion => RenderPipeline.DefaultOcclusion;

    string GbufferRenderTargetName;
    public TranslucentConstantAmbientPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;

        VertexShader = ShaderResource.MeshVert;

        FragmentShader = ShaderResource.pbr_constant_ambient_frag;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        if (outputRenderTargetName == null)
            throw new InvalidOperationException("Output render target is not set.");

        var gbuffer = GetRenderTarget(GbufferRenderTargetName,
                new System.Drawing.Size((int)camera.Width, (int)camera.Height));

        gl.FramebufferTexture2D(GLEnum.Framebuffer, gbuffer.DepthTextureFormat.ToGlAttachment(), GLEnum.Texture2D, gbuffer.DepthStencilTexture.TextureId, 0);

        var error = gl.GetError();

        gl.Enable(EnableCap.DepthTest);

        gl.DepthMask(false);

        gl.Enable(EnableCap.Blend);

        gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);

    }
    public override void AfterRender(Camera camera)
    {
        if (outputRenderTargetName == null)
            throw new InvalidOperationException("Output render target is not set.");
        var outputRt = GetRenderTarget(outputRenderTargetName,
                new System.Drawing.Size((int)camera.Width, (int)camera.Height));

        gl.BindFramebuffer(GLEnum.Framebuffer, outputRt.FrameBufferId);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, outputRt.DepthTextureFormat.ToGlAttachment(), GLEnum.Texture2D, outputRt.DepthStencilTexture.TextureId, 0);

        var error = gl.GetError();

        gl.DepthMask(true);


    }

    public override void Render(Camera camera)
    {

        foreach (var mesh in VisibleMeshesInCamera)
        {
            if (IsMaterialBlendMode(mesh, BlendMode.Translucent))
            {
                RenderTranslucentMesh(mesh, camera.View, camera.Projection);
            }
        }

        var instancedMeshes = EnableFrustumCulling
            ? renderPipeline.VisibleInstancedMeshesInCamera
            : renderPipeline.InstancedMeshes;

        foreach (var instancedMesh in instancedMeshes)
        {
            if (instancedMesh.Enable == false)
                continue;

            if (IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent))
            {
                RenderTranslucentInstancedMesh(instancedMesh, camera.View, camera.Projection);
            }
        }
    }


    public void RenderTranslucentMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        UseShader("BLENDMODE_TRANSLUCENT", "IS_FIRST_LIGHT");
        if (mesh.IsSkinnedMesh)
            AddDefines("SKINNED_MESH");
        UseShader_Internal(mesh);
        SetupUpMeshUniforms(mesh, view, projection);
        base.RenderMesh(mesh, view, projection);


    }

    public void SetupUpMeshUniforms(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);


        {

            var baseColor = mesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor;
            UniformTexture("Texture_BaseColor", baseColor);

            var occlusion = mesh.Material?.GetTexture("Occlusion") ?? defaultOcclusion;
            UniformTexture("Texture_Occlusion", occlusion);
        }


        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);


        if (mesh.IsSkinnedMesh)
        {
            var boneBuffer = mesh.AnimationSampler?.BoneMatrixBuffer ?? mesh.Skeleton.BoneMatrixBuffer;
            renderPipeline.EnsureUploaded(boneBuffer);
            boneBuffer.Bind();
        }
    }

    private void SetupUpInstancedMeshUniforms(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        {
            var baseColor = instancedMesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor;
            UniformTexture("Texture_BaseColor", baseColor);

            var occlusion = instancedMesh.Material?.GetTexture("Occlusion") ?? defaultOcclusion;
            UniformTexture("Texture_Occlusion", occlusion);
        }
    }

    public void RenderTranslucentInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UseShader("BLENDMODE_TRANSLUCENT", "IS_FIRST_LIGHT", "INSTANCED_MESH");
        UseShader_Internal(instancedMesh.Material);
        SetupUpInstancedMeshUniforms(instancedMesh, view, projection);
        base.RenderInstancedMesh(instancedMesh, view, projection);
    }
}
