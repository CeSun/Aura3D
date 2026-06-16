using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Aura3D.Core.Serialization;

/// <summary>
/// 二进制写入器。封装底层 Stream 写入，提供 StringTable 去重、blittable 批量写入等功能。
/// </summary>
public class AuraBinaryWriter : IDisposable
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

    private readonly BinaryWriter _writer;
    private readonly Dictionary<string, uint> _stringTable = new();
    private readonly List<byte[]> _strings = new();
    private readonly Dictionary<object, uint> _resourceMap;
    private readonly Dictionary<object, int> _nodeIndexMap;
    private uint _nextStringId;

    public Stream BaseStream => _writer.BaseStream;

    public AuraBinaryWriter(Stream stream, Dictionary<object, uint> resourceMap, Dictionary<object, int> nodeIndexMap)
    {
        _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        _resourceMap = resourceMap;
        _nodeIndexMap = nodeIndexMap;
    }

    public AuraBinaryWriter(Stream stream)
        : this(stream, new Dictionary<object, uint>(), new Dictionary<object, int>())
    {
    }

    // ---- Primitives ----

    public void Write(bool value) => _writer.Write(value);
    public void Write(byte value) => _writer.Write(value);
    public void Write(sbyte value) => _writer.Write(value);
    public void Write(short value) => _writer.Write(value);
    public void Write(ushort value) => _writer.Write(value);
    public void Write(int value) => _writer.Write(value);
    public void Write(uint value) => _writer.Write(value);
    public void Write(long value) => _writer.Write(value);
    public void Write(ulong value) => _writer.Write(value);
    public void Write(float value) => _writer.Write(value);
    public void Write(double value) => _writer.Write(value);

    // ---- String via StringTable ----

    public void WriteString(string? value)
    {
        if (value == null)
        {
            _writer.Write(uint.MaxValue);
            return;
        }

        if (!_stringTable.TryGetValue(value, out var index))
        {
            index = _nextStringId++;
            _stringTable[value] = index;
            _strings.Add(Encoding.UTF8.GetBytes(value));
        }

        _writer.Write(index);
    }

    // ---- Blittable types ----

    public void WriteBlittable<T>(T value) where T : unmanaged
    {
        Span<T> span = stackalloc T[1];
        span[0] = value;
        var byteSpan = MemoryMarshal.AsBytes((ReadOnlySpan<T>)span);
        _writer.Write(byteSpan);
    }

    public void WriteBlittableList<T>(List<T>? list) where T : unmanaged
    {
        if (list == null || list.Count == 0)
        {
            Write(0);
            return;
        }

        Write(list.Count);
        var span = (ReadOnlySpan<T>)CollectionsMarshal.AsSpan(list);
        var byteSpan = MemoryMarshal.AsBytes(span);
        _writer.Write(byteSpan);
    }

    public void WriteBytes(List<byte>? data)
    {
        if (data == null || data.Count == 0)
        {
            Write(0);
            return;
        }

        Write(data.Count);
        _writer.Write(CollectionsMarshal.AsSpan(data));
    }

    public void WriteByteArrayList(List<byte>[]? array)
    {
        if (array == null || array.Length == 0)
        {
            Write(0);
            return;
        }

        Write(array.Length);
        foreach (var list in array)
        {
            WriteBytes(list);
        }
    }

    public void WriteArray<T>(T[]? array)
    {
        if (array == null || array.Length == 0)
        {
            Write(0);
            return;
        }

        Write(array.Length);
        foreach (var item in array)
        {
            WriteValue(typeof(T), item);
        }
    }

    public void WriteList<T>(List<T>? list)
    {
        if (list == null || list.Count == 0)
        {
            Write(0);
            return;
        }

        Write(list.Count);
        foreach (var item in list)
        {
            WriteValue(typeof(T), item);
        }
    }

    public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue>? dictionary)
        where TKey : notnull
    {
        if (dictionary == null || dictionary.Count == 0)
        {
            Write(0);
            return;
        }

        Write(dictionary.Count);
        foreach (var (key, value) in dictionary)
        {
            WriteValue(typeof(TKey), key);
            WriteValue(typeof(TValue), value);
        }
    }

    public void WriteNullable<T>(T? value) where T : struct
    {
        Write(value.HasValue);
        if (value.HasValue)
        {
            WriteValue(typeof(T), value.Value);
        }
    }

    public void WriteCustom<T>(T value)
    {
        WriteValue(typeof(T), value);
    }

    // ---- Reference ----

    public void WriteResourceRef(object? value)
    {
        if (value == null)
        {
            Write(uint.MaxValue);
            return;
        }

        if (_resourceMap.TryGetValue(value, out var id))
        {
            Write(id);
        }
        else if (_nodeIndexMap.TryGetValue(value, out var nodeIndex))
        {
            Write((uint)(nodeIndex | 0x80000000));
        }
        else
        {
            Write(uint.MaxValue);
        }
    }

    // ---- String Table Flush ----

    public void FlushStringTable(Stream destination)
    {
        using var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true);
        writer.Write(_strings.Count);
        foreach (var bytes in _strings)
        {
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
    }

    private void WriteValue(Type type, object? value)
    {
        if (type == typeof(object))
        {
            WriteDynamicObject(value);
            return;
        }

        if (type == typeof(string))
        {
            WriteString((string?)value);
            return;
        }

        if (IsNullableType(type))
        {
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            Write(value != null);
            if (value != null)
            {
                WriteValue(underlyingType, value);
            }
            return;
        }

        if (!type.IsValueType)
        {
            Write(value != null);
            if (value == null)
            {
                return;
            }
        }

        if (type == typeof(bool))
        {
            Write((bool)value!);
            return;
        }

        if (type == typeof(byte))
        {
            Write((byte)value!);
            return;
        }

        if (type == typeof(sbyte))
        {
            Write((sbyte)value!);
            return;
        }

        if (type == typeof(short))
        {
            Write((short)value!);
            return;
        }

        if (type == typeof(ushort))
        {
            Write((ushort)value!);
            return;
        }

        if (type == typeof(int))
        {
            Write((int)value!);
            return;
        }

        if (type == typeof(uint))
        {
            Write((uint)value!);
            return;
        }

        if (type == typeof(long))
        {
            Write((long)value!);
            return;
        }

        if (type == typeof(ulong))
        {
            Write((ulong)value!);
            return;
        }

        if (type == typeof(float))
        {
            Write((float)value!);
            return;
        }

        if (type == typeof(double))
        {
            Write((double)value!);
            return;
        }

        if (type == typeof(Vector2))
        {
            WriteBlittable((Vector2)value!);
            return;
        }

        if (type == typeof(Vector3))
        {
            WriteBlittable((Vector3)value!);
            return;
        }

        if (type == typeof(Vector4))
        {
            WriteBlittable((Vector4)value!);
            return;
        }

        if (type == typeof(Quaternion))
        {
            WriteBlittable((Quaternion)value!);
            return;
        }

        if (type == typeof(Matrix4x4))
        {
            WriteBlittable((Matrix4x4)value!);
            return;
        }

        if (type == typeof(System.Drawing.Color))
        {
            Write((uint)((System.Drawing.Color)value!).ToArgb());
            return;
        }

        if (type.IsEnum)
        {
            Write(Convert.ToInt32(value));
            return;
        }

        if (type.IsArray && type.GetArrayRank() == 1)
        {
            var array = (Array)value!;
            Write(array.Length);
            var elementType = type.GetElementType()!;
            foreach (var item in array)
            {
                WriteValue(elementType, item);
            }
            return;
        }

        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();

            if (genericDefinition == typeof(List<>))
            {
                WriteDynamicList((IList)value!, type.GetGenericArguments()[0]);
                return;
            }

            if (genericDefinition == typeof(Dictionary<,>))
            {
                var genericArguments = type.GetGenericArguments();
                WriteDynamicDictionary((IDictionary)value!, genericArguments[0], genericArguments[1]);
                return;
            }
        }

        if (value is IAuraSerializable serializable)
        {
            serializable.Serialize(this);
            return;
        }

        throw new NotSupportedException($"Serialization does not support values of type '{type.FullName}'.");
    }

    private void WriteDynamicList(IList list, Type elementType)
    {
        Write(list.Count);
        foreach (var item in list)
        {
            WriteValue(elementType, item);
        }
    }

    private void WriteDynamicDictionary(IDictionary dictionary, Type keyType, Type valueType)
    {
        Write(dictionary.Count);
        foreach (DictionaryEntry entry in dictionary)
        {
            WriteValue(keyType, entry.Key);
            WriteValue(valueType, entry.Value);
        }
    }

    private void WriteDynamicObject(object? value)
    {
        switch (value)
        {
            case null:
                Write((byte)DynamicValueType.Null);
                return;
            case bool boolValue:
                Write((byte)DynamicValueType.Boolean);
                Write(boolValue);
                return;
            case int intValue:
                Write((byte)DynamicValueType.Int32);
                Write(intValue);
                return;
            case uint uintValue:
                Write((byte)DynamicValueType.UInt32);
                Write(uintValue);
                return;
            case float floatValue:
                Write((byte)DynamicValueType.Single);
                Write(floatValue);
                return;
            case double doubleValue:
                Write((byte)DynamicValueType.Double);
                Write(doubleValue);
                return;
            case long longValue:
                Write((byte)DynamicValueType.Int64);
                Write(longValue);
                return;
            case ulong ulongValue:
                Write((byte)DynamicValueType.UInt64);
                Write(ulongValue);
                return;
            case string stringValue:
                Write((byte)DynamicValueType.String);
                WriteString(stringValue);
                return;
            case Vector2 vector2Value:
                Write((byte)DynamicValueType.Vector2);
                WriteBlittable(vector2Value);
                return;
            case Vector3 vector3Value:
                Write((byte)DynamicValueType.Vector3);
                WriteBlittable(vector3Value);
                return;
            case Vector4 vector4Value:
                Write((byte)DynamicValueType.Vector4);
                WriteBlittable(vector4Value);
                return;
            case System.Drawing.Color colorValue:
                Write((byte)DynamicValueType.Color);
                Write((uint)colorValue.ToArgb());
                return;
        }

        throw new NotSupportedException($"Material parameter serialization does not support values of type '{value.GetType().FullName}'.");
    }

    private static bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
