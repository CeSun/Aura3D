namespace Aura3D.Core.Serialization;

/// <summary>
/// 资源收集器。从一个根资源出发递归收集所有被引用的资源，去重并分配 ResourceId。
/// </summary>
public class ResourceCollector
{
    private readonly Dictionary<object, uint> _resourceMap = new();
    private readonly List<object> _resources = new();
    private readonly HashSet<object> _resourceTraversal = new();
    private uint _nextResourceId;

    public IReadOnlyDictionary<object, uint> ResourceMap => _resourceMap;
    public IReadOnlyList<object> Resources => _resources;

    public object? RootResource { get; private set; }
    public uint RootResourceId { get; private set; } = uint.MaxValue;
    public AuraChunkType RootChunkType { get; private set; }

    /// <summary>
    /// 从根资源出发收集整个资源图。
    /// </summary>
    public void Collect(object rootResource)
    {
        if (rootResource == null)
            throw new ArgumentNullException(nameof(rootResource));

        Reset();

        RootChunkType = AuraResourceTypeRegistry.GetChunkType(rootResource);
        CollectResource(rootResource);

        RootResource = rootResource;
        RootResourceId = _resourceMap[rootResource];
    }

    /// <summary>
    /// 场景序列化暂未支持。
    /// </summary>
    public void CollectFromScene(Scenes.Scene scene)
    {
        throw new NotSupportedException("Scene serialization is not supported yet. Save resources through AssetManager.SaveResource instead.");
    }

    public uint RegisterResource(object resource)
    {
        if (_resourceMap.TryGetValue(resource, out var existingId))
            return existingId;

        var id = _nextResourceId++;
        _resourceMap[resource] = id;
        _resources.Add(resource);
        return id;
    }

    private void Reset()
    {
        _resourceMap.Clear();
        _resources.Clear();
        _resourceTraversal.Clear();
        _nextResourceId = 0;
        RootResource = null;
        RootResourceId = uint.MaxValue;
        RootChunkType = AuraChunkType.None;
    }

    private void CollectResource(object resource)
    {
        AuraResourceTypeRegistry.GetChunkType(resource);

        if (!_resourceTraversal.Add(resource))
            return;

        AuraReferenceWalker.VisitSerializableReferences(resource, reference =>
        {
            if (AuraResourceTypeRegistry.TryGetChunkType(reference, out _))
                CollectResource(reference);
        });

        RegisterResource(resource);
    }
}
