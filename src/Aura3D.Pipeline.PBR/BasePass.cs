using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Renderers;

using Aura3D.Pipeline.PBR.Common;

namespace Aura3D.Pipeline.PBR;

internal class BasePass : RenderPass <PBRDeferredPipeline>
{
    Core.Resources.Texture defaultBaseColor => RenderPipeline.DefaultBaseColor;

    Core.Resources.Texture defaultNormal => RenderPipeline.DefaultNormal;

    Core.Resources.Texture defaultMetallicRoughness => RenderPipeline.DefaultMetallicRoughness;

    Core.Resources.Texture defaultEmissive => RenderPipeline.DefaultEmissive;

    Core.Resources.Texture defaultOcclusion => RenderPipeline.DefaultOcclusion;


    public BasePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = PbrResources.MeshVertexShader;

        FragmentShader = PbrResources.DeferredMeshFragmentShader;

        ShaderName = nameof(BasePass);

    }


    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);
        base.BeforeRender(camera);
        gl.CullFace(TriangleFace.Back);
    }

    public override void Render(Camera camera)
    {
        BindOutputRenderTarget(camera);
        gl.DepthMask(true);

        gl.ClearDepth(1.0f);
        gl.ClearColor(0, 0, 0, 0);

        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        UseShader();

        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("INSTANCED_MESH");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_MASKED");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Masked), camera.View, camera.Projection);
    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }


    private void setupMeshUniforms(Material? material, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);


        {

            var baseColor = material?.GetTexture("BaseColor") ?? defaultBaseColor;
            UniformTexture("Texture_BaseColor", baseColor);

            var normal = material?.GetTexture("Normal") ?? defaultNormal;
            UniformTexture("Texture_Normal", normal);

            var metallicRoughness = material?.GetTexture("MetallicRoughness") ?? defaultMetallicRoughness;
            UniformTexture("Texture_MetallicRoughness", metallicRoughness);


            var occlusion = material?.GetTexture("Occlusion") ?? defaultOcclusion;
            UniformTexture("Texture_Occlusion", occlusion);

            var emissive = material?.GetTexture("Emissive") ?? defaultEmissive;
            UniformTexture("Texture_Emissive", emissive);
        }

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

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        setupMeshUniforms(instancedMesh.Material, view, projection);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        setupMeshUniforms(mesh.Material, view, projection);

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
