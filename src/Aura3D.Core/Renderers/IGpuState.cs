using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal interface IGpuState
{
    public void Destroy(GL gl);
}

internal interface IResourceGpuState : IGpuState
{
    public bool IsAlive { get; }
}

internal interface IResourceGpuState<T> : IResourceGpuState where T : class
{
    public T Resource { get; }
}
