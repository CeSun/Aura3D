using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

internal class SpotLightingPass : RenderPass
{
    readonly RenderTargetHandle gbufferRenderTarget;
    public SpotLightingPass(RenderPipeline renderPipeline, RenderTargetHandle gbufferRendertarget) : base(renderPipeline)
    {
        gbufferRenderTarget = gbufferRendertarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        ShaderName = nameof(SpotLightingPass);
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

        foreach (var sl in renderPipeline.SpotLights)
        {
            if (sl.Enable == false)
                continue;

            if (sl.CastShadow == false)
                UseShader("ENABLE_SPOT_LIGHT", "ENBALE_DEFERRED_SHADING");
            else
                UseShader("ENABLE_SPOT_LIGHT", "ENABLE_SHADOWS", "ENBALE_DEFERRED_SHADING");
            UseShader_Internal();

            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            UniformVector3("spotLightPosition", sl.WorldTransform.Translation);
            UniformVector3("spotLightDirection", sl.Forward);
            UniformColor("spotLightColor", sl.LightColor);
            UniformFloat("spotLightIntensity", sl.Intensity);
            UniformFloat("spotLightCutOff", MathF.Cos(sl.InnerConeAngleDegree.DegreeToRadians()));
            UniformFloat("spotLightOuterCutOff", MathF.Cos(sl.OuterConeAngleDegree.DegreeToRadians()));
            UniformFloat("radius", sl.AttenuationRadius);
            UniformFloat("softRatio", sl.SoftRatio);

            var shadowmap = sl.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
            if (sl.CastShadow && shadowmap != null)
            {
                var position = sl.WorldTransform.Translation;
                var shadowView = Matrix4x4.CreateLookAt(position, position + sl.WorldTransform.ForwardVector(), sl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(sl.OuterConeAngleDegree.DegreeToRadians(), shadowmap.Width / (float)shadowmap.Height, sl.ShadowConfig.NearPlane, sl.ShadowConfig.FarPlane);

                UniformTexture($"spotLightshadowMap", shadowmap.DepthStencilTexture);
                UniformMatrix4($"spotLightshadowMapMatrix", shadowView * shadowProjection);

            }

            RenderQuad();
        }

    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
