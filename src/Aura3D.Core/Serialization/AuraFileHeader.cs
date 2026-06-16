namespace Aura3D.Core.Serialization;

/// <summary>
/// .aura 文件格式常量。
/// </summary>
public static class AuraFileHeader
{
    /// <summary>
    /// 文件 Magic: "aura"
    /// </summary>
    public static readonly byte[] Magic = { 0x61, 0x75, 0x72, 0x61 }; // "aura"

    /// <summary>
    /// 当前文件格式版本。
    /// </summary>
    public const uint CurrentFileVersion = 2;
}

/// <summary>
/// Chunk 类型 ID。
/// </summary>
public static class AuraChunkType
{
    public const uint Texture = 1;
    public const uint CubeTexture = 2;
    public const uint Geometry = 3;
    public const uint Material = 4;
    public const uint Skeleton = 5;
    public const uint Animation = 6;
}
