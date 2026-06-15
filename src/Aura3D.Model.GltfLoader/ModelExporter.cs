using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using System.Numerics;
using System.Text;
using Material = Aura3D.Core.Resources.Material;
using Mesh = Aura3D.Core.Nodes.Mesh;
using Texture = Aura3D.Core.Resources.Texture;

namespace Aura3D.Model;

public static class ModelExporter
{
    public static void SaveGlbModel(Core.Nodes.Model model, string filePath)
    {
        var sceneBuilder = new SceneBuilder(model.Name ?? "Scene");
        var materialCache = new Dictionary<Material, MaterialBuilder>();

        foreach (var mesh in model.Meshes)
        {
            var meshBuilder = BuildMesh(mesh, materialCache);
            sceneBuilder.AddRigidMesh(meshBuilder, new AffineTransform(mesh.WorldTransform));
        }

        var modelRoot = sceneBuilder.ToGltf2();

        // Post-process: save material extensions
        SaveMaterialExtensions(model, modelRoot, materialCache);

        modelRoot.SaveGLB(filePath);
    }

    private static void SaveMaterialExtensions(
        Core.Nodes.Model model,
        SharpGLTF.Schema2.ModelRoot modelRoot,
        Dictionary<Material, MaterialBuilder> materialCache)
    {
        // Build Core Material → glTF Material mapping
        var materialList = materialCache.Keys.ToList();
        var gltfMaterials = modelRoot.LogicalTextures.Count > 0
            ? modelRoot.LogicalMaterials.ToList()
            : [];

        for (int i = 0; i < materialList.Count && i < gltfMaterials.Count; i++)
        {
            var coreMaterial = materialList[i];
            var gltfMaterial = gltfMaterials[i];

            if (coreMaterial.ExtensionNames.Count == 0) continue;

            // Build texture index map for this material's extension textures
            var textureIndexMap = new Dictionary<Texture, int>();
            foreach (var channel in coreMaterial.Channels)
            {
                if (channel.Texture is Texture tex && !textureIndexMap.ContainsKey(tex))
                {
                    int index = AddTextureToModelRoot(tex, modelRoot);
                    textureIndexMap[tex] = index;
                }
            }

            foreach (var extName in coreMaterial.ExtensionNames)
            {
                var loader = ModelLoader.GetExtensionLoader(extName);
                loader?.SaveMaterialExtension(coreMaterial, gltfMaterial, modelRoot, textureIndexMap);
            }
        }
    }

    private static int AddTextureToModelRoot(Texture coreTexture, SharpGLTF.Schema2.ModelRoot modelRoot)
    {
        var imageBuilder = CreateImageBuilder(coreTexture);
        var image = modelRoot.UseImage(imageBuilder.Content);
        var sampler = modelRoot.UseTextureSampler(
            SharpGLTF.Schema2.TextureWrapMode.REPEAT,
            SharpGLTF.Schema2.TextureWrapMode.REPEAT,
            TextureMipMapFilter.LINEAR,
            TextureInterpolationFilter.LINEAR);
        var texture = modelRoot.UseTexture(image, sampler);
        return texture.LogicalIndex;
    }

    private static MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> BuildMesh(
        Mesh mesh,
        Dictionary<Material, MaterialBuilder> materialCache)
    {
        var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(mesh.Name ?? "Mesh");

        var materialBuilder = mesh.Material != null
            ? GetOrCreateMaterialBuilder(mesh.Material, materialCache)
            : new MaterialBuilder().WithMetallicRoughnessShader();

        var primitiveBuilder = meshBuilder.UsePrimitive(materialBuilder);

        var geometry = mesh.Geometry;
        if (geometry == null)
            return meshBuilder;

        var positions = geometry.GetAttributeData(BuildInVertexAttribute.Position);
        var normals = geometry.GetAttributeData(BuildInVertexAttribute.Normal);
        var texCoords = geometry.GetAttributeData(BuildInVertexAttribute.TexCoord_0);
        var indices = geometry.Indices;

        if (positions == null || indices == null || indices.Count == 0)
            return meshBuilder;

        for (int i = 0; i < indices.Count; i += 3)
        {
            var i0 = (int)indices[i];
            var i1 = (int)indices[i + 1];
            var i2 = (int)indices[i + 2];

            var v0 = BuildVertex(positions, normals, texCoords, i0);
            var v1 = BuildVertex(positions, normals, texCoords, i1);
            var v2 = BuildVertex(positions, normals, texCoords, i2);

            primitiveBuilder.AddTriangle(v0, v1, v2);
        }

        return meshBuilder;
    }

    private static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> BuildVertex(
        List<float> positions, List<float>? normals, List<float>? texCoords, int index)
    {
        var px = positions[index * 3];
        var py = positions[index * 3 + 1];
        var pz = positions[index * 3 + 2];

        Vector3 normal = Vector3.UnitY;
        if (normals != null && normals.Count > index * 3 + 2)
        {
            normal = new Vector3(normals[index * 3], normals[index * 3 + 1], normals[index * 3 + 2]);
        }

        Vector2 uv = Vector2.Zero;
        if (texCoords != null && texCoords.Count > index * 2 + 1)
        {
            uv = new Vector2(texCoords[index * 2], texCoords[index * 2 + 1]);
        }

        return new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
            new VertexPositionNormal(px, py, pz, normal.X, normal.Y, normal.Z),
            new VertexTexture1(uv));
    }

    private static MaterialBuilder GetOrCreateMaterialBuilder(
        Material material,
        Dictionary<Material, MaterialBuilder> cache)
    {
        if (cache.TryGetValue(material, out var cached))
            return cached;

        var builder = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithAlpha(
                material.BlendMode switch
                {
                    BlendMode.Translucent => SharpGLTF.Materials.AlphaMode.BLEND,
                    BlendMode.Masked => SharpGLTF.Materials.AlphaMode.MASK,
                    _ => SharpGLTF.Materials.AlphaMode.OPAQUE
                },
                material.AlphaCutoff)
            .WithDoubleSide(material.DoubleSided);

        var baseColorTexture = material.GetTexture("BaseColor");
        if (baseColorTexture is Texture bcTex)
        {
            var imageBuilder = CreateImageBuilder(bcTex);
            builder.WithBaseColor(imageBuilder);
        }

        var normalTexture = material.GetTexture("Normal");
        if (normalTexture is Texture nTex)
        {
            var imageBuilder = CreateImageBuilder(nTex);
            builder.WithNormal(imageBuilder, 1.0f);
        }

        cache[material] = builder;
        return builder;
    }

    private static ImageBuilder CreateImageBuilder(Texture texture)
    {
        if (texture.LdrData == null || texture.LdrData.Count == 0 || texture.Width == 0 || texture.Height == 0)
        {
            var fallback = new MemoryImage(PngEncoder.EncodeRgba([255, 255, 255, 255], 1, 1));
            return ImageBuilder.From(fallback, "fallback");
        }

        var width = (int)texture.Width;
        var height = (int)texture.Height;
        var srcChannels = texture.ColorFormat == ColorFormat.RGBA ? 4 : 3;

        var rgbaData = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            var si = i * srcChannels;
            rgbaData[i * 4] = texture.LdrData[si];
            rgbaData[i * 4 + 1] = texture.LdrData[si + 1];
            rgbaData[i * 4 + 2] = texture.LdrData[si + 2];
            rgbaData[i * 4 + 3] = srcChannels == 4 ? texture.LdrData[si + 3] : (byte)255;
        }

        var pngBytes = PngEncoder.EncodeRgba(rgbaData, width, height);
        var memoryImage = new MemoryImage(pngBytes);
        return ImageBuilder.From(memoryImage, "texture");
    }
}

/// <summary>
/// Minimal PNG encoder that writes RGBA raw pixel data as a valid PNG file
/// using stored (uncompressed) zlib blocks. No external dependencies needed.
/// </summary>
internal static class PngEncoder
{
    public static byte[] EncodeRgba(byte[] rgbaData, int width, int height)
    {
        using var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // PNG signature
        bw.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        // IHDR
        var ihdr = new byte[13];
        WriteBE32(ihdr, 0, width);
        WriteBE32(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(bw, "IHDR", ihdr);

        // IDAT — raw pixel data with filter byte 0 per row, wrapped in stored zlib
        var rawData = new byte[height * (1 + width * 4)]; // 1 filter byte per row
        for (int y = 0; y < height; y++)
        {
            rawData[y * (1 + width * 4)] = 0; // filter: None
            System.Buffer.BlockCopy(rgbaData, y * width * 4, rawData, y * (1 + width * 4) + 1, width * 4);
        }

        var idat = ZlibStored(rawData);
        WriteChunk(bw, "IDAT", idat);

        // IEND
        WriteChunk(bw, "IEND", []);

        return ms.ToArray();
    }

    private static byte[] ZlibStored(byte[] data)
    {
        // Zlib header (CMF=0x78, FLG=0x01 for no compression/level 0)
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); // CMF
        ms.WriteByte(0x01); // FLG

        var offset = 0;
        while (offset < data.Length)
        {
            int blockLen = Math.Min(65535, data.Length - offset);
            bool last = offset + blockLen >= data.Length;

            ms.WriteByte((byte)(last ? 1 : 0)); // BFINAL + BTYPE=00 (stored)
            ms.WriteByte((byte)(blockLen & 0xFF));
            ms.WriteByte((byte)((blockLen >> 8) & 0xFF));
            ms.WriteByte((byte)(~blockLen & 0xFF));
            ms.WriteByte((byte)((~blockLen >> 8) & 0xFF));
            ms.Write(data, offset, blockLen);
            offset += blockLen;
        }

        // Adler-32 checksum
        uint a = 1, b2 = 0;
        foreach (var byt in data)
        {
            a = (a + byt) % 65521;
            b2 = (b2 + a) % 65521;
        }
        var adler = (b2 << 16) | a;
        ms.WriteByte((byte)((adler >> 24) & 0xFF));
        ms.WriteByte((byte)((adler >> 16) & 0xFF));
        ms.WriteByte((byte)((adler >> 8) & 0xFF));
        ms.WriteByte((byte)(adler & 0xFF));

        return ms.ToArray();
    }

    private static void WriteChunk(BinaryWriter bw, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        WriteBE32(bw, data.Length);
        bw.Write(typeBytes);
        bw.Write(data);

        uint crc = Crc32(typeBytes, data);
        WriteBE32(bw, (int)crc);
    }

    private static void WriteBE32(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)((value >> 24) & 0xFF);
        buf[offset + 1] = (byte)((value >> 16) & 0xFF);
        buf[offset + 2] = (byte)((value >> 8) & 0xFF);
        buf[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteBE32(BinaryWriter bw, int value)
    {
        bw.Write((byte)((value >> 24) & 0xFF));
        bw.Write((byte)((value >> 16) & 0xFF));
        bw.Write((byte)((value >> 8) & 0xFF));
        bw.Write((byte)(value & 0xFF));
    }

    private static uint Crc32(params byte[][] datas)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var data in datas)
            foreach (var b in data)
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
