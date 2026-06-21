using Aura3D.Core.Resources;
using System.Drawing;
using Xunit;

namespace Aura3D.Tests.Resources;

public class MaterialTests
{
    [Fact]
    public void SetParameterValue_ShouldSupportRoundTrip()
    {
        var material = new Material();

        material.SetParameterValue("roughness", 0.42f);

        Assert.True(material.TryGetParameterValue("roughness", out float roughness));
        Assert.Equal(0.42f, roughness, 5);
        Assert.False(material.TryGetParameterValue("roughness", out int _));
        Assert.Contains(material.EnumerateParameters(), kv => kv.Key == "roughness" && Equals(kv.Value, 0.42f));
    }

    [Fact]
    public void Clone_ShouldShareTextureReference()
    {
        var texture = Texture.CreateFromColor(Color.Green);
        var material = new Material();
        material.SetTexture("BaseColor", texture);

        var clone = material.Clone();

        Assert.NotSame(material, clone);
        Assert.Same(texture, clone.GetTexture("BaseColor"));
    }

    [Fact]
    public void DeepClone_ShouldRespectTextureCopyMode()
    {
        var texture = Texture.CreateFromColor(Color.Blue);
        var material = new Material();
        material.SetTexture("BaseColor", texture);

        var shallowTextureClone = Assert.IsType<Texture>(material.DeepClone().GetTexture("BaseColor"));
        var deepTextureClone = Assert.IsType<Texture>(material.DeepClone(deepCopyTextures: true).GetTexture("BaseColor"));

        Assert.NotSame(texture, shallowTextureClone);
        Assert.Equal(texture.AsLdrData().ToArray(), shallowTextureClone.AsLdrData().ToArray());

        Assert.NotSame(texture, deepTextureClone);
        Assert.Equal(texture.AsLdrData().ToArray(), deepTextureClone.AsLdrData().ToArray());

        var modified = texture.AsLdrData().ToArray();
        modified[0] = 123;
        texture.SetLdrData(modified, texture.Width, texture.Height);
        Assert.Equal(texture.AsLdrData().ToArray(), shallowTextureClone.AsLdrData().ToArray());
        Assert.NotEqual(texture.AsLdrData().ToArray(), deepTextureClone.AsLdrData().ToArray());
    }

    [Fact]
    public void RemoveShader_ShouldUpdateHasShaderState()
    {
        var material = new Material();

        material.SetShaderSource("forward", ShaderType.Vertex, "vertex");
        material.SetShaderSource("forward", ShaderType.Fragment, "fragment");

        var (vertexShader, fragmentShader) = material.GetShaderSource("forward");

        Assert.True(material.HasShader);
        Assert.Equal("vertex", vertexShader);
        Assert.Equal("fragment", fragmentShader);

        material.RemoveShader("forward", ShaderType.Vertex);
        Assert.True(material.HasShader);

        material.RemoveShader("forward", ShaderType.Fragment);
        Assert.False(material.HasShader);
    }
}
