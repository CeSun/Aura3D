using System.Reflection;
using Aura3D.Core;
using Aura3D.Core.Renderers;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Scenes;
using Xunit;

namespace Aura3D.Tests.Renderers;

public class DesktopShaderConversionTests
{
    [Theory]
    [InlineData("precision mediump float;")]
    [InlineData("precision highp float;")]
    [InlineData("precision lowp int;")]
    [InlineData("precision mediump sampler2DArray;")]
    [InlineData("    precision mediump float;")]
    [InlineData("precision mediump float; // default precision")]
    public void ConvertToDesktopGLSL_ShouldStripPrecisionDeclarations(string declaration)
    {
        var source = $"#version 300 es\n{declaration}\n\nuniform sampler2D u_texture;\nvoid main() {{ }}\n";

        var converted = RenderPass.ConvertToDesktopGLSL(source);

        Assert.DoesNotContain("precision", converted);
        Assert.StartsWith("#version 330 core", converted);
        Assert.Contains("uniform sampler2D u_texture;", converted);
        Assert.Contains("void main() { }", converted);
    }

    [Fact]
    public void ConvertToDesktopGLSL_ShouldBeIdempotent()
    {
        var source = "#version 300 es\nprecision mediump float;\nvoid main() { }\n";

        var once = RenderPass.ConvertToDesktopGLSL(source);
        var twice = RenderPass.ConvertToDesktopGLSL(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ConvertToDesktopGLSL_ShouldHandleSourcesWithoutVersionDirective()
    {
        var converted = RenderPass.ConvertToDesktopGLSL("precision lowp float;\nvoid main() { }\n");

        Assert.DoesNotContain("precision", converted);
        Assert.DoesNotContain("#version", converted);
    }

    [Fact]
    public void BundledShaderAssets_ShouldBeDesktopCompatibleAfterConversion()
    {
        var sources = typeof(ShaderResource)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (p.Name, Source: (string)p.GetValue(null)!))
            .ToList();

        Assert.NotEmpty(sources);
        Assert.Contains(sources, s => s.Source.Contains("precision"));

        foreach (var (name, source) in sources)
        {
            var converted = RenderPass.ConvertToDesktopGLSL(source);

            Assert.DoesNotContain("precision", converted);

            if (source.Contains("#version 300 es"))
                Assert.Contains("#version 330 core", converted);
        }
    }

    [Fact]
    public void BuiltInPassShaders_ShouldBeDesktopCompatibleAfterConversion()
    {
        (string Name, Func<TestPipeline, RenderPass> Factory)[] passes =
        [
            (nameof(ParticlePass), p => new ParticlePass(p)),
            (nameof(PointCloudPass), p => new PointCloudPass(p)),
            (nameof(CopyPass), p => new CopyPass(p, p.CreateInputTexture())),
            (nameof(ToneMappingPass), p => new ToneMappingPass(p, p.CreateInputTexture())),
            (nameof(GammaCorrectionPass), p => new GammaCorrectionPass(p, p.CreateInputTexture())),
            (nameof(FxaaPass), p => new FxaaPass(p, p.CreateInputTexture())),
        ];

        var inspected = new List<(string Name, string Source)>();

        foreach (var (name, factory) in passes)
        {
            var pass = CreatePass(factory);

            foreach (var source in GetShaderSources(pass))
                inspected.Add((name, source));
        }

        Assert.Contains(inspected, s => s.Source.Contains("precision"));

        foreach (var (name, source) in inspected)
        {
            var converted = RenderPass.ConvertToDesktopGLSL(source);

            Assert.DoesNotContain("precision", converted);

            if (source.Contains("#version 300 es"))
                Assert.Contains("#version 330 core", converted);
        }
    }

    private static RenderPass CreatePass(Func<TestPipeline, RenderPass> factory)
    {
        TestPipeline? pipeline = null;
        _ = new Scene(scene => pipeline = new TestPipeline(scene));
        return factory(pipeline!);
    }

    private static List<string> GetShaderSources(RenderPass pass)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        return new[] { "VertexShader", "FragmentShader" }
            .Select(name => (string)typeof(RenderPass).GetField(name, flags)!.GetValue(pass)!)
            .Where(source => !string.IsNullOrEmpty(source))
            .ToList();
    }

    private sealed class TestPipeline(Scene scene) : RenderPipeline(scene)
    {
        public RenderTargetTextureHandle CreateInputTexture()
        {
            var target = RegisterRenderTarget("TestInput");
            target.AddTexture("Color", TextureFormat.Rgba8);
            return target.GetTexture("Color");
        }
    }
}
