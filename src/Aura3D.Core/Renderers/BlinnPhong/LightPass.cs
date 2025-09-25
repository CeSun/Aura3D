using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;
using Aura3D.Core.Math;

namespace Aura3D.Core.Renderers;

public class LightPass : RenderPass
{
    private int directionalLightLimit;
    private int pointLightLimit;
    private int spotLightLimit;
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
    

    public float AmbientIntensity = 0.1f;

    public LightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.MeshVert;
        FragmentShader = ShaderResource.MeshFrag;
    }
    public override void Setup()
    {
        // Setup logic for the base pass
        // This can include setting up shaders, buffers, etc.
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

        ClearTextureUnit();

        UseShader();
        SetupUniform(camera);
        using (PushTextureUnit())
        {
            RenderMeshes(mesh => FilterSkeletonMesh(mesh) == false && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), camera.View, camera.Projection);
        }

        UseShader("BLENDMODE_MASKED");
        SetupUniform(camera);
        using (PushTextureUnit())
        {
            RenderMeshes(mesh => FilterSkeletonMesh(mesh) == false && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Masked), camera.View, camera.Projection);
        }

        UseShader("SKINNED_MESH");
        SetupUniform(camera);
        using (PushTextureUnit())
        {
            RenderMeshes(mesh => FilterSkeletonMesh(mesh) && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), camera.View, camera.Projection);
        }

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        SetupUniform(camera);
        using (PushTextureUnit())
        {
            RenderMeshes(mesh => FilterSkeletonMesh(mesh) && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Masked), camera.View, camera.Projection);
        }
    }

    protected void SetupUniform(Camera camera)
    {

        ClearTextureUnit();
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformVector3("cameraPosition", camera.WorldTransform.Translation);


        for(int i = 0; i < directionalLightLimit; i++)
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
                UniformVector3($"DirectionalLights[{i}].color", new Vector3(directionalLight.LightColor.R / 255f, directionalLight.LightColor.G / 255f, directionalLight.LightColor.B / 255f));

                UniformFloat($"DirectionalLights[{i}].castShadow", directionalLight.CastShadow ? 1.0f : 0.0f);

                if (directionalLight.CastShadow)
                {
                    var view = Matrix4x4.CreateLookAt(directionalLight.WorldTransform.Translation, directionalLight.WorldTransform.Translation + directionalLight.WorldTransform.ForwardVector(), directionalLight.WorldTransform.UpVector());
                    var projection = Matrix4x4.CreateOrthographic(directionalLight.ShadowConfig.Width, directionalLight.ShadowConfig.Height, directionalLight.ShadowConfig.NearPlane, directionalLight.ShadowConfig.FarPlane);

                    UniformTexture($"DirectionalLightShadowMaps[{i}]", directionalLight.ShadowMapRenderTarget.DepthStencilTexture);
                    UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", view * projection);
                }
                else
                {
                    UniformTexture($"DirectionalLightShadowMaps[{i}]", 0);
                    UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", Matrix4x4.Identity);
                }



            }

        }

        Span<Matrix4x4> views = stackalloc Matrix4x4[6];

        for (int i = 0; i < pointLightLimit; i ++)
        {
            if (i >= renderPipeline.PointLights.Count)
            {
                UniformVector3($"PointLights[{i}].color", Vector3.Zero);
                UniformVector3($"PointLights[{i}].position", Vector3.Zero);
                UniformFloat($"PointLights[{i}].radius", 0.0f);
                UniformTextureCubeMap($"PointLightShadowMaps[{i}]", 0);
                for (int j = 0; j < 6; j++)
                {
                    UniformMatrix4($"PointLights[{i}].shadowMapMatrices[{j}]", Matrix4x4.Identity);
                }
            }
            else
            {
                var pointLight = renderPipeline.PointLights[i];


                UniformVector3($"PointLights[{i}].color", new Vector3(pointLight.LightColor.R / 255f, pointLight.LightColor.G / 255f, pointLight.LightColor.B / 255f));
                UniformVector3($"PointLights[{i}].position", pointLight.WorldTransform.Translation);
                UniformFloat($"PointLights[{i}].radius", pointLight.AttenuationRadius);
                UniformFloat($"PointLights[{i}].castShadow", pointLight.CastShadow ? 1.0f : 0.0f);
                if (pointLight.CastShadow)
                {

                    var position = pointLight.WorldTransform.Translation;

                    views[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
                    views[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
                    views[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
                    views[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
                    views[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
                    views[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));


                    var projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), pointLight.ShadowMapRenderTarget.Width / (float)pointLight.ShadowMapRenderTarget.Height, pointLight.ShadowConfig.NearPlane, pointLight.ShadowConfig.FarPlane);

                    for (int j = 0; j < 6; j++)
                    {
                        UniformMatrix4($"PointLights[{i}].shadowMapMatrices[{j}]", views[j] * projection);
                    }
                    UniformTextureCubeMap($"PointLightShadowMaps[{i}]", pointLight.ShadowMapRenderTarget.DepthStencilTexture);
                }
                else
                {
                    UniformTextureCubeMap($"PointLightShadowMaps[{i}]", 0);
                    for (int j = 0; j < 6; j++)
                    {
                        UniformMatrix4($"PointLights[{i}].shadowMapMatrices[{j}]", Matrix4x4.Identity);
                    }
                }

            }
        }
        for (int i = 0; i < spotLightLimit; i++)
        {
            if (i >= renderPipeline.SpotLights.Count)
            {
                UniformVector3($"SpotLights[{i}].color", Vector3.Zero);
                UniformVector3($"SpotLights[{i}].position", Vector3.Zero);
                UniformVector3($"SpotLights[{i}].direction", Vector3.Zero);
                UniformFloat($"SpotLights[{i}].inner_cone_cos", 0.0f);
                UniformFloat($"SpotLights[{i}].outer_cone_cos", 0.0f);
                UniformFloat($"SpotLights[{i}].radius", 0.0f);
                UniformTexture($"SpotLightShadowMaps[{i}]", 0);
                UniformMatrix4($"SpotLights[{i}].shadowMapMatrix", Matrix4x4.Identity);

            }
            else
            {
                var spotLight = renderPipeline.SpotLights[i];
                UniformVector3($"SpotLights[{i}].color", new Vector3(spotLight.LightColor.R / 255f, spotLight.LightColor.G / 255f, spotLight.LightColor.B / 255f));
                UniformVector3($"SpotLights[{i}].position", spotLight.WorldTransform.Translation);
                UniformVector3($"SpotLights[{i}].direction", spotLight.Forward);
                UniformFloat($"SpotLights[{i}].inner_cone_cos", MathF.Cos(spotLight.InnerConeAngleDegree.DegreeToRadians()));
                UniformFloat($"SpotLights[{i}].outer_cone_cos", MathF.Cos(spotLight.OuterAngleDegree.DegreeToRadians()));
                UniformFloat($"SpotLights[{i}].radius", spotLight.AttenuationRadius);

                UniformFloat($"SpotLights[{i}].castShadow", spotLight.CastShadow ? 1.0f : 0.0f);

                if (spotLight.CastShadow)
                {
                    var position = spotLight.WorldTransform.Translation;
                    var view = Matrix4x4.CreateLookAt(position, position + spotLight.WorldTransform.ForwardVector(), spotLight.WorldTransform.UpVector());
                    var projection = Matrix4x4.CreatePerspectiveFieldOfView(spotLight.OuterAngleDegree.DegreeToRadians(), spotLight.ShadowMapRenderTarget.Width / (float)spotLight.ShadowMapRenderTarget.Height, spotLight.ShadowConfig.NearPlane, spotLight.ShadowConfig.FarPlane);

                    UniformTexture($"SpotLightShadowMaps[{i}]", spotLight.ShadowMapRenderTarget.DepthStencilTexture);
                    UniformMatrix4($"SpotLights[{i}].shadowMapMatrix", view * projection);

                }
                else
                {

                    UniformTexture($"SpotLightShadowMaps[{i}]", 0);
                    UniformMatrix4($"SpotLights[{i}].shadowMapMatrix", Matrix4x4.Identity);
                }
            }
        }
    }
    public override void AfterRender(Camera camera)
    {
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        if (mesh.Material != null)
        {
            ClearTextureUnit();

            foreach(var channel in mesh.Material.Channels)
            {
                if (channel.Name == "BaseColor")
                {
                    if (channel.Texture != null)
                    {
                        UniformTexture("BaseColorTexture", channel.Texture);
                        UniformInt("HasBaseColorTexture", 1);
                    }
                    else
                    {
                        UniformInt("HasBaseColorTexture", 0);
                        UniformTexture("BaseColorTexture", 0);
                        UniformColor("BaseColor", channel.Color);
                    }
                }
                else if (channel.Name == "Normal")
                {


                    if (channel.Texture != null)
                    {
                        UniformTexture("NormalTexture", channel.Texture);
                        UniformInt("HasNormalTexture", 1);
                    }
                    else
                    {
                        UniformTexture("NormalTexture", 0);
                        UniformInt("HasNormalTexture", 0);
                    }
                }

                if (mesh.Material.DoubleSided == false)
                {
                    gl.Enable(EnableCap.CullFace);
                }
                else
                {
                    gl.Disable(EnableCap.CullFace);
                }

            }
            UniformFloat("alphaCutoff", mesh.Material.AlphaCutoff);
        }

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        if (FilterSkeletonMesh(mesh))
        {
            var skinnedMesh = mesh as SkinnedMesh;
            var skeleton = skinnedMesh!.Skeleton!;
            if (skinnedMesh!.SkinnedModel!.AnimationSampler != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skinnedMesh!.SkinnedModel!.AnimationSampler.BonesTransform[i]);
                }
            }
            else
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skeleton.Bones[i].WorldMatrix);
                }
            }
        }
        
        base.RenderMesh(mesh, view, projection);
    }

}
