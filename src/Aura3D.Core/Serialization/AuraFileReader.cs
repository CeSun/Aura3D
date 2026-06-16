namespace Aura3D.Core.Serialization;

/// <summary>
/// 编排 .aura 资源文件的读取。
/// 解析顺序：FileHeader -> StringTable -> ResourceChunks
/// </summary>
public class AuraFileReader
{
    private readonly Dictionary<uint, object> _resourceMap = new();
    private uint _fileVersion;
    private AuraChunkType _rootChunkType;
    private uint _rootResourceId;

    public IReadOnlyDictionary<uint, object> ResourceMap => _resourceMap;
    public uint FileVersion => _fileVersion;
    public AuraChunkType RootChunkType => _rootChunkType;
    public uint RootResourceId => _rootResourceId;
    public object RootResource => _resourceMap[_rootResourceId];

    public AuraFileReader(Stream stream)
    {
        using var reader = new AuraBinaryReader(stream, _resourceMap, new List<object>());

        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();
        var magic2 = reader.ReadByte();
        var magic3 = reader.ReadByte();

        if (magic0 != AuraFileHeader.Magic[0] ||
            magic1 != AuraFileHeader.Magic[1] ||
            magic2 != AuraFileHeader.Magic[2] ||
            magic3 != AuraFileHeader.Magic[3])
        {
            throw new InvalidDataException("Invalid .aura file: magic mismatch.");
        }

        _fileVersion = reader.ReadUInt32();
        if (_fileVersion < AuraFileHeader.MinimumSupportedFileVersion ||
            _fileVersion > AuraFileHeader.CurrentFileVersion)
        {
            throw new NotSupportedException(
                $"Unsupported .aura file version {_fileVersion}. Supported range: {AuraFileHeader.MinimumSupportedFileVersion}-{AuraFileHeader.CurrentFileVersion}.");
        }

        reader.FileVersion = _fileVersion;

        var stringTableSize = reader.ReadUInt32();
        _rootChunkType = (AuraChunkType)reader.ReadUInt32();
        _rootResourceId = reader.ReadUInt32();

        var stringTableStart = stream.Position;
        reader.LoadStringTable();
        var consumedStringTableBytes = stream.Position - stringTableStart;
        if (consumedStringTableBytes != stringTableSize)
        {
            stream.Position = stringTableStart + stringTableSize;
        }

        while (stream.Position < stream.Length)
        {
            var chunkType = (AuraChunkType)reader.ReadUInt32();
            var chunkVersion = reader.ReadUInt32();
            var chunkDataSize = reader.ReadUInt32();
            var resourceId = reader.ReadUInt32();
            var chunkEnd = stream.Position + chunkDataSize;

            var resource = DeserializeResourceByType(reader, chunkType, chunkVersion);
            if (resource != null)
            {
                _resourceMap[resourceId] = resource;
            }

            stream.Position = chunkEnd;
        }

        if (!_resourceMap.ContainsKey(_rootResourceId))
            throw new InvalidDataException($"Root resource id {_rootResourceId} was not found in the file.");
    }

    private static object? DeserializeResourceByType(AuraBinaryReader reader, AuraChunkType chunkType, uint chunkVersion)
    {
        var resource = AuraResourceTypeRegistry.CreateResource(chunkType);
        if (resource is IAuraSerializable serializable)
        {
            serializable.Deserialize(reader, chunkVersion);
        }

        return resource;
    }
}
