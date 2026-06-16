using Aura3D.Core.Nodes;

namespace Aura3D.Core.Serialization;

internal static class AuraNodeTypeRegistry
{
    public static uint GetChunkType(Node node)
    {
        return node switch
        {
            Model => AuraChunkType.Model,
            Mesh => AuraChunkType.Mesh,
            Node => AuraChunkType.Node,
            _ => throw new InvalidOperationException($"Unsupported node type: {node.GetType().FullName}")
        };
    }

    public static bool IsNodeChunkType(uint chunkType)
    {
        return chunkType == AuraChunkType.Node
            || chunkType == AuraChunkType.Model
            || chunkType == AuraChunkType.Mesh;
    }

    public static Node CreateNode(uint chunkType)
    {
        return chunkType switch
        {
            AuraChunkType.Node => new Node(),
            AuraChunkType.Model => new Model(),
            AuraChunkType.Mesh => new Mesh(),
            _ => throw new InvalidOperationException($"Unsupported node chunk type: {chunkType}")
        };
    }

    public static uint GetChunkVersion(Node node)
    {
        return 1;
    }
}
