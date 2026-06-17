using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Aura3D.Core.Math;

namespace Aura3D.Core.Serialization;

/// <summary>
/// 二进制读取器。封装底层 Stream 读取，提供 StringTable 重建、blittable 批量读取等功能。
/// </summary>
public class AuraBinaryReader : IDisposable
{
    private enum DynamicValueType : byte
    {
        Null = 0,
        Boolean = 1,
        Int32 = 2,
        UInt32 = 3,
        Single = 4,
        String = 5,
        Vector2 = 6,
        Vector3 = 7,
        Vector4 = 8,
        Color = 9,
        Double = 10,
        Int64 = 11,
        UInt64 = 12
    }

    private readonly BinaryReader _reader;
    private List<string>? _stringTable;
    private readonly Dictionary<uint, object>? _resourceMap;
    private readonly List<object>? _nodeList;

    public Stream BaseStream => _reader.BaseStream;
    public uint FileVersion { get; set; } = AuraFileHeader.CurrentFileVersion;

    public AuraBinaryReader(Stream stream, Dictionary<uint, object> resourceMap, List<object> nodeList)
    {
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _resourceMap = resourceMap;
        _nodeList = nodeList;
    }

    public AuraBinaryReader(Stream stream)
        : this(stream, new Dictionary<uint, object>(), new List<object>())
    {
    }

    // ---- Primitives ----

    public bool ReadBoolean() => _reader.ReadBoolean();
    public byte ReadByte() => _reader.ReadByte();
    public sbyte ReadSByte() => _reader.ReadSByte();
    public short ReadInt16() => _reader.ReadInt16();
    public ushort ReadUInt16() => _reader.ReadUInt16();
    public int ReadInt32() => _reader.ReadInt32();
    public uint ReadUInt32() => _reader.ReadUInt32();
    public long ReadInt64() => _reader.ReadInt64();
    public ulong ReadUInt64() => _reader.ReadUInt64();
    public float ReadSingle() => _reader.ReadSingle();
    public double ReadDouble() => _reader.ReadDouble();

    // ---- String via StringTable ----

    public string ReadString()
    {
        var index = _reader.ReadUInt32();
        if (index == uint.MaxValue)
            return string.Empty;

        if (_stringTable == null)
            throw new InvalidOperationException("StringTable has not been loaded. Call LoadStringTable() first.");

        if (index >= (uint)_stringTable.Count)
            throw new InvalidOperationException($"String index {index} out of range (table size {_stringTable.Count}).");

        return _stringTable[(int)index];
    }

    // ---- String Table ----

    public void LoadStringTable()
    {
        var count = _reader.ReadInt32();
        _stringTable = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            var length = FileVersion >= 3
                ? _reader.ReadInt32()
                : _reader.ReadUInt16();
            var bytes = _reader.ReadBytes(length);
            _stringTable.Add(Encoding.UTF8.GetString(bytes));
        }
    }

    // ---- Blittable types ----

    public T ReadBlittable<T>() where T : unmanaged
    {
        var size = Marshal.SizeOf<T>();
        var bytes = _reader.ReadBytes(size);
        return MemoryMarshal.Read<T>(bytes);
    }

    public List<T> ReadBlittableList<T>() where T : unmanaged
    {
        var count = _reader.ReadInt32();
        if (count == 0)
            return new List<T>();

        var elementSize = Marshal.SizeOf<T>();
        var totalBytes = count * elementSize;
        var bytes = _reader.ReadBytes(totalBytes);

        var result = new List<T>(count);
        var byteSpan = bytes.AsSpan();
        for (int i = 0; i < count; i++)
        {
            result.Add(MemoryMarshal.Read<T>(byteSpan.Slice(i * elementSize, elementSize)));
        }

        return result;
    }

    public List<byte> ReadBytes()
    {
        var count = _reader.ReadInt32();
        if (count == 0)
            return new List<byte>();

        var bytes = _reader.ReadBytes(count);
        return new List<byte>(bytes);
    }

    public List<byte>[] ReadByteArrayList()
    {
        var count = _reader.ReadInt32();
        if (count == 0)
            return new List<byte>[6];

        var result = new List<byte>[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ReadBytes();
        }

        return result;
    }

    public T[] ReadArray<T>()
    {
        var count = _reader.ReadInt32();
        if (count == 0)
            return Array.Empty<T>();

        var result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ReadValue<T>();
        }

        return result;
    }

    public List<T> ReadList<T>()
    {
        var count = _reader.ReadInt32();
        if (count == 0)
            return new List<T>();

        var result = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            result.Add(ReadValue<T>());
        }

        return result;
    }

    public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>()
        where TKey : notnull
    {
        var count = _reader.ReadInt32();
        var result = new Dictionary<TKey, TValue>(count);
        for (int i = 0; i < count; i++)
        {
            var key = ReadValue<TKey>();
            var value = ReadValue<TValue>();
            result[key] = value;
        }

        return result;
    }

    public T? ReadNullable<T>() where T : struct
    {
        var hasValue = ReadBoolean();
        return hasValue ? ReadValue<T>() : null;
    }

    public T ReadCustom<T>()
    {
        return ReadValue<T>();
    }

    public BoundingBox? ReadBoundingBox()
    {
        var hasBoundingBox = ReadBoolean();
        if (!hasBoundingBox)
            return null;

        var min = ReadBlittable<Vector3>();
        var max = ReadBlittable<Vector3>();
        return new BoundingBox(min, max);
    }

    // ---- Reference ----

    public T? ReadResourceRef<T>() where T : class
    {
        var id = _reader.ReadUInt32();
        if (id == uint.MaxValue)
            return null;

        if ((id & 0x80000000) != 0)
        {
            var nodeIndex = (int)(id & 0x7FFFFFFF);
            if (_nodeList == null || nodeIndex >= _nodeList.Count)
                throw new InvalidDataException($"Node reference id {nodeIndex} could not be resolved during deserialization.");

            return _nodeList[nodeIndex] as T
                ?? throw new InvalidDataException(
                    $"Node reference id {nodeIndex} could not be cast to '{typeof(T).FullName}'.");
        }

        if (_resourceMap == null || !_resourceMap.TryGetValue(id, out var obj))
            throw new InvalidDataException($"Resource reference id {id} could not be resolved during deserialization.");

        return obj as T
            ?? throw new InvalidDataException(
                $"Resource reference id {id} could not be cast to '{typeof(T).FullName}'.");
    }

    private T ReadValue<T>()
    {
        var value = ReadValue(typeof(T));
        return value is null ? default! : (T)value;
    }

    private object? ReadValue(Type type)
    {
        if (type == typeof(object))
            return ReadDynamicObject();

        if (type == typeof(string))
            return ReadString();

        if (IsNullableType(type))
        {
            var hasValue = ReadBoolean();
            if (!hasValue)
                return null;

            var underlyingType = Nullable.GetUnderlyingType(type)!;
            return ReadValue(underlyingType);
        }

        if (!type.IsValueType)
        {
            var hasValue = ReadBoolean();
            if (!hasValue)
                return null;
        }

        if (type == typeof(bool))
            return ReadBoolean();
        if (type == typeof(byte))
            return ReadByte();
        if (type == typeof(sbyte))
            return ReadSByte();
        if (type == typeof(short))
            return ReadInt16();
        if (type == typeof(ushort))
            return ReadUInt16();
        if (type == typeof(int))
            return ReadInt32();
        if (type == typeof(uint))
            return ReadUInt32();
        if (type == typeof(long))
            return ReadInt64();
        if (type == typeof(ulong))
            return ReadUInt64();
        if (type == typeof(float))
            return ReadSingle();
        if (type == typeof(double))
            return ReadDouble();
        if (type == typeof(Vector2))
            return ReadBlittable<Vector2>();
        if (type == typeof(Vector3))
            return ReadBlittable<Vector3>();
        if (type == typeof(Vector4))
            return ReadBlittable<Vector4>();
        if (type == typeof(Quaternion))
            return ReadBlittable<Quaternion>();
        if (type == typeof(Matrix4x4))
            return ReadBlittable<Matrix4x4>();
        if (type == typeof(System.Drawing.Color))
            return System.Drawing.Color.FromArgb((int)ReadUInt32());
        if (type.IsEnum)
            return Enum.ToObject(type, ReadInt32());

        if (type.IsArray && type.GetArrayRank() == 1)
        {
            var count = ReadInt32();
            var elementType = type.GetElementType()!;
            var array = Array.CreateInstance(elementType, count);
            for (int i = 0; i < count; i++)
            {
                array.SetValue(ReadValue(elementType), i);
            }

            return array;
        }

        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            if (genericDefinition == typeof(List<>))
            {
                return ReadDynamicList(type.GetGenericArguments()[0]);
            }

            if (genericDefinition == typeof(Dictionary<,>))
            {
                var genericArguments = type.GetGenericArguments();
                return ReadDynamicDictionary(genericArguments[0], genericArguments[1]);
            }
        }

        if (typeof(IAuraSerializable).IsAssignableFrom(type))
        {
            var instance = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Unable to create an instance of '{type.FullName}' during deserialization.");

            if (FileVersion >= 4)
            {
                var chunkVersion = ReadUInt32();
                var payloadSize = ReadUInt32();
                var payloadEnd = BaseStream.Position + payloadSize;
                ((IAuraSerializable)instance).Deserialize(this, chunkVersion);
                BaseStream.Position = payloadEnd;
                return instance;
            }

            var legacyChunkVersion = FileVersion >= 3
                ? ReadUInt32()
                : GetChunkVersion(type);
            ((IAuraSerializable)instance).Deserialize(this, legacyChunkVersion);
            return instance;
        }

        throw new NotSupportedException($"Deserialization does not support values of type '{type.FullName}'.");
    }

    private object ReadDynamicList(Type elementType)
    {
        var count = ReadInt32();
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        for (int i = 0; i < count; i++)
        {
            list.Add(ReadValue(elementType));
        }

        return list;
    }

    private object ReadDynamicDictionary(Type keyType, Type valueType)
    {
        var count = ReadInt32();
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;
        for (int i = 0; i < count; i++)
        {
            dictionary.Add(ReadValue(keyType), ReadValue(valueType));
        }

        return dictionary;
    }

    private object? ReadDynamicObject()
    {
        var valueType = (DynamicValueType)ReadByte();
        return valueType switch
        {
            DynamicValueType.Null => null,
            DynamicValueType.Boolean => ReadBoolean(),
            DynamicValueType.Int32 => ReadInt32(),
            DynamicValueType.UInt32 => ReadUInt32(),
            DynamicValueType.Single => ReadSingle(),
            DynamicValueType.String => ReadString(),
            DynamicValueType.Vector2 => ReadBlittable<Vector2>(),
            DynamicValueType.Vector3 => ReadBlittable<Vector3>(),
            DynamicValueType.Vector4 => ReadBlittable<Vector4>(),
            DynamicValueType.Color => System.Drawing.Color.FromArgb((int)ReadUInt32()),
            DynamicValueType.Double => ReadDouble(),
            DynamicValueType.Int64 => ReadInt64(),
            DynamicValueType.UInt64 => ReadUInt64(),
            _ => throw new InvalidDataException($"Unknown dynamic value tag '{(byte)valueType}'.")
        };
    }

    private static uint GetChunkVersion(Type type)
    {
        var attribute = type.GetCustomAttributes(typeof(AuraChunkAttribute), inherit: false)
            .OfType<AuraChunkAttribute>()
            .FirstOrDefault();

        return attribute?.ChunkVersion ?? 1u;
    }

    private static bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
