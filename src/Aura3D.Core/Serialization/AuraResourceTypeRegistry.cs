namespace Aura3D.Core.Serialization;

internal static class AuraResourceTypeRegistry
{
    public static AuraChunkType GetChunkType(object resource)
    {
        if (TryGetChunkType(resource, out var chunkType))
            return chunkType;

        throw new InvalidOperationException($"Unsupported resource type: {resource.GetType().FullName}");
    }

    public static bool TryGetChunkType(object resource, out AuraChunkType chunkType)
    {
        chunkType = resource switch
        {
            Resources.Texture => AuraChunkType.Texture,
            Resources.CubeTexture => AuraChunkType.CubeTexture,
            Resources.Geometry => AuraChunkType.Geometry,
            Resources.Material => AuraChunkType.Material,
            Resources.Skeleton => AuraChunkType.Skeleton,
            Resources.Animation => AuraChunkType.Animation,
            _ => AuraChunkType.None
        };

        return chunkType != AuraChunkType.None;
    }

    public static object? CreateResource(AuraChunkType chunkType)
    {
        return chunkType switch
        {
            AuraChunkType.Texture => new Resources.Texture(),
            AuraChunkType.CubeTexture => new Resources.CubeTexture(),
            AuraChunkType.Geometry => new Resources.Geometry(),
            AuraChunkType.Material => new Resources.Material(),
            AuraChunkType.Skeleton => new Resources.Skeleton(),
            AuraChunkType.Animation => new Resources.Animation(),
            _ => null
        };
    }

    public static uint GetChunkVersion(object resource)
    {
        var attribute = resource.GetType().GetCustomAttributes(typeof(AuraChunkAttribute), false)
            .OfType<AuraChunkAttribute>()
            .FirstOrDefault();

        return attribute?.ChunkVersion ?? 1u;
    }
}
