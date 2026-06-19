using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class MaterialGpuState : IResourceGpuState<Material>
{
    private WeakReference<Material> material;

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

    public void Destroy(GL gl)
    {
        foreach (var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }
}
