using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Linq;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class ShadowMapPass : RenderPass
{
    public ShadowMapPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.ShadowMapVert;
        FragmentShader = ShaderResource.ShadowMapFrag;
        ShaderName = nameof(ShadowMapPass);
    }

    public override void Setup()
    {
        // Setup logic for the shadow map pass
        // This can include setting up shaders, buffers, etc.
    }

    public override void BeforeRender()
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.CullFace(TriangleFace.Front);
    }
    public override void AfterRender()
    {
        gl.CullFace(TriangleFace.Back);
    }


    public override void Render()
    {
        Span<Matrix4x4> views = stackalloc Matrix4x4[6];

        int index = 0;
        foreach (var pointLight in renderPipeline.PointLights)
        {
            if (pointLight.Enable == false)
                continue;
            if (pointLight.CastShadow == false)
                continue;
            if (index++ >= renderPipeline.Settings.PointLightLimit)
                break;

            var rt = pointLight.GetPipelineGpuResource<CubeRenderTarget>("ShadowMapRenderTarget");

            if (rt == null)
            {
                rt = new CubeRenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
            }

            gl.Viewport(0, 0, rt.Width, rt.Height);
            gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

            var position = pointLight.WorldTransform.Translation;

            views[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
            views[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
            views[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            views[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
            views[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
            views[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));


            var projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), rt.Width/(float)rt.Height, pointLight.ShadowConfig.NearPlane, pointLight.ShadowConfig.FarPlane);
           
            for (int i = 0; i < 6; i++)
            {
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX + i, rt.DepthStencilTexture.TextureId, 0);


                gl.Clear(ClearBufferMask.DepthBufferBit);
                gl.ClearDepth(1.0f);

                RenderMesh(views[i], projection);

            }

        }

        index = 0;
        foreach (var spotLight in renderPipeline.SpotLights)
        {
            if (spotLight.Enable == false)
                continue;
            if (spotLight.CastShadow == false)
                continue;
            if (index++ >= renderPipeline.Settings.SpotLightLimit)
                break;

            var rt = spotLight.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

            if (rt == null)
            {
                rt = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
                renderPipeline.EnsureUploaded(rt);
                spotLight.SetPipelineGpuResource("ShadowMapRenderTarget", rt);
            }

            gl.Viewport(0, 0, rt.Width, rt.Height);
            gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

            gl.Clear(ClearBufferMask.DepthBufferBit);
            gl.ClearDepth(1.0f);

            var position = spotLight.WorldTransform.Translation;
            var view = Matrix4x4.CreateLookAt(position, position + spotLight.WorldTransform.ForwardVector(), spotLight.WorldTransform.UpVector());
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(spotLight.OuterAngleDegree.DegreeToRadians(), rt.Width / (float)rt.Height, spotLight.ShadowConfig.NearPlane, spotLight.ShadowConfig.FarPlane);

            RenderMesh(view, projection);
        }

        index = 0;
        int csmCascadeCount = renderPipeline.Settings.CsmCascadeCount;
        float csmSplitLambda = renderPipeline.Settings.CsmSplitLambda;
        var mainCamera = renderPipeline.Scene.MainCamera;
        int csmRes = renderPipeline.Settings.CsmShadowMapResolution;
        const int DefaultShadowMapRes = 1024;
        const int MaxCascades = 8;
        Span<float> csmSplits = stackalloc float[MaxCascades + 1];
        Span<Vector3> frustumCorners = stackalloc Vector3[8];

        // 主方向光：显式设置优先，否则自动选取第一个启用且投射阴影的方向光
        var mainLight = renderPipeline.Scene.MainDirectionalLight
            ?? renderPipeline.DirectionalLights.FirstOrDefault(l => l.Enable && l.CastShadow);

        foreach (var directionalLight in renderPipeline.DirectionalLights)
        {
            if (directionalLight.Enable == false)
                continue;
            if (directionalLight.CastShadow == false)
                continue;
            if (index++ >= renderPipeline.Settings.DirectionalLightLimit)
                break;

            bool isMainLight = directionalLight == mainLight;
            bool useCsm = isMainLight && csmCascadeCount > 1 && renderPipeline.SupportsCSM;

            if (useCsm)
            {
                // === CSM: 纹理数组 + 多级联渲染 ===
                int cascades = System.Math.Min(csmCascadeCount, MaxCascades);

                // 计算 PSSM 级联分割
                CalculateCascadeSplits(
                    mainCamera.NearPlane, mainCamera.FarPlane,
                    cascades, csmSplitLambda, csmSplits);

                // 获取或创建 CSM 资源（通过管线缓存，IGpuResource 生命周期由管线管理）
                var csmData = directionalLight.GetPipelineGpuResource<CsmShadowData>(nameof(CsmShadowData));
                if (csmData == null ||
                    csmData.Resolution != csmRes ||
                    csmData.CascadeCount != cascades)
                {
                    // 销毁旧资源
                    csmData?.Destroy(gl);

                    csmData = new CsmShadowData
                    {
                        Resolution = csmRes,
                        CascadeCount = cascades,
                        CascadeMatrices = new Matrix4x4[cascades],
                        CascadeSplitDepths = new float[cascades + 1],
                    };

                    // 创建 2D 纹理数组
                    csmData.TextureArrayId = gl.GenTexture();
                    gl.BindTexture(GLEnum.Texture2DArray, csmData.TextureArrayId);
                    gl.TexImage3D(GLEnum.Texture2DArray, 0, (int)InternalFormat.DepthComponent24,
                        (uint)csmRes, (uint)csmRes, (uint)cascades, 0,
                        GLEnum.DepthComponent, GLEnum.UnsignedInt, ReadOnlySpan<byte>.Empty);
                    gl.TexParameter(GLEnum.Texture2DArray, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
                    gl.TexParameter(GLEnum.Texture2DArray, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
                    gl.TexParameter(GLEnum.Texture2DArray, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
                    gl.TexParameter(GLEnum.Texture2DArray, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

                    csmData.FboId = gl.GenFramebuffer();

                    directionalLight.SetPipelineGpuResource(nameof(CsmShadowData), csmData);
                }

                // 更新每帧变化的级联数据
                for (int c = 0; c <= cascades; c++)
                    csmData.CascadeSplitDepths[c] = csmSplits[c];

                gl.BindFramebuffer(GLEnum.Framebuffer, csmData.FboId);
                gl.Viewport(0, 0, (uint)csmRes, (uint)csmRes);

                var lightPos = directionalLight.WorldTransform.Translation;
                var lightForward = directionalLight.WorldTransform.ForwardVector();
                var lightUp = directionalLight.WorldTransform.UpVector();
                var lightView = Matrix4x4.CreateLookAt(lightPos, lightPos + lightForward, lightUp);

                for (int cascade = 0; cascade < cascades; cascade++)
                {
                    GetFrustumSliceCorners(mainCamera, csmSplits[cascade], csmSplits[cascade + 1], frustumCorners);

                    Vector3 lightMin = new(float.MaxValue);
                    Vector3 lightMax = new(float.MinValue);
                    for (int c = 0; c < 8; c++)
                    {
                        var lightCorner = Vector3.Transform(frustumCorners[c], lightView);
                        lightMin = Vector3.Min(lightMin, lightCorner);
                        lightMax = Vector3.Max(lightMax, lightCorner);
                    }

                    float zPadding = 50f;
                    lightMin.Z -= zPadding;
                    lightMax.Z += zPadding;

                    var lightProjection = Matrix4x4.CreateOrthographicOffCenter(
                        lightMin.X, lightMax.X, lightMin.Y, lightMax.Y,
                        -lightMax.Z, -lightMin.Z);

                    csmData.CascadeMatrices[cascade] = lightView * lightProjection;

                    gl.FramebufferTextureLayer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
                        csmData.TextureArrayId, 0, cascade);

                    gl.Clear(ClearBufferMask.DepthBufferBit);
                    gl.ClearDepth(1.0f);

                    RenderMesh(lightView, lightProjection);
                }

                // 清除旧的单张 shadow map（BlinnPhong 改用 CSM，不再需要）
                var oldRt = directionalLight.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");
                if (oldRt != null)
                {
                    renderPipeline.GpuResources.Remove(oldRt);
                    oldRt.Destroy(gl);
                    directionalLight.RemovePipelineGpuResource("ShadowMapRenderTarget");
                }
            }
            else
            {
                // === 普通阴影（单张）===
                // 清除 CSM 资源
                var oldCsm = directionalLight.GetPipelineGpuResource<CsmShadowData>(nameof(CsmShadowData));
                if (oldCsm != null)
                {
                    oldCsm.Destroy(gl);
                    directionalLight.RemovePipelineGpuResource(nameof(CsmShadowData));
                }

                var rt = directionalLight.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

                if (rt == null)
                {
                    rt = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(DefaultShadowMapRes, DefaultShadowMapRes);
                    renderPipeline.EnsureUploaded(rt);
                    directionalLight.SetPipelineGpuResource("ShadowMapRenderTarget", rt);
                }

                gl.Viewport(0, 0, rt.Width, rt.Height);

                gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);
                gl.Clear(ClearBufferMask.DepthBufferBit);
                gl.ClearDepth(1.0f);

                var view = Matrix4x4.CreateLookAt(directionalLight.WorldTransform.Translation,
                    directionalLight.WorldTransform.Translation + directionalLight.WorldTransform.ForwardVector(),
                    directionalLight.WorldTransform.UpVector());
                var projection = Matrix4x4.CreateOrthographic(
                    directionalLight.ShadowConfig.Width, directionalLight.ShadowConfig.Height,
                    directionalLight.ShadowConfig.NearPlane, directionalLight.ShadowConfig.FarPlane);

                RenderMesh(view, projection);
            }
        }
    }

    List<Mesh> meshes = new List<Mesh>();
    public void RenderMesh(Matrix4x4 view, Matrix4x4 projection)
    {
        meshes.Clear();

        renderPipeline.UpdateVisibleMeshesInCamera(view, projection, meshes);

        if (meshes.Count == 0)
            return;

        UseShader();
        RenderMeshesFromList(meshes, mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), view, projection);


        UseShader("BLENDMODE_MASKED");
        RenderMeshesFromList(meshes, mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), view, projection);


        UseShader("SKINNED_MESH");
        RenderMeshesFromList(meshes, mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), view, projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderMeshesFromList(meshes, mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), view, projection);

        UseShader("INSTANCED_MESH");
        RenderInstancedMeshes(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque), view, projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_MASKED");
        RenderInstancedMeshes(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Masked), view, projection);
    }

    void setupUniform(Material? material, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        if (material != null && material.BlendMode == BlendMode.Masked)
        {
            ClearTextureUnit();

            foreach (var channel in material.Channels)
            {
                if (channel.Name == "BaseColor")
                {
                    if (channel.Texture != null)
                    {
                        UniformTexture("BaseColorTexture", channel.Texture);
                    }

                }
            }
            UniformFloat("alphaCutoff", material.AlphaCutoff);
        }
    }

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        setupUniform(instancedMesh.Material, view, projection);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }
    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        setupUniform(mesh.Material, view, projection);

        if (mesh.IsSkinnedMesh)
        {
            var boneBuffer = mesh.AnimationSampler?.BoneMatrixBuffer ?? mesh.Skeleton.BoneMatrixBuffer;
            renderPipeline.EnsureUploaded(boneBuffer);
            boneBuffer.Bind();
        }
        base.RenderMesh(mesh, view, projection);
    }

    /// <summary>
    /// 计算 PSSM 级联分割深度。
    /// </summary>
    private static void CalculateCascadeSplits(float near, float far, int count, float lambda, Span<float> splits)
    {
        splits[0] = near;
        splits[count] = far;
        for (int i = 1; i < count; i++)
        {
            float p = (float)i / count;
            float logSplit = near * MathF.Pow(far / near, p);
            float uniformSplit = near + (far - near) * p;
            splits[i] = lambda * logSplit + (1.0f - lambda) * uniformSplit;
        }
    }

    /// <summary>
    /// 获取相机视锥体在指定深度范围内的 8 个角点（世界空间）。
    /// </summary>
    private static void GetFrustumSliceCorners(Camera camera, float near, float far, Span<Vector3> corners)
    {
        float fovRadians = camera.FieldOfView.DegreeToRadians();
        float tanHalfFov = MathF.Tan(fovRadians / 2.0f);
        float aspect = camera.Width / (float)camera.Height;

        float nearHalfH = near * tanHalfFov;
        float nearHalfW = nearHalfH * aspect;
        float farHalfH = far * tanHalfFov;
        float farHalfW = farHalfH * aspect;

        var forward = camera.WorldTransform.ForwardVector();
        var right = camera.WorldTransform.RightVector();
        var up = camera.WorldTransform.UpVector();
        var pos = camera.WorldTransform.Translation;

        var nearCenter = pos + forward * near;
        var farCenter = pos + forward * far;

        corners[0] = nearCenter - right * nearHalfW - up * nearHalfH;
        corners[1] = nearCenter + right * nearHalfW - up * nearHalfH;
        corners[2] = nearCenter - right * nearHalfW + up * nearHalfH;
        corners[3] = nearCenter + right * nearHalfW + up * nearHalfH;
        corners[4] = farCenter - right * farHalfW - up * farHalfH;
        corners[5] = farCenter + right * farHalfW - up * farHalfH;
        corners[6] = farCenter - right * farHalfW + up * farHalfH;
        corners[7] = farCenter + right * farHalfW + up * farHalfH;
    }
}
