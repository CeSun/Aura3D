using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the instanced mesh type.
/// </summary>
public class InstancedMesh : Node
{
    /// <summary>
    /// Adds the instance.
    /// </summary>
    public unsafe int AddInstance(Matrix4x4 transform)
    {
        _instanceCount = geometry.AddInstance(transform) + 1;
        // 更新每个实例的世界包围盒
        UpdateInstanceWorldBoundingBox(_instanceCount - 1, transform);

        return _instanceCount - 1;
    }


    /// <summary>
    /// Removes the instance.
    /// </summary>
    public void RemoveInstance(int index)
    {
        geometry.RemoveInstance(index);
        _instanceCount = geometry.InstanceCount;
        // 移除对应的世界包围盒
        if (index < _instanceWorldBoundingBoxes.Count)
        {
            _instanceWorldBoundingBoxes.RemoveAt(index);
            _worldBoundingBoxDirty = true;
        }
    }

    /// <summary>
    /// Updates the instance.
    /// </summary>
    public unsafe void UpdateInstance(int index, Matrix4x4 transform)
    {
        geometry.UpdateInstance(index, transform);
        // 更新每个实例的世界包围盒
        UpdateInstanceWorldBoundingBox(index, transform);
    }

    private int _instanceCount;

    private Material? _material;

    /// <summary>
    /// Gets the material.
    /// </summary>
    public Material? Material
    {
        get => _material;
        set
        {
            if (_material == value) return;
            _material = value;
        }
    }

    private InstancedGeometry geometry { get; set; } = null!;

    /// <summary>
    /// Gets the indices count.
    /// </summary>
    public int IndicesCount => geometry.IndicesCount;

    /// <summary>
    /// Gets the instance count.
    /// </summary>
    public int InstanceCount => _instanceCount;

    /// <summary>
    /// Gets the primitive type.
    /// </summary>
    public Aura3D.Core.Resources.PrimitiveType PrimitiveType => geometry.PrimitiveType;

    /// <summary>
    /// Gets the vertex count.
    /// </summary>
    public int VertexCount => geometry.VertexCount;

    /// <summary>
    /// Gets or sets the enable frustum culling.
    /// </summary>
    public bool EnableFrustumCulling { get; set; } = true;

    /// <summary>
    /// Gets the local bounding box.
    /// </summary>
    public BoundingBox? LocalBoundingBox
    {
        get
        {
            if (_localBoundingBox == null && _localBoundingBoxComputed == false)
            {
                _localBoundingBoxComputed = true;
                _localBoundingBox = ComputeLocalBoundingBox();
            }
            return _localBoundingBox;
        }
    }

    private BoundingBox? _localBoundingBox;
    private bool _localBoundingBoxComputed;

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    private readonly List<BoundingBox?> _instanceWorldBoundingBoxes = new();

    /// <summary>
    /// Gets the world bounding box dirty.
    /// </summary>
    private bool _worldBoundingBoxDirty = true;

    /// <summary>
    /// Gets the cached world bounding box.
    /// </summary>
    private BoundingBox? _cachedWorldBoundingBox;

    /// <summary>
    /// Gets the world bounding box.
    /// </summary>
    public BoundingBox? WorldBoundingBox
    {
        get
        {
            if (_worldBoundingBoxDirty)
            {
                _cachedWorldBoundingBox = ComputeWorldBoundingBox();
                _worldBoundingBoxDirty = false;
            }
            return _cachedWorldBoundingBox;
        }
    }

    /// <summary>
    /// Gets the instance world bounding box.
    /// </summary>
    public BoundingBox? GetInstanceWorldBoundingBox(int index)
    {
        if (index < 0 || index >= _instanceWorldBoundingBoxes.Count)
            return null;
        return _instanceWorldBoundingBoxes[index];
    }

    /// <summary>
    /// Gets the instance transform.
    /// </summary>
    public unsafe Matrix4x4? GetInstanceTransform(int index)
    {
        return geometry.GetInstanceTransform(index);
    }

    /// <summary>
    /// Gets the geometry.
    /// </summary>
    public Geometry? GetGeometry()
    {
        return geometry;
    }

    /// <summary>
    /// Determines whether inside frustum.
    /// </summary>
    public bool IsInsideFrustum(Span<Plane> planes)
    {
        var wbb = WorldBoundingBox;
        if (wbb == null)
            return true; // 没有包围盒时默认可见
        return wbb.IsBoxInsideFrustum(planes);
    }

    /// <summary>
    /// Computes the local bounding box.
    /// </summary>
    private BoundingBox? ComputeLocalBoundingBox()
    {
        var positionData = geometry.GetAttributeData(BuildInVertexAttribute.Position);
        if (positionData == null || positionData.Count < 3)
            return null;

        var positions = new List<Vector3>(positionData.Count / 3);
        for (int i = 0; i + 2 < positionData.Count; i += 3)
        {
            positions.Add(new Vector3(positionData[i], positionData[i + 1], positionData[i + 2]));
        }

        if (positions.Count == 0)
            return null;

        return BoundingBox.CreateFromPoints(positions);
    }

    /// <summary>
    /// Updates the instance world bounding box.
    /// </summary>
    private void UpdateInstanceWorldBoundingBox(int index, Matrix4x4 transform)
    {
        var localBB = LocalBoundingBox;
        if (localBB == null)
        {
            // 确保列表长度与实例数一致
            while (_instanceWorldBoundingBoxes.Count <= index)
                _instanceWorldBoundingBoxes.Add(null);
            return;
        }

        var worldBB = localBB.Transform(transform);

        if (index < _instanceWorldBoundingBoxes.Count)
            _instanceWorldBoundingBoxes[index] = worldBB;
        else
            _instanceWorldBoundingBoxes.Add(worldBB);

        _worldBoundingBoxDirty = true;
    }

    /// <summary>
    /// Computes the world bounding box.
    /// </summary>
    private BoundingBox? ComputeWorldBoundingBox()
    {
        var validBoxes = new List<BoundingBox>();
        foreach (var bb in _instanceWorldBoundingBoxes)
        {
            if (bb != null)
                validBoxes.Add(bb);
        }

        if (validBoxes.Count == 0)
            return null;

        return BoundingBox.CreateMerged(validBoxes);
    }

    /// <summary>
    /// Performs the from mesh operation.
    /// </summary>
    public static InstancedMesh FromMesh(Mesh mesh)
    {
        if (mesh.Geometry == null)
        {
            throw Aura3D.Core.Exceptions.NodeErrors.InstancedMeshRequiresGeometry(nameof(mesh));
        }

        var geometry = new InstancedGeometry(mesh.Geometry);

        var material = mesh.Material?.DeepClone();

        var instancedMesh = new InstancedMesh
        {
            geometry = geometry,
            Material = material
        };

        return instancedMesh;
    }

    /// <summary>
    /// Sets the attribute enabled.
    /// </summary>
    public void SetAttributeEnabled(string name, bool enabled)
    {
        geometry.SetAttributeEnabled(name, enabled);
    }

    /// <summary>
    /// Sets the instance attribute.
    /// </summary>
    public unsafe void SetInstanceAttribute<T>(BuildInVertexAttribute attribute, int componentCount, IReadOnlyList<T> data)
        where T : unmanaged
    {
        geometry.SetInstanceAttribute(attribute, componentCount, data);
    }

    /// <summary>
    /// Bulk-replace all instances with the given world-space transforms.
    /// More efficient than calling AddInstance/RemoveInstance per frame for dynamic data like particles.
    /// </summary>
    /// <param name="transforms">World-space transform for each instance.</param>
    public unsafe void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        geometry.SetInstances(transforms);
        _instanceCount = geometry.InstanceCount;

        _worldBoundingBoxDirty = true;

    }

    /// <summary>
    /// Set a single static world bounding box (e.g., estimated from emitter spread).
    /// Call after creation to avoid per-frame bounding box updates.
    /// </summary>
    public void SetStaticWorldBoundingBox(BoundingBox box)
    {
        _instanceWorldBoundingBoxes.Clear();
        _instanceWorldBoundingBoxes.Add(box);
        _worldBoundingBoxDirty = true;
    }

}
