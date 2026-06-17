using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Aura3D.Core.Serialization;

internal static class AuraReferenceWalker
{
    private sealed class SerializableMember
    {
        public required bool IsReference { get; init; }
        public required Func<object, object?> Getter { get; init; }
    }

    private static readonly ConcurrentDictionary<Type, SerializableMember[]> MemberCache = new();

    public static void VisitSerializableReferences(object? instance, Action<object> onReference)
    {
        if (instance == null)
            return;

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        VisitObject(instance, onReference, visited);
    }

    private static void VisitObject(object? instance, Action<object> onReference, HashSet<object> visited)
    {
        if (instance == null)
            return;

        var type = instance.GetType();
        if (IsLeafType(type))
            return;

        if (!type.IsValueType && !visited.Add(instance))
            return;

        foreach (var member in GetSerializableMembers(type))
        {
            var value = member.Getter(instance);
            if (value == null)
                continue;

            if (member.IsReference)
            {
                VisitReferenceValue(value, onReference);
            }
            else
            {
                VisitNestedValue(value, onReference, visited);
            }
        }
    }

    private static void VisitReferenceValue(object value, Action<object> onReference)
    {
        if (value is string)
        {
            onReference(value);
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key != null)
                    onReference(entry.Key);
                if (entry.Value != null)
                    onReference(entry.Value);
            }
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    onReference(item);
            }
            return;
        }

        onReference(value);
    }

    private static void VisitNestedValue(object value, Action<object> onReference, HashSet<object> visited)
    {
        var type = value.GetType();
        if (IsLeafType(type))
            return;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key != null)
                    VisitNestedValue(entry.Key, onReference, visited);
                if (entry.Value != null)
                    VisitNestedValue(entry.Value, onReference, visited);
            }
            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    VisitNestedValue(item, onReference, visited);
            }
            return;
        }

        if (GetSerializableMembers(type).Length > 0)
        {
            VisitObject(value, onReference, visited);
        }
    }

    private static SerializableMember[] GetSerializableMembers(Type type)
    {
        return MemberCache.GetOrAdd(type, static currentType =>
        {
            var members = new List<(int Order, SerializableMember Member)>();
            var hierarchy = new Stack<Type>();
            for (var typeCursor = currentType; typeCursor != null && typeCursor != typeof(object); typeCursor = typeCursor.BaseType)
            {
                hierarchy.Push(typeCursor);
            }

            while (hierarchy.Count > 0)
            {
                var declaredType = hierarchy.Pop();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (var field in declaredType.GetFields(flags))
                {
                    var isReference = field.IsDefined(typeof(AuraReferenceAttribute), inherit: true);
                    var isField = field.IsDefined(typeof(AuraFieldAttribute), inherit: true);
                    if (!isReference && !isField)
                        continue;

                    members.Add((field.MetadataToken, new SerializableMember
                    {
                        IsReference = isReference,
                        Getter = instance => field.GetValue(instance)
                    }));
                }

                foreach (var property in declaredType.GetProperties(flags))
                {
                    if (property.GetIndexParameters().Length > 0 || property.GetMethod == null)
                        continue;

                    var isReference = property.IsDefined(typeof(AuraReferenceAttribute), inherit: true);
                    var isField = property.IsDefined(typeof(AuraFieldAttribute), inherit: true);
                    if (!isReference && !isField)
                        continue;

                    members.Add((property.MetadataToken, new SerializableMember
                    {
                        IsReference = isReference,
                        Getter = instance => property.GetValue(instance)
                    }));
                }
            }

            return members
                .OrderBy(item => item.Order)
                .Select(item => item.Member)
                .ToArray();
        });
    }

    private static bool IsLeafType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
            return true;

        return type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(System.Numerics.Vector2)
            || type == typeof(System.Numerics.Vector3)
            || type == typeof(System.Numerics.Vector4)
            || type == typeof(System.Numerics.Quaternion)
            || type == typeof(System.Numerics.Matrix4x4)
            || type == typeof(System.Drawing.Color)
            || type == typeof(Aura3D.Core.Math.BoundingBox);
    }
}
