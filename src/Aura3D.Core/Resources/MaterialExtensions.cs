using Aura3D.Core.Renderers;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the material extensions type.
/// </summary>
public static class MaterialExtensions
{
#if NET10_0_OR_GREATER
    extension (Material material)
    {
        /// <summary>
        /// Gets the base color.
        /// </summary>
        public Texture? BaseColor
        {
            get => material.GetTexture("BaseColor");
            set => material.SetTexture("BaseColor", value);
        }

        /// <summary>
        /// Gets the normal.
        /// </summary>
        public Texture? Normal
        {
            get => material.GetTexture("Normal");
            set => material.SetTexture("Normal", value);
        }
    }
#endif

    /// <summary>
    /// Gets the base color.
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BaseColor extension property instead.", false)]
#endif
    public static Texture? GetBaseColor(this Material material) => material.GetTexture("BaseColor");

    /// <summary>
    /// Sets the base color.
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BaseColor extension property instead.", false)]
#endif
    public static void SetBaseColor(this Material material, Texture? texture) => material.SetTexture("BaseColor", texture);

    /// <summary>
    /// Gets the normal.
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the Normal extension property instead.", false)]
#endif
    public static Texture? GetNormal(this Material material) => material.GetTexture("Normal");

    /// <summary>
    /// Sets the normal.
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the Normal extension property instead.", false)]
#endif
    public static void SetNormal(this Material material, Texture? texture) => material.SetTexture("Normal", texture);
}
