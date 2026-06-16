using Aura3D.Core.Math;
using Aura3D.Core.Nodes;

namespace Aura3D.Core.Serialization;

public class AuraNodeFileWriter
{
    private readonly NodeCollector _collector;

    public AuraNodeFileWriter(NodeCollector collector)
    {
        _collector = collector;
    }

    public void Write(Stream stream)
    {
        if (_collector.RootNode == null)
            throw new InvalidOperationException("No root node has been collected.");

        var resourceMap = new Dictionary<object, uint>(_collector.ResourceMap);
        var nodeIndexMap = _collector.NodeMap.ToDictionary(pair => (object)pair.Key, pair => unchecked((int)pair.Value));

        using var payloadStream = new MemoryStream();
        using var payloadWriter = new AuraBinaryWriter(payloadStream, resourceMap, nodeIndexMap);
        payloadWriter.FileVersion = AuraFileHeader.CurrentFileVersion;

        foreach (var resource in _collector.Resources)
        {
            WriteResourceChunk(payloadWriter, resource, resourceMap[resource]);
        }

        foreach (var node in _collector.Nodes)
        {
            var parentId = node.Parent != null && _collector.NodeMap.TryGetValue(node.Parent, out var resolvedParentId)
                ? resolvedParentId
                : uint.MaxValue;
            WriteNodeChunk(payloadWriter, node, _collector.NodeMap[node], parentId);
        }

        using var stringTableStream = new MemoryStream();
        payloadWriter.FlushStringTable(stringTableStream);

        using var writer = new AuraBinaryWriter(stream, resourceMap, nodeIndexMap);
        writer.FileVersion = AuraFileHeader.CurrentFileVersion;
        writer.Write(AuraFileHeader.Magic[0]);
        writer.Write(AuraFileHeader.Magic[1]);
        writer.Write(AuraFileHeader.Magic[2]);
        writer.Write(AuraFileHeader.Magic[3]);
        writer.Write(AuraFileHeader.CurrentFileVersion);
        writer.Write((uint)stringTableStream.Length);
        writer.Write(_collector.RootChunkType);
        writer.Write(_collector.RootNodeId);

        stringTableStream.Position = 0;
        stringTableStream.CopyTo(stream);

        payloadStream.Position = 0;
        payloadStream.CopyTo(stream);
    }

    private static void WriteResourceChunk(AuraBinaryWriter writer, object resource, uint resourceId)
    {
        if (resource is not IAuraSerializable serializable)
            throw new InvalidOperationException($"Resource {resource.GetType().Name} does not implement IAuraSerializable.");

        writer.Write(AuraResourceTypeRegistry.GetChunkType(resource));
        writer.Write(AuraResourceTypeRegistry.GetChunkVersion(resource));

        var stream = writer.BaseStream;
        var sizePos = stream.Position;
        writer.Write(0u);
        writer.Write(resourceId);

        var dataStart = stream.Position;
        serializable.Serialize(writer);

        var dataEnd = stream.Position;
        var dataSize = (uint)(dataEnd - dataStart);
        stream.Position = sizePos;
        writer.Write(dataSize);
        stream.Position = dataEnd;
    }

    private static void WriteNodeChunk(AuraBinaryWriter writer, Node node, uint nodeId, uint parentId)
    {
        writer.Write(AuraNodeTypeRegistry.GetChunkType(node));
        writer.Write(AuraNodeTypeRegistry.GetChunkVersion(node));

        var stream = writer.BaseStream;
        var sizePos = stream.Position;
        writer.Write(0u);
        writer.Write(nodeId);

        var dataStart = stream.Position;
        WriteNodePayload(writer, node, parentId);

        var dataEnd = stream.Position;
        var dataSize = (uint)(dataEnd - dataStart);
        stream.Position = sizePos;
        writer.Write(dataSize);
        stream.Position = dataEnd;
    }

    private static void WriteNodePayload(AuraBinaryWriter writer, Node node, uint parentId)
    {
        writer.Write(parentId);
        writer.WriteString(node.Name);
        writer.Write(node.Enable);
        writer.WriteBlittable(node.LocalTransform);
        WriteTags(writer, node.Tags);

        switch (node)
        {
            case Model model:
                writer.WriteResourceRef(model.Skeleton);
                writer.Write(model.BoundingBoxPadding);
                WriteBoundingBox(writer, model.CustomBoundingBox);
                break;

            case Mesh mesh:
                writer.WriteResourceRef(mesh.Geometry);
                writer.WriteResourceRef(mesh.Material);
                break;
        }
    }

    private static void WriteTags(AuraBinaryWriter writer, IEnumerable<string> tags)
    {
        var orderedTags = tags.OrderBy(tag => tag, StringComparer.Ordinal).ToList();
        writer.Write(orderedTags.Count);
        foreach (var tag in orderedTags)
        {
            writer.WriteString(tag);
        }
    }

    private static void WriteBoundingBox(AuraBinaryWriter writer, BoundingBox? boundingBox)
    {
        writer.Write(boundingBox != null);
        if (boundingBox == null)
            return;

        writer.WriteBlittable(boundingBox.Min);
        writer.WriteBlittable(boundingBox.Max);
    }
}
