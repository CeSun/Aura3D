using Aura3D.Core.Nodes;

namespace Aura3D.Core.Serialization;

public class AuraNodeFileReader
{
    private sealed class PendingNodeState
    {
        public required uint NodeId { get; set; }
        public required Node Node { get; init; }
        public required uint ChunkVersion { get; init; }
        public required long PayloadStart { get; init; }
        public uint ParentId { get; set; } = uint.MaxValue;
        public bool Enable { get; set; }
    }

    private readonly Dictionary<uint, object> _resourceMap = new();
    private readonly Dictionary<uint, Node> _nodeMap = new();
    private readonly List<object> _nodeList = [];
    private readonly List<PendingNodeState> _pendingNodes = [];
    private uint _fileVersion;
    private AuraChunkType _rootChunkType;
    private uint _rootNodeId;

    public IReadOnlyDictionary<uint, object> ResourceMap => _resourceMap;
    public IReadOnlyDictionary<uint, Node> NodeMap => _nodeMap;
    public uint FileVersion => _fileVersion;
    public AuraChunkType RootChunkType => _rootChunkType;
    public uint RootNodeId => _rootNodeId;
    public Node RootNode => _nodeMap[_rootNodeId];

    public AuraNodeFileReader(Stream stream)
    {
        using var reader = new AuraBinaryReader(stream, _resourceMap, _nodeList);

        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();
        var magic2 = reader.ReadByte();
        var magic3 = reader.ReadByte();

        if (magic0 != AuraFileHeader.Magic[0] ||
            magic1 != AuraFileHeader.Magic[1] ||
            magic2 != AuraFileHeader.Magic[2] ||
            magic3 != AuraFileHeader.Magic[3])
        {
            throw new InvalidDataException("Invalid .aura file: magic mismatch.");
        }

        _fileVersion = reader.ReadUInt32();
        if (_fileVersion < AuraFileHeader.MinimumSupportedFileVersion ||
            _fileVersion > AuraFileHeader.CurrentFileVersion)
        {
            throw new NotSupportedException(
                $"Unsupported .aura file version {_fileVersion}. Supported range: {AuraFileHeader.MinimumSupportedFileVersion}-{AuraFileHeader.CurrentFileVersion}.");
        }

        reader.FileVersion = _fileVersion;

        var stringTableSize = reader.ReadUInt32();
        _rootChunkType = (AuraChunkType)reader.ReadUInt32();
        _rootNodeId = reader.ReadUInt32();

        var stringTableStart = stream.Position;
        reader.LoadStringTable();
        var consumedStringTableBytes = stream.Position - stringTableStart;
        if (consumedStringTableBytes != stringTableSize)
        {
            stream.Position = stringTableStart + stringTableSize;
        }

        while (stream.Position < stream.Length)
        {
            var chunkType = (AuraChunkType)reader.ReadUInt32();
            var chunkVersion = reader.ReadUInt32();
            var chunkDataSize = reader.ReadUInt32();
            var objectId = reader.ReadUInt32();
            var chunkEnd = stream.Position + chunkDataSize;

            if (AuraNodeTypeRegistry.IsNodeChunkType(chunkType))
            {
                var node = AuraNodeTypeRegistry.CreateNode(chunkType);
                EnsureNodeListCapacity(objectId);
                _nodeList[(int)objectId] = node;
                _nodeMap[objectId] = node;
                _pendingNodes.Add(new PendingNodeState
                {
                    NodeId = objectId,
                    Node = node,
                    ChunkVersion = chunkVersion,
                    PayloadStart = stream.Position
                });
            }
            else
            {
                var resource = DeserializeResourceByType(reader, chunkType, chunkVersion);
                if (resource != null)
                {
                    _resourceMap[objectId] = resource;
                }
            }

            stream.Position = chunkEnd;
        }

        if (!_nodeMap.ContainsKey(_rootNodeId))
            throw new InvalidDataException($"Root node id {_rootNodeId} s wanot found in the file.");

        DeserializePendingNodes(reader, stream);
        RebuildHierarchy();
        RestoreRuntimeState();
    }

    private static object? DeserializeResourceByType(AuraBinaryReader reader, AuraChunkType chunkType, uint chunkVersion)
    {
        var resource = AuraResourceTypeRegistry.CreateResource(chunkType);
        if (resource is IAuraSerializable serializable)
        {
            serializable.Deserialize(reader, chunkVersion);
        }

        return resource;
    }

    private void DeserializePendingNodes(AuraBinaryReader reader, Stream stream)
    {
        foreach (var pendingState in _pendingNodes.OrderBy(state => state.NodeId))
        {
            stream.Position = pendingState.PayloadStart;
            pendingState.ParentId = reader.ReadUInt32();

            if (pendingState.Node is not IAuraSerializable serializable)
                throw new InvalidOperationException($"Node {pendingState.Node.GetType().Name} does not implement IAuraSerializable.");

            serializable.Deserialize(reader, pendingState.ChunkVersion);
            pendingState.Enable = pendingState.Node.Enable;
        }
    }

    private void EnsureNodeListCapacity(uint nodeId)
    {
        while (_nodeList.Count <= nodeId)
        {
            _nodeList.Add(null!);
        }
    }

    private void RebuildHierarchy()
    {
        foreach (var pendingState in _pendingNodes.OrderBy(state => state.NodeId))
        {
            if (pendingState.ParentId == uint.MaxValue)
                continue;

            if (!_nodeMap.TryGetValue(pendingState.ParentId, out var parent))
                throw new InvalidDataException($"Parent node id {pendingState.ParentId} was not found in the file.");

            parent.AddChild(pendingState.Node, AttachToParentRule.KeepLocal);
        }
    }

    private void RestoreRuntimeState()
    {
        RestoreModelAssociations(RootNode, null);

        foreach (var pendingState in _pendingNodes.OrderBy(state => state.NodeId))
        {
            pendingState.Node.Enable = pendingState.Enable;
        }
    }

    private static void RestoreModelAssociations(Node node, Model? currentModel)
    {
        if (node is Model model)
        {
            currentModel = model;
        }

        if (node is Mesh mesh)
        {
            mesh.Model = currentModel;
            mesh.UpdateWorldBoundingBox();
        }

        foreach (var child in node.Children)
        {
            RestoreModelAssociations(child, currentModel);
        }
    }
}
