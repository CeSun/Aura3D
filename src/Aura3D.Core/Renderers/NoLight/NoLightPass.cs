using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

public class NoLightPass : RenderPass
{
    public NoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        this.FragmentShader = ShaderResource.NoLightFrag;
        this.VertexShader = ShaderResource.NoLightVert;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest); 

        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);

    }
    public override void Render(Camera camera)
    {
        UseShader();
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        RenderMeshes(mesh => FilterSkeletonMesh(mesh) == false && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), camera.View, camera.Projection);


        UseShader("BLENDMODE_MASKED");
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        RenderMeshes(mesh => FilterSkeletonMesh(mesh) == false && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Masked), camera.View, camera.Projection);

        UseShader("SKINNED_MESH");
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        RenderMeshes(mesh => FilterSkeletonMesh(mesh) && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        RenderMeshes(mesh => mesh.IsSkinnedMesh && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Masked), camera.View, camera.Projection);


        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);

        UseShader("BLENDMODE_TRANSLUCENT");
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        RenderMeshes(mesh => FilterSkeletonMesh(mesh) == false && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Translucent), camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        RenderMeshes(mesh => FilterSkeletonMesh(mesh) && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Translucent), camera.View, camera.Projection);

    }

    public override void AfterRender(Camera camera)
    {
        
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        if (mesh.Material != null)
        {
            ClearTextureUnit();

            foreach (var channel in mesh.Material.Channels)
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
            }

            if (mesh.Material.DoubleSided == false)
            {
                gl.Enable(EnableCap.CullFace);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
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
