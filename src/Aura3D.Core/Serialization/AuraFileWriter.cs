namespace Aura3D.Core.Serialization;

/// <summary>
/// 编排 .aura 资源文件的写入。
/// 写入顺序：FileHeader -> StringTable -> ResourceChunks
/// </summary>
public class AuraFileWriter
{
    private readonly ResourceCollector _collector;

    public AuraFileWriter(ResourceCollector collector)
    {
        _collector = collector;
    }

    public void Write(Stream stream)
    {
        if (_collector.RootResource == null)
            throw new InvalidOperationException("No root resource has been collected.");

        var resourceMap = new Dictionary<object, uint>(_collector.ResourceMap);

        using var payloadStream = new MemoryStream();
        using var payloadWriter = new AuraBinaryWriter(payloadStream, resourceMap, new Dictionary<object, int>());

        foreach (var resource in _collector.Resources)
        {
            WriteResourceChunk(payloadWriter, resource, resourceMap[resource]);
        }

        using var stringTableStream = new MemoryStream();
        payloadWriter.FlushStringTable(stringTableStream);

        using var writer = new AuraBinaryWriter(stream, resourceMap, new Dictionary<object, int>());
        writer.Write(AuraFileHeader.Magic[0]);
        writer.Write(AuraFileHeader.Magic[1]);
        writer.Write(AuraFileHeader.Magic[2]);
        writer.Write(AuraFileHeader.Magic[3]);
        writer.Write(AuraFileHeader.CurrentFileVersion);
        writer.Write((uint)stringTableStream.Length);
        writer.Write(_collector.RootChunkType);
        writer.Write(_collector.RootResourceId);

        stringTableStream.Position = 0;
        stringTableStream.CopyTo(stream);

        payloadStream.Position = 0;
        payloadStream.CopyTo(stream);
    }

    private static void WriteResourceChunk(AuraBinaryWriter writer, object resource, uint resourceId)
    {
        if (resource is not IAuraSerializable serializable)
            throw new InvalidOperationException($"Resource {resource.GetType().Name} does not implement IAuraSerializable.");

        writer.Write(AuraResourceTypeRegistry.GetChunkType(resource));
        writer.Write(AuraResourceTypeRegistry.GetChunkVersion(resource));

        var stream = writer.BaseStream;
        var sizePos = stream.Position;
        writer.Write(0u);
        writer.Write(resourceId);

        var dataStart = stream.Position;
        serializable.Serialize(writer);

        var dataEnd = stream.Position;
        var dataSize = (uint)(dataEnd - dataStart);
        stream.Position = sizePos;
        writer.Write(dataSize);
        stream.Position = dataEnd;
    }
}
