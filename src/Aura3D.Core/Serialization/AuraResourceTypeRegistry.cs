namespace Aura3D.Core.Serialization;

internal static class AuraResourceTypeRegistry
{
    public static uint GetChunkType(object resource)
    {
        return resource switch
        {
            Resources.Texture => AuraChunkType.Texture,
            Resources.CubeTexture => AuraChunkType.CubeTexture,
            Resources.Geometry => AuraChunkType.Geometry,
            Resources.Material => AuraChunkType.Material,
            Resources.Skeleton => AuraChunkType.Skeleton,
            Resources.Animation => AuraChunkType.Animation,
            _ => throw new InvalidOperationException($"Unsupported resource type: {resource.GetType().FullName}")
        };
    }

    public static object? CreateResource(uint chunkType)
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
