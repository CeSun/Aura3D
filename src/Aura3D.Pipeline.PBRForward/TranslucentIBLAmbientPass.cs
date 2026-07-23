using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Camera = Aura3D.Core.Nodes.Camera;
using Aura3D.Core.Renderers;

using Aura3D.Pipeline.PBR.Common;

namespace Aura3D.Pipeline.PBRForward;

internal class TranslucentIBLAmbientPass : RenderPass<PBRPipelineBase>
{
    Core.Resources.Texture defaultBaseColor => RenderPipeline.DefaultBaseColor;

    Core.Resources.Texture defaultNormal => RenderPipeline.DefaultNormal;

    Core.Resources.Texture defaultMetallicRoughness => RenderPipeline.DefaultMetallicRoughness;

    Core.Resources.Texture defaultEmissive => RenderPipeline.DefaultEmissive;

    Core.Resources.Texture defaultOcclusion => RenderPipeline.DefaultOcclusion;

    readonly RenderTargetHandle depthRenderTarget;

    public TranslucentIBLAmbientPass(RenderPipeline renderPipeline, RenderTargetHandle depthRenderTarget) : base(renderPipeline)
    {
        this.depthRenderTarget = depthRenderTarget;

        VertexShader = PbrForwardResources.MeshVertexShader;

        FragmentShader = PbrForwardResources.IblAmbientFragmentShader;
    }

    public override void BeforeRender(Camera camera)
    {
        this.camera = camera;
        BindOutput(camera);

        var gbuffer = GetRenderTarget(depthRenderTarget, camera);

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
        var outputRt = GetOutputRenderTargetOrThrow(camera);

        gl.BindFramebuffer(GLEnum.Framebuffer, outputRt.FrameBufferId);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, outputRt.DepthTextureFormat.ToGlAttachment(), GLEnum.Texture2D, outputRt.DepthStencilTexture.TextureId, 0);

        var error = gl.GetError();

        gl.DepthMask(true);


    }

    public override void Render(Camera camera)
    {


        var perfilteredEnvMap = camera!.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap")!;
        var u_prefilterMap = perfilteredEnvMap.GetTexture(0)!;

        int nearestPowerOfTwo = (int)MathF.Pow(2, MathF.Floor(MathF.Log2(u_prefilterMap.Width)));
        mipmap = BitOperations.TrailingZeroCount((uint)nearestPowerOfTwo) + 1;

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

    Camera? camera;
    int mipmap;
    public void RenderTranslucentMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        UseShader("BLENDMODE_TRANSLUCENT");
        if (mesh.IsSkinnedMesh)
            AddDefines("SKINNED_MESH");
        UseShader_Internal(mesh);
        SetupUpMeshUniforms(mesh, view, projection);
        base.RenderMesh(mesh, view, projection);


    }

    public void SetupUpMeshUniforms(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        var u_brdfLUT = RenderPipeline.BrdfLutTexture;


        var irradianceMap = camera!.GetPipelineGpuState<CubeRenderTarget>("IrradianceMap")!;
        var u_irradianceMap = irradianceMap.GetTexture(0)!;

        var perfilteredEnvMap = camera!.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap")!;
        var u_prefilterMap = perfilteredEnvMap.GetTexture(0)!;


        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        UniformVector3("u_cameraPos", camera!.WorldTransform.Translation);

        ClearTextureUnit();
        {

            var baseColor = mesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor;
            UniformTexture("Texture_BaseColor", baseColor);


            var normal = mesh.Material?.GetTexture("Normal") ?? defaultNormal;
            UniformTexture("Texture_Normal", normal);

            var metallicRoughness = mesh.Material?.GetTexture("MetallicRoughness") ?? defaultMetallicRoughness;
            UniformTexture("Texture_MetallicRoughness", metallicRoughness);


            var occlusion = mesh.Material?.GetTexture("Occlusion") ?? defaultOcclusion;
            UniformTexture("Texture_Occlusion", occlusion);

            var emissive = mesh.Material?.GetTexture("Emissive") ?? defaultEmissive;
            UniformTexture("Texture_Emissive", emissive);


            UniformTexture(nameof(u_brdfLUT), u_brdfLUT);

            UniformTextureCubeMap(nameof(u_irradianceMap), u_irradianceMap);

            UniformTextureCubeMap(nameof(u_prefilterMap), u_prefilterMap);

        }


        UniformFloat("u_max_mipmap", mipmap);

        if (mesh.IsSkinnedMesh)
        {
            SyncAndBindBoneMatrixBuffer(mesh);
        }
    }

    private void SetupUpInstancedMeshUniforms(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        var u_brdfLUT = RenderPipeline.BrdfLutTexture;

        var irradianceMap = camera!.GetPipelineGpuState<CubeRenderTarget>("IrradianceMap")!;
        var u_irradianceMap = irradianceMap.GetTexture(0)!;

        var perfilteredEnvMap = camera!.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap")!;
        var u_prefilterMap = perfilteredEnvMap.GetTexture(0)!;

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        UniformVector3("u_cameraPos", camera!.WorldTransform.Translation);

        ClearTextureUnit();
        {
            var baseColor = instancedMesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor;
            UniformTexture("Texture_BaseColor", baseColor);

            var normal = instancedMesh.Material?.GetTexture("Normal") ?? defaultNormal;
            UniformTexture("Texture_Normal", normal);

            var metallicRoughness = instancedMesh.Material?.GetTexture("MetallicRoughness") ?? defaultMetallicRoughness;
            UniformTexture("Texture_MetallicRoughness", metallicRoughness);

            var occlusion = instancedMesh.Material?.GetTexture("Occlusion") ?? defaultOcclusion;
            UniformTexture("Texture_Occlusion", occlusion);

            var emissive = instancedMesh.Material?.GetTexture("Emissive") ?? defaultEmissive;
            UniformTexture("Texture_Emissive", emissive);

            UniformTexture(nameof(u_brdfLUT), u_brdfLUT);

            UniformTextureCubeMap(nameof(u_irradianceMap), u_irradianceMap);

            UniformTextureCubeMap(nameof(u_prefilterMap), u_prefilterMap);
        }

        UniformFloat("u_max_mipmap", mipmap);
    }

    public void RenderTranslucentInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UseShader("BLENDMODE_TRANSLUCENT", "INSTANCED_MESH");
        UseShader_Internal(instancedMesh.Material);
        SetupUpInstancedMeshUniforms(instancedMesh, view, projection);
        base.RenderInstancedMesh(instancedMesh, view, projection);
    }
}
