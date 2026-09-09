namespace Aura3D.Core.Renderers;

/// <summary>
/// Configures render-pipeline limits and runtime effects. Invalid values are rejected when assigned.
/// </summary>
public class PipelineSettings
{
    /// <summary>The minimum supported light limit.</summary>
    public const int MinLightLimit = 1;

    /// <summary>The maximum supported light limit.</summary>
    public const int MaxLightLimit = 10;

    /// <summary>The maximum number of CSM cascades supported by built-in shaders.</summary>
    public const int MaxCsmCascades = 4;

    // Construction-time settings. Changing these requires rebuilding the pipeline.

    /// <summary>Gets or sets the depth format.</summary>
    public TextureFormat DepthFormat
    {
        get => _depthFormat;
        set
        {
            if (!IsDepthFormat(value))
                throw new ArgumentOutOfRangeException(nameof(DepthFormat), value, "A depth or depth-stencil format is required.");
            _depthFormat = value;
        }
    }

    private TextureFormat _depthFormat = TextureFormat.DepthComponent32f;

    /// <summary>Gets or sets the directional light limit.</summary>
    public int DirectionalLightLimit
    {
        get => _directionalLightLimit;
        set => _directionalLightLimit = ValidateLightLimit(value, nameof(DirectionalLightLimit));
    }

    private int _directionalLightLimit = 4;

    /// <summary>Gets or sets the point light limit.</summary>
    public int PointLightLimit
    {
        get => _pointLightLimit;
        set => _pointLightLimit = ValidateLightLimit(value, nameof(PointLightLimit));
    }

    private int _pointLightLimit = 4;

    /// <summary>Gets or sets the spot light limit.</summary>
    public int SpotLightLimit
    {
        get => _spotLightLimit;
        set => _spotLightLimit = ValidateLightLimit(value, nameof(SpotLightLimit));
    }

    private int _spotLightLimit = 4;

    // Runtime settings.

    /// <summary>Gets or sets the tone mapping exposure.</summary>
    public float ToneMappingExposure
    {
        get => _toneMappingExposure;
        set => _toneMappingExposure = ValidateNonNegativeFinite(value, nameof(ToneMappingExposure));
    }

    private float _toneMappingExposure = 0.7f;

    /// <summary>Gets or sets the brightness clamp.</summary>
    public float BrightnessClamp
    {
        get => _brightnessClamp;
        set => _brightnessClamp = ValidateNonNegativeFinite(value, nameof(BrightnessClamp));
    }

    private float _brightnessClamp = 4f;

    /// <summary>Gets or sets the ambient intensity.</summary>
    public float AmbientIntensity
    {
        get => _ambientIntensity;
        set => _ambientIntensity = ValidateNonNegativeFinite(value, nameof(AmbientIntensity));
    }

    private float _ambientIntensity = 0.1f;

    /// <summary>Gets or sets the PBR IBL ambient intensity.</summary>
    public float IblAmbientIntensity
    {
        get => _iblAmbientIntensity;
        set => _iblAmbientIntensity = ValidateNonNegativeFinite(value, nameof(IblAmbientIntensity));
    }

    private float _iblAmbientIntensity = 1f;

    /// <summary>Gets or sets whether FXAA is enabled.</summary>
    public bool EnableFxaa { get; set; } = true;

    /// <summary>Gets or sets whether frustum culling is enabled.</summary>
    public bool EnableFrustumCulling { get; set; } = true;

    /// <summary>Gets or sets debug rendering settings.</summary>
    public DebugSettings Debug
    {
        get => _debug;
        set => _debug = value ?? throw new ArgumentNullException(nameof(Debug));
    }

    private DebugSettings _debug = new();

    /// <summary>Gets or sets the CSM cascade count.</summary>
    public int CsmCascadeCount
    {
        get => _csmCascadeCount;
        set
        {
            if (value < 1 || value > MaxCsmCascades)
                throw new ArgumentOutOfRangeException(nameof(CsmCascadeCount), $"Cascade count must be between 1 and {MaxCsmCascades}.");
            _csmCascadeCount = value;
        }
    }

    private int _csmCascadeCount = 3;

    /// <summary>Gets or sets the CSM logarithmic/uniform split blend in the range [0, 1].</summary>
    public float CsmSplitLambda
    {
        get => _csmSplitLambda;
        set
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(nameof(CsmSplitLambda), "Split lambda must be finite and between 0 and 1.");
            _csmSplitLambda = value;
        }
    }

    private float _csmSplitLambda = 0.5f;

    /// <summary>Gets or sets the CSM shadow map resolution.</summary>
    public int CsmShadowMapResolution
    {
        get => _csmShadowMapResolution;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(CsmShadowMapResolution), "Shadow map resolution must be greater than zero.");
            _csmShadowMapResolution = value;
        }
    }

    private int _csmShadowMapResolution = 1024;

    private static bool IsDepthFormat(TextureFormat value) => value is
        TextureFormat.DepthComponent16 or
        TextureFormat.DepthComponent24 or
        TextureFormat.DepthComponent32f or
        TextureFormat.Depth24Stencil8 or
        TextureFormat.Depth32fStencil8;

    private static int ValidateLightLimit(int value, string paramName)
    {
        if (value < MinLightLimit || value > MaxLightLimit)
            throw new ArgumentOutOfRangeException(paramName, $"Light limit must be between {MinLightLimit} and {MaxLightLimit}.");
        return value;
    }

    private static float ValidateNonNegativeFinite(float value, string paramName)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(paramName, "Value must be finite and non-negative.");
        return value;
    }
}
