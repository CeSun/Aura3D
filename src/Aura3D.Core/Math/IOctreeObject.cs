using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Math;


/// <summary>
/// Defines the contract for octree object.
/// </summary>
public interface IOctreeObject
{
    /// <summary>
    /// Gets the bounding box.
    /// </summary>
    BoundingBox? BoundingBox { get; }

    /// <summary>
    /// Gets the belonging nodes.
    /// </summary>
    List<object> BelongingNodes { get; }

    /// <summary>
    /// Occurs when on bounding box changed is raised.
    /// </summary>
    event Action<IOctreeObject>? OnBoundingBoxChanged;
}