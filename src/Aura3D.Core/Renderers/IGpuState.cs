using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal interface IGpuState
{
    public void Destroy(GL gl);
}
