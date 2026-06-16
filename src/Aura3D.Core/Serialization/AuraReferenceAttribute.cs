namespace Aura3D.Core.Serialization;

/// <summary>
/// 标记一个字段是资源引用。序列化时写入 ResourceId (uint32)，
/// 反序列化时通过映射表解析为实际对象。
/// 对于外部资源，写入 0xFFFFFFFF 表示 null。
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class AuraReferenceAttribute : Attribute
{
}