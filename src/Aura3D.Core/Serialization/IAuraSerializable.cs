namespace Aura3D.Core.Serialization;

/// <summary>
/// 序列化接口。由 Source Generator 自动生成实现。
/// </summary>
public interface IAuraSerializable
{
    void Serialize(AuraBinaryWriter writer);
    void Deserialize(AuraBinaryReader reader, uint chunkVersion);
}