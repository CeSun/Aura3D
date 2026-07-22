namespace Aura3D.Core.Resources;

/// <summary>
/// Defines the contract for versioned resource.
/// </summary>
public interface IVersionedResource
{
    /// <summary>
    /// Gets the version.
    /// </summary>
    ulong Version { get; }
}
