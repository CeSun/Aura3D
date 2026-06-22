using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

public class PointLightingPass : RenderPass
{
    readonly RenderTargetHandle gbufferRenderTarget;
    public PointLightingPass(RenderPipeline renderPipeline, RenderTargetHandle gbufferRendertarget) : base(renderPipeline)
    {
        gbufferRenderTarget = gbufferRendertarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        ShaderName = nameof(PointLightingPass);
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


        Span<Matrix4x4> ShadowViews = stackalloc Matrix4x4[6];
        foreach (var pl in renderPipeline.PointLights)
        {
            if (pl.Enable == false)
                continue;
            if (pl.CastShadow == false)
                UseShader("ENABLE_POINT_LIGHT", "ENBALE_DEFERRED_SHADING");
            else
                UseShader("ENABLE_POINT_LIGHT", "ENABLE_SHADOWS", "ENBALE_DEFERRED_SHADING");
            UseShader_Internal();


            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            UniformVector3("pointLightPosition", pl.WorldTransform.Translation);
            UniformColor("pointLightColor", pl.LightColor);
            UniformFloat("pointLightIntensity", pl.Intensity);
            UniformFloat("radius", pl.AttenuationRadius);
            UniformFloat("softRatio", pl.SoftRatio);

            var shadowmap = pl.GetPipelineGpuState<CubeRenderTarget>("ShadowMapRenderTarget");

            if (pl.CastShadow && shadowmap != null)
            {
                var position = pl.WorldTransform.Translation;

                ShadowViews[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
                ShadowViews[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
                ShadowViews[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
                ShadowViews[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
                ShadowViews[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
                ShadowViews[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));


                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), shadowmap.Width / (float)shadowmap.Height, pl.ShadowConfig.NearPlane, pl.ShadowConfig.FarPlane);


                UniformTextureCubeMap("pointLightShadowMap", shadowmap.DepthStencilTexture);
                for (int i = 0; i < 6; i++)
                {
                    UniformMatrix4($"pointShadowMapMatrices[{i}]", ShadowViews[i] * shadowProjection);
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
