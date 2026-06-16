namespace Aura3D.Core.Serialization;

/// <summary>
/// .aura file format constants.
/// </summary>
public static class AuraFileHeader
{
    /// <summary>
    /// File magic: "aura"
    /// </summary>
    public static readonly byte[] Magic = { 0x61, 0x75, 0x72, 0x61 }; // "aura"

    /// <summary>
    /// Current file format version.
    /// </summary>
    public const uint CurrentFileVersion = 3;

    /// <summary>
    /// Current earliest supported file format version.
    /// </summary>
    public const uint MinimumSupportedFileVersion = 2;
}

/// <summary>
/// Chunk type IDs.
/// </summary>
public static class AuraChunkType
{
    public const uint Texture = 1;
    public const uint CubeTexture = 2;
    public const uint Geometry = 3;
    public const uint Material = 4;
    public const uint Skeleton = 5;
    public const uint Animation = 6;
    public const uint Node = 101;
    public const uint Model = 102;
    public const uint Mesh = 103;
}
