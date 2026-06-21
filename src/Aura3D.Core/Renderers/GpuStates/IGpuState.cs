using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public interface IGpuState
{
    public ulong Version { get; }

    public ulong SyncedVersion { get; }

    public void Upload(GL gl);

    public void Destroy(GL gl);
}

public interface IRuntimeGpuState : IGpuState
{
}

internal interface IResourceGpuState : IGpuState
{
    public bool IsAlive { get; }
}

internal interface IResourceGpuState<T> : IResourceGpuState where T : class, IVersionedResource
{
    public T Resource { get; }
}
