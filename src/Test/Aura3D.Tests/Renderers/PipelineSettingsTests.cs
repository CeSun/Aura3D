using Aura3D.Core.Renderers;
using Xunit;

namespace Aura3D.Tests.Renderers;

public class PipelineSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void LightLimits_ShouldMatchBuiltInShaderCapacity(int value)
    {
        var settings = new PipelineSettings();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.DirectionalLightLimit = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.PointLightLimit = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SpotLightLimit = value);
    }

    [Fact]
    public void DepthFormat_ShouldRejectColorFormat()
    {
        var settings = new PipelineSettings();
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.DepthFormat = TextureFormat.Rgba8);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RuntimeIntensities_ShouldRejectNegativeOrNonFiniteValues(float value)
    {
        var settings = new PipelineSettings();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToneMappingExposure = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.BrightnessClamp = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.AmbientIntensity = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.IblAmbientIntensity = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void CsmCascadeCount_ShouldRejectUnsupportedValues(int value)
    {
        var settings = new PipelineSettings();
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.CsmCascadeCount = value);
    }

    [Fact]
    public void Debug_ShouldRejectNull()
    {
        var settings = new PipelineSettings();
        Assert.Throws<ArgumentNullException>(() => settings.Debug = null!);
    }
}
