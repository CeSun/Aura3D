using Aura3D.Core.Renderers;

namespace Aura3D.Core.Resources;

/// <summary>
/// Material 扩展 —— 提供所有管线通用的 BaseColor / Normal 的 Getter / Setter。
/// .NET 10+ 使用 C# 14 extension 块语法（支持扩展属性），同时保留传统扩展函数并标记为已弃用；.NET 10 以下使用传统扩展函数。
/// </summary>
public static class MaterialExtensions
{
#if NET10_0_OR_GREATER
    extension (Material material)
    {
        /// <summary>
        /// 获取或设置 BaseColor（基础颜色）纹理。
        /// </summary>
        public ITexture? BaseColor
        {
            get => material.GetTexture("BaseColor");
            set => material.SetTexture("BaseColor", value);
        }

        /// <summary>
        /// 获取或设置 Normal（法线）纹理。
        /// </summary>
        public ITexture? Normal
        {
            get => material.GetTexture("Normal");
            set => material.SetTexture("Normal", value);
        }
    }
#endif

    /// <summary>
    /// 获取 BaseColor（基础颜色）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BaseColor extension property instead.", false)]
#endif
    public static ITexture? GetBaseColor(this Material material) => material.GetTexture("BaseColor");

    /// <summary>
    /// 设置 BaseColor（基础颜色）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BaseColor extension property instead.", false)]
#endif
    public static void SetBaseColor(this Material material, ITexture? texture) => material.SetTexture("BaseColor", texture);

    /// <summary>
    /// 获取 Normal（法线）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the Normal extension property instead.", false)]
#endif
    public static ITexture? GetNormal(this Material material) => material.GetTexture("Normal");

    /// <summary>
    /// 设置 Normal（法线）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the Normal extension property instead.", false)]
#endif
    public static void SetNormal(this Material material, ITexture? texture) => material.SetTexture("Normal", texture);
}
