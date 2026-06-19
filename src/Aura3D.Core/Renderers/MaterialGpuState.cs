using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class MaterialGpuState : IResourceGpuState<Material>
{
    public Material Material { get; }

    public Material Resource => Material;

    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    public MaterialGpuState(Material material)
    {
        Material = material;
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
