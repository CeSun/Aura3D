namespace Aura3D.Core.Resources;

/// <summary>
/// 表示带有版本号的 CPU 资源。
/// 版本号用于后续 GPU 同步判断，本次改造仅先建立统一骨架。
/// </summary>
public interface IVersionedResource
{
    ulong Version { get; }
}
