using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace Aura3D.Core.Serialization.SourceGenerator;

[Generator]
public class SerializationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all types marked with [AuraChunk]
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax tds &&
                    tds.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetTypeSymbolInfo(ctx))
            .Where(static info => info != null);

        var compilationAndTypes = context.CompilationProvider.Combine(typeDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndTypes, (spc, source) =>
        {
            var compilation = source.Left;
            var typeInfos = source.Right;

            // Group by type symbol to deduplicate partial declarations
            var seenTypes = new HashSet<string>();

            foreach (var info in typeInfos)
            {
                if (info == null) continue;
                if (!seenTypes.Add(info.FullName)) continue;

                var generatedSource = GenerateSerializationCode(info);
                if (generatedSource != null)
                {
                    spc.AddSource($"{info.TypeName}.Serialization.g.cs", generatedSource);
                }
            }
        });
    }

    private static TypeSerializationInfo? GetTypeSymbolInfo(GeneratorSyntaxContext ctx)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null) return null;

        // Check for [AuraChunk] attribute
        var auraChunkAttr = symbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Name == "AuraChunkAttribute" ||
            a.AttributeClass?.Name == "AuraChunk");

        if (auraChunkAttr == null) return null;

        var chunkType = GetUInt32Value(auraChunkAttr.ConstructorArguments[0], defaultValue: 0u);
        var chunkVersion = GetUInt32Value(auraChunkAttr.ConstructorArguments[1], defaultValue: 1u);

        var info = new TypeSerializationInfo
        {
            FullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TypeName = symbol.Name,
            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            TypeKeyword = symbol.TypeKind == TypeKind.Struct ? "struct" : "class",
            TypeParameters = GetTypeParameters(symbol),
            TypeConstraints = GetTypeConstraints(symbol),
            ChunkType = chunkType,
            ChunkVersion = chunkVersion,
            IsNodeType = IsNodeType(symbol),
        };

        // Collect fields from this type and all base types with [AuraChunk] or [AuraField]
        CollectFields(symbol, info);

        return info;
    }

    private static string GetTypeParameters(INamedTypeSymbol symbol)
    {
        if (symbol.TypeParameters.Length == 0)
            return string.Empty;

        return $"<{string.Join(", ", symbol.TypeParameters.Select(tp => tp.Name))}>";
    }

    private static string GetTypeConstraints(INamedTypeSymbol symbol)
    {
        if (symbol.TypeParameters.Length == 0)
            return string.Empty;

        var clauses = new List<string>();
        foreach (var typeParameter in symbol.TypeParameters)
        {
            var constraints = new List<string>();

            if (typeParameter.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }
            else if (typeParameter.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            else if (typeParameter.HasReferenceTypeConstraint)
            {
                constraints.Add("class");
            }

            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                constraints.Add(constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            if (typeParameter.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            if (typeParameter.HasConstructorConstraint && !typeParameter.HasValueTypeConstraint)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                clauses.Add($"    where {typeParameter.Name} : {string.Join(", ", constraints)}");
            }
        }

        return string.Join("\n", clauses);
    }

    private static bool IsNodeType(INamedTypeSymbol symbol)
    {
        var current = symbol;
        while (current != null)
        {
            var baseName = current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (baseName.Contains("Aura3D.Core.Nodes.Node") || baseName == "global::Aura3D.Core.Nodes.Node")
                return true;
            if (current.BaseType != null)
                current = current.BaseType;
            else
                break;
        }
        return false;
    }

    private static void CollectFields(INamedTypeSymbol symbol, TypeSerializationInfo info)
    {
        // Walk up the inheritance chain
        var current = symbol;
        var typeChain = new Stack<INamedTypeSymbol>();
        var collectedFields = new List<FieldSerializationInfo>();

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            typeChain.Push(current);
            current = current.BaseType;
        }

        while (typeChain.Count > 0)
        {
            current = typeChain.Pop();
            // Get members of current type (fields + properties)
            // Process in reverse declaration order — we'll reverse at the end
            var members = current.GetMembers()
                .Where(m => m is IFieldSymbol || m is IPropertySymbol)
                .OrderBy(GetDeclarationOrder)
                .ThenBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            // To get declaration order, we need to look at syntax
            // For now collect from symbol metadata
            foreach (var member in members)
            {
                // Check for [AuraField]
                var auraFieldAttr = member.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.Name == "AuraFieldAttribute" ||
                    a.AttributeClass?.Name == "AuraField");

                if (auraFieldAttr == null) continue;

                var since = GetUInt32Value(auraFieldAttr.ConstructorArguments[0], defaultValue: 1u);

                // Check for [AuraReference]
                var isReference = member.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "AuraReferenceAttribute" ||
                    a.AttributeClass?.Name == "AuraReference");

                var fieldType = member switch
                {
                    IFieldSymbol f => f.Type,
                    IPropertySymbol p => p.Type,
                    _ => null
                };

                if (fieldType == null) continue;

                var fieldInfo = new FieldSerializationInfo
                {
                    Name = member.Name,
                    Since = since,
                    IsReference = isReference,
                    TypeCategory = CategorizeType(fieldType),
                    TypeName = fieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    IsProperty = member is IPropertySymbol,
                    DeclaringTypeName = current.Name,
                    IsUnsigned = fieldType.SpecialType == SpecialType.System_UInt32 ||
                                 fieldType.SpecialType == SpecialType.System_UInt16 ||
                                 fieldType.SpecialType == SpecialType.System_UInt64 ||
                                 fieldType.SpecialType == SpecialType.System_Byte
                };

                collectedFields.Add(fieldInfo);
            }
        }
        info.Fields = collectedFields;
    }

    private static int GetDeclarationOrder(ISymbol member)
    {
        var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
        return syntaxRef?.Span.Start ?? int.MaxValue;
    }

    private static uint GetUInt32Value(TypedConstant constant, uint defaultValue)
    {
        var value = constant.Value;
        if (value == null)
            return defaultValue;

        return value switch
        {
            byte byteValue => byteValue,
            sbyte sbyteValue => unchecked((uint)sbyteValue),
            short shortValue => unchecked((uint)shortValue),
            ushort ushortValue => ushortValue,
            int intValue => unchecked((uint)intValue),
            uint uintValue => uintValue,
            long longValue => unchecked((uint)longValue),
            ulong ulongValue => unchecked((uint)ulongValue),
            _ => defaultValue
        };
    }

    private static TypeCategory CategorizeType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Boolean) return TypeCategory.Bool;
        if (type.SpecialType == SpecialType.System_Byte) return TypeCategory.Byte;
        if (type.SpecialType == SpecialType.System_SByte) return TypeCategory.Byte;
        if (type.SpecialType == SpecialType.System_Int16 || type.SpecialType == SpecialType.System_UInt16)
            return TypeCategory.Short;
        if (type.SpecialType == SpecialType.System_Int32 || type.SpecialType == SpecialType.System_UInt32)
            return TypeCategory.Int;
        if (type.SpecialType == SpecialType.System_Int64 || type.SpecialType == SpecialType.System_UInt64)
            return TypeCategory.Long;
        if (type.SpecialType == SpecialType.System_Single) return TypeCategory.Float;
        if (type.SpecialType == SpecialType.System_Double) return TypeCategory.Double;
        if (type.SpecialType == SpecialType.System_String) return TypeCategory.String;

        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fullName == "global::System.Numerics.Vector2") return TypeCategory.Vector2;
        if (fullName == "global::System.Numerics.Vector3") return TypeCategory.Vector3;
        if (fullName == "global::System.Numerics.Vector4") return TypeCategory.Vector4;
        if (fullName == "global::System.Numerics.Quaternion") return TypeCategory.Quaternion;
        if (fullName == "global::System.Numerics.Matrix4x4") return TypeCategory.Matrix4x4;
        if (fullName == "global::System.Drawing.Color") return TypeCategory.Color;
        if (fullName == "global::Aura3D.Core.Math.BoundingBox") return TypeCategory.BoundingBox;

        if (type.TypeKind == TypeKind.Enum) return TypeCategory.Enum;

        // Check for List<T>
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericDef = namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (genericDef.StartsWith("global::System.Collections.Generic.List<"))
            {
                var elementType = namedType.TypeArguments[0];
                var elemCategory = CategorizeType(elementType);
                if (elemCategory == TypeCategory.Byte)
                    return TypeCategory.ListByte;
                if (elemCategory == TypeCategory.Float)
                    return TypeCategory.ListFloat;
                if (elemCategory == TypeCategory.Int && elementType.SpecialType == SpecialType.System_UInt32)
                    return TypeCategory.ListUInt;
                return TypeCategory.List;
            }
            if (genericDef.StartsWith("global::System.Collections.Generic.Dictionary<"))
                return TypeCategory.Dictionary;
            if (genericDef.StartsWith("global::System.Nullable<"))
                return TypeCategory.Nullable;
        }

        // Check for arrays (List<byte>[] etc.)
        if (type is IArrayTypeSymbol)
            return TypeCategory.Array;

        return TypeCategory.Custom;
    }

    private static string? GenerateSerializationCode(TypeSerializationInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Numerics;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine("using Aura3D.Core.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {info.Namespace};");
        sb.AppendLine();

        // Serialize method
        sb.Append($"partial {info.TypeKeyword} {info.TypeName}{info.TypeParameters} : IAuraSerializable");
        if (!string.IsNullOrEmpty(info.TypeConstraints))
        {
            sb.AppendLine();
            sb.AppendLine(info.TypeConstraints);
        }
        else
        {
            sb.AppendLine();
        }
        sb.AppendLine("{");
        sb.AppendLine("    public void Serialize(AuraBinaryWriter writer)");
        sb.AppendLine("    {");

        foreach (var field in info.Fields)
        {
            EmitSerializeField(sb, field);
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Deserialize method
        sb.AppendLine("    public void Deserialize(AuraBinaryReader reader, uint chunkVersion)");
        sb.AppendLine("    {");
        EmitDeserializeFields(sb, info.Fields);
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitSerializeField(StringBuilder sb, FieldSerializationInfo field)
    {
        var access = field.IsProperty ? field.Name : field.Name;
        var writerCall = GetWriterCall(field, access);

        if (field.IsReference)
        {
            sb.AppendLine($"        writer.WriteResourceRef({access});");
            return;
        }

        sb.AppendLine($"        {writerCall};");
    }

    private static void EmitDeserializeFields(StringBuilder sb, List<FieldSerializationInfo> fields)
    {
        foreach (var field in fields)
        {
            if (field.Since == 1)
            {
                EmitDeserializeField(sb, field, null);
            }
            else
            {
                sb.AppendLine($"        if (chunkVersion >= {field.Since})");
                sb.AppendLine("        {");
                EmitDeserializeField(sb, field, null);
                sb.AppendLine("        }");
                sb.AppendLine("        else");
                sb.AppendLine("        {");
                EmitDeserializeField(sb, field, GetDefaultValue(field));
                sb.AppendLine("        }");
            }
        }
    }

    private static void EmitDeserializeField(StringBuilder sb, FieldSerializationInfo field, string? defaultValue)
    {
        var access = field.IsProperty ? field.Name : field.Name;
        var readerCall = GetReaderCall(field);

        if (field.IsReference)
        {
            if (defaultValue != null)
                sb.AppendLine($"            {access} = {defaultValue};");
            else
                sb.AppendLine($"            {access} = reader.ReadResourceRef<{field.TypeName}>();");
            return;
        }

        if (defaultValue != null)
            sb.AppendLine($"            {access} = {defaultValue};");
        else
            sb.AppendLine($"            {access} = {readerCall};");
    }

    private static string GetWriterCall(FieldSerializationInfo field, string access)
    {
        return field.TypeCategory switch
        {
            TypeCategory.Bool => $"writer.Write({access})",
            TypeCategory.Byte => $"writer.Write({access})",
            TypeCategory.Short => $"writer.Write({access})",
            TypeCategory.Int => $"writer.Write({access})",
            TypeCategory.Long => $"writer.Write({access})",
            TypeCategory.Float => $"writer.Write({access})",
            TypeCategory.Double => $"writer.Write({access})",
            TypeCategory.String => $"writer.WriteString({access})",
            TypeCategory.Vector2 => $"writer.WriteBlittable({access})",
            TypeCategory.Vector3 => $"writer.WriteBlittable({access})",
            TypeCategory.Vector4 => $"writer.WriteBlittable({access})",
            TypeCategory.Quaternion => $"writer.WriteBlittable({access})",
            TypeCategory.Matrix4x4 => $"writer.WriteBlittable({access})",
            TypeCategory.Color => $"writer.Write((uint){access}.ToArgb())",
            TypeCategory.BoundingBox => $"writer.WriteBoundingBox({access})",
            TypeCategory.Enum => $"writer.Write((int){access})",
            TypeCategory.ListByte => $"writer.WriteBytes({access})",
            TypeCategory.ListFloat => $"writer.WriteBlittableList({access})",
            TypeCategory.ListUInt => $"writer.WriteBlittableList({access})",
            TypeCategory.List => $"writer.WriteList({access})",
            TypeCategory.Dictionary => $"writer.WriteDictionary({access})",
            TypeCategory.Nullable => $"writer.WriteNullable({access})",
            TypeCategory.Array => $"writer.WriteArray({access})",
            _ => $"writer.WriteCustom({access})"
        };
    }

    private static string GetReaderCall(FieldSerializationInfo field)
    {
        return field.TypeCategory switch
        {
            TypeCategory.Bool => "reader.ReadBoolean()",
            TypeCategory.Byte => field.IsUnsigned ? "reader.ReadByte()" : "reader.ReadSByte()",
            TypeCategory.Short => field.IsUnsigned ? "reader.ReadUInt16()" : "reader.ReadInt16()",
            TypeCategory.Int => field.IsUnsigned ? "reader.ReadUInt32()" : "reader.ReadInt32()",
            TypeCategory.Long => field.IsUnsigned ? "reader.ReadUInt64()" : "reader.ReadInt64()",
            TypeCategory.Float => "reader.ReadSingle()",
            TypeCategory.Double => "reader.ReadDouble()",
            TypeCategory.String => "reader.ReadString()",
            TypeCategory.Vector2 => "reader.ReadBlittable<Vector2>()",
            TypeCategory.Vector3 => "reader.ReadBlittable<Vector3>()",
            TypeCategory.Vector4 => "reader.ReadBlittable<Vector4>()",
            TypeCategory.Quaternion => "reader.ReadBlittable<Quaternion>()",
            TypeCategory.Matrix4x4 => "reader.ReadBlittable<Matrix4x4>()",
            TypeCategory.Color => "System.Drawing.Color.FromArgb((int)reader.ReadUInt32())",
            TypeCategory.BoundingBox => "reader.ReadBoundingBox()",
            TypeCategory.Enum => $"({field.TypeName})reader.ReadInt32()",
            TypeCategory.ListByte => "reader.ReadBytes()",
            TypeCategory.ListFloat => "reader.ReadBlittableList<float>()",
            TypeCategory.ListUInt => "reader.ReadBlittableList<uint>()",
            TypeCategory.List => $"reader.ReadList<{GetElementType(field.TypeName)}>()",
            TypeCategory.Dictionary => $"reader.ReadDictionary<string, {GetDictionaryValueType(field.TypeName)}>()",
            TypeCategory.Nullable => $"reader.ReadNullable<{GetNullableInnerType(field.TypeName)}>()",
            TypeCategory.Array => $"reader.ReadArray<{GetArrayElementType(field.TypeName)}>()",
            _ => $"reader.ReadCustom<{field.TypeName}>()"
        };
    }

    private static string GetDefaultValue(FieldSerializationInfo field)
    {
        return field.TypeCategory switch
        {
            TypeCategory.Bool => "false",
            TypeCategory.Byte => field.IsUnsigned ? "(byte)0" : "(sbyte)0",
            TypeCategory.Short => field.IsUnsigned ? "(ushort)0" : "(short)0",
            TypeCategory.Int => field.IsUnsigned ? "0u" : "0",
            TypeCategory.Long => field.IsUnsigned ? "0UL" : "0L",
            TypeCategory.Float => "0f",
            TypeCategory.Double => "0.0",
            TypeCategory.String => "string.Empty",
            TypeCategory.Vector2 => "Vector2.Zero",
            TypeCategory.Vector3 => "Vector3.Zero",
            TypeCategory.Vector4 => "Vector4.Zero",
            TypeCategory.Quaternion => "Quaternion.Identity",
            TypeCategory.Matrix4x4 => "Matrix4x4.Identity",
            TypeCategory.Color => "System.Drawing.Color.White",
            TypeCategory.BoundingBox => "null",
            TypeCategory.Enum => $"default({field.TypeName})",
            TypeCategory.ListByte => "new List<byte>()",
            TypeCategory.ListFloat => "new List<float>()",
            TypeCategory.ListUInt => "new List<uint>()",
            TypeCategory.List => $"new List<{GetElementType(field.TypeName)}>()",
            TypeCategory.Dictionary => $"new Dictionary<string, {GetDictionaryValueType(field.TypeName)}>()",
            TypeCategory.Nullable => "null",
            TypeCategory.Array => $"System.Array.Empty<{GetArrayElementType(field.TypeName)}>()",
            _ => $"default({field.TypeName})"
        };
    }

    private static string GetElementType(string typeName)
    {
        // Extract T from global::System.Collections.Generic.List<T>
        var start = typeName.IndexOf('<') + 1;
        var end = typeName.LastIndexOf('>');
        if (start > 0 && end > start)
            return typeName.Substring(start, end - start);
        return "object";
    }

    private static string GetDictionaryValueType(string typeName)
    {
        // Extract V from Dictionary<K,V>
        var parts = typeName.Split(',');
        if (parts.Length >= 2)
            return parts[1].Trim().TrimEnd('>');
        return "object";
    }

    private static string GetNullableInnerType(string typeName)
    {
        // Extract T from Nullable<T> or T?
        var start = typeName.IndexOf('<') + 1;
        var end = typeName.LastIndexOf('>');
        if (start > 0 && end > start)
            return typeName.Substring(start, end - start);
        return typeName.TrimEnd('?');
    }

    private static string GetArrayElementType(string typeName)
    {
        if (typeName.EndsWith("[]", StringComparison.Ordinal))
            return typeName.Substring(0, typeName.Length - 2);

        return "object";
    }
}
