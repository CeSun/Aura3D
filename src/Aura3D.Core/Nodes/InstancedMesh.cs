using Aura3D.Core.Math;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 表示一个可用于实例化渲染的网格节点。
/// </summary>
public class InstancedMesh : Node, IGpuResource
{
    /// <summary>
    /// 添加一个新的实例。
    /// </summary>
    /// <param name="transform">实例的模型变换矩阵。</param>
    /// <returns>新实例的索引。</returns>
    public unsafe int AddInstance(Matrix4x4 transform)
    {
        _instanceCount = geometry.AddInstance(transform) + 1;
        NeedsUpload = true;

        // 更新每个实例的世界包围盒
        UpdateInstanceWorldBoundingBox(_instanceCount - 1, transform);

        return _instanceCount - 1;
    }


    public void RemoveInstance(int index)
    {
        geometry.RemoveInstance(index);
        _instanceCount = geometry.InstanceCount;
        NeedsUpload = true;

        // 移除对应的世界包围盒
        if (index < _instanceWorldBoundingBoxes.Count)
        {
            _instanceWorldBoundingBoxes.RemoveAt(index);
            _worldBoundingBoxDirty = true;
        }
    }

    public unsafe void UpdateInstance(int index, Matrix4x4 transform)
    {
        geometry.UpdateInstance(index, transform);
        NeedsUpload = true;

        // 更新每个实例的世界包围盒
        UpdateInstanceWorldBoundingBox(index, transform);
    }

    private int _instanceCount;

    private Material? _material;

    public Material? Material
    {
        get => _material;
        set
        {
            if (_material == value) return;
            _material = value;
        }
    }

    public bool NeedsUpload { get; set; }

    private InstancedGeometry geometry { get; set; } = null!;

    private InstancedGeometryGpuState? geometryGpuState;

    public uint Vao => geometryGpuState?.Vao ?? 0;

    public int IndicesCount => geometry.IndicesCount;

    public int InstanceCount => _instanceCount;

    /// <summary>
    /// 图元类型，委托给内部 Geometry。
    /// </summary>
    public Aura3D.Core.Resources.PrimitiveType PrimitiveType => geometry.PrimitiveType;

    /// <summary>
    /// 顶点数量，委托给内部 Geometry。
    /// </summary>
    public int VertexCount => geometry.VertexCount;

    /// <summary>
    /// 获取或设置是否对此 InstancedMesh 启用视锥体剔除。
    /// </summary>
    public bool EnableFrustumCulling { get; set; } = true;

    /// <summary>
    /// 获取局部空间中的包围盒（从源几何体计算，不考虑实例变换）。
    /// 如果几何体没有位置数据，则为 <c>null</c>。
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
    /// 每个实例的世界空间包围盒缓存。
    /// </summary>
    private readonly List<BoundingBox?> _instanceWorldBoundingBoxes = new();

    /// <summary>
    /// 世界包围盒脏标记，当实例发生增删改时设为 true。
    /// </summary>
    private bool _worldBoundingBoxDirty = true;

    /// <summary>
    /// 合并后的世界空间包围盒缓存。
    /// </summary>
    private BoundingBox? _cachedWorldBoundingBox;

    /// <summary>
    /// 获取合并后的世界空间包围盒（所有实例包围盒的并集）。
    /// 如果没有实例或没有局部包围盒，则为 <c>null</c>。
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
    /// 获取指定索引实例的世界空间包围盒。
    /// </summary>
    /// <param name="index">实例索引。</param>
    /// <returns>该实例的世界包围盒；如果索引无效或没有局部包围盒则为 <c>null</c>。</returns>
    public BoundingBox? GetInstanceWorldBoundingBox(int index)
    {
        if (index < 0 || index >= _instanceWorldBoundingBoxes.Count)
            return null;
        return _instanceWorldBoundingBoxes[index];
    }

    /// <summary>
    /// 获取指定索引实例的世界变换矩阵。
    /// </summary>
    /// <param name="index">实例索引。</param>
    /// <returns>世界变换矩阵；如果索引无效则返回 null。</returns>
    public unsafe Matrix4x4? GetInstanceTransform(int index)
    {
        return geometry.GetInstanceTransform(index);
    }

    /// <summary>
    /// 获取底层几何体数据，用于射线三角形相交检测等。
    /// </summary>
    /// <returns>底层 <see cref="Geometry"/> 实例。</returns>
    public Geometry? GetGeometry()
    {
        return geometry;
    }

    /// <summary>
    /// 测试此 InstancedMesh 的合并世界包围盒是否在给定视锥体内。
    /// </summary>
    /// <param name="planes">视锥体的 6 个裁剪平面。</param>
    /// <returns>如果在视锥体内或相交则为 <c>true</c>，完全在外则为 <c>false</c>。</returns>
    public bool IsInsideFrustum(Span<Plane> planes)
    {
        var wbb = WorldBoundingBox;
        if (wbb == null)
            return true; // 没有包围盒时默认可见
        return wbb.IsBoxInsideFrustum(planes);
    }

    /// <summary>
    /// 从源几何体的 Position 属性计算局部包围盒。
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
    /// 更新指定实例的世界包围盒。
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
    /// 计算合并后的世界空间包围盒（所有实例包围盒的并集）。
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
    /// 从给定的网格创建一个实例化网格节点。
    /// </summary>
    /// <param name="mesh">要实例化的网格。</param>
    /// <returns>创建的实例化网格节点。</returns>
    public static InstancedMesh FromMesh(Mesh mesh)
    {
        if (mesh.Geometry == null)
        {
            throw new ArgumentException("The provided mesh does not contain geometry.");
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
    /// 开启或关闭指定逐实例属性的上传。
    /// </summary>
    /// <param name="name">属性名称。</param>
    /// <param name="enabled">是否启用上传。</param>
    public void SetAttributeEnabled(string name, bool enabled)
    {
        geometry.SetAttributeEnabled(name, enabled);
        NeedsUpload = true;
    }

    /// <summary>
    /// 设置通用的逐实例自定义属性。
    /// </summary>
    /// <typeparam name="T">非托管值类型，每个实例的数据元素。</typeparam>
    /// <param name="attribute">内置顶点属性枚举，同时作为名称和 location。</param>
    /// <param name="componentCount">分量数：1=float, 2=vec2, 3=vec3, 4=vec4。</param>
    /// <param name="data">逐实例数据列表，数量必须与 <see cref="InstanceCount"/> 一致。</param>
    public unsafe void SetInstanceAttribute<T>(BuildInVertexAttribute attribute, int componentCount, IReadOnlyList<T> data)
        where T : unmanaged
    {
        geometry.SetInstanceAttribute(attribute, componentCount, data);
        NeedsUpload = true;
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

        NeedsUpload = true;
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

    public void Destroy(GL gl)
    {
        geometryGpuState?.Destroy(gl);
        geometryGpuState = null;
        geometry.NeedsUpload = true;
    }


    public unsafe void Upload(GL gl)
    {
        if (_instanceCount == 0)
            return;

        geometryGpuState ??= new InstancedGeometryGpuState(geometry);

        if (geometryGpuState.Vao == 0 || geometry.NeedsUpload)
        {
            geometryGpuState.Upload(gl);
            geometry.NeedsUpload = false;
        }
    }
}
