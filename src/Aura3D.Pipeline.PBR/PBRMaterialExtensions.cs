using Aura3D.Core.Resources;

namespace Aura3D.Pipeline.PBR;

/// <summary>
/// PBR 延迟渲染管线的 Material 扩展 —— 提供 MetallicRoughness、Occlusion、Emissive 等 PBR 特有纹理的 Getter / Setter。
/// .NET 10+ 使用 C# 14 extension 块语法（支持扩展属性），.NET 10 以下使用传统扩展函数。
/// </summary>
public static class PBRMaterialExtensions
{
#if NET10_0_OR_GREATER
    extension (Material material)
    {
        /// <summary>
        /// 获取或设置 MetallicRoughness（金属度/粗糙度）纹理。
        /// R 通道为金属度，G 通道为粗糙度。
        /// </summary>
        public ITexture? MetallicRoughness
        {
            get => material.GetTexture("MetallicRoughness");
            set => material.SetTexture("MetallicRoughness", value);
        }

        /// <summary>
        /// 获取或设置 Occlusion（环境光遮蔽）纹理。
        /// </summary>
        public ITexture? Occlusion
        {
            get => material.GetTexture("Occlusion");
            set => material.SetTexture("Occlusion", value);
        }

        /// <summary>
        /// 获取或设置 Emissive（自发光）纹理。
        /// </summary>
        public ITexture? Emissive
        {
            get => material.GetTexture("Emissive");
            set => material.SetTexture("Emissive", value);
        }
    }
#else
    /// <summary>
    /// 获取 MetallicRoughness（金属度/粗糙度）纹理。
    /// R 通道为金属度，G 通道为粗糙度。
    /// </summary>
    public static ITexture? GetMetallicRoughness(this Material material) => material.GetTexture("MetallicRoughness");

    /// <summary>
    /// 设置 MetallicRoughness（金属度/粗糙度）纹理。
    /// R 通道为金属度，G 通道为粗糙度。
    /// </summary>
    public static void SetMetallicRoughness(this Material material, ITexture? texture) => material.SetTexture("MetallicRoughness", texture);

    /// <summary>
    /// 获取 Occlusion（环境光遮蔽）纹理。
    /// </summary>
    public static ITexture? GetOcclusion(this Material material) => material.GetTexture("Occlusion");

    /// <summary>
    /// 设置 Occlusion（环境光遮蔽）纹理。
    /// </summary>
    public static void SetOcclusion(this Material material, ITexture? texture) => material.SetTexture("Occlusion", texture);

    /// <summary>
    /// 获取 Emissive（自发光）纹理。
    /// </summary>
    public static ITexture? GetEmissive(this Material material) => material.GetTexture("Emissive");

    /// <summary>
    /// 设置 Emissive（自发光）纹理。
    /// </summary>
    public static void SetEmissive(this Material material, ITexture? texture) => material.SetTexture("Emissive", texture);
#endif
}
