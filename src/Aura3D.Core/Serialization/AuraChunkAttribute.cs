namespace Aura3D.Core.Serialization;

/// <summary>
/// 标记一个类或结构体参与序列化。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public class AuraChunkAttribute : Attribute
{
    /// <summary>
    /// Chunk 类型 ID，文件内唯一。
    /// </summary>
    public uint ChunkType { get; }

    /// <summary>
    /// 当前字段集的版本号。新增字段时应递增此值。
    /// </summary>
    public uint ChunkVersion { get; }

    public AuraChunkAttribute(uint chunkType, uint chunkVersion)
    {
        ChunkType = chunkType;
        ChunkVersion = chunkVersion;
    }
}