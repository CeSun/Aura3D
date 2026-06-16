using Aura3D.Core.Nodes;

namespace Aura3D.Core.Serialization;

/// <summary>
/// Collects a node tree together with every referenced resource into a single graph.
/// </summary>
public class NodeCollector
{
    private readonly Dictionary<object, uint> _resourceMap = new();
    private readonly List<object> _resources = new();
    private readonly HashSet<object> _resourceTraversal = new();
    private readonly Dictionary<Node, uint> _nodeMap = new();
    private readonly List<Node> _nodes = new();
    private uint _nextResourceId;
    private uint _nextNodeId;

    public IReadOnlyDictionary<object, uint> ResourceMap => _resourceMap;
    public IReadOnlyList<object> Resources => _resources;
    public IReadOnlyDictionary<Node, uint> NodeMap => _nodeMap;
    public IReadOnlyList<Node> Nodes => _nodes;

    public Node? RootNode { get; private set; }
    public uint RootNodeId { get; private set; } = uint.MaxValue;
    public uint RootChunkType { get; private set; }

    public void Collect(Node rootNode)
    {
        ArgumentNullException.ThrowIfNull(rootNode);

        Reset();

        RootNode = rootNode;
        RootChunkType = AuraNodeTypeRegistry.GetChunkType(rootNode);

        CollectNode(rootNode);

        RootNodeId = _nodeMap[rootNode];
    }

    private void Reset()
    {
        _resourceMap.Clear();
        _resources.Clear();
        _resourceTraversal.Clear();
        _nodeMap.Clear();
        _nodes.Clear();
        _nextResourceId = 0;
        _nextNodeId = 0;
        RootNode = null;
        RootNodeId = uint.MaxValue;
        RootChunkType = 0;
    }

    private void CollectNode(Node node)
    {
        AuraNodeTypeRegistry.GetChunkType(node);

        if (_nodeMap.ContainsKey(node))
            return;

        _nodeMap[node] = _nextNodeId++;
        _nodes.Add(node);

        switch (node)
        {
            case Model model when model.Skeleton != null:
                CollectResource(model.Skeleton);
                break;
            case Mesh mesh:
                if (mesh.Geometry != null)
                    CollectResource(mesh.Geometry);
                if (mesh.Material != null)
                    CollectResource(mesh.Material);
                break;
        }

        foreach (var child in node.Children.OrderBy(child => child.Name, StringComparer.Ordinal))
        {
            CollectNode(child);
        }
    }

    private void CollectResource(object resource)
    {
        AuraResourceTypeRegistry.GetChunkType(resource);

        if (!_resourceTraversal.Add(resource))
            return;

        switch (resource)
        {
            case Resources.Material material:
                foreach (var channel in material.Channels)
                {
                    if (channel.Texture != null)
                        CollectResource(channel.Texture);
                }
                break;

            case Resources.Animation animation when animation.Skeleton != null:
                CollectResource(animation.Skeleton);
                break;
        }

        RegisterResource(resource);
    }

    private void RegisterResource(object resource)
    {
        if (_resourceMap.ContainsKey(resource))
            return;

        _resourceMap[resource] = _nextResourceId++;
        _resources.Add(resource);
    }
}
