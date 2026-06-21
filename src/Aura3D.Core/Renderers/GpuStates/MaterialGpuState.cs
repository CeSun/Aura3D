using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class MaterialGpuState : IResourceGpuState<Material>
{
    private WeakReference<Material> material;
    public ulong Version => Resource.Version;
    public ulong SyncedVersion { get; protected set; }

    public Material Material => Resource;

    public Material Resource
    {
        get
        {
            if (material.TryGetTarget(out var value))
                return value;

            throw new InvalidOperationException("The CPU resource has already been collected.");
        }
    }

    public bool IsAlive => material.TryGetTarget(out _);

    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    public MaterialGpuState(Material material)
    {
        this.material = new WeakReference<Material>(material);
    }

    public void Upload(GL gl)
    {
        // MaterialGpuState currently only caches compiled shader programs.
        // Shader compilation is still triggered lazily by RenderPass_Shader.
        SyncedVersion = Resource.Version;
    }

    public void Destroy(GL gl)
    {
        foreach (var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }
}
