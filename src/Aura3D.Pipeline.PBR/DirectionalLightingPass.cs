using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

internal class DirectionalLightingPass : RenderPass
{
    readonly RenderTargetHandle gbufferRenderTarget;
    public DirectionalLightingPass(RenderPipeline renderPipeline, RenderTargetHandle gbufferRenderTarget) : base(renderPipeline)
    {
        this.gbufferRenderTarget = gbufferRenderTarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        ShaderName = nameof(DirectionalLightingPass);
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutputRenderTarget(camera);
    }

    public override void Render(Camera camera)
    {
        var rt = GetRenderTarget(gbufferRenderTarget, camera);

        var gBufferBaseColor = rt.GetTexture("BaseColor");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferMetallicEmissive = rt.GetTexture("MetallicEmissive");
        var depthTexture = rt.DepthStencilTexture;



        foreach (var dl in renderPipeline.DirectionalLights)
        {
            if (dl.Enable == false)
                continue;

            var csmData = dl.GetPipelineGpuState<CsmShadowData>(nameof(CsmShadowData));
            bool useCsm = dl.CastShadow && csmData != null;

            if (dl.CastShadow == false)
                UseShader("ENABLE_DIR_LIGHT", "ENBALE_DEFERRED_SHADING");
            else if (useCsm)
                UseShader("ENABLE_DIR_LIGHT", "ENABLE_SHADOWS", "ENABLE_CSM", "ENBALE_DEFERRED_SHADING");
            else
                UseShader("ENABLE_DIR_LIGHT", "ENABLE_SHADOWS", "ENBALE_DEFERRED_SHADING");

            UseShader_Internal();
            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", dl.Intensity);

            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());
            UniformMatrix4("viewMatrix", camera.View);

            if (useCsm)
            {
                for (int c = 0; c < csmData.CascadeCount; c++)
                    UniformMatrix4($"dirLightCSMMatrices[{c}]", csmData.CascadeMatrices[c]);
                UniformInt("dirLightCascadeCount", csmData.CascadeCount);
                for (int c = 0; c <= csmData.CascadeCount; c++)
                    UniformFloat($"dirLightCascadeSplitDepths[{c}]", csmData.CascadeSplitDepths[c]);
                UniformTextureArray("dirLightCSMMap", csmData.TextureArrayId);
            }
            else
            {
                var shadowmap = dl.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
                if (dl.CastShadow == true && shadowmap != null)
                {
                    var shadowView = Matrix4x4.CreateLookAt(dl.WorldTransform.Translation,
                        dl.WorldTransform.Translation + dl.WorldTransform.ForwardVector(),
                        dl.WorldTransform.UpVector());
                    var shadowProjection = Matrix4x4.CreateOrthographic(
                        dl.ShadowConfig.Width, dl.ShadowConfig.Height,
                        dl.ShadowConfig.NearPlane, dl.ShadowConfig.FarPlane);

                    UniformTexture($"dirLightshadowMap", shadowmap.DepthStencilTexture);
                    UniformMatrix4($"dirLightshadowMapMatrix", shadowView * shadowProjection);
                }
            }
            RenderQuad();
        }

    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
