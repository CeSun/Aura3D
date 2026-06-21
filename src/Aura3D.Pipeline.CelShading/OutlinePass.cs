using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;
using Aura3D.Core.Math;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class OutlinePass : RenderPass
{
    private float outlineWidth = 2f;
    private float widthOffset = 1.5f;
    private Vector4 outlineColor = new Vector4(0.0f, 0.0f, 0.0f, 1f);

    public float AmbientIntensity = 0.1f;

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

        UseShader();
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);
    }

    protected void SetupUniform(Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformVector3("cameraPosition", view.Inverse().Translation);
        UniformFloat("outlineWidth", outlineWidth);
        UniformFloat("widthOffset", widthOffset);
        UniformVector4("BaseColor", outlineColor);
        

    }
    public override void AfterRender(Camera camera)
    {
        gl.Disable(EnableCap.CullFace);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        SetupUniform(view, projection);

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        var normalPrjMatrix = (projection * view * mesh.WorldTransform).Inverse();
        normalPrjMatrix = Matrix4x4.Transpose(normalPrjMatrix);
        UniformMatrix4("normalPrjMatrix", normalPrjMatrix);


        if (mesh.IsSkinnedMesh)
        {
            SyncAndBindBoneMatrixBuffer(mesh);
        }
        base.RenderMesh(mesh, view, projection);
    }

}
