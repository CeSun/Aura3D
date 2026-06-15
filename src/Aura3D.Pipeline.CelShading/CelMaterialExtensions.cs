using System.Numerics;
using Aura3D.Core.Resources;

namespace Aura3D.Pipeline.CelShading;

/// <summary>
/// CelShading（卡通渲染）管线的 Material 扩展 —— 提供 ILM、SDF、ShadowRamp、SpecularRamp 等
/// CelShading 特有纹理的 Getter / Setter，以及 RampIndex、LightFactor、ColorTint、SDF 等参数的访问。
/// .NET 10+ 使用 C# 14 extension 块语法（支持扩展属性），.NET 10 以下使用传统扩展函数。
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
        public ITexture? ILM
        {
            get => material.GetTexture("ILM");
            set => material.SetTexture("ILM", value);
        }

        /// <summary>
        /// 获取或设置 SDF（Signed Distance Field，脸部阴影距离场）纹理。
        /// </summary>
        public ITexture? SDF
        {
            get => material.GetTexture("SDF");
            set => material.SetTexture("SDF", value);
        }

        /// <summary>
        /// 获取或设置 ShadowRamp（阴影渐变）纹理。
        /// </summary>
        public ITexture? ShadowRamp
        {
            get => material.GetTexture("ShadowRamp");
            set => material.SetTexture("ShadowRamp", value);
        }

        /// <summary>
        /// 获取或设置 SpecularRamp（高光渐变）纹理。
        /// </summary>
        public ITexture? SpecularRamp
        {
            get => material.GetTexture("SpecularRamp");
            set => material.SetTexture("SpecularRamp", value);
        }

        #endregion

        #region RenderType

        /// <summary>
        /// 获取或设置渲染类型（0 = Body 身体, 1 = Face 脸部）。
        /// </summary>
        public int RenderType
        {
            get => material.TryGetParameterValue("RenderType", out int value) ? value : 0;
            set => material.SetParameterValue("RenderType", value);
        }

        #endregion

        #region RampIndex

        /// <summary>
        /// 获取或设置渐变索引 0。
        /// </summary>
        public float RampIndex0
        {
            get => material.TryGetParameterValue("_RampIndex0", out float value) ? value : 0f;
            set => material.SetParameterValue("_RampIndex0", value);
        }

        /// <summary>
        /// 获取或设置渐变索引 1。
        /// </summary>
        public float RampIndex1
        {
            get => material.TryGetParameterValue("_RampIndex1", out float value) ? value : 0f;
            set => material.SetParameterValue("_RampIndex1", value);
        }

        /// <summary>
        /// 获取或设置渐变索引 2。
        /// </summary>
        public float RampIndex2
        {
            get => material.TryGetParameterValue("_RampIndex2", out float value) ? value : 0f;
            set => material.SetParameterValue("_RampIndex2", value);
        }

        /// <summary>
        /// 获取或设置渐变索引 3。
        /// </summary>
        public float RampIndex3
        {
            get => material.TryGetParameterValue("_RampIndex3", out float value) ? value : 0f;
            set => material.SetParameterValue("_RampIndex3", value);
        }

        /// <summary>
        /// 获取或设置渐变索引 4。
        /// </summary>
        public float RampIndex4
        {
            get => material.TryGetParameterValue("_RampIndex4", out float value) ? value : 0f;
            set => material.SetParameterValue("_RampIndex4", value);
        }

        #endregion

        #region LightFactor

        /// <summary>
        /// 获取或设置亮面光照系数。
        /// </summary>
        public float BrightFac
        {
            get => material.TryGetParameterValue("_BrightFac", out float value) ? value : 0f;
            set => material.SetParameterValue("_BrightFac", value);
        }

        /// <summary>
        /// 获取或设置灰面光照系数。
        /// </summary>
        public float GreyFac
        {
            get => material.TryGetParameterValue("_GreyFac", out float value) ? value : 0f;
            set => material.SetParameterValue("_GreyFac", value);
        }

        /// <summary>
        /// 获取或设置暗面光照系数。
        /// </summary>
        public float DarkFac
        {
            get => material.TryGetParameterValue("_DarkFac", out float value) ? value : 0f;
            set => material.SetParameterValue("_DarkFac", value);
        }

        /// <summary>
        /// 获取或设置亮面阴影区域系数。
        /// </summary>
        public float BrightAreaShadowFac
        {
            get => material.TryGetParameterValue("_BrightAreaShadowFac", out float value) ? value : 0f;
            set => material.SetParameterValue("_BrightAreaShadowFac", value);
        }

        #endregion

        #region ColorTint

        /// <summary>
        /// 获取或设置亮面颜色色调（Vector4）。
        /// </summary>
        public Vector4 LightAreaColorTint
        {
            get => material.TryGetParameterValue("_LightAreaColorTint", out Vector4 value) ? value : Vector4.Zero;
            set => material.SetParameterValue("_LightAreaColorTint", value);
        }

        /// <summary>
        /// 获取或设置暗部阴影颜色（Vector4）。
        /// </summary>
        public Vector4 DarkShadowColor
        {
            get => material.TryGetParameterValue("_DarkShadowColor", out Vector4 value) ? value : Vector4.Zero;
            set => material.SetParameterValue("_DarkShadowColor", value);
        }

        /// <summary>
        /// 获取或设置冷色暗部阴影颜色（Vector4）。
        /// </summary>
        public Vector4 CoolDarkShadowColor
        {
            get => material.TryGetParameterValue("_CoolDarkShadowColor", out Vector4 value) ? value : Vector4.Zero;
            set => material.SetParameterValue("_CoolDarkShadowColor", value);
        }

        #endregion

        #region SDF Face

        /// <summary>
        /// 获取或设置脸部阴影偏移量。
        /// </summary>
        public float FaceShadowOffset
        {
            get => material.TryGetParameterValue("_FaceShadowOffset", out float value) ? value : 0f;
            set => material.SetParameterValue("_FaceShadowOffset", value);
        }

        /// <summary>
        /// 获取或设置脸部阴影过渡柔和度。
        /// </summary>
        public float FaceShadowTransitionSoftness
        {
            get => material.TryGetParameterValue("_FaceShadowTransitionSoftness", out float value) ? value : 0f;
            set => material.SetParameterValue("_FaceShadowTransitionSoftness", value);
        }

        #endregion
    }
#else
    #region Textures

    /// <summary>
    /// 获取 ILM（Indirect Light Map，间接光照贴图）纹理。
    /// </summary>
    public static ITexture? GetILM(this Material material) => material.GetTexture("ILM");

    /// <summary>
    /// 设置 ILM（Indirect Light Map，间接光照贴图）纹理。
    /// </summary>
    public static void SetILM(this Material material, ITexture? texture) => material.SetTexture("ILM", texture);

    /// <summary>
    /// 获取 SDF（Signed Distance Field，脸部阴影距离场）纹理。
    /// </summary>
    public static ITexture? GetSDF(this Material material) => material.GetTexture("SDF");

    /// <summary>
    /// 设置 SDF（Signed Distance Field，脸部阴影距离场）纹理。
    /// </summary>
    public static void SetSDF(this Material material, ITexture? texture) => material.SetTexture("SDF", texture);

    /// <summary>
    /// 获取 ShadowRamp（阴影渐变）纹理。
    /// </summary>
    public static ITexture? GetShadowRamp(this Material material) => material.GetTexture("ShadowRamp");

    /// <summary>
    /// 设置 ShadowRamp（阴影渐变）纹理。
    /// </summary>
    public static void SetShadowRamp(this Material material, ITexture? texture) => material.SetTexture("ShadowRamp", texture);

    /// <summary>
    /// 获取 SpecularRamp（高光渐变）纹理。
    /// </summary>
    public static ITexture? GetSpecularRamp(this Material material) => material.GetTexture("SpecularRamp");

    /// <summary>
    /// 设置 SpecularRamp（高光渐变）纹理。
    /// </summary>
    public static void SetSpecularRamp(this Material material, ITexture? texture) => material.SetTexture("SpecularRamp", texture);

    #endregion

    #region RenderType

    /// <summary>
    /// 获取渲染类型（0 = Body 身体, 1 = Face 脸部）。
    /// </summary>
    public static bool TryGetRenderType(this Material material, out int value) =>
        material.TryGetParameterValue("RenderType", out value);

    /// <summary>
    /// 设置渲染类型（0 = Body 身体, 1 = Face 脸部）。
    /// </summary>
    public static void SetRenderType(this Material material, int value) =>
        material.SetParameterValue("RenderType", value);

    #endregion

    #region RampIndex

    /// <summary>
    /// 获取渐变索引 0。
    /// </summary>
    public static bool TryGetRampIndex0(this Material material, out float value) =>
        material.TryGetParameterValue("_RampIndex0", out value);

    /// <summary>
    /// 设置渐变索引 0。
    /// </summary>
    public static void SetRampIndex0(this Material material, float value) =>
        material.SetParameterValue("_RampIndex0", value);

    /// <summary>
    /// 获取渐变索引 1。
    /// </summary>
    public static bool TryGetRampIndex1(this Material material, out float value) =>
        material.TryGetParameterValue("_RampIndex1", out value);

    /// <summary>
    /// 设置渐变索引 1。
    /// </summary>
    public static void SetRampIndex1(this Material material, float value) =>
        material.SetParameterValue("_RampIndex1", value);

    /// <summary>
    /// 获取渐变索引 2。
    /// </summary>
    public static bool TryGetRampIndex2(this Material material, out float value) =>
        material.TryGetParameterValue("_RampIndex2", out value);

    /// <summary>
    /// 设置渐变索引 2。
    /// </summary>
    public static void SetRampIndex2(this Material material, float value) =>
        material.SetParameterValue("_RampIndex2", value);

    /// <summary>
    /// 获取渐变索引 3。
    /// </summary>
    public static bool TryGetRampIndex3(this Material material, out float value) =>
        material.TryGetParameterValue("_RampIndex3", out value);

    /// <summary>
    /// 设置渐变索引 3。
    /// </summary>
    public static void SetRampIndex3(this Material material, float value) =>
        material.SetParameterValue("_RampIndex3", value);

    /// <summary>
    /// 获取渐变索引 4。
    /// </summary>
    public static bool TryGetRampIndex4(this Material material, out float value) =>
        material.TryGetParameterValue("_RampIndex4", out value);

    /// <summary>
    /// 设置渐变索引 4。
    /// </summary>
    public static void SetRampIndex4(this Material material, float value) =>
        material.SetParameterValue("_RampIndex4", value);

    #endregion

    #region LightFactor

    /// <summary>
    /// 获取亮面光照系数。
    /// </summary>
    public static bool TryGetBrightFac(this Material material, out float value) =>
        material.TryGetParameterValue("_BrightFac", out value);

    /// <summary>
    /// 设置亮面光照系数。
    /// </summary>
    public static void SetBrightFac(this Material material, float value) =>
        material.SetParameterValue("_BrightFac", value);

    /// <summary>
    /// 获取灰面光照系数。
    /// </summary>
    public static bool TryGetGreyFac(this Material material, out float value) =>
        material.TryGetParameterValue("_GreyFac", out value);

    /// <summary>
    /// 设置灰面光照系数。
    /// </summary>
    public static void SetGreyFac(this Material material, float value) =>
        material.SetParameterValue("_GreyFac", value);

    /// <summary>
    /// 获取暗面光照系数。
    /// </summary>
    public static bool TryGetDarkFac(this Material material, out float value) =>
        material.TryGetParameterValue("_DarkFac", out value);

    /// <summary>
    /// 设置暗面光照系数。
    /// </summary>
    public static void SetDarkFac(this Material material, float value) =>
        material.SetParameterValue("_DarkFac", value);

    /// <summary>
    /// 获取亮面阴影区域系数。
    /// </summary>
    public static bool TryGetBrightAreaShadowFac(this Material material, out float value) =>
        material.TryGetParameterValue("_BrightAreaShadowFac", out value);

    /// <summary>
    /// 设置亮面阴影区域系数。
    /// </summary>
    public static void SetBrightAreaShadowFac(this Material material, float value) =>
        material.SetParameterValue("_BrightAreaShadowFac", value);

    #endregion

    #region ColorTint

    /// <summary>
    /// 获取亮面颜色色调（Vector4）。
    /// </summary>
    public static bool TryGetLightAreaColorTint(this Material material, out Vector4 value) =>
        material.TryGetParameterValue("_LightAreaColorTint", out value);

    /// <summary>
    /// 设置亮面颜色色调（Vector4）。
    /// </summary>
    public static void SetLightAreaColorTint(this Material material, Vector4 value) =>
        material.SetParameterValue("_LightAreaColorTint", value);

    /// <summary>
    /// 获取暗部阴影颜色（Vector4）。
    /// </summary>
    public static bool TryGetDarkShadowColor(this Material material, out Vector4 value) =>
        material.TryGetParameterValue("_DarkShadowColor", out value);

    /// <summary>
    /// 设置暗部阴影颜色（Vector4）。
    /// </summary>
    public static void SetDarkShadowColor(this Material material, Vector4 value) =>
        material.SetParameterValue("_DarkShadowColor", value);

    /// <summary>
    /// 获取冷色暗部阴影颜色（Vector4）。
    /// </summary>
    public static bool TryGetCoolDarkShadowColor(this Material material, out Vector4 value) =>
        material.TryGetParameterValue("_CoolDarkShadowColor", out value);

    /// <summary>
    /// 设置冷色暗部阴影颜色（Vector4）。
    /// </summary>
    public static void SetCoolDarkShadowColor(this Material material, Vector4 value) =>
        material.SetParameterValue("_CoolDarkShadowColor", value);

    #endregion

    #region SDF Face

    /// <summary>
    /// 获取脸部阴影偏移量。
    /// </summary>
    public static bool TryGetFaceShadowOffset(this Material material, out float value) =>
        material.TryGetParameterValue("_FaceShadowOffset", out value);

    /// <summary>
    /// 设置脸部阴影偏移量。
    /// </summary>
    public static void SetFaceShadowOffset(this Material material, float value) =>
        material.SetParameterValue("_FaceShadowOffset", value);

    /// <summary>
    /// 获取脸部阴影过渡柔和度。
    /// </summary>
    public static bool TryGetFaceShadowTransitionSoftness(this Material material, out float value) =>
        material.TryGetParameterValue("_FaceShadowTransitionSoftness", out value);

    /// <summary>
    /// 设置脸部阴影过渡柔和度。
    /// </summary>
    public static void SetFaceShadowTransitionSoftness(this Material material, float value) =>
        material.SetParameterValue("_FaceShadowTransitionSoftness", value);

    #endregion
#endif
}
