using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the material gpu state type.
/// </summary>
public class MaterialGpuState : IResourceGpuState<Material>
{
    private WeakReference<Material> material;
    /// <summary>
    /// Gets the version.
    /// </summary>
    public ulong Version => Resource.Version;
    /// <summary>
    /// Gets or sets the synced version.
    /// </summary>
    public ulong SyncedVersion { get; protected set; }

    /// <summary>
    /// Gets the material.
    /// </summary>
    public Material Material => Resource;

    /// <summary>
    /// Gets the resource.
    /// </summary>
    public Material Resource
    {
        get
        {
            if (material.TryGetTarget(out var value))
                return value;

            throw Aura3D.Core.Exceptions.RendererErrors.CpuResourceCollected(nameof(Material));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the object is alive.
    /// </summary>
    public bool IsAlive => material.TryGetTarget(out _);

    /// <summary>
    /// Gets the shaders.
    /// </summary>
    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    /// <summary>
    /// Initializes a new instance of the material gpu state type.
    /// </summary>
    public MaterialGpuState(Material material)
    {
        this.material = new WeakReference<Material>(material);
    }

    /// <summary>
    /// Uploads the associated data.
    /// </summary>
    public void Upload(GL gl)
    {
        // MaterialGpuState currently only caches compiled shader programs.
        // Shader compilation is still triggered lazily by RenderPass_Shader.
        SyncedVersion = Resource.Version;
    }

    /// <summary>
    /// Destroys the associated data.
    /// </summary>
    public void Destroy(GL gl)
    {
        foreach (var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }
}
