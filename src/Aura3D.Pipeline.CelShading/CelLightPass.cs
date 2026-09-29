using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using Texture = Aura3D.Core.Resources.Texture;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public enum CelShadingTextureBit
{
    None = 0,
    BaseColorBit = 1,
    NormalBit = 2,
    ILMBit = 4,
    SDFBit = 8,
    ShadowRampBit = 16,
    SpecularRampBit = 32,
}

public class CelLightPass : RenderPass
{

    private int directionalLightLimit;
    private int pointLightLimit;
    private int spotLightLimit;
    private Texture rampTexture;
    public void UpdateLightNumLimit(int directionalLightLimit, int pointLightLimit, int spotLightLimit)
    {
        FragmentShader = CelShadingResources.CelFragmentShader
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
    }


    public CelLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = CelShadingResources.MeshVertexShader;
        FragmentShader = CelShadingResources.CelFragmentShader;
        rampTexture = TextureLoader.LoadTexture(CelShadingResources.CelRamp2Data);
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.CullFace(TriangleFace.Back);
    }

    // 判断是否是渲染脸部的pass
    private static bool IsMeshFaceRender(Mesh mesh)
    {
        if (mesh.Material == null)
            return false;
        if(mesh.Material.TryGetParameterValue<int>("RenderType", out int renderType))
        {
            if(renderType == 1)
            {
                return true;
            }
        }
        return false;
    }

    public override void Render(Camera camera)
    {
        BindOutputRenderTarget(camera);

        // Render Body
        UseShader();
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && !IsMeshFaceRender(mesh) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && !IsMeshFaceRender(mesh) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && !IsMeshFaceRender(mesh) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && !IsMeshFaceRender(mesh) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

        // Instanced Mesh
        UseShader("INSTANCED_MESH");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_MASKED");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Masked), camera.View, camera.Projection);

        // Render Face
        UseShader("FACE_RENDER");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && IsMeshFaceRender(mesh) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("FACE_RENDER", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && IsMeshFaceRender(mesh) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("FACE_RENDER", "SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && IsMeshFaceRender(mesh) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

        UseShader("FACE_RENDER", "SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && IsMeshFaceRender(mesh) && mesh.IsSkinnedMesh, camera.View, camera.Projection);
    }

    // ==================== 共享 Uniform 设置 ====================

    /// <summary>
    /// 设置相机和方向光相关的 Uniform（RenderMesh 和 RenderInstancedMesh 共用）。
    /// </summary>
    private void SetupLightUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("ambientIntensity", renderPipeline.Settings.AmbientIntensity);
        UniformVector3("cameraPosition", view.Inverse().Translation);
        UniformTexture("ShadowRamp", rampTexture);

        for (int i = 0; i < directionalLightLimit; i++)
        {
            if (i >= renderPipeline.DirectionalLights.Count)
            {
                UniformVector3($"DirectionalLights[{i}].direction", Vector3.Zero);
                UniformVector3($"DirectionalLights[{i}].color", Vector3.Zero);
                UniformTexture($"DirectionalLightShadowMaps[{i}]", 0);
                UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", Matrix4x4.Identity);
            }
            else
            {
                var directionalLight = renderPipeline.DirectionalLights[i];
                UniformVector3($"DirectionalLights[{i}].direction", directionalLight.Forward);
                UniformVector3($"DirectionalLights[{i}].color", new Vector3(directionalLight.LightColor.R / 255f * directionalLight.Intensity, directionalLight.LightColor.G / 255f * directionalLight.Intensity, directionalLight.LightColor.B / 255f * directionalLight.Intensity));
                UniformFloat($"DirectionalLights[{i}].castShadow", directionalLight.CastShadow ? 1.0f : 0.0f);

                var rt = directionalLight.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");
                if (directionalLight.CastShadow && rt != null)
                {
                    var dlview = Matrix4x4.CreateLookAt(directionalLight.WorldTransform.Translation, directionalLight.WorldTransform.Translation + directionalLight.WorldTransform.ForwardVector(), directionalLight.WorldTransform.UpVector());
                    var dlprojection = Matrix4x4.CreateOrthographic(directionalLight.ShadowConfig.Width, directionalLight.ShadowConfig.Height, directionalLight.ShadowConfig.NearPlane, directionalLight.ShadowConfig.FarPlane);
                    UniformTexture($"DirectionalLightShadowMaps[{i}]", rt.DepthStencilTexture);
                    UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", dlview * dlprojection);
                }
                else
                {
                    UniformTexture($"DirectionalLightShadowMaps[{i}]", 0);
                    UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", Matrix4x4.Identity);
                }
            }
        }
    }

    /// <summary>
    /// 写入卡通着色的全部材质参数。默认值取自历史示例角色 NPC_Avatar_Girl_Sword_Nilou
    /// 内嵌的正确材质：uniform 不设值时 GL 默认全零，会让未配置的卡通材质一片黑，
    /// 且同一 program 下还会残留上一个 mesh 的值。
    /// </summary>
    private void SetupCelParameters(Material? material)
    {
        float F(string key, float fallback) =>
            material != null && material.TryGetParameterValue<float>(key, out var value) ? value : fallback;
        Vector4 V4(string key, Vector4 fallback) =>
            material != null && material.TryGetParameterValue<Vector4>(key, out var value) ? value : fallback;

        // 控制昼夜
        UniformInt("_UseCoolShadowColorOrTex", 1);

        UniformFloat("_RampIndex0", F("_RampIndex0", 1f));
        UniformFloat("_RampIndex1", F("_RampIndex1", 4f));
        UniformFloat("_RampIndex2", F("_RampIndex2", 3f));
        UniformFloat("_RampIndex3", F("_RampIndex3", 5f));
        UniformFloat("_RampIndex4", F("_RampIndex4", 2f));

        UniformFloat("_BrightFac", F("_BrightFac", 0.99f));
        UniformFloat("_GreyFac", F("_GreyFac", 1.08f));
        UniformFloat("_DarkFac", F("_DarkFac", 0.55f));
        UniformFloat("_BrightAreaShadowFac", F("_BrightAreaShadowFac", 1f));

        UniformFloat("_FaceShadowOffset", F("_FaceShadowOffset", 0f));
        UniformFloat("_FaceShadowTransitionSoftness", F("_FaceShadowTransitionSoftness", 0.05f));

        var white = new Vector4(1f);
        UniformVector4("_LightAreaColorTint", V4("_LightAreaColorTint", white));
        UniformVector4("_DarkShadowColor", V4("_DarkShadowColor", white));
        UniformVector4("_CoolDarkShadowColor", V4("_CoolDarkShadowColor", white));
    }

    /// <summary>
    /// 设置材质的通用纹理通道和参数（RenderMesh 和 RenderInstancedMesh 共用）。
    /// 返回纹理标志位掩码。
    /// </summary>
    private int SetupMaterialTextures(Material? material)
    {
        int textureFlags = 0;

        // 缺底色贴图时兜底为不透明白，让 Masked/Translucent 材质也能正常渲染
        UniformVector4("BaseColor", new Vector4(1f));

        if (material == null)
            return textureFlags;

        foreach (var channel in material.Channels)
        {
            switch (channel.Name)
            {
                case "BaseColor":
                    if (channel.Texture != null)
                    {
                        UniformTexture("BaseColorTexture", channel.Texture);
                        textureFlags |= (int)CelShadingTextureBit.BaseColorBit;
                    }
                    else
                    {
                        UniformTexture("BaseColorTexture", 0);
                    }
                    break;
                case "Normal":
                    if (channel.Texture != null)
                    {
                        UniformTexture("NormalTexture", channel.Texture);
                        textureFlags |= (int)CelShadingTextureBit.NormalBit;
                    }
                    else
                    {
                        UniformTexture("NormalTexture", 0);
                    }
                    break;
                case "ILM":
                    if (channel.Texture != null)
                    {
                        UniformTexture("ILMTextures", channel.Texture);
                        textureFlags |= (int)CelShadingTextureBit.ILMBit;
                    }
                    break;
                case "ShadowRamp":
                    if (channel.Texture != null)
                    {
                        UniformTexture("ShadowRamp", channel.Texture);
                        textureFlags |= (int)CelShadingTextureBit.ShadowRampBit;
                    }
                    break;
                case "SpecularRamp":
                    if (channel.Texture != null)
                    {
                        UniformTexture("SpecularRamp", channel.Texture);
                        textureFlags |= (int)CelShadingTextureBit.SpecularRampBit;
                    }
                    break;
            }
        }

        UniformInt("TexturesFlags", textureFlags);
        UniformFloat("alphaCutoff", material.AlphaCutoff);

        if (material.DoubleSided == false)
            gl.Enable(EnableCap.CullFace);
        else
            gl.Disable(EnableCap.CullFace);

        return textureFlags;
    }

    // ==================== RenderInstancedMesh ====================

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupLightUniforms(view, projection);
        SetupCelParameters(instancedMesh.Material);
        SetupMaterialTextures(instancedMesh.Material);
        base.RenderInstancedMesh(instancedMesh, view, projection);
    }

    // ==================== RenderMesh ====================

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        SetupLightUniforms(view, projection);

        SetupCelParameters(mesh.Material);

        int textureFlags = SetupMaterialTextures(mesh.Material);

        if (mesh.Material != null)
        {
            if (mesh.Tags.Contains("face"))
            {
                UniformMatrix4("faceModelMatrix", mesh.WorldTransform);
            }

            // SDF 脸部贴图
            foreach (var channel in mesh.Material.Channels)
            {
                if (channel.Name == "SDF" && channel.Texture != null)
                {
                    UniformTexture("SDFTextures", channel.Texture);
                    textureFlags |= (int)CelShadingTextureBit.SDFBit;

                    // Todo: 临时在这里设置脸部M矩阵，后期改为绑定骨骼矩阵形式
                    Matrix4x4 faceM = new Matrix4x4(1, 0, 0, 0,
                                                    0, 1, 0, 0,
                                                    0, 0, 1, 0,
                                                    0, 0, 0, 1);
                    UniformMatrix4("faceModelMatrix", faceM);
                }
            }

            // 更新 textureFlags（SDF 位可能在上面被添加）
            UniformInt("TexturesFlags", textureFlags);
        }

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
