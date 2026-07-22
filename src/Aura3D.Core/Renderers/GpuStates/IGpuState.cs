using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Defines the contract for gpu state.
/// </summary>
public interface IGpuState
{
    /// <summary>
    /// Gets the version.
    /// </summary>
    public ulong Version { get; }

    /// <summary>
    /// Gets the synced version.
    /// </summary>
    public ulong SyncedVersion { get; }

    /// <summary>
    /// Uploads the associated data.
    /// </summary>
    public void Upload(GL gl);

    /// <summary>
    /// Destroys the associated data.
    /// </summary>
    public void Destroy(GL gl);
}

/// <summary>
/// Defines the contract for runtime gpu state.
/// </summary>
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
