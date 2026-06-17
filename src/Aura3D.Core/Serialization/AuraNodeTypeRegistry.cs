using Aura3D.Core.Nodes;
using System.Collections.Concurrent;

namespace Aura3D.Core.Serialization;

internal static class AuraNodeTypeRegistry
{
    private static readonly Lazy<Dictionary<AuraChunkType, Type>> ChunkTypeToNodeType = new(BuildNodeTypeMap);
    private static readonly ConcurrentDictionary<Type, AuraChunkAttribute?> ChunkAttributes = new();

    public static AuraChunkType GetChunkType(Node node)
    {
        var attribute = GetChunkAttribute(node.GetType());
        if (attribute != null)
            return attribute.ChunkType;

        throw new InvalidOperationException($"Unsupported node type: {node.GetType().FullName}");
    }

    public static bool IsNodeChunkType(AuraChunkType chunkType)
    {
        return ChunkTypeToNodeType.Value.ContainsKey(chunkType);
    }

    public static Node CreateNode(AuraChunkType chunkType)
    {
        if (!ChunkTypeToNodeType.Value.TryGetValue(chunkType, out var nodeType))
            throw new InvalidOperationException($"Unsupported node chunk type: {chunkType}");

        return (Node)(Activator.CreateInstance(nodeType)
            ?? throw new InvalidOperationException($"Failed to create node type '{nodeType.FullName}'."));
    }

    public static uint GetChunkVersion(Node node)
    {
        return GetChunkAttribute(node.GetType())?.ChunkVersion ?? 1u;
    }

    private static Dictionary<AuraChunkType, Type> BuildNodeTypeMap()
    {
        var map = new Dictionary<AuraChunkType, Type>();
        foreach (var type in typeof(Node).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(Node).IsAssignableFrom(type))
                continue;

            var attribute = GetChunkAttribute(type);
            if (attribute == null)
                continue;

            if (map.TryGetValue(attribute.ChunkType, out var existingType))
            {
                throw new InvalidOperationException(
                    $"Duplicate node chunk type '{attribute.ChunkType}' found on '{existingType.FullName}' and '{type.FullName}'.");
            }

            map[attribute.ChunkType] = type;
        }

        return map;
    }

    private static AuraChunkAttribute? GetChunkAttribute(Type type)
    {
        return ChunkAttributes.GetOrAdd(type, static currentType =>
            currentType.GetCustomAttributes(typeof(AuraChunkAttribute), inherit: false)
                .OfType<AuraChunkAttribute>()
                .FirstOrDefault());
    }
}
