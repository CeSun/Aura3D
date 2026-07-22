using Aura3D.Core.Nodes;
using System.Numerics;

namespace Aura3D.Core.Scenes;

/// <summary>
/// Represents the pick result type.
/// </summary>
public class PickResult
{
    /// <summary>
    /// Gets or sets the node.
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// Gets or sets the instance index.
    /// </summary>
    public int? InstanceIndex { get; init; }

    /// <summary>
    /// Gets or sets the distance.
    /// </summary>
    public float Distance { get; init; }

    /// <summary>
    /// Gets or sets the world position.
    /// </summary>
    public Vector3 WorldPosition { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (InstanceIndex.HasValue)
            return $"PickResult: {Node.Name}[{InstanceIndex.Value}] at {WorldPosition} (dist={Distance:F3})";
        return $"PickResult: {Node.Name} at {WorldPosition} (dist={Distance:F3})";
    }
}
