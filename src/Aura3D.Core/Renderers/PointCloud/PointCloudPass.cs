using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Renderers;

namespace Aura3D.Core;

/// <summary>
/// Represents the point cloud pass type.
/// </summary>
public class PointCloudPass : RenderPass
{
    /// <summary>
    /// Gets or sets the default point size.
    /// </summary>
    public float DefaultPointSize { get; set; } = 5.0f;

    /// <summary>
    /// Initializes a new instance of the point cloud pass type.
    /// </summary>
    public PointCloudPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        ShaderName = nameof(PointCloudPass);

        VertexShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            layout(location = 0) in vec3 position;
            layout(location = 2) in vec4 color;

            #ifdef INSTANCED_MESH
            layout(location = 8) in mat4 modelMatrix;
            #endif

            #ifndef INSTANCED_MESH
            uniform mat4 modelMatrix;
            #endif

            uniform mat4 viewMatrix;
            uniform mat4 projectionMatrix;
            uniform float uPointSize;

            out vec4 vColor;

            void main()
            {
                vec4 worldPosition = modelMatrix * vec4(position, 1.0);
                gl_Position = projectionMatrix * viewMatrix * worldPosition;
                gl_PointSize = uPointSize;
                vColor = color;
            }
            """;

        FragmentShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            in vec4 vColor;
            out vec4 outColor;

            void main()
            {
                float dist = length(gl_PointCoord - 0.5);
                if (dist > 0.5) discard;

                outColor = vColor;
            }
            """;
    }

    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public override void BeforeRender(Camera camera)
    {
        BindOutputRenderTarget(camera);

        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.CullFace);
    }

    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public override void Render(Camera camera)
    {
        // Opaque point cloud meshes (regular)
        UseShader();
        RenderVisibleMeshesInCamera(
            mesh => IsPointCloudMesh(mesh) && IsMaterialBlendMode(mesh, BlendMode.Opaque),
            camera.View, camera.Projection);

        // Opaque point cloud meshes (instanced)
        UseShader("INSTANCED_MESH");
        RenderVisibleInstancedMeshesInCamera(
            instancedMesh => IsPointCloudInstancedMesh(instancedMesh)
                && IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque),
            camera.View, camera.Projection);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);

        // Translucent point cloud meshes (regular)
        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(
            mesh => IsPointCloudMesh(mesh) && IsMaterialBlendMode(mesh, BlendMode.Translucent),
            camera.View, camera.Projection);

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
    }

    /// <summary>
    /// Performs the after render operation.
    /// </summary>
    public override void AfterRender(Camera camera)
    {
    }

    /// <summary>
    /// Renders the mesh.
    /// </summary>
    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        float pointSize = DefaultPointSize;
        if (mesh.Material != null &&
            mesh.Material.TryGetParameterValue<float>("uPointSize", out var materialPointSize))
        {
            pointSize = materialPointSize;
        }
        UniformFloat("uPointSize", pointSize);

        base.RenderMesh(mesh, view, projection);
    }

    /// <summary>
    /// Renders the instanced mesh.
    /// </summary>
    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        float pointSize = DefaultPointSize;
        if (instancedMesh.Material != null &&
            instancedMesh.Material.TryGetParameterValue<float>("uPointSize", out var materialPointSize))
        {
            pointSize = materialPointSize;
        }
        UniformFloat("uPointSize", pointSize);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }

    private static bool IsPointCloudMesh(Mesh mesh)
    {
        return mesh.Geometry != null
            && mesh.Geometry.PrimitiveType == Resources.PrimitiveType.Points;
    }

    private static bool IsPointCloudInstancedMesh(InstancedMesh instancedMesh)
    {
        return instancedMesh.PrimitiveType == Resources.PrimitiveType.Points;
    }
}
