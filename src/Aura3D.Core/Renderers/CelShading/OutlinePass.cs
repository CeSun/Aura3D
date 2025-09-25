using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;
using Aura3D.Core.Math;
using Texture = Aura3D.Core.Resources.Texture;
using System.Reflection;

namespace Aura3D.Core.Renderers;

public class OutlinePass : RenderPass
{
    private float outlineWidth = 2f;
    private Vector4 outlineColor = new Vector4(0.0f, 0.0f, 0.0f, 1f);

    public float AmbientIntensity = 0.1f;

    private Camera camera = null;

    public OutlinePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.OutlineVert;
        FragmentShader = ShaderResource.OutlineFrag;
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
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Front);


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
            RenderMeshes(mesh => mesh.IsSkinnedMesh && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque), camera.View, camera.Projection);
        }

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        SetupUniform(camera);
        using (PushTextureUnit())
        {
            RenderMeshes(mesh => mesh.IsSkinnedMesh && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Masked), camera.View, camera.Projection);
        }
    }

    protected void SetupUniform(Camera camera)
    {
        this.camera = camera;
        ClearTextureUnit();
        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformVector3("cameraPosition", camera.WorldTransform.Translation);
        UniformFloat("outlineWidth", outlineWidth);
        UniformVector4("BaseColor", outlineColor);
        

    }
    public override void AfterRender(Camera camera)
    {
        gl.Disable(EnableCap.CullFace);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        var normalPrjMatrix = (camera.Projection * camera.View * mesh.WorldTransform).Inverse();
        normalPrjMatrix = Matrix4x4.Transpose(normalPrjMatrix);
        UniformMatrix4("normalPrjMatrix", normalPrjMatrix);


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
