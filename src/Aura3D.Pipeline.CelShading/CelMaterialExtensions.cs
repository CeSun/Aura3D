using System.Numerics;
using Aura3D.Core.Resources;

namespace Aura3D.Pipeline.CelShading;

/// <summary>
/// CelShading（卡通渲染）管线的 Material 扩展 —— 提供 ILM、SDF、ShadowRamp、SpecularRamp 等
/// CelShading 特有纹理的 Getter / Setter，以及 RampIndex、LightFactor、ColorTint、SDF 等参数的访问。
/// .NET 10+ 使用 C# 14 extension 块语法（支持扩展属性），同时保留传统扩展函数并标记为已弃用；.NET 10 以下使用传统扩展函数。
/// </summary>
public static class CelMaterialExtensions
{
#if NET10_0_OR_GREATER
    extension (Material material)
    {
        #region Textures

        /// <summary>
        /// 获取或设置 ILM（Indirect Light Map，间接光照贴图）纹理。
        /// </summary>
        public Texture? ILM
        {
            get => material.GetTexture("ILM");
            set => material.SetTexture("ILM", value);
        }

        /// <summary>
        /// 获取或设置 SDF（Signed Distance Field，脸部阴影距离场）纹理。
        /// </summary>
        public Texture? SDF
        {
            get => material.GetTexture("SDF");
            set => material.SetTexture("SDF", value);
        }

        /// <summary>
        /// 获取或设置 ShadowRamp（阴影渐变）纹理。
        /// </summary>
        public Texture? ShadowRamp
        {
            get => material.GetTexture("ShadowRamp");
            set => material.SetTexture("ShadowRamp", value);
        }

        /// <summary>
        /// 获取或设置 SpecularRamp（高光渐变）纹理。
        /// </summary>
        public Texture? SpecularRamp
        {
            get => material.GetTexture("SpecularRamp");
            set => material.SetTexture("SpecularRamp", value);
        }

        #endregion

        #region RenderType

        /// <summary>
        /// 获取或设置渲染类型（0 = Body 身体, 1 = Face 脸部）。
        /// 未设置时返回 null。
        /// </summary>
        public int? RenderType
        {
            get => material.TryGetParameterValue("RenderType", out int value) ? value : null;
            set
            {
                if (value is int v)
                {
                    material.SetParameterValue("RenderType", v);
                }
                else
                {
                    material.RemoveParameterValue("RenderType");
                }
            }
        }

        #endregion

        #region RampIndex

        /// <summary>
        /// 获取或设置渐变索引 0。
        /// 未设置时返回 null。
        /// </summary>
        public float? RampIndex0
        {
            get => material.TryGetParameterValue("_RampIndex0", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_RampIndex0", v);
                }
                else
                {
                    material.RemoveParameterValue("_RampIndex0");
                }
            }
        }

        /// <summary>
        /// 获取或设置渐变索引 1。
        /// 未设置时返回 null。
        /// </summary>
        public float? RampIndex1
        {
            get => material.TryGetParameterValue("_RampIndex1", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_RampIndex1", v);
                }
                else
                {
                    material.RemoveParameterValue("_RampIndex1");
                }
            }
        }

        /// <summary>
        /// 获取或设置渐变索引 2。
        /// 未设置时返回 null。
        /// </summary>
        public float? RampIndex2
        {
            get => material.TryGetParameterValue("_RampIndex2", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_RampIndex2", v);
                }
                else
                {
                    material.RemoveParameterValue("_RampIndex2");
                }
            }
        }

        /// <summary>
        /// 获取或设置渐变索引 3。
        /// 未设置时返回 null。
        /// </summary>
        public float? RampIndex3
        {
            get => material.TryGetParameterValue("_RampIndex3", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_RampIndex3", v);
                }
                else
                {
                    material.RemoveParameterValue("_RampIndex3");
                }
            }
        }

        /// <summary>
        /// 获取或设置渐变索引 4。
        /// 未设置时返回 null。
        /// </summary>
        public float? RampIndex4
        {
            get => material.TryGetParameterValue("_RampIndex4", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_RampIndex4", v);
                }
                else
                {
                    material.RemoveParameterValue("_RampIndex4");
                }
            }
        }

        #endregion

        #region LightFactor

        /// <summary>
        /// 获取或设置亮面光照系数。
        /// 未设置时返回 null。
        /// </summary>
        public float? BrightFac
        {
            get => material.TryGetParameterValue("_BrightFac", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_BrightFac", v);
                }
                else
                {
                    material.RemoveParameterValue("_BrightFac");
                }
            }
        }

        /// <summary>
        /// 获取或设置灰面光照系数。
        /// 未设置时返回 null。
        /// </summary>
        public float? GreyFac
        {
            get => material.TryGetParameterValue("_GreyFac", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_GreyFac", v);
                }
                else
                {
                    material.RemoveParameterValue("_GreyFac");
                }
            }
        }

        /// <summary>
        /// 获取或设置暗面光照系数。
        /// 未设置时返回 null。
        /// </summary>
        public float? DarkFac
        {
            get => material.TryGetParameterValue("_DarkFac", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_DarkFac", v);
                }
                else
                {
                    material.RemoveParameterValue("_DarkFac");
                }
            }
        }

        /// <summary>
        /// 获取或设置亮面阴影区域系数。
        /// 未设置时返回 null。
        /// </summary>
        public float? BrightAreaShadowFac
        {
            get => material.TryGetParameterValue("_BrightAreaShadowFac", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_BrightAreaShadowFac", v);
                }
                else
                {
                    material.RemoveParameterValue("_BrightAreaShadowFac");
                }
            }
        }

        #endregion

        #region ColorTint

        /// <summary>
        /// 获取或设置亮面颜色色调（Vector4）。
        /// 未设置时返回 null。
        /// </summary>
        public Vector4? LightAreaColorTint
        {
            get => material.TryGetParameterValue("_LightAreaColorTint", out Vector4 value) ? value : null;
            set
            {
                if (value is Vector4 v)
                {
                    material.SetParameterValue("_LightAreaColorTint", v);
                }
                else
                {
                    material.RemoveParameterValue("_LightAreaColorTint");
                }
            }
        }

        /// <summary>
        /// 获取或设置暗部阴影颜色（Vector4）。
        /// 未设置时返回 null。
        /// </summary>
        public Vector4? DarkShadowColor
        {
            get => material.TryGetParameterValue("_DarkShadowColor", out Vector4 value) ? value : null;
            set
            {
                if (value is Vector4 v)
                {
                    material.SetParameterValue("_DarkShadowColor", v);
                }
                else
                {
                    material.RemoveParameterValue("_DarkShadowColor");
                }
            }
        }

        /// <summary>
        /// 获取或设置冷色暗部阴影颜色（Vector4）。
        /// 未设置时返回 null。
        /// </summary>
        public Vector4? CoolDarkShadowColor
        {
            get => material.TryGetParameterValue("_CoolDarkShadowColor", out Vector4 value) ? value : null;
            set
            {
                if (value is Vector4 v)
                {
                    material.SetParameterValue("_CoolDarkShadowColor", v);
                }
                else
                {
                    material.RemoveParameterValue("_CoolDarkShadowColor");
                }
            }
        }

        #endregion

        #region SDF Face

        /// <summary>
        /// 获取或设置脸部阴影偏移量。
        /// 未设置时返回 null。
        /// </summary>
        public float? FaceShadowOffset
        {
            get => material.TryGetParameterValue("_FaceShadowOffset", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_FaceShadowOffset", v);
                }
                else
                {
                    material.RemoveParameterValue("_FaceShadowOffset");
                }
            }
        }

        /// <summary>
        /// 获取或设置脸部阴影过渡柔和度。
        /// 未设置时返回 null。
        /// </summary>
        public float? FaceShadowTransitionSoftness
        {
            get => material.TryGetParameterValue("_FaceShadowTransitionSoftness", out float value) ? value : null;
            set
            {
                if (value is float v)
                {
                    material.SetParameterValue("_FaceShadowTransitionSoftness", v);
                }
                else
                {
                    material.RemoveParameterValue("_FaceShadowTransitionSoftness");
                }
            }
        }

        #endregion
    }
#endif

    #region Textures

    /// <summary>
    /// 获取 ILM（Indirect Light Map，间接光照贴图）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the ILM extension property instead.", false)]
#endif
    public static Texture? GetILM(this Material material) => material.GetTexture("ILM");

    /// <summary>
    /// 设置 ILM（Indirect Light Map，间接光照贴图）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the ILM extension property instead.", false)]
#endif
    public static void SetILM(this Material material, Texture? texture) => material.SetTexture("ILM", texture);

    /// <summary>
    /// 获取 SDF（Signed Distance Field，脸部阴影距离场）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the SDF extension property instead.", false)]
#endif
    public static Texture? GetSDF(this Material material) => material.GetTexture("SDF");

    /// <summary>
    /// 设置 SDF（Signed Distance Field，脸部阴影距离场）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the SDF extension property instead.", false)]
#endif
    public static void SetSDF(this Material material, Texture? texture) => material.SetTexture("SDF", texture);

    /// <summary>
    /// 获取 ShadowRamp（阴影渐变）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the ShadowRamp extension property instead.", false)]
#endif
    public static Texture? GetShadowRamp(this Material material) => material.GetTexture("ShadowRamp");

    /// <summary>
    /// 设置 ShadowRamp（阴影渐变）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the ShadowRamp extension property instead.", false)]
#endif
    public static void SetShadowRamp(this Material material, Texture? texture) => material.SetTexture("ShadowRamp", texture);

    /// <summary>
    /// 获取 SpecularRamp（高光渐变）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the SpecularRamp extension property instead.", false)]
#endif
    public static Texture? GetSpecularRamp(this Material material) => material.GetTexture("SpecularRamp");

    /// <summary>
    /// 设置 SpecularRamp（高光渐变）纹理。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the SpecularRamp extension property instead.", false)]
#endif
    public static void SetSpecularRamp(this Material material, Texture? texture) => material.SetTexture("SpecularRamp", texture);

    #endregion

    #region RenderType

    /// <summary>
    /// 获取渲染类型（0 = Body 身体, 1 = Face 脸部）。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RenderType extension property instead.", false)]
#endif
    public static int? GetRenderType(this Material material) =>
        material.TryGetParameterValue("RenderType", out int value) ? value : null;

    /// <summary>
    /// 设置渲染类型（0 = Body 身体, 1 = Face 脸部）。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RenderType extension property instead.", false)]
#endif
    public static void SetRenderType(this Material material, int value) =>
        material.SetParameterValue("RenderType", value);

    #endregion

    #region RampIndex

    /// <summary>
    /// 获取渐变索引 0。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex0 extension property instead.", false)]
#endif
    public static float? GetRampIndex0(this Material material) =>
        material.TryGetParameterValue("_RampIndex0", out float value) ? value : null;

    /// <summary>
    /// 设置渐变索引 0。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex0 extension property instead.", false)]
#endif
    public static void SetRampIndex0(this Material material, float value) =>
        material.SetParameterValue("_RampIndex0", value);

    /// <summary>
    /// 获取渐变索引 1。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex1 extension property instead.", false)]
#endif
    public static float? GetRampIndex1(this Material material) =>
        material.TryGetParameterValue("_RampIndex1", out float value) ? value : null;

    /// <summary>
    /// 设置渐变索引 1。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex1 extension property instead.", false)]
#endif
    public static void SetRampIndex1(this Material material, float value) =>
        material.SetParameterValue("_RampIndex1", value);

    /// <summary>
    /// 获取渐变索引 2。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex2 extension property instead.", false)]
#endif
    public static float? GetRampIndex2(this Material material) =>
        material.TryGetParameterValue("_RampIndex2", out float value) ? value : null;

    /// <summary>
    /// 设置渐变索引 2。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex2 extension property instead.", false)]
#endif
    public static void SetRampIndex2(this Material material, float value) =>
        material.SetParameterValue("_RampIndex2", value);

    /// <summary>
    /// 获取渐变索引 3。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex3 extension property instead.", false)]
#endif
    public static float? GetRampIndex3(this Material material) =>
        material.TryGetParameterValue("_RampIndex3", out float value) ? value : null;

    /// <summary>
    /// 设置渐变索引 3。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex3 extension property instead.", false)]
#endif
    public static void SetRampIndex3(this Material material, float value) =>
        material.SetParameterValue("_RampIndex3", value);

    /// <summary>
    /// 获取渐变索引 4。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex4 extension property instead.", false)]
#endif
    public static float? GetRampIndex4(this Material material) =>
        material.TryGetParameterValue("_RampIndex4", out float value) ? value : null;

    /// <summary>
    /// 设置渐变索引 4。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the RampIndex4 extension property instead.", false)]
#endif
    public static void SetRampIndex4(this Material material, float value) =>
        material.SetParameterValue("_RampIndex4", value);

    #endregion

    #region LightFactor

    /// <summary>
    /// 获取亮面光照系数。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BrightFac extension property instead.", false)]
#endif
    public static float? GetBrightFac(this Material material) =>
        material.TryGetParameterValue("_BrightFac", out float value) ? value : null;

    /// <summary>
    /// 设置亮面光照系数。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BrightFac extension property instead.", false)]
#endif
    public static void SetBrightFac(this Material material, float value) =>
        material.SetParameterValue("_BrightFac", value);

    /// <summary>
    /// 获取灰面光照系数。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the GreyFac extension property instead.", false)]
#endif
    public static float? GetGreyFac(this Material material) =>
        material.TryGetParameterValue("_GreyFac", out float value) ? value : null;

    /// <summary>
    /// 设置灰面光照系数。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the GreyFac extension property instead.", false)]
#endif
    public static void SetGreyFac(this Material material, float value) =>
        material.SetParameterValue("_GreyFac", value);

    /// <summary>
    /// 获取暗面光照系数。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the DarkFac extension property instead.", false)]
#endif
    public static float? GetDarkFac(this Material material) =>
        material.TryGetParameterValue("_DarkFac", out float value) ? value : null;

    /// <summary>
    /// 设置暗面光照系数。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the DarkFac extension property instead.", false)]
#endif
    public static void SetDarkFac(this Material material, float value) =>
        material.SetParameterValue("_DarkFac", value);

    /// <summary>
    /// 获取亮面阴影区域系数。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BrightAreaShadowFac extension property instead.", false)]
#endif
    public static float? GetBrightAreaShadowFac(this Material material) =>
        material.TryGetParameterValue("_BrightAreaShadowFac", out float value) ? value : null;

    /// <summary>
    /// 设置亮面阴影区域系数。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the BrightAreaShadowFac extension property instead.", false)]
#endif
    public static void SetBrightAreaShadowFac(this Material material, float value) =>
        material.SetParameterValue("_BrightAreaShadowFac", value);

    #endregion

    #region ColorTint

    /// <summary>
    /// 获取亮面颜色色调（Vector4）。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the LightAreaColorTint extension property instead.", false)]
#endif
    public static Vector4? GetLightAreaColorTint(this Material material) =>
        material.TryGetParameterValue("_LightAreaColorTint", out Vector4 value) ? value : null;

    /// <summary>
    /// 设置亮面颜色色调（Vector4）。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the LightAreaColorTint extension property instead.", false)]
#endif
    public static void SetLightAreaColorTint(this Material material, Vector4 value) =>
        material.SetParameterValue("_LightAreaColorTint", value);

    /// <summary>
    /// 获取暗部阴影颜色（Vector4）。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the DarkShadowColor extension property instead.", false)]
#endif
    public static Vector4? GetDarkShadowColor(this Material material) =>
        material.TryGetParameterValue("_DarkShadowColor", out Vector4 value) ? value : null;

    /// <summary>
    /// 设置暗部阴影颜色（Vector4）。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the DarkShadowColor extension property instead.", false)]
#endif
    public static void SetDarkShadowColor(this Material material, Vector4 value) =>
        material.SetParameterValue("_DarkShadowColor", value);

    /// <summary>
    /// 获取冷色暗部阴影颜色（Vector4）。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the CoolDarkShadowColor extension property instead.", false)]
#endif
    public static Vector4? GetCoolDarkShadowColor(this Material material) =>
        material.TryGetParameterValue("_CoolDarkShadowColor", out Vector4 value) ? value : null;

    /// <summary>
    /// 设置冷色暗部阴影颜色（Vector4）。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the CoolDarkShadowColor extension property instead.", false)]
#endif
    public static void SetCoolDarkShadowColor(this Material material, Vector4 value) =>
        material.SetParameterValue("_CoolDarkShadowColor", value);

    #endregion

    #region SDF Face

    /// <summary>
    /// 获取脸部阴影偏移量。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the FaceShadowOffset extension property instead.", false)]
#endif
    public static float? GetFaceShadowOffset(this Material material) =>
        material.TryGetParameterValue("_FaceShadowOffset", out float value) ? value : null;

    /// <summary>
    /// 设置脸部阴影偏移量。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the FaceShadowOffset extension property instead.", false)]
#endif
    public static void SetFaceShadowOffset(this Material material, float value) =>
        material.SetParameterValue("_FaceShadowOffset", value);

    /// <summary>
    /// 获取脸部阴影过渡柔和度。
    /// 未设置时返回 null。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the FaceShadowTransitionSoftness extension property instead.", false)]
#endif
    public static float? GetFaceShadowTransitionSoftness(this Material material) =>
        material.TryGetParameterValue("_FaceShadowTransitionSoftness", out float value) ? value : null;

    /// <summary>
    /// 设置脸部阴影过渡柔和度。
    /// </summary>
#if NET10_0_OR_GREATER
    [Obsolete("Use the FaceShadowTransitionSoftness extension property instead.", false)]
#endif
    public static void SetFaceShadowTransitionSoftness(this Material material, float value) =>
        material.SetParameterValue("_FaceShadowTransitionSoftness", value);

    #endregion
}
