using Aura3D.Core;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

using Aura3D.Pipeline.PBR.Common;

namespace Aura3D.Pipeline.PBRForward;

internal abstract class PBRForwardMeshPass : RenderPass<PBRPipelineBase>
{
    protected Core.Resources.Texture DefaultBaseColor => RenderPipeline.DefaultBaseColor;

    protected Core.Resources.Texture DefaultNormal => RenderPipeline.DefaultNormal;

    protected Core.Resources.Texture DefaultMetallicRoughness => RenderPipeline.DefaultMetallicRoughness;

    protected Core.Resources.Texture DefaultEmissive => RenderPipeline.DefaultEmissive;

    protected Core.Resources.Texture DefaultOcclusion => RenderPipeline.DefaultOcclusion;

    protected PBRForwardMeshPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = PbrForwardResources.MeshVertexShader;
    }

    protected void RenderMeshesForBlendMode(BlendMode mode, Camera camera, params string[] baseDefines)
    {
        UseShader(baseDefines);
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, mode) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader([..baseDefines, "SKINNED_MESH"]);
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, mode) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

        UseShader([..baseDefines, "INSTANCED_MESH"]);
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, mode), camera.View, camera.Projection);
    }

    protected void SetupCommonMeshUniforms(Material? material, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        UniformTexture("Texture_BaseColor", material?.GetTexture("BaseColor") ?? DefaultBaseColor);
        UniformTexture("Texture_Normal", material?.GetTexture("Normal") ?? DefaultNormal);
        UniformTexture("Texture_MetallicRoughness", material?.GetTexture("MetallicRoughness") ?? DefaultMetallicRoughness);
        UniformTexture("Texture_Occlusion", material?.GetTexture("Occlusion") ?? DefaultOcclusion);
        UniformTexture("Texture_Emissive", material?.GetTexture("Emissive") ?? DefaultEmissive);

        if (material != null)
        {
            if (material.DoubleSided)
                gl.Disable(EnableCap.CullFace);
            else
                gl.Enable(EnableCap.CullFace);

            UniformFloat("alphaCutoff", material.AlphaCutoff);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            UniformFloat("alphaCutoff", 0.0f);
        }
    }

    protected virtual void SetupPassUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupCommonMeshUniforms(mesh.Material, view, projection);
        SetupPassUniforms(view, projection);

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        if (mesh.IsSkinnedMesh)
        {
            SyncAndBindBoneMatrixBuffer(mesh);
        }

        base.RenderMesh(mesh, view, projection);
    }

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupCommonMeshUniforms(instancedMesh.Material, view, projection);
        SetupPassUniforms(view, projection);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }
}

internal class PBRForwardIBLAmbientPass : PBRForwardMeshPass
{
    private Camera? camera;
    private int mipmap;

    public PBRForwardIBLAmbientPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        FragmentShader = PbrForwardResources.IblAmbientFragmentShader;
        ShaderName = nameof(PBRForwardIBLAmbientPass);
    }

    public override void BeforeRender(Camera camera)
    {
        this.camera = camera;

        BindOutput(camera);

        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Less);
        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
        gl.CullFace(TriangleFace.Back);
    }

    public override void Render(Camera camera)
    {
        var prefilteredEnvMap = camera.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap")!;
        var prefilteredTexture = prefilteredEnvMap.GetTexture(0)!;

        int nearestPowerOfTwo = (int)MathF.Pow(2, MathF.Floor(MathF.Log2(prefilteredTexture.Width)));
        mipmap = BitOperations.TrailingZeroCount((uint)nearestPowerOfTwo) + 1;

        RenderMeshesForBlendMode(BlendMode.Opaque, camera);
        RenderMeshesForBlendMode(BlendMode.Masked, camera, "BLENDMODE_MASKED");
    }

    protected override void SetupPassUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
        var activeCamera = camera ?? throw new InvalidOperationException("Camera has not been set for PBR forward IBL pass.");

        var irradianceMap = activeCamera.GetPipelineGpuState<CubeRenderTarget>("IrradianceMap")!;
        var prefilteredEnvMap = activeCamera.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap")!;

        UniformMatrix4("u_viewMatrix", activeCamera.View);
        UniformMatrix4("u_projMatrix", activeCamera.Projection);
        UniformMatrix4("u_invViewProjMatrix", (activeCamera.View * activeCamera.Projection).Inverse());
        UniformVector3("u_cameraPos", activeCamera.WorldTransform.Translation);
        UniformFloat("u_max_mipmap", mipmap);
        UniformFloat("iblAmbientIntensity", renderPipeline.Settings.IblAmbientIntensity);
        UniformTexture("u_brdfLUT", RenderPipeline.BrdfLutTexture);
        UniformTextureCubeMap("u_irradianceMap", irradianceMap.GetTexture(0)!);
        UniformTextureCubeMap("u_prefilterMap", prefilteredEnvMap.GetTexture(0)!);
    }

}

internal class PBRForwardLightingPass : PBRForwardMeshPass
{
    private Camera? camera;
    private readonly List<string> lightDefines = [];
    private DirectionalLight? activeDirectionalLight;
    private PointLight? activePointLight;
    private SpotLight? activeSpotLight;

    public PBRForwardLightingPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        FragmentShader = PbrForwardResources.LightingFragmentShader;
        ShaderName = nameof(PBRForwardLightingPass);
    }

    public override void BeforeRender(Camera camera)
    {
        this.camera = camera;

        BindOutput(camera);

        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Equal);
        gl.DepthMask(false);
        gl.Enable(EnableCap.Blend);
        gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        gl.CullFace(TriangleFace.Back);
    }

    public override void AfterRender(Camera camera)
    {
        activeDirectionalLight = null;
        activePointLight = null;
        activeSpotLight = null;

        gl.DepthFunc(DepthFunction.Less);
        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
    }

    public override void Render(Camera camera)
    {
        foreach (var light in renderPipeline.DirectionalLights)
        {
            if (!light.Enable)
                continue;

            activeDirectionalLight = light;
            activePointLight = null;
            activeSpotLight = null;
            lightDefines.Clear();
            lightDefines.Add("ENABLE_DIR_LIGHT");

            if (light.CastShadow)
            {
                lightDefines.Add("ENABLE_SHADOWS");
                if (light.GetPipelineGpuState<CsmShadowData>(nameof(CsmShadowData)) != null)
                    lightDefines.Add("ENABLE_CSM");
            }

            RenderMeshesForBlendMode(BlendMode.Opaque, camera, [.. lightDefines]);
            RenderMeshesForBlendMode(BlendMode.Masked, camera, [.. lightDefines, "BLENDMODE_MASKED"]);
        }

        foreach (var light in renderPipeline.PointLights)
        {
            if (!light.Enable)
                continue;

            activeDirectionalLight = null;
            activePointLight = light;
            activeSpotLight = null;
            lightDefines.Clear();
            lightDefines.Add("ENABLE_POINT_LIGHT");

            if (light.CastShadow && light.GetPipelineGpuState<CubeRenderTarget>("ShadowMapRenderTarget") != null)
                lightDefines.Add("ENABLE_SHADOWS");

            RenderMeshesForBlendMode(BlendMode.Opaque, camera, [.. lightDefines]);
            RenderMeshesForBlendMode(BlendMode.Masked, camera, [.. lightDefines, "BLENDMODE_MASKED"]);
        }

        foreach (var light in renderPipeline.SpotLights)
        {
            if (!light.Enable)
                continue;

            activeDirectionalLight = null;
            activePointLight = null;
            activeSpotLight = light;
            lightDefines.Clear();
            lightDefines.Add("ENABLE_SPOT_LIGHT");

            if (light.CastShadow && light.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget") != null)
                lightDefines.Add("ENABLE_SHADOWS");

            RenderMeshesForBlendMode(BlendMode.Opaque, camera, [.. lightDefines]);
            RenderMeshesForBlendMode(BlendMode.Masked, camera, [.. lightDefines, "BLENDMODE_MASKED"]);
        }
    }

    protected override void SetupPassUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
        var activeCamera = camera ?? throw new InvalidOperationException("Camera has not been set for PBR forward lighting pass.");

        UniformVector3("viewPos", activeCamera.WorldTransform.Translation);

        if (activeDirectionalLight != null)
            SetupDirectionalLight(activeCamera, activeDirectionalLight);
        else if (activePointLight != null)
            SetupPointLight(activePointLight);
        else if (activeSpotLight != null)
            SetupSpotLight(activeSpotLight);
    }

    private void SetupDirectionalLight(Camera activeCamera, DirectionalLight light)
    {
        UniformVector3("dirLightDirection", light.Forward);
        UniformColor("dirLightColor", light.LightColor);
        UniformFloat("dirLightIntensity", light.Intensity);
        UniformMatrix4("viewMatrix", activeCamera.View);

        var csmData = light.GetPipelineGpuState<CsmShadowData>(nameof(CsmShadowData));
        if (light.CastShadow && csmData != null)
        {
            UniformTextureArray("dirLightCSMMap", csmData.TextureArrayId);
            UniformInt("dirLightCascadeCount", csmData.CascadeCount);

            for (int i = 0; i < csmData.CascadeCount; i++)
            {
                UniformMatrix4($"dirLightCSMMatrices[{i}]", csmData.CascadeMatrices[i]);
            }

            for (int i = 0; i < csmData.CascadeCount + 1; i++)
            {
                UniformFloat($"dirLightCascadeSplitDepths[{i}]", csmData.CascadeSplitDepths[i]);
            }

            return;
        }

        var shadowMap = light.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
        if (light.CastShadow && shadowMap != null)
        {
            var shadowView = Matrix4x4.CreateLookAt(light.WorldTransform.Translation, light.WorldTransform.Translation + light.WorldTransform.ForwardVector(), light.WorldTransform.UpVector());
            var shadowProjection = Matrix4x4.CreateOrthographic(light.ShadowConfig.Width, light.ShadowConfig.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

            UniformTexture("dirLightshadowMap", shadowMap.DepthStencilTexture);
            UniformMatrix4("dirLightshadowMapMatrix", shadowView * shadowProjection);
        }
    }

    private void SetupPointLight(PointLight light)
    {
        UniformVector3("pointLightPosition", light.WorldTransform.Translation);
        UniformColor("pointLightColor", light.LightColor);
        UniformFloat("pointLightIntensity", light.Intensity);
        UniformFloat("radius", light.AttenuationRadius);
        UniformFloat("softRadius", light.SoftRatio);

        var shadowMap = light.GetPipelineGpuState<CubeRenderTarget>("ShadowMapRenderTarget");
        if (!light.CastShadow || shadowMap == null)
            return;

        var position = light.WorldTransform.Translation;
        Span<Matrix4x4> shadowViews =
        [
            Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0)),
            Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0)),
            Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1)),
            Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1)),
            Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0)),
            Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0)),
        ];

        var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), shadowMap.Width / (float)shadowMap.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

        UniformTextureCubeMap("pointLightShadowMap", shadowMap.DepthStencilTexture);
        for (int i = 0; i < shadowViews.Length; i++)
        {
            UniformMatrix4($"pointShadowMapMatrices[{i}]", shadowViews[i] * shadowProjection);
        }
    }

    private void SetupSpotLight(SpotLight light)
    {
        UniformVector3("spotLightPosition", light.WorldTransform.Translation);
        UniformVector3("spotLightDirection", light.Forward);
        UniformColor("spotLightColor", light.LightColor);
        UniformFloat("spotLightIntensity", light.Intensity);
        UniformFloat("spotLightCutOff", MathF.Cos(light.InnerConeAngleDegree.DegreeToRadians()));
        UniformFloat("spotLightOuterCutOff", MathF.Cos(light.OuterConeAngleDegree.DegreeToRadians()));
        UniformFloat("radius", light.AttenuationRadius);
        UniformFloat("softRadius", light.SoftRatio);

        var shadowMap = light.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
        if (!light.CastShadow || shadowMap == null)
            return;

        var shadowView = Matrix4x4.CreateLookAt(light.WorldTransform.Translation, light.WorldTransform.Translation + light.WorldTransform.ForwardVector(), light.WorldTransform.UpVector());
        var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(light.OuterConeAngleDegree.DegreeToRadians(), shadowMap.Width / (float)shadowMap.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

        UniformTexture("spotLightshadowMap", shadowMap.DepthStencilTexture);
        UniformMatrix4("spotLightshadowMapMatrix", shadowView * shadowProjection);
    }
}
