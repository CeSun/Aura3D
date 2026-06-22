using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

internal class TranslucentPass : RenderPass<PBRDeferredPipeline>
{
    Core.Resources.Texture defaultBaseColor => RenderPipeline.DefaultBaseColor;

    Core.Resources.Texture defaultNormal => RenderPipeline.DefaultNormal;

    Core.Resources.Texture defaultMetallicRoughness => RenderPipeline.DefaultMetallicRoughness;

    Core.Resources.Texture defaultEmissive => RenderPipeline.DefaultEmissive;

    Core.Resources.Texture defaultOcclusion => RenderPipeline.DefaultOcclusion;
    readonly RenderTargetHandle gbufferRenderTarget;
    public TranslucentPass(RenderPipeline renderPipeline, RenderTargetHandle gbufferRendertarget) : base(renderPipeline)
    {
        gbufferRenderTarget = gbufferRendertarget;

        VertexShader = ShaderResource.MeshVert;

        FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        ShaderName = nameof(TranslucentPass);
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutput(camera);

        var gbuffer = GetRenderTarget(gbufferRenderTarget, camera);

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

        foreach(var mesh in VisibleMeshesInCamera)
        {
            if (mesh.Enable == false)
                continue;

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

        List<string> defines = [];
        if (mesh.IsSkinnedMesh)
            defines = ["SKINNED_MESH"];
        foreach(var dl in renderPipeline.DirectionalLights)
        {
            if (dl.Enable == false)
                continue;

            UseShader("ENABLE_DIR_LIGHT", "BLENDMODE_TRANSLUCENT");
            if (mesh.IsSkinnedMesh)
                AddDefines("SKINNED_MESH");
            UseShader_Internal(mesh);

            ClearTextureUnit();
            SetupUpMeshUniforms(mesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", dl.Intensity);

            var rt = dl.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
            if (dl.CastShadow == true && rt != null)
            {
                var shadowView = Matrix4x4.CreateLookAt(dl.WorldTransform.Translation, dl.WorldTransform.Translation + dl.WorldTransform.ForwardVector(), dl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreateOrthographic(dl.ShadowConfig.Width, dl.ShadowConfig.Height, dl.ShadowConfig.NearPlane, dl.ShadowConfig.FarPlane);

                UniformTexture($"dirLightshadowMap", rt.DepthStencilTexture);
                UniformMatrix4($"dirLightshadowMapMatrix", shadowView * shadowProjection);

            }
            base.RenderMesh(mesh, view, projection);
        }

        Span<Matrix4x4> ShadowViews = stackalloc Matrix4x4[6];

        foreach (var pl in renderPipeline.PointLights)
        {
            if (pl.Enable == false)
                continue;

            UseShader("ENABLE_POINT_LIGHT", "BLENDMODE_TRANSLUCENT");

            if (mesh.IsSkinnedMesh)
                AddDefines("SKINNED_MESH");
            UseShader_Internal(mesh);

            ClearTextureUnit();
            SetupUpMeshUniforms(mesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);

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
            base.RenderMesh(mesh, view, projection);
        }

        foreach(var sl in renderPipeline.SpotLights)
        {
            if (sl.Enable == false)
                continue;

            UseShader("ENABLE_SPOT_LIGHT", "BLENDMODE_TRANSLUCENT");

            if (mesh.IsSkinnedMesh)
                AddDefines("SKINNED_MESH");
            UseShader_Internal(mesh);

            UniformColor("ambientColor", Color.White);
            UniformFloat("ambientIntensity", 0.3f);
            ClearTextureUnit();
            SetupUpMeshUniforms(mesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);
            UniformVector3("spotLightPosition", sl.WorldTransform.Translation);
            UniformVector3("spotLightDirection", sl.Forward);
            UniformColor("spotLightColor", sl.LightColor);
            UniformFloat("spotLightIntensity", sl.Intensity);
            UniformFloat("spotLightCutOff", MathF.Cos(sl.InnerConeAngleDegree.DegreeToRadians()));
            UniformFloat("spotLightOuterCutOff", MathF.Cos(sl.OuterConeAngleDegree.DegreeToRadians()));
            UniformFloat("radius", sl.AttenuationRadius);
            UniformFloat("softRatio", sl.SoftRatio);

            var rt = sl.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");

            if (sl.CastShadow && rt != null)
            {
                var position = sl.WorldTransform.Translation;
                var shadowView = Matrix4x4.CreateLookAt(position, position + sl.WorldTransform.ForwardVector(), sl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(sl.OuterConeAngleDegree.DegreeToRadians(), rt.Width / (float)rt.Height, sl.ShadowConfig.NearPlane, sl.ShadowConfig.FarPlane);

                UniformTexture($"spotLightshadowMap", rt.DepthStencilTexture);
                UniformMatrix4($"spotLightshadowMapMatrix", shadowView * shadowProjection);

            }

            base.RenderMesh(mesh, view, projection);
        }


    }

    public void SetupUpMeshUniforms(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);


        {
            ClearTextureUnit();
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



        }


        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);


        if (mesh.IsSkinnedMesh)
        {
            SyncAndBindBoneMatrixBuffer(mesh);
        }
    }

    private void SetupUpInstancedMeshUniforms(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        {
            ClearTextureUnit();
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
        }

    }

    public void RenderTranslucentInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {

        foreach (var dl in renderPipeline.DirectionalLights)
        {
            if (dl.Enable == false)
                continue;

            UseShader("ENABLE_DIR_LIGHT", "BLENDMODE_TRANSLUCENT", "INSTANCED_MESH");
            UseShader_Internal(instancedMesh.Material);

            ClearTextureUnit();
            SetupUpInstancedMeshUniforms(instancedMesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", dl.Intensity);

            var rt = dl.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
            if (dl.CastShadow == true && rt != null)
            {
                var shadowView = Matrix4x4.CreateLookAt(dl.WorldTransform.Translation, dl.WorldTransform.Translation + dl.WorldTransform.ForwardVector(), dl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreateOrthographic(dl.ShadowConfig.Width, dl.ShadowConfig.Height, dl.ShadowConfig.NearPlane, dl.ShadowConfig.FarPlane);

                UniformTexture($"dirLightshadowMap", rt.DepthStencilTexture);
                UniformMatrix4($"dirLightshadowMapMatrix", shadowView * shadowProjection);

            }
            base.RenderInstancedMesh(instancedMesh, view, projection);
        }

        Span<Matrix4x4> ShadowViews = stackalloc Matrix4x4[6];

        foreach (var pl in renderPipeline.PointLights)
        {
            if (pl.Enable == false)
                continue;

            UseShader("ENABLE_POINT_LIGHT", "BLENDMODE_TRANSLUCENT", "INSTANCED_MESH");
            UseShader_Internal(instancedMesh.Material);

            ClearTextureUnit();
            SetupUpInstancedMeshUniforms(instancedMesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);

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
            base.RenderInstancedMesh(instancedMesh, view, projection);
        }

        foreach (var sl in renderPipeline.SpotLights)
        {
            if (sl.Enable == false)
                continue;

            UseShader("ENABLE_SPOT_LIGHT", "BLENDMODE_TRANSLUCENT", "INSTANCED_MESH");
            UseShader_Internal(instancedMesh.Material);

            ClearTextureUnit();
            SetupUpInstancedMeshUniforms(instancedMesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);
            UniformVector3("spotLightPosition", sl.WorldTransform.Translation);
            UniformVector3("spotLightDirection", sl.Forward);
            UniformColor("spotLightColor", sl.LightColor);
            UniformFloat("spotLightIntensity", sl.Intensity);
            UniformFloat("spotLightCutOff", MathF.Cos(sl.InnerConeAngleDegree.DegreeToRadians()));
            UniformFloat("spotLightOuterCutOff", MathF.Cos(sl.OuterConeAngleDegree.DegreeToRadians()));
            UniformFloat("radius", sl.AttenuationRadius);
            UniformFloat("softRatio", sl.SoftRatio);

            var rt = sl.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");

            if (sl.CastShadow && rt != null)
            {
                var position = sl.WorldTransform.Translation;
                var shadowView = Matrix4x4.CreateLookAt(position, position + sl.WorldTransform.ForwardVector(), sl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(sl.OuterConeAngleDegree.DegreeToRadians(), rt.Width / (float)rt.Height, sl.ShadowConfig.NearPlane, sl.ShadowConfig.FarPlane);

                UniformTexture($"spotLightshadowMap", rt.DepthStencilTexture);
                UniformMatrix4($"spotLightshadowMapMatrix", shadowView * shadowProjection);

            }

            base.RenderInstancedMesh(instancedMesh, view, projection);
        }


    }
}
