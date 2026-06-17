using Aura3D.Core.Serialization.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using Xunit;

namespace Aura3D.Core.Serialization.SourceGenerator.Tests;

public class SerializationGeneratorTests
{
    [Fact]
    public void Generator_ShouldKeepOlderChunkVersionsBackwardCompatible()
    {
        const string source = """
using Aura3D.Core.Serialization;

namespace Demo
{
    [AuraChunk(42, 2)]
    public partial class DemoResource
    {
        [AuraField(1)]
        public string Name { get; set; } = string.Empty;

        [AuraField(2)]
        public int Extra { get; set; }
    }
}

namespace Aura3D.Core.Serialization
{
    public interface IAuraSerializable
    {
        void Serialize(AuraBinaryWriter writer);
        void Deserialize(AuraBinaryReader reader, uint chunkVersion);
    }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class AuraChunkAttribute : System.Attribute
    {
        public AuraChunkAttribute(uint chunkType, uint chunkVersion)
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AuraFieldAttribute : System.Attribute
    {
        public AuraFieldAttribute(uint since)
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AuraReferenceAttribute : System.Attribute
    {
    }

    public sealed class AuraBinaryWriter
    {
        public System.Collections.Generic.List<object?> Values { get; } = new();

        public void WriteString(string? value) => Values.Add(value);
        public void Write(int value) => Values.Add(value);
    }

    public sealed class AuraBinaryReader
    {
        private readonly System.Collections.Generic.Queue<object?> _values;

        public AuraBinaryReader(params object?[] values)
        {
            _values = new System.Collections.Generic.Queue<object?>(values);
        }

        public string ReadString() => (string)(_values.Dequeue() ?? string.Empty);
        public int ReadInt32() => (int)(_values.Dequeue() ?? 0);
    }
}
""";

        var inputCompilation = CreateCompilation(source);
        var generator = new SerializationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var runResult = driver.GetRunResult();
        var generatedSource = Assert.Single(runResult.Results).GeneratedSources.Single().SourceText.ToString();

        Assert.Contains("if (chunkVersion >= 2)", generatedSource);
        Assert.Contains("Extra = 0;", generatedSource);

        var assembly = EmitAssembly(outputCompilation);
        var resourceType = assembly.GetType("Demo.DemoResource");
        var readerType = assembly.GetType("Aura3D.Core.Serialization.AuraBinaryReader");
        var writerType = assembly.GetType("Aura3D.Core.Serialization.AuraBinaryWriter");

        Assert.NotNull(resourceType);
        Assert.NotNull(readerType);
        Assert.NotNull(writerType);

        var deserialize = resourceType!.GetMethod("Deserialize");
        var serialize = resourceType.GetMethod("Serialize");
        var nameProperty = resourceType.GetProperty("Name");
        var extraProperty = resourceType.GetProperty("Extra");
        var valuesProperty = writerType!.GetProperty("Values");

        Assert.NotNull(deserialize);
        Assert.NotNull(serialize);
        Assert.NotNull(nameProperty);
        Assert.NotNull(extraProperty);
        Assert.NotNull(valuesProperty);

        var legacyInstance = Activator.CreateInstance(resourceType);
        var legacyReader = Activator.CreateInstance(readerType!, new object?[] { new object?[] { "legacy-name" } });

        deserialize!.Invoke(legacyInstance, new object?[] { legacyReader, 1u });

        Assert.Equal("legacy-name", nameProperty!.GetValue(legacyInstance));
        Assert.Equal(0, extraProperty!.GetValue(legacyInstance));

        var currentInstance = Activator.CreateInstance(resourceType);
        var currentReader = Activator.CreateInstance(readerType!, new object?[] { new object?[] { "current-name", 99 } });

        deserialize.Invoke(currentInstance, new object?[] { currentReader, 2u });

        Assert.Equal("current-name", nameProperty.GetValue(currentInstance));
        Assert.Equal(99, extraProperty.GetValue(currentInstance));

        nameProperty.SetValue(currentInstance, "serialized-name");
        extraProperty.SetValue(currentInstance, 7);
        var writer = Activator.CreateInstance(writerType);

        serialize!.Invoke(currentInstance, new object?[] { writer });

        var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(valuesProperty.GetValue(writer));
        Assert.Equal(new object?[] { "serialized-name", 7 }, values.Cast<object?>().ToArray());
    }

    [Fact]
    public void Generator_ShouldUseActualDictionaryKeyType()
    {
        const string source = """
using Aura3D.Core.Serialization;
using System.Collections.Generic;

namespace Demo
{
    [AuraChunk(42, 2)]
    public partial class DemoResource
    {
        [AuraField(2)]
        public Dictionary<int, string> Entries { get; set; } = new();
    }
}

namespace Aura3D.Core.Serialization
{
    public interface IAuraSerializable
    {
        void Serialize(AuraBinaryWriter writer);
        void Deserialize(AuraBinaryReader reader, uint chunkVersion);
    }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class AuraChunkAttribute : System.Attribute
    {
        public AuraChunkAttribute(uint chunkType, uint chunkVersion)
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AuraFieldAttribute : System.Attribute
    {
        public AuraFieldAttribute(uint since)
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AuraReferenceAttribute : System.Attribute
    {
    }

    public sealed class AuraBinaryWriter
    {
        public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> value) where TKey : notnull
        {
        }
    }

    public sealed class AuraBinaryReader
    {
        public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>() where TKey : notnull => new();
    }
}
""";

        var inputCompilation = CreateCompilation(source);
        var generator = new SerializationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var runResult = driver.GetRunResult();
        var generatedSource = Assert.Single(runResult.Results).GeneratedSources.Single().SourceText.ToString();

        Assert.Contains("reader.ReadDictionary<int, string>()", generatedSource);
        Assert.Contains("new Dictionary<int, string>()", generatedSource);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            assemblyName: "Aura3D.GeneratorTests.Dynamic",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Assembly EmitAssembly(Compilation compilation)
    {
        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        Assert.False(string.IsNullOrWhiteSpace(trustedPlatformAssemblies));

        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}
