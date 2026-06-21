using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class LightPass : RenderPass
{
    Resources.Texture defaultBaseColor;

    Resources.Texture defaultNormal;

    private int directionalLightLimit;
    private int pointLightLimit;
    private int spotLightLimit;

    // Uniform 名称缓存，避免每帧重复分配字符串
    private readonly string[] _directionalLightDirectionUniforms;
    private readonly string[] _directionalLightColorUniforms;
    private readonly string[] _directionalLightCastShadowUniforms;
    private readonly string[] _directionalLightShadowMapUniforms;
    private readonly string[] _directionalLightShadowMapMatrixUniforms;

    private readonly string[] _pointLightColorUniforms;
    private readonly string[] _pointLightPositionUniforms;
    private readonly string[] _pointLightRadiusUniforms;
    private readonly string[] _pointLightSoftRatioUniforms;
    private readonly string[] _pointLightCastShadowUniforms;
    private readonly string[] _pointLightShadowMapUniforms;
    private readonly string[][] _pointLightShadowMapMatricesUniforms;

    private readonly string[] _spotLightColorUniforms;
    private readonly string[] _spotLightPositionUniforms;
    private readonly string[] _spotLightDirectionUniforms;
    private readonly string[] _spotLightInnerConeCosUniforms;
    private readonly string[] _spotLightOuterConeCosUniforms;
    private readonly string[] _spotLightRadiusUniforms;
    private readonly string[] _spotLightSoftRatioUniforms;
    private readonly string[] _spotLightCastShadowUniforms;
    private readonly string[] _spotLightShadowMapUniforms;
    private readonly string[] _spotLightShadowMapMatrixUniforms;

    private const int MaxLightLimit = 10;
    private const int MaxCsmCascades = 4;

    // CSM uniform names for the main directional light
    private const string MainLightUseCSMUniform = "MainLightUseCSM";
    private const string MainLightCSMShadowMapUniform = "MainLightCSMShadowMap";
    private const string MainLightCSMCascadeCountUniform = "MainLightCSMCascadeCount";
    private readonly string[] _mainLightCSMMatricesUniforms;
    private readonly string[] _mainLightCSMSplitDepthsUniforms;

    public void UpdateLightNumLimit(int directionalLightLimit, int pointLightLimit, int spotLightLimit)
    {
        FragmentShader = ShaderResource.MeshFrag
            .Replace("#define MAX_DIRECTIONAL_LIGHTS 4", "#define MAX_DIRECTIONAL_LIGHTS " + directionalLightLimit)
            .Replace("#define MAX_POINT_LIGHTS 4", "#define MAX_POINT_LIGHTS " + pointLightLimit)
            .Replace("#define MAX_SPOT_LIGHTS 4", "#define MAX_SPOT_LIGHTS " + spotLightLimit)
            .Replace("REPEAT_DL_SHADOW_ASSIGN_4//", "REPEAT_DL_SHADOW_ASSIGN_" + directionalLightLimit)
            .Replace("REPEAT_PL_SHADOW_ASSIGN_4//", "REPEAT_PL_SHADOW_ASSIGN_" + pointLightLimit)
            .Replace("REPEAT_SP_SHADOW_ASSIGN_4//", "REPEAT_SP_SHADOW_ASSIGN_" + spotLightLimit);
        foreach (var (key, shader) in Shaders)
        {
            gl.DeleteProgram(shader.ProgramId);
        }
        Shaders.Clear();

        this.directionalLightLimit = directionalLightLimit;
        this.pointLightLimit = pointLightLimit;
        this.spotLightLimit = spotLightLimit;
    }
    

    public LightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.MeshVert;

        FragmentShader = ShaderResource.MeshFrag;

        ShaderName = nameof(LightPass);

        defaultBaseColor = Resources.Texture.CreateFromColor(Color.White);

        defaultNormal = Resources.Texture.CreateFromColor(Color.FromArgb(255, 128, 128, 255));

        // 初始化 Uniform 名称缓存
        _directionalLightDirectionUniforms = new string[MaxLightLimit];
        _directionalLightColorUniforms = new string[MaxLightLimit];
        _directionalLightCastShadowUniforms = new string[MaxLightLimit];
        _directionalLightShadowMapUniforms = new string[MaxLightLimit];
        _directionalLightShadowMapMatrixUniforms = new string[MaxLightLimit];

        _pointLightColorUniforms = new string[MaxLightLimit];
        _pointLightPositionUniforms = new string[MaxLightLimit];
        _pointLightRadiusUniforms = new string[MaxLightLimit];
        _pointLightSoftRatioUniforms = new string[MaxLightLimit];
        _pointLightCastShadowUniforms = new string[MaxLightLimit];
        _pointLightShadowMapUniforms = new string[MaxLightLimit];
        _pointLightShadowMapMatricesUniforms = new string[MaxLightLimit][];

        _spotLightColorUniforms = new string[MaxLightLimit];
        _spotLightPositionUniforms = new string[MaxLightLimit];
        _spotLightDirectionUniforms = new string[MaxLightLimit];
        _spotLightInnerConeCosUniforms = new string[MaxLightLimit];
        _spotLightOuterConeCosUniforms = new string[MaxLightLimit];
        _spotLightRadiusUniforms = new string[MaxLightLimit];
        _spotLightSoftRatioUniforms = new string[MaxLightLimit];
        _spotLightCastShadowUniforms = new string[MaxLightLimit];
        _spotLightShadowMapUniforms = new string[MaxLightLimit];
        _spotLightShadowMapMatrixUniforms = new string[MaxLightLimit];

        for (int i = 0; i < MaxLightLimit; i++)
        {
            _directionalLightDirectionUniforms[i] = $"DirectionalLights[{i}].direction";
            _directionalLightColorUniforms[i] = $"DirectionalLights[{i}].color";
            _directionalLightCastShadowUniforms[i] = $"DirectionalLights[{i}].castShadow";
            _directionalLightShadowMapUniforms[i] = $"DirectionalLightShadowMaps[{i}]";
            _directionalLightShadowMapMatrixUniforms[i] = $"DirectionalLights[{i}].shadowMapMatrix";

            _pointLightColorUniforms[i] = $"PointLights[{i}].color";
            _pointLightPositionUniforms[i] = $"PointLights[{i}].position";
            _pointLightRadiusUniforms[i] = $"PointLights[{i}].radius";
            _pointLightSoftRatioUniforms[i] = $"PointLights[{i}].softRatio";
            _pointLightCastShadowUniforms[i] = $"PointLights[{i}].castShadow";
            _pointLightShadowMapUniforms[i] = $"PointLightShadowMaps[{i}]";

            _pointLightShadowMapMatricesUniforms[i] = new string[6];
            for (int j = 0; j < 6; j++)
            {
                _pointLightShadowMapMatricesUniforms[i][j] = $"PointLights[{i}].shadowMapMatrices[{j}]";
            }

            _spotLightColorUniforms[i] = $"SpotLights[{i}].color";
            _spotLightPositionUniforms[i] = $"SpotLights[{i}].position";
            _spotLightDirectionUniforms[i] = $"SpotLights[{i}].direction";
            _spotLightInnerConeCosUniforms[i] = $"SpotLights[{i}].inner_cone_cos";
            _spotLightOuterConeCosUniforms[i] = $"SpotLights[{i}].outer_cone_cos";
            _spotLightRadiusUniforms[i] = $"SpotLights[{i}].radius";
            _spotLightSoftRatioUniforms[i] = $"SpotLights[{i}].softRatio";
            _spotLightCastShadowUniforms[i] = $"SpotLights[{i}].castShadow";
            _spotLightShadowMapUniforms[i] = $"SpotLightShadowMaps[{i}]";
            _spotLightShadowMapMatrixUniforms[i] = $"SpotLights[{i}].shadowMapMatrix";
        }

        // 初始化 CSM Uniform 名称缓存
        _mainLightCSMMatricesUniforms = new string[MaxCsmCascades];
        _mainLightCSMSplitDepthsUniforms = new string[MaxCsmCascades + 1];

        for (int i = 0; i < MaxCsmCascades; i++)
        {
            _mainLightCSMMatricesUniforms[i] = $"MainLightCSMMatrices[{i}]";
        }
        for (int i = 0; i < MaxCsmCascades + 1; i++)
        {
            _mainLightCSMSplitDepthsUniforms[i] = $"MainLightCSMSplitDepths[{i}]";
        }
    }
    public override void Setup()
    {
        renderPipeline.EnsureSynced(defaultBaseColor);
        renderPipeline.EnsureSynced(defaultNormal);
    }

    public override void Destroy()
    {
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.CullFace(TriangleFace.Back);
    }

    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        var csm = CheckCsmEnabled() ? new[] { "ENABLE_CSM" } : [];

        UseShader(csm);
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader([..csm, "BLENDMODE_MASKED"]);
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);


        UseShader([..csm, "SKINNED_MESH"]);
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader([..csm, "SKINNED_MESH", "BLENDMODE_MASKED"]);
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader([..csm, "INSTANCED_MESH"]);
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader([..csm, "INSTANCED_MESH", "BLENDMODE_MASKED"]);
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Masked), camera.View, camera.Projection);
    }

    protected void SetupUniform(Material? material, Matrix4x4 view, Matrix4x4 projection)
    {
        SetupCameraUniforms(view, projection);

        UniformTexture("BaseColorTexture", material?.GetTexture("BaseColor") ?? defaultBaseColor);
        UniformTexture("NormalTexture", material?.GetTexture("Normal") ?? defaultNormal);
        SetupDirectionalLights();
        SetupPointLights();
        SetupSpotLights();

        if (material != null)
        {
            if (material.DoubleSided == false)
            {
                gl.Enable(EnableCap.CullFace);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }

            UniformFloat("alphaCutoff", material.AlphaCutoff);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            UniformFloat("alphaCutoff", 0.0f);
        }
    }

    private void SetupCameraUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("ambientIntensity", renderPipeline.Settings.AmbientIntensity);
        UniformVector3("cameraPosition", view.Inverse().Translation);
    }

    private void SetupDirectionalLights()
    {
        // 重置 CSM 标志，仅在主方向光有 CSM 数据时由 SetupActiveDirectionalLight 设为 1.0
        UniformFloat(MainLightUseCSMUniform, 0.0f);

        for (int i = 0; i < directionalLightLimit; i++)
        {
            if (i >= renderPipeline.DirectionalLights.Count || !renderPipeline.DirectionalLights[i].Enable)
            {
                SetupInactiveDirectionalLight(i);
            }
            else
            {
                SetupActiveDirectionalLight(i, renderPipeline.DirectionalLights[i]);
            }
        }
    }

    /// <summary>
    /// 检查当前帧是否有方向光启用了 CSM（级联阴影贴图）。
    /// 用于决定是否在着色器编译时启用 ENABLE_CSM 宏。
    /// </summary>
    private bool CheckCsmEnabled()
    {
        foreach (var light in renderPipeline.DirectionalLights)
        {
            if (!light.Enable)
                continue;
            if (!light.CastShadow)
                continue;
            var csmData = light.GetPipelineGpuState<CsmShadowData>(nameof(CsmShadowData));
            if (csmData != null)
                return true;
        }
        return false;
    }

    private void SetupInactiveDirectionalLight(int index)
    {
        UniformVector3(_directionalLightDirectionUniforms[index], Vector3.Zero);
        UniformVector3(_directionalLightColorUniforms[index], Vector3.Zero);
        UniformTexture(_directionalLightShadowMapUniforms[index], 0);
        UniformMatrix4(_directionalLightShadowMapMatrixUniforms[index], Matrix4x4.Identity);
    }

    private void SetupActiveDirectionalLight(int index, DirectionalLight light)
    {
        UniformVector3(_directionalLightDirectionUniforms[index], light.Forward);
        UniformVector3(_directionalLightColorUniforms[index], new Vector3(light.LightColor.R / 255f * light.Intensity, light.LightColor.G / 255f * light.Intensity, light.LightColor.B / 255f * light.Intensity));
        UniformFloat(_directionalLightCastShadowUniforms[index], light.CastShadow ? 1.0f : 0.0f);

        var csmData = light.GetPipelineGpuState<CsmShadowData>(nameof(CsmShadowData));
        bool useCsm = light.CastShadow && csmData != null;

        if (useCsm)
        {
            // CSM: 绑定级联阴影贴图数组和所有级联矩阵、分割深度
            UniformFloat(MainLightUseCSMUniform, 1.0f);
            UniformTextureArray(MainLightCSMShadowMapUniform, csmData.TextureArrayId);
            UniformInt(MainLightCSMCascadeCountUniform, csmData.CascadeCount);

            for (int c = 0; c < csmData.CascadeCount && c < _mainLightCSMMatricesUniforms.Length; c++)
            {
                UniformMatrix4(_mainLightCSMMatricesUniforms[c], csmData.CascadeMatrices[c]);
            }

            for (int c = 0; c < csmData.CascadeCount + 1 && c < _mainLightCSMSplitDepthsUniforms.Length; c++)
            {
                UniformFloat(_mainLightCSMSplitDepthsUniforms[c], csmData.CascadeSplitDepths[c]);
            }

            // 绑定一个空纹理到常规阴影贴图槽位（CSM 路径不使用此槽位）
            UniformTexture(_directionalLightShadowMapUniforms[index], 0);
            UniformMatrix4(_directionalLightShadowMapMatrixUniforms[index], Matrix4x4.Identity);
        }
        else
        {
            var rt = light.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");

            if (light.CastShadow && rt != null)
            {
                var shadowView = Matrix4x4.CreateLookAt(light.WorldTransform.Translation, light.WorldTransform.Translation + light.WorldTransform.ForwardVector(), light.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreateOrthographic(light.ShadowConfig.Width, light.ShadowConfig.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

                UniformTexture(_directionalLightShadowMapUniforms[index], rt.DepthStencilTexture);
                UniformMatrix4(_directionalLightShadowMapMatrixUniforms[index], shadowView * shadowProjection);
            }
            else
            {
                UniformTexture(_directionalLightShadowMapUniforms[index], 0);
                UniformMatrix4(_directionalLightShadowMapMatrixUniforms[index], Matrix4x4.Identity);
            }
        }
    }

    private void SetupPointLights()
    {
        Span<Matrix4x4> shadowViews = stackalloc Matrix4x4[6];

        for (int i = 0; i < pointLightLimit; i++)
        {
            if (i >= renderPipeline.PointLights.Count || !renderPipeline.PointLights[i].Enable)
            {
                SetupInactivePointLight(i);
            }
            else
            {
                SetupActivePointLight(i, renderPipeline.PointLights[i], shadowViews);
            }
        }
    }

    private void SetupInactivePointLight(int index)
    {
        UniformVector3(_pointLightColorUniforms[index], Vector3.Zero);
        UniformVector3(_pointLightPositionUniforms[index], Vector3.Zero);
        UniformFloat(_pointLightRadiusUniforms[index], 0.0f);
        UniformFloat(_pointLightSoftRatioUniforms[index], 0.1f);
        UniformTextureCubeMap(_pointLightShadowMapUniforms[index], 0);

        for (int j = 0; j < 6; j++)
        {
            UniformMatrix4(_pointLightShadowMapMatricesUniforms[index][j], Matrix4x4.Identity);
        }
    }

    private void SetupActivePointLight(int index, PointLight light, Span<Matrix4x4> shadowViews)
    {
        UniformVector3(_pointLightColorUniforms[index], new Vector3(light.LightColor.R / 255f * light.Intensity, light.LightColor.G / 255f * light.Intensity, light.LightColor.B / 255f * light.Intensity));
        UniformVector3(_pointLightPositionUniforms[index], light.WorldTransform.Translation);
        UniformFloat(_pointLightRadiusUniforms[index], light.AttenuationRadius);
        UniformFloat(_pointLightSoftRatioUniforms[index], light.SoftRatio);
        UniformFloat(_pointLightCastShadowUniforms[index], light.CastShadow ? 1.0f : 0.0f);

        var rt = light.GetPipelineGpuState<CubeRenderTarget>("ShadowMapRenderTarget");

        if (light.CastShadow && rt != null)
        {
            var position = light.WorldTransform.Translation;

            shadowViews[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
            shadowViews[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
            shadowViews[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            shadowViews[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
            shadowViews[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
            shadowViews[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));

            var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), rt.Width / (float)rt.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

            for (int j = 0; j < 6; j++)
            {
                UniformMatrix4(_pointLightShadowMapMatricesUniforms[index][j], shadowViews[j] * shadowProjection);
            }
            UniformTextureCubeMap(_pointLightShadowMapUniforms[index], rt.DepthStencilTexture);
        }
        else
        {
            UniformTextureCubeMap(_pointLightShadowMapUniforms[index], 0);
            for (int j = 0; j < 6; j++)
            {
                UniformMatrix4(_pointLightShadowMapMatricesUniforms[index][j], Matrix4x4.Identity);
            }
        }
    }

    private void SetupSpotLights()
    {
        for (int i = 0; i < spotLightLimit; i++)
        {
            if (i >= renderPipeline.SpotLights.Count || !renderPipeline.SpotLights[i].Enable)
            {
                SetupInactiveSpotLight(i);
            }
            else
            {
                SetupActiveSpotLight(i, renderPipeline.SpotLights[i]);
            }
        }
    }

    private void SetupInactiveSpotLight(int index)
    {
        UniformVector3(_spotLightColorUniforms[index], Vector3.Zero);
        UniformVector3(_spotLightPositionUniforms[index], Vector3.Zero);
        UniformVector3(_spotLightDirectionUniforms[index], Vector3.Zero);
        UniformFloat(_spotLightInnerConeCosUniforms[index], 0.0f);
        UniformFloat(_spotLightOuterConeCosUniforms[index], 0.0f);
        UniformFloat(_spotLightRadiusUniforms[index], 0.0f);
        UniformFloat(_spotLightSoftRatioUniforms[index], 0.1f);
        UniformMatrix4(_spotLightShadowMapMatrixUniforms[index], Matrix4x4.Identity);
        UniformTexture(_spotLightShadowMapUniforms[index], 0);
    }

    private void SetupActiveSpotLight(int index, SpotLight light)
    {
        UniformVector3(_spotLightColorUniforms[index], new Vector3(light.LightColor.R / 255f * light.Intensity, light.LightColor.G / 255f * light.Intensity, light.LightColor.B / 255f * light.Intensity));
        UniformVector3(_spotLightPositionUniforms[index], light.WorldTransform.Translation);
        UniformVector3(_spotLightDirectionUniforms[index], light.Forward);
        UniformFloat(_spotLightInnerConeCosUniforms[index], MathF.Cos(light.InnerConeAngleDegree.DegreeToRadians()));
        UniformFloat(_spotLightOuterConeCosUniforms[index], MathF.Cos(light.OuterAngleDegree.DegreeToRadians()));
        UniformFloat(_spotLightRadiusUniforms[index], light.AttenuationRadius);
        UniformFloat(_spotLightSoftRatioUniforms[index], light.SoftRatio);
        UniformFloat(_spotLightCastShadowUniforms[index], light.CastShadow ? 1.0f : 0.0f);

        var rt = light.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");

        if (light.CastShadow && rt != null)
        {
            var position = light.WorldTransform.Translation;
            var shadowView = Matrix4x4.CreateLookAt(position, position + light.WorldTransform.ForwardVector(), light.WorldTransform.UpVector());
            var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(light.OuterAngleDegree.DegreeToRadians(), rt.Width / (float)rt.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

            UniformTexture(_spotLightShadowMapUniforms[index], rt.DepthStencilTexture);
            UniformMatrix4(_spotLightShadowMapMatrixUniforms[index], shadowView * shadowProjection);
        }
        else
        {
            UniformTexture(_spotLightShadowMapUniforms[index], 0);
            UniformMatrix4(_spotLightShadowMapMatrixUniforms[index], Matrix4x4.Identity);
        }
    }

    public override void AfterRender(Camera camera)
    {
    }

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupUniform(instancedMesh.Material, view, projection);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupUniform(mesh.Material, view, projection);

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        if (mesh.IsSkinnedMesh)
        {
            SyncAndBindBoneMatrixBuffer(mesh);
        }

        base.RenderMesh(mesh, view, projection);
    }

}
