namespace Aura3D.Core.Serialization;

/// <summary>
/// 标记一个字段或属性参与序列化。
/// 只有标记了此 Attribute 的成员才会被序列化，其余自动忽略。
/// 字段按声明顺序写入文件。
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class AuraFieldAttribute : Attribute
{
    /// <summary>
    /// 此字段首次出现在哪个 chunkVersion。
    /// 反序列化时，如果文件版本小于此值，字段使用默认值。
    /// </summary>
    public uint Since { get; }

    public AuraFieldAttribute(uint since)
    {
        Since = since;
    }
}