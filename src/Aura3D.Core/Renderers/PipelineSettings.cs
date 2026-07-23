namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the pipeline settings type.
/// </summary>
public class PipelineSettings
{
    // ═══════════════════════════════════════════════
    //  构造时确定（修改后需重建 Pipeline 才能生效）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the depth format.
    /// </summary>
    public TextureFormat DepthFormat { get; set; } = TextureFormat.DepthComponent32f;

    /// <summary>
    /// Gets or sets the directional light limit.
    /// </summary>
    public int DirectionalLightLimit { get; set; } = 4;

    /// <summary>
    /// Gets or sets the point light limit.
    /// </summary>
    public int PointLightLimit { get; set; } = 4;

    /// <summary>
    /// Gets or sets the spot light limit.
    /// </summary>
    public int SpotLightLimit { get; set; } = 4;

    // ═══════════════════════════════════════════════
    //  运行时可变（修改后下帧即刻生效）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the tone mapping exposure.
    /// </summary>
    public float ToneMappingExposure { get; set; } = 0.7f;

    /// <summary>
    /// Gets or sets the brightness clamp.
    /// </summary>
    public float BrightnessClamp { get; set; } = 4.0f;

    /// <summary>
    /// Gets or sets the ambient intensity.
    /// </summary>
    public float AmbientIntensity { get; set; } = 0.1f;

    /// <summary>
    /// Gets or sets the PBR IBL ambient intensity.
    /// </summary>
    public float IblAmbientIntensity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the enable fxaa.
    /// </summary>
    public bool EnableFxaa { get; set; } = true;

    /// <summary>
    /// Gets or sets the enable frustum culling.
    /// </summary>
    public bool EnableFrustumCulling { get; set; } = true;

    /// <summary>
    /// Gets or sets the debug.
    /// </summary>
    public DebugSettings Debug { get; set; } = new DebugSettings();

    // ═══════════════════════════════════════════════
    //  CSM（级联阴影贴图）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the csm cascade count.
    /// </summary>
    public int CsmCascadeCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the csm split lambda.
    /// </summary>
    public float CsmSplitLambda { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the csm shadow map resolution.
    /// </summary>
    public int CsmShadowMapResolution { get; set; } = 1024;
}
