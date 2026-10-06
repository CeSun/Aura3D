using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model.Exceptions;
using System.Numerics;
using Ufbx.NET;

namespace Aura3D.Model;

/// <summary>
/// Turns a <see cref="UfbxMesh"/> into one <see cref="Mesh"/> per material part.
/// </summary>
internal static class FbxMeshBuilder
{
    public static List<Mesh> Build(
        UfbxMesh ufbxMesh,
        Matrix4x4 localTransform,
        IReadOnlyDictionary<uint, Core.Resources.Material> materials,
        Core.Resources.Material defaultMaterial,
        Skeleton? skeleton)
    {
        var faces = ufbxMesh.Faces ?? [];

        if (faces.Length == 0)
            return [];

        var faceMaterial = ufbxMesh.FaceMaterial;

        List<uint> order = [];
        Dictionary<uint, List<int>> groups = [];

        for (int f = 0; f < faces.Length; f++)
        {
            var key = faceMaterial is null ? 0u : faceMaterial[f];

            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups[key] = group;
                order.Add(key);
            }

            group.Add(f);
        }

        var attributes = new CornerAttributes(ufbxMesh);

        List<Mesh> result = [];

        foreach (var materialIndex in order)
        {
            var mesh = BuildPart(ufbxMesh, attributes, groups[materialIndex], localTransform, skeleton);

            if (mesh is null)
                continue;

            mesh.Material = ResolveMaterial(ufbxMesh, materials, materialIndex) ?? defaultMaterial;

            // Same rule as AssimpLoader: point primitives are always unlit (lighting is undefined
            // without a normal), and the material is shared by index, so it has to be cloned.
            if (mesh.Geometry?.PrimitiveType == PrimitiveType.Points && mesh.Material != null)
            {
                var pointMaterial = mesh.Material.Clone();

                pointMaterial.SetParameterValue(Core.Resources.Material.UnlitParameterName, 1f);

                if (attributes.HasColor)
                    pointMaterial.SetParameterValue(Core.Resources.Material.UseVertexColorParameterName, 1f);

                pointMaterial.SetParameterValue(Core.Resources.Material.PointSizeParameterName, 3f);

                mesh.Material = pointMaterial;
            }

            result.Add(mesh);
        }

        return result;
    }

    private static Core.Resources.Material? ResolveMaterial(
        UfbxMesh ufbxMesh, IReadOnlyDictionary<uint, Core.Resources.Material> materials, uint materialIndex)
    {
        var meshMaterials = ufbxMesh.Materials;

        if (meshMaterials is null || materialIndex >= meshMaterials.Length)
            return null;

        var material = meshMaterials[materialIndex];

        if (material is null || !materials.TryGetValue(material.TypedId, out var mapped))
            return null;

        return mapped;
    }

    private static Mesh? BuildPart(
        UfbxMesh ufbxMesh,
        CornerAttributes attributes,
        List<int> faces,
        Matrix4x4 localTransform,
        Skeleton? skeleton)
    {
        var cornerCount = faces.Sum(f => (int)ufbxMesh.Faces![f].NumIndices);

        if (cornerCount == 0)
            return null;

        var primitiveType = cornerCount == faces.Count
            ? PrimitiveType.Points
            : cornerCount == faces.Count * 2
                ? PrimitiveType.Lines
                : PrimitiveType.Triangles;

        var corners = primitiveType == PrimitiveType.Triangles
            ? Triangulate(ufbxMesh, faces)
            : EnumerateCorners(ufbxMesh, faces).ToList();

        if (corners.Count == 0)
            return null;

        var geometry = new Geometry { PrimitiveType = primitiveType };

        List<float> positions = [];
        List<float> normals = [];
        List<float> uvs = [];
        List<float> colors = [];
        List<float> tangents = [];
        List<float> bitangents = [];
        List<uint> indices = new(corners.Count);
        List<int> sourceVertices = [];

        Dictionary<(uint, uint, uint, uint, uint, uint, uint), int> weld = [];

        foreach (var corner in corners)
        {
            var key = (
                attributes.Vertex[corner],
                attributes.Position[corner],
                attributes.Normal[corner],
                attributes.Uv[corner],
                attributes.Color[corner],
                attributes.Tangent[corner],
                attributes.Bitangent[corner]);

            if (!weld.TryGetValue(key, out var index))
            {
                index = sourceVertices.Count;
                weld[key] = index;
                sourceVertices.Add((int)attributes.Vertex[corner]);

                var p = attributes.PositionValues[attributes.Position[corner]];
                positions.Add((float)p.X);
                positions.Add((float)p.Y);
                positions.Add((float)p.Z);

                if (attributes.HasNormal)
                {
                    var n = attributes.NormalValues![attributes.Normal[corner]];
                    normals.Add((float)n.X);
                    normals.Add((float)n.Y);
                    normals.Add((float)n.Z);
                }

                if (attributes.HasUv)
                {
                    var uv = attributes.UvValues![attributes.Uv[corner]];
                    uvs.Add((float)uv.X);
                    uvs.Add((float)uv.Y);
                }

                if (attributes.HasColor)
                {
                    var c = attributes.ColorValues![attributes.Color[corner]];
                    colors.Add((float)c.X);
                    colors.Add((float)c.Y);
                    colors.Add((float)c.Z);
                    colors.Add((float)c.W);
                }

                if (attributes.HasTangent)
                {
                    var t = attributes.TangentValues![attributes.Tangent[corner]];
                    tangents.Add((float)t.X);
                    tangents.Add((float)t.Y);
                    tangents.Add((float)t.Z);
                }

                if (attributes.HasBitangent)
                {
                    var b = attributes.BitangentValues![attributes.Bitangent[corner]];
                    bitangents.Add((float)b.X);
                    bitangents.Add((float)b.Y);
                    bitangents.Add((float)b.Z);
                }
            }

            indices.Add((uint)index);
        }

        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);

        if (attributes.HasNormal)
            geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);

        if (attributes.HasUv)
            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);

        if (attributes.HasColor)
            geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);

        geometry.SetIndices(indices);

        if (primitiveType == PrimitiveType.Triangles && attributes.HasNormal && attributes.HasUv && !attributes.HasTangent)
        {
            ModelHelper.CalcVerticsTbn(indices, positions, normals, uvs, out tangents, out bitangents);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);
        }
        else
        {
            if (attributes.HasTangent)
                geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
            if (attributes.HasBitangent)
                geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);
        }

        var skin = ufbxMesh.SkinDeformers is { Length: > 0 } skins ? skins[0] : null;

        if (skin is not null && skeleton is not null)
            ApplySkinning(geometry, ufbxMesh, skin, skeleton, sourceVertices);

        return new Mesh
        {
            Name = ufbxMesh.Name,
            Geometry = geometry,
            LocalTransform = localTransform,
        };
    }

    private static IEnumerable<int> EnumerateCorners(UfbxMesh ufbxMesh, List<int> faces)
    {
        foreach (var face in faces)
        {
            var f = ufbxMesh.Faces![face];

            for (int i = 0; i < f.NumIndices; i++)
                yield return (int)f.IndexBegin + i;
        }
    }

    private static List<int> Triangulate(UfbxMesh ufbxMesh, List<int> faces)
    {
        var buffer = new uint[System.Math.Max(1, ufbxMesh.MaxFaceTriangles) * 3];

        List<int> corners = [];

        foreach (var face in faces)
        {
            var f = ufbxMesh.Faces![face];

            if (f.NumIndices < 3)
                continue;

            var triangles = UfbxTopologyApi.TriangulateFace(buffer, buffer.Length, ufbxMesh, f);

            for (int i = 0; i < triangles * 3; i++)
                corners.Add((int)buffer[i]);
        }

        return corners;
    }

    private static void ApplySkinning(
        Geometry geometry, UfbxMesh ufbxMesh, UfbxSkinDeformer skin, Skeleton skeleton, List<int> sourceVertices)
    {
        var skinVertices = skin.Vertices;
        var skinWeights = skin.Weights;
        var clusters = skin.Clusters;

        if (skinVertices is null || skinWeights is null || clusters is null)
            return;

        var clusterBones = new int[clusters.Length];

        for (int i = 0; i < clusters.Length; i++)
        {
            var name = clusters[i].BoneNode?.Name ?? string.Empty;
            var boneIndex = skeleton.GetBoneIndex(name);

            if (boneIndex < 0)
                throw FbxImportErrors.SkeletonBoneNotFound(name);

            clusterBones[i] = boneIndex;
        }

        var joints = new float[4 * sourceVertices.Count];
        var weights = new float[4 * sourceVertices.Count];
        var filled = new int[sourceVertices.Count];

        for (int v = 0; v < sourceVertices.Count; v++)
        {
            var source = sourceVertices[v];

            if (source < 0 || source >= skinVertices.Length)
                continue;

            var entry = skinVertices[source];

            for (int w = 0; w < entry.NumWeights && filled[v] < 4; w++)
            {
                var weight = skinWeights[entry.WeightBegin + w];
                var slot = v * 4 + filled[v];

                joints[slot] = clusterBones[weight.ClusterIndex];
                weights[slot] = (float)weight.Weight;
                filled[v]++;
            }
        }

        geometry.SetVertexAttribute(BuildInVertexAttribute.Joints_0, 4, joints.ToList());
        geometry.SetVertexAttribute(BuildInVertexAttribute.Weights_0, 4, weights.ToList());
    }

    /// <summary>
    /// Per-corner value indices for every attribute Aura3D consumes. Attributes the mesh does not
    /// carry get an all-zero key so the welding key stays a fixed-shape tuple. Normals are generated
    /// through the ufbx topology API when the file omits them.
    /// </summary>
    private sealed class CornerAttributes
    {
        public uint[] Vertex { get; }

        public uint[] Position { get; }

        public uint[] Normal { get; }

        public uint[] Uv { get; }

        public uint[] Color { get; }

        public uint[] Tangent { get; }

        public uint[] Bitangent { get; }

        public bool HasNormal { get; }

        public bool HasUv { get; }

        public bool HasColor { get; }

        public bool HasTangent { get; }

        public bool HasBitangent { get; }

        public UfbxVec3[] PositionValues { get; }

        public UfbxVec3[]? NormalValues { get; }

        public UfbxVec2[]? UvValues { get; }

        public UfbxVec4[]? ColorValues { get; }

        public UfbxVec3[]? TangentValues { get; }

        public UfbxVec3[]? BitangentValues { get; }

        public CornerAttributes(UfbxMesh mesh)
        {
            var numIndices = mesh.NumIndices;

            Vertex = CornerKeys(mesh.VertexIndices, numIndices);
            Position = CornerKeys(mesh.VertexPosition.Exists, mesh.VertexPosition.Indices, numIndices);
            PositionValues = mesh.VertexPosition.Values ?? [];

            Uv = CornerKeys(mesh.VertexUv.Exists, mesh.VertexUv.Indices, numIndices);
            UvValues = mesh.VertexUv.Values;
            HasUv = UvValues is { Length: > 0 };

            Color = CornerKeys(mesh.VertexColor.Exists, mesh.VertexColor.Indices, numIndices);
            ColorValues = mesh.VertexColor.Values;
            HasColor = ColorValues is { Length: > 0 };

            Tangent = CornerKeys(mesh.VertexTangent.Exists, mesh.VertexTangent.Indices, numIndices);
            TangentValues = mesh.VertexTangent.Values;
            HasTangent = TangentValues is { Length: > 0 };

            Bitangent = CornerKeys(mesh.VertexBitangent.Exists, mesh.VertexBitangent.Indices, numIndices);
            BitangentValues = mesh.VertexBitangent.Values;
            HasBitangent = BitangentValues is { Length: > 0 };

            if (mesh.VertexNormal.Exists && mesh.VertexNormal.Values is { Length: > 0 })
            {
                Normal = CornerKeys(true, mesh.VertexNormal.Indices, numIndices);
                NormalValues = mesh.VertexNormal.Values;
                HasNormal = true;
            }
            else
            {
                var (keys, values) = GenerateNormals(mesh, numIndices);
                Normal = keys;
                NormalValues = values;
                HasNormal = values is { Length: > 0 };
            }
        }

        private static (uint[] Keys, UfbxVec3[]? Values) GenerateNormals(UfbxMesh mesh, int numIndices)
        {
            if (numIndices == 0)
                return ([], null);

            var topology = new UfbxTopoEdge[numIndices];
            UfbxTopologyApi.ComputeTopology(mesh, topology, numIndices);

            var mapping = new uint[numIndices];
            var numNormals = UfbxTopologyApi.GenerateNormalMapping(mesh, topology, numIndices, mapping, numIndices, false);

            if (numNormals <= 0)
                return (new uint[numIndices], null);

            var values = new UfbxVec3[numNormals];
            UfbxTopologyApi.ComputeNormals(mesh, mesh.VertexPosition, mapping, numIndices, values, numNormals);

            return (mapping, values);
        }

        private static uint[] CornerKeys(uint[]? source, int count)
        {
            var keys = new uint[count];

            if (source is null)
            {
                for (int i = 0; i < count; i++)
                    keys[i] = (uint)i;
            }
            else
            {
                Array.Copy(source, keys, System.Math.Min(source.Length, count));
            }

            return keys;
        }

        private static uint[] CornerKeys(bool exists, uint[]? indices, int count)
        {
            var keys = new uint[count];

            if (!exists)
                return keys;

            if (indices is null)
            {
                for (int i = 0; i < count; i++)
                    keys[i] = (uint)i;
            }
            else
            {
                Array.Copy(indices, keys, System.Math.Min(indices.Length, count));
            }

            return keys;
        }
    }
}
