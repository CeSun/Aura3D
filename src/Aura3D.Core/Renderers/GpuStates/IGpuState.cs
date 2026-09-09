using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Defines GPU state owned by the render pipeline that first synchronizes it.
/// Implementations retain any CPU-side source data needed for recreation.
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
    /// Creates or updates handles in the supplied current GL context.
    /// </summary>
    public void Upload(GL gl);

    /// <summary>
    /// Releases owned handles from the supplied current GL context.
    /// This operation must be safe to call repeatedly.
    /// </summary>
    public void Destroy(GL gl);

    /// <summary>
    /// Forgets all context-owned handles without issuing GL calls.
    /// Called after context loss; this operation must be safe to call repeatedly.
    /// A later upload must recreate the complete GPU state.
    /// </summary>
    public void Invalidate();
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
