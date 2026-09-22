using System.Reflection;
using Aura3D.Core;
using Aura3D.Core.Renderers;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Scenes;
using Xunit;

namespace Aura3D.Tests.Renderers;

public class DesktopShaderConversionTests
{
    private const string EsVersion = "#version 300 es";
    private const string DesktopVersion = "#version 410 core";

    [Theory]
    [InlineData("precision mediump float;")]
    [InlineData("precision highp float;")]
    [InlineData("precision lowp int;")]
    [InlineData("precision mediump sampler2DArray;")]
    [InlineData("    precision mediump float;")]
    [InlineData("precision mediump float; // default precision")]
    public void ToDesktopGlsl_ShouldStripPrecisionDeclarations(string declaration)
    {
        var source = $"{EsVersion}\n{declaration}\n\nuniform sampler2D u_texture;\nvoid main() {{ }}\n";

        var converted = ShaderDialectConverter.ToDesktopGlsl(source);

        Assert.DoesNotContain("precision", converted);
        Assert.StartsWith(DesktopVersion, converted);
        Assert.Contains("uniform sampler2D u_texture;", converted);
        Assert.Contains("void main() { }", converted);
    }

    [Theory]
    [InlineData("\uFEFF" + EsVersion + "\nvoid main() { }\n")]
    [InlineData("\n" + EsVersion + "\nvoid main() { }\n")]
    [InlineData("   " + EsVersion + "\nvoid main() { }\n")]
    public void ToDesktopGlsl_ShouldKeepVersionDirectiveFirst(string source)
    {
        var converted = ShaderDialectConverter.ToDesktopGlsl(source);

        Assert.StartsWith(DesktopVersion, converted.TrimStart());
        Assert.False(converted.Contains('\uFEFF'));
    }

    [Fact]
    public void ToDesktopGlsl_ShouldAddVersionDirectiveWhenAbsent()
    {
        var converted = ShaderDialectConverter.ToDesktopGlsl("void main() { }\n");

        Assert.StartsWith(DesktopVersion, converted);
        Assert.Contains("void main() { }", converted);
    }

    [Fact]
    public void ToDesktopGlsl_ShouldLeaveForeignVersionDirectiveUntouched()
    {
        var converted = ShaderDialectConverter.ToDesktopGlsl("#version 450\nvoid main() { }\n");

        Assert.StartsWith("#version 450", converted);
    }

    [Fact]
    public void ToDesktopGlsl_ShouldBeIdempotent()
    {
        var source = $"{EsVersion}\nprecision mediump float;\nvoid main() {{ }}\n";

        var once = ShaderDialectConverter.ToDesktopGlsl(source);
        var twice = ShaderDialectConverter.ToDesktopGlsl(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ToDesktopGlsl_ShouldPreserveInjectedDefines()
    {
        var source = $"{EsVersion}\nprecision mediump float;\n#define ENABLE_CSM\nvoid main() {{ }}\n";

        var converted = ShaderDialectConverter.ToDesktopGlsl(source);

        Assert.Contains("#define ENABLE_CSM", converted);
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
            AssertDesktopCompatible(name, source);
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
            AssertDesktopCompatible(name, source);
    }

    /// <summary>
    /// Covers the shader files shipped by every project, including the pipeline assemblies the
    /// test project does not reference, so no authored source escapes the desktop dialect check.
    /// </summary>
    [Fact]
    public void RepositoryShaderFiles_ShouldBeDesktopCompatibleAfterConversion()
    {
        var files = RepositoryShaderFiles();

        Assert.NotEmpty(files);
        Assert.True(files.Count > 20, $"Expected the full bundled shader set, found {files.Count}.");

        foreach (var file in files)
            AssertDesktopCompatible(Path.GetFileName(file), File.ReadAllText(file));
    }

    private static void AssertDesktopCompatible(string name, string source)
    {
        Assert.StartsWith(EsVersion, source.TrimStart('\uFEFF', ' ', '\t', '\r', '\n'));

        var converted = ShaderDialectConverter.ToDesktopGlsl(source);

        Assert.StartsWith(DesktopVersion, converted.TrimStart());
        Assert.DoesNotContain("precision", converted);
        Assert.Equal(1, CountOccurrences(converted, "#version"));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;

        return count;
    }

    private static List<string> RepositoryShaderFiles()
    {
        var root = FindRepositoryRoot();
        if (root is null)
            return [];

        return Directory
            .GetFiles(Path.Combine(root, "src"), "*.vert", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "src"), "*.frag", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aura3D.sln")))
            directory = directory.Parent;

        return directory?.FullName;
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
