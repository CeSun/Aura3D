using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using Xunit;

namespace Aura3D.Tests.Serialization;

public class SerializationMechanismTests
{
    [Fact]
    public void ResourceCollector_ShouldCollectNestedResourcesTransitively()
    {
        var nestedMaterial = new Material();
        var holder = new NestedTextureHolder
        {
            NestedMaterial = nestedMaterial
        };

        var collector = new ResourceCollector();
        collector.Collect(holder);

        Assert.Contains(holder, collector.ResourceMap.Keys);
        Assert.Contains(nestedMaterial, collector.ResourceMap.Keys);
    }

    [Fact]
    public void NodeCollector_ShouldCollectNestedResourcesTransitively()
    {
        var nestedMaterial = new Material();
        var holder = new NestedTextureHolder
        {
            NestedMaterial = nestedMaterial
        };

        var material = new Material();
        material.SetTexture("BaseColor", holder);

        var mesh = new Mesh
        {
            Material = material
        };

        var collector = new NodeCollector();
        collector.Collect(mesh);

        Assert.Contains(material, collector.ResourceMap.Keys);
        Assert.Contains(holder, collector.ResourceMap.Keys);
        Assert.Contains(nestedMaterial, collector.ResourceMap.Keys);
    }

    [Fact]
    public void WriteResourceRef_ShouldThrowWhenReferenceWasNotCollected()
    {
        using var stream = new MemoryStream();
        using var writer = new AuraBinaryWriter(stream);

        var ex = Record.Exception(() => writer.WriteResourceRef(new Texture()));

        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public void ReadResourceRef_ShouldThrowWhenReferenceCannotBeResolved()
    {
        using var stream = new MemoryStream();
        using (var binaryWriter = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            binaryWriter.Write(123u);
        }

        stream.Position = 0;

        using var reader = new AuraBinaryReader(stream);

        var ex = Record.Exception(() => reader.ReadResourceRef<Texture>());

        Assert.IsType<InvalidDataException>(ex);
    }

    [Fact]
    public void DeserializeNestedCustomObject_ShouldNotShiftFollowingFieldsWhenNestedVersionIsNewer()
    {
        using var stream = new MemoryStream();
        using (var writer = new AuraBinaryWriter(stream))
        {
            writer.FileVersion = AuraFileHeader.CurrentFileVersion;
            writer.WriteCustom(new NestedChunkV2
            {
                Value = 7,
                Extra = 42
            });
            writer.Write(99);
        }

        stream.Position = 0;

        using var reader = new AuraBinaryReader(stream)
        {
            FileVersion = AuraFileHeader.CurrentFileVersion
        };

        var wrapper = new WrapperChunkV1();
        wrapper.Deserialize(reader, chunkVersion: 1);

        Assert.NotNull(wrapper.Nested);
        Assert.Equal(7, wrapper.Nested!.Value);
        Assert.Equal(99, wrapper.Tail);
    }

    private sealed class NestedTextureHolder : Texture
    {
        [AuraField(since: 1)]
        [AuraReference]
        public Material? NestedMaterial { get; set; }
    }

    [AuraChunk(chunkType: 9001, chunkVersion: 1)]
    private sealed class NestedChunkV1 : IAuraSerializable
    {
        public int Value { get; set; }

        public void Serialize(AuraBinaryWriter writer)
        {
            writer.Write(Value);
        }

        public void Deserialize(AuraBinaryReader reader, uint chunkVersion)
        {
            Value = reader.ReadInt32();
        }
    }

    [AuraChunk(chunkType: 9001, chunkVersion: 2)]
    private sealed class NestedChunkV2 : IAuraSerializable
    {
        public int Value { get; set; }
        public int Extra { get; set; }

        public void Serialize(AuraBinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write(Extra);
        }

        public void Deserialize(AuraBinaryReader reader, uint chunkVersion)
        {
            Value = reader.ReadInt32();
            if (chunkVersion >= 2)
            {
                Extra = reader.ReadInt32();
            }
        }
    }

    private sealed class WrapperChunkV1 : IAuraSerializable
    {
        public NestedChunkV1? Nested { get; private set; }
        public int Tail { get; private set; }

        public void Serialize(AuraBinaryWriter writer)
        {
            writer.WriteCustom(Nested);
            writer.Write(Tail);
        }

        public void Deserialize(AuraBinaryReader reader, uint chunkVersion)
        {
            Nested = reader.ReadCustom<NestedChunkV1>();
            Tail = reader.ReadInt32();
        }
    }
}
