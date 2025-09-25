using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;
using Aura3D.Core.Math;

namespace Aura3D.Core.Renderers;

public class ShadowMapPass : RenderPass
{
    private int directionalLightLimit;
    private int pointLightLimit;
    private int spotLightLimit;
    public void UpdateLightNumLimit(int directionalLightLimit, int pointLightLimit, int spotLightLimit)
    {
        this.directionalLightLimit = directionalLightLimit;
        this.pointLightLimit = pointLightLimit;
        this.spotLightLimit = spotLightLimit;

    }
    public ShadowMapPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.ShadowMapVert;
        FragmentShader = ShaderResource.ShadowMapFrag;
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
            if (index++ >= pointLightLimit)
                break;

            gl.Viewport(0, 0, pointLight.ShadowMapRenderTarget.Width, pointLight.ShadowMapRenderTarget.Height);
            gl.BindFramebuffer(GLEnum.Framebuffer, pointLight.ShadowMapRenderTarget.FrameBufferId);

            var position = pointLight.WorldTransform.Translation;

            views[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
            views[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
            views[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            views[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
            views[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
            views[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));


            var projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), pointLight.ShadowMapRenderTarget.Width/(float)pointLight.ShadowMapRenderTarget.Height, pointLight.ShadowConfig.NearPlane, pointLight.ShadowConfig.FarPlane);


            for (int i = 0; i < 6; i++)
            {
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX + i, pointLight.ShadowMapRenderTarget.DepthStencilTexture.TextureId, 0);


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
            if (index++ >= spotLightLimit)
                break;

            gl.Viewport(0, 0, spotLight.ShadowMapRenderTarget.Width, spotLight.ShadowMapRenderTarget.Height);
            gl.BindFramebuffer(GLEnum.Framebuffer, spotLight.ShadowMapRenderTarget.FrameBufferId);

            gl.Clear(ClearBufferMask.DepthBufferBit);
            gl.ClearDepth(1.0f);

            var position = spotLight.WorldTransform.Translation;
            var view = Matrix4x4.CreateLookAt(position, position + spotLight.WorldTransform.ForwardVector(), spotLight.WorldTransform.UpVector());
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(spotLight.OuterAngleDegree.DegreeToRadians(), spotLight.ShadowMapRenderTarget.Width / (float)spotLight.ShadowMapRenderTarget.Height, spotLight.ShadowConfig.NearPlane, spotLight.ShadowConfig.FarPlane);

            RenderMesh(view, projection);
        }

        index = 0;
        foreach (var directionalLight in renderPipeline.DirectionalLights)
        {
            if (directionalLight.Enable == false)
                continue;
            if (directionalLight.CastShadow == false)
                continue;
            if (index++ >= directionalLightLimit)
                break;

            gl.Viewport(0, 0, directionalLight.ShadowMapRenderTarget.Width, directionalLight.ShadowMapRenderTarget.Height);

            gl.BindFramebuffer(GLEnum.Framebuffer, directionalLight.ShadowMapRenderTarget.FrameBufferId);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            gl.ClearDepth(1.0f);

            var view = Matrix4x4.CreateLookAt(directionalLight.WorldTransform.Translation, directionalLight.WorldTransform.Translation + directionalLight.WorldTransform.ForwardVector(), directionalLight.WorldTransform.UpVector());
            var projection = Matrix4x4.CreateOrthographic(directionalLight.ShadowConfig.Width , directionalLight.ShadowConfig.Height, directionalLight.ShadowConfig.NearPlane, directionalLight.ShadowConfig.FarPlane);
            
            RenderMesh(view, projection);
        }
    }

    public void RenderMesh(Matrix4x4 view, Matrix4x4 projection)
    {
        UseShader();

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        RenderMeshes(mesh =>  mesh.IsSkinnedMesh == false && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), view, projection);


        UseShader("BLENDMODE_MASKED");

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        RenderMeshes(mesh =>  mesh.IsSkinnedMesh == false && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Masked), view, projection);


        UseShader("SKINNED_MESH");
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        RenderMeshes(mesh => mesh.IsSkinnedMesh && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), view, projection);



        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        RenderMeshes(mesh => mesh.IsSkinnedMesh && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Masked), view, projection);

    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        if (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Masked)
        {
            ClearTextureUnit();

            foreach(var channel in mesh.Material.Channels)
            {
                if (channel.Name == "BaseColor")
                {
                    if (channel.Texture != null)
                    {
                        UniformTexture("BaseColorTexture", channel.Texture);
                    }

                }
            }
            UniformFloat("alphaCutoff", mesh.Material.AlphaCutoff);
        }


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
