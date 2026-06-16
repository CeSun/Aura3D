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
public enum AuraChunkType : uint
{
    None = 0,
    Texture = 1,
    CubeTexture = 2,
    Geometry = 3,
    Material = 4,
    Skeleton = 5,
    Animation = 6,
    Bone = 7,
    AnimationChannel = 8,
    Keyframe = 9,
    MaterialChannel = 10,
    VertexAttribute = 11,
    Node = 101,
    Model = 102,
    Mesh = 103,
}
