namespace Aura3D.Core.Serialization.SourceGenerator;

internal class TypeSerializationInfo
{
    public string FullName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string TypeKeyword { get; set; } = "class";
    public string TypeParameters { get; set; } = string.Empty;
    public string TypeConstraints { get; set; } = string.Empty;
    public uint ChunkType { get; set; }
    public uint ChunkVersion { get; set; }
    public bool IsNodeType { get; set; }
    public List<FieldSerializationInfo> Fields { get; set; } = new();
}

internal class FieldSerializationInfo
{
    public string Name { get; set; } = string.Empty;
    public uint Since { get; set; }
    public bool IsReference { get; set; }
    public TypeCategory TypeCategory { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public bool IsProperty { get; set; }
    public string DeclaringTypeName { get; set; } = string.Empty;
    public bool IsUnsigned { get; set; }
}

internal enum TypeCategory
{
    Bool,
    Byte,
    Short,
    Int,
    Long,
    Float,
    Double,
    String,
    Vector2,
    Vector3,
    Vector4,
    Quaternion,
    Matrix4x4,
    Color,
    BoundingBox,
    Enum,
    ListByte,
    ListFloat,
    ListUInt,
    List,
    Dictionary,
    Nullable,
    Array,
    Custom
}
