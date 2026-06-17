using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using Silk.NET.OpenGLES;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 表示一个可用于实例化渲染的网格节点。
/// </summary>
[AuraChunk(chunkType: AuraChunkType.InstancedMesh, chunkVersion: 1)]
public partial class InstancedMesh : Node, IGpuResource
{
    /// <summary>
    /// 添加一个新的实例。
    /// </summary>
    /// <param name="transform">实例的模型变换矩阵。</param>
    /// <returns>新实例的索引。</returns>
    public unsafe int AddInstance(Matrix4x4 transform)
    {
        EnsureDefaultAttributes();

        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        // 追加 transform 数据（16 个 float）
        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.Data.Add(p[i]);

        // 计算并追加 normal matrix 数据
        var normalMatrix = transform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.Data.Add(p[i]);

        _instanceCount++;
        NeedsUpload = true;

        // 更新每个实例的世界包围盒
        UpdateInstanceWorldBoundingBox(_instanceCount - 1, transform);

        return _instanceCount - 1;
    }


    public void RemoveInstance(int index)
    {
        foreach (var attr in InstanceAttributes.Values)
        {
            int floatsPerInstance = attr.Stride / sizeof(float);
            attr.Data.RemoveRange(index * floatsPerInstance, floatsPerInstance);
        }
        _instanceCount--;
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
        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        int baseIndex = index * 16;

        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.Data[baseIndex + i] = p[i];

        var normalMatrix = transform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.Data[baseIndex + i] = p[i];

        NeedsUpload = true;

        // 更新每个实例的世界包围盒
        UpdateInstanceWorldBoundingBox(index, transform);
    }

    private int _instanceCount;

    private static readonly InstanceAttributePointer[] DefaultTransformPointers =
    [
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn0, ComponentCount = 4, Offset = 0 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn1, ComponentCount = 4, Offset = sizeof(float) * 4 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn2, ComponentCount = 4, Offset = sizeof(float) * 8 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn3, ComponentCount = 4, Offset = sizeof(float) * 12 },
    ];

    private static readonly InstanceAttributePointer[] DefaultNormalTransformPointers =
    [
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn0, ComponentCount = 4, Offset = 0 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn1, ComponentCount = 4, Offset = sizeof(float) * 4 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn2, ComponentCount = 4, Offset = sizeof(float) * 8 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn3, ComponentCount = 4, Offset = sizeof(float) * 12 },
    ];

    /// <summary>
    /// 逐实例属性字典，key 为属性名称。
    /// </summary>
    public Dictionary<string, InstanceAttribute> InstanceAttributes { get; } = new();

    private Material? _material;

    [AuraField(since: 1)]
    [AuraReference]
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

    private Geometry geometry = null!;

    [AuraField(since: 1)]
    [AuraReference]
    protected Geometry SerializedGeometry
    {
        get => geometry;
        set
        {
            geometry = value ?? throw new InvalidOperationException("InstancedMesh geometry cannot be null.");
            _localBoundingBox = null;
            _localBoundingBoxComputed = false;
            _instanceWorldBoundingBoxes.Clear();
            _cachedWorldBoundingBox = null;
            _worldBoundingBoxDirty = true;
        }
    }

    public uint Vao => geometry.Vao;

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
    [AuraField(since: 1)]
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
        if (!InstanceAttributes.TryGetValue("InstanceTransform", out var attr))
            return null;

        int baseIdx = index * 16;
        if (baseIdx < 0 || baseIdx + 15 >= attr.Data.Count)
            return null;

        var m = new Matrix4x4();
        float* p = (float*)&m;
        for (int i = 0; i < 16; i++)
            p[i] = attr.Data[baseIdx + i];

        return m;
    }

    [AuraField(since: 1)]
    private List<Matrix4x4> SerializedInstanceTransforms
    {
        get
        {
            var transforms = new List<Matrix4x4>(_instanceCount);
            for (int i = 0; i < _instanceCount; i++)
            {
                var transform = GetInstanceTransform(i);
                if (transform != null)
                    transforms.Add(transform.Value);
            }

            return transforms;
        }
        set
        {
            SetInstances(value ?? new List<Matrix4x4>());
        }
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

    private unsafe void EnsureDefaultAttributes()
    {
        if (!InstanceAttributes.ContainsKey("InstanceTransform"))
        {
            InstanceAttributes["InstanceTransform"] = new InstanceAttribute
            {
                Name = "InstanceTransform",
                Stride = sizeof(Matrix4x4),
                Pointers = new List<InstanceAttributePointer>(DefaultTransformPointers)
            };
        }
        if (!InstanceAttributes.ContainsKey("InstanceNormalTransform"))
        {
            InstanceAttributes["InstanceNormalTransform"] = new InstanceAttribute
            {
                Name = "InstanceNormalTransform",
                Stride = sizeof(Matrix4x4),
                Pointers = new List<InstanceAttributePointer>(DefaultNormalTransformPointers)
            };
        }
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

        var geometry = mesh.Geometry.DeepClone();

        var material = mesh.Material?.DeepClone();

        var instancedMesh = new InstancedMesh
        {
            geometry = geometry,
            Material = material
        };

        instancedMesh.EnsureDefaultAttributes();

        return instancedMesh;
    }

    /// <summary>
    /// 开启或关闭指定逐实例属性的上传。
    /// </summary>
    /// <param name="name">属性名称。</param>
    /// <param name="enabled">是否启用上传。</param>
    public void SetAttributeEnabled(string name, bool enabled)
    {
        if (InstanceAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            NeedsUpload = true;
        }
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
        if (data.Count != _instanceCount)
            throw new ArgumentException($"数据数量 ({data.Count}) 与实例数量 ({_instanceCount}) 不一致。");

        var name = attribute.ToString();
        int elementSize = sizeof(T) / sizeof(float);
        int stride = componentCount * sizeof(float);
        var floatData = new List<float>(data.Count * componentCount);

        foreach (var item in data)
        {
            float* ptr = (float*)&item;
            for (int i = 0; i < componentCount; i++)
            {
                floatData.Add(i < elementSize ? ptr[i] : 0f);
            }
        }

        InstanceAttributes[name] = new InstanceAttribute
        {
            Name = name,
            Stride = stride,
            Data = floatData,
            Pointers = new List<InstanceAttributePointer>
            {
                new() { Location = (uint)attribute, ComponentCount = componentCount, Offset = 0 }
            }
        };

        NeedsUpload = true;
    }

    /// <summary>
    /// Bulk-replace all instances with the given world-space transforms.
    /// More efficient than calling AddInstance/RemoveInstance per frame for dynamic data like particles.
    /// </summary>
    /// <param name="transforms">World-space transform for each instance.</param>
    public unsafe void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        EnsureDefaultAttributes();

        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        // Clear existing data
        transformAttr.Data.Clear();
        normalAttr.Data.Clear();

        int count = transforms.Count;
        transformAttr.Data.Capacity = count * 16;
        normalAttr.Data.Capacity = count * 16;

        for (int i = 0; i < count; i++)
        {
            var t = transforms[i];

            // Transform matrix (16 floats)
            float* p = (float*)&t;
            for (int j = 0; j < 16; j++)
                transformAttr.Data.Add(p[j]);

            // Normal matrix = transpose(inverse(transform))
            Matrix4x4.Invert(t, out var inv);
            var normalMatrix = Matrix4x4.Transpose(inv);
            p = (float*)&normalMatrix;
            for (int j = 0; j < 16; j++)
                normalAttr.Data.Add(p[j]);
        }

        _instanceCount = count;
        _instanceWorldBoundingBoxes.Clear();
        _cachedWorldBoundingBox = null;
        for (int i = 0; i < count; i++)
        {
            UpdateInstanceWorldBoundingBox(i, transforms[i]);
        }

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
        geometry.Destroy(gl);
        foreach (var attr in InstanceAttributes.Values)
        {
            if (attr.Vbo != 0)
            {
                gl.DeleteBuffer(attr.Vbo);
                attr.Vbo = 0;
            }
        }
    }


    public unsafe void Upload(GL gl)
    {
        if (_instanceCount == 0)
            return;
        if (geometry.Vao == 0)
        {
            geometry.Upload(gl);
        }

        EnsureDefaultAttributes();

        gl.BindVertexArray(geometry.Vao);

        foreach (var attr in InstanceAttributes.Values)
        {
            if (!attr.Enabled)
                continue;

            if (attr.Vbo == 0)
            {
                attr.Vbo = gl.GenBuffer();
            }

            gl.BindBuffer(GLEnum.ArrayBuffer, attr.Vbo);

            fixed (float* p = CollectionsMarshal.AsSpan(attr.Data))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attr.Data.Count * sizeof(float)), p, GLEnum.DynamicDraw);
            }

            foreach (var ptr in attr.Pointers)
            {
                gl.EnableVertexAttribArray(ptr.Location);
                gl.VertexAttribPointer(ptr.Location, ptr.ComponentCount, GLEnum.Float, false, (uint)attr.Stride, (void*)ptr.Offset);
                gl.VertexAttribDivisor(ptr.Location, 1);
            }
        }

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }
}
