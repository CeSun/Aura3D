using Assimp;
using Assimp.Unmanaged;
using Aura3D.Core;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using StbImageSharp;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Node = Assimp.Node;

namespace Aura3D.Model;

public static class AssimpLoader
{
    public unsafe static Core.Nodes.Model Load(string path, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        using var importer = new AssimpContext();

        var directory = Path.GetDirectoryName(path);

        var scene = importer.ImportFile(path, DefaultFlags);

        var model = processScene(scene, directory, loadTextureFunc);

        var skeleton = processSkeleton(scene);

        model.Skeleton = skeleton;

        foreach (var mesh in model.Meshes)
        {
            mesh.Model = model;
        }

        return model;
    }


    public unsafe static List<Core.Resources.Animation> LoadAnimations(string path, Skeleton? skeleton = null)
    {
        using var importer = new AssimpContext();

        var directory = Path.GetDirectoryName(path);

        var scene = importer.ImportFile(path, DefaultFlags);

        if (skeleton == null)
            skeleton = processSkeleton(scene);

        if (skeleton == null)
            return [];

        var animations = processAnimations(scene);

        foreach (var animation in animations)
        {
            animation.Skeleton = skeleton;
        }
        return animations;
    }

    public unsafe static List<Core.Resources.Animation> LoadAnimations(Stream stream, Skeleton? skeleton = null, string? extension = null)
    {
        using var importer = new AssimpContext();

        var scene = importer.ImportFileFromStream(stream, DefaultFlags, extension);

        if (skeleton == null)
            skeleton = processSkeleton(scene);

        if (skeleton == null)
            return [];

        var animations = processAnimations(scene);


        foreach (var animation in animations)
        {
            animation.Skeleton = skeleton;
        }

        return animations;
    }

    public unsafe static Core.Nodes.Model Load(Stream stream, string? extension = null, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        using var importer = new AssimpContext();

        var scene = importer.ImportFileFromStream(stream, DefaultFlags, extension);

        var model = processScene(scene, null, loadTextureFunc);

        var skeleton = processSkeleton(scene);

        model.Skeleton = skeleton;

        foreach (var mesh in model.Meshes)
        {
            mesh.Model = model;
        }

        return model;
    }


    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadModelAndAnimations(string path, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {

        using var importer = new AssimpContext();

        var directory = Path.GetDirectoryName(path);

        var scene = importer.ImportFile(path, DefaultFlags);

        var model = processScene(scene, directory, loadTextureFunc);

        var skeleton = processSkeleton(scene);

        model.Skeleton = skeleton;

        foreach (var mesh in model.Meshes)
        {
            mesh.Model = model;
        }
        List<Core.Resources.Animation> animations = [];

        if (skeleton != null)
        {
            animations = processAnimations(scene);

            foreach (var animation in animations)
            {
                animation.Skeleton = skeleton;
            }

        }
        return (model, animations);
    }

    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadModelAndAnimations(Stream stream, string? extension = null, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        using var importer = new AssimpContext();

        var scene = importer.ImportFileFromStream(stream, DefaultFlags, extension);

        var model = processScene(scene, null, loadTextureFunc);

        var skeleton = processSkeleton(scene);

        model.Skeleton = skeleton;

        foreach (var mesh in model.Meshes)
        {
            mesh.Model = model;
        }

        List<Core.Resources.Animation> animations = [];

        if (skeleton != null)
        {
            animations = processAnimations(scene);

            foreach (var animation in animations)
            {
                animation.Skeleton = skeleton;
            }
        }

        return (model, animations);
    }

    private static PostProcessSteps DefaultFlags => PostProcessSteps.Triangulate
                  | PostProcessSteps.GenerateNormals
                  | PostProcessSteps.OptimizeMeshes
                  | PostProcessSteps.CalculateTangentSpace
                  | PostProcessSteps.GenerateUVCoords;
    private unsafe static List<Core.Resources.Animation> processAnimations(Scene scene)
    {
        List<Core.Resources.Animation> animations = [];

        foreach (var assAnimation in scene.Animations)
        {
            if (assAnimation.HasNodeAnimations == false)
                continue;
            var animation = new Core.Resources.Animation();

            float maxTime = 0;
            foreach (var assChannel in assAnimation.NodeAnimationChannels)
            {
                var channel = new AnimationChannel();

                foreach (var posKey in assChannel.PositionKeys)
                {
                    channel.PositionKeyframes.Add(new Keyframe<Vector3>
                    {
                        Time = (float)(posKey.Time / assAnimation.TicksPerSecond),
                        Value = new Vector3(posKey.Value.X, posKey.Value.Y, posKey.Value.Z),
                    });
                    if (channel.PositionKeyframes.Last().Time > maxTime)
                        maxTime = channel.PositionKeyframes.Last().Time;
                }
                foreach (var rotKey in assChannel.RotationKeys)
                {
                    channel.RotationKeyframes.Add(new Keyframe<Quaternion>
                    {
                        Time = (float)(rotKey.Time / assAnimation.TicksPerSecond),
                        Value = new Quaternion(rotKey.Value.X, rotKey.Value.Y, rotKey.Value.Z, rotKey.Value.W),
                    });
                    if (channel.RotationKeyframes.Last().Time > maxTime)
                        maxTime = channel.RotationKeyframes.Last().Time;
                }

                foreach (var scaleKey in assChannel.ScalingKeys)
                {
                    channel.ScaleKeyframes.Add(new Keyframe<Vector3>
                    {
                        Time = (float)(scaleKey.Time / assAnimation.TicksPerSecond),
                        Value = new Vector3(scaleKey.Value.X, scaleKey.Value.Y, scaleKey.Value.Z),
                    });
                    if (channel.ScaleKeyframes.Last().Time > maxTime)
                        maxTime = channel.ScaleKeyframes.Last().Time;
                }

                animation.Channels.Add(assChannel.NodeName, channel);
            }
            animation.Name = assAnimation.Name;
            animation.Duration = maxTime;
            animations.Add(animation);
        }
        return animations;
    }


    private unsafe static Core.Nodes.Model processScene(Scene scene, string? directory, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {

        Dictionary<int, Core.Resources.Material> materialsMap = new();

        var model = new Core.Nodes.Model();

        processMaterial(scene, materialsMap, directory, loadTextureFunc);

        var skeleton = processSkeleton(scene);

        List<Core.Nodes.Mesh> meshes = [];

        processNodeMesh(scene, scene.RootNode, skeleton, materialsMap, meshes);

        foreach (var mesh in meshes)
        {
            model.AddChild(mesh, AttachToParentRule.KeepLocal);
        }

        return model;
    }

    public unsafe static void processNodeMesh(Scene scene, Node assimpNode, Skeleton? skeleton, Dictionary<int, Core.Resources.Material> materialsMap, List<Core.Nodes.Mesh> meshes)
    {
        if (assimpNode.HasMeshes)
        {
            foreach (var meshIndex in assimpNode.MeshIndices)
            {
                var assimpMesh = scene.Meshes[meshIndex];

                var mesh = processMesh(scene, assimpMesh, skeleton, materialsMap);

                mesh.Name = assimpMesh.Name;

                mesh.WorldTransform = GetWorldTransform(assimpNode);

                meshes.Add(mesh);
            }
        }
        foreach (var child in assimpNode.Children)
        {
            processNodeMesh(scene, child, skeleton, materialsMap, meshes);
        }

    }

    private static Matrix4x4 GetWorldTransform(Node assimpNode)
    {
        if (assimpNode.Parent == null)
            return Matrix4x4.Transpose(assimpNode.Transform);
        else
            return Matrix4x4.Transpose(assimpNode.Transform) * GetWorldTransform(assimpNode.Parent);
    }

    private static unsafe void processMaterial(Scene scene, Dictionary<int, Core.Resources.Material> materialsMap, string? path, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {
        for (int i = 0; i < scene.MaterialCount; i++)
        {
            var material = new Core.Resources.Material();

            var assimpMaterial = scene.Materials[i];

            if (assimpMaterial.HasTwoSided)
            {
                material.DoubleSided = assimpMaterial.IsTwoSided;
            }

            if (assimpMaterial.BlendMode == Assimp.BlendMode.Default)
            {
                material.BlendMode = Core.Resources.BlendMode.Opaque;
            }

            Core.Resources.Texture? texture = null;

            if (assimpMaterial.HasTextureDiffuse && assimpMaterial.TextureDiffuse.FilePath != null)
            {
                var slot = assimpMaterial.TextureDiffuse;
                texture = processTexture(scene, slot, path, loadTextureFunc);


            }
            else if (assimpMaterial.PBR.HasTextureBaseColor && assimpMaterial.PBR.TextureBaseColor.FilePath != null)
            {
                var slot = assimpMaterial.PBR.TextureBaseColor;
                texture = processTexture(scene, slot, path, loadTextureFunc);

            }
            else if (assimpMaterial.HasColorDiffuse)
            {
                texture = Texture.CreateFromColor(Color.FromArgb((int)(assimpMaterial.ColorDiffuse.X * 255), (int)(assimpMaterial.ColorDiffuse.Y * 255), (int)(assimpMaterial.ColorDiffuse.Z * 255)));
            }

            if (texture == null)
            {
                texture = Texture.CreateFromColor(Color.White);
            }

            texture.IsGammaSpace = true;

            material.SetChannel(new Channel
            {
                Name = "BaseColor",
                Texture = texture,
            });

            Core.Resources.Texture? normalTexture = null;
            if (assimpMaterial.HasTextureNormal && assimpMaterial.TextureNormal.FilePath != null)
            {
                var slot = assimpMaterial.TextureNormal;
                normalTexture = processTexture(scene, slot, path, loadTextureFunc);
            }

            if (normalTexture == null)
            {
                normalTexture = Texture.CreateFromColor(Color.FromArgb(255, 128, 128, 255));
            }

            material.SetChannel(new Channel
            {
                Name = "Normal",
                Texture = normalTexture,
            });
            materialsMap.Add(i, material);
        }
    }


    private static unsafe Core.Resources.Texture? processTexture(Scene scene, TextureSlot textureSlot, string? path, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {
        EmbeddedTexture? assimpTexture = null;

        if (textureSlot.FilePath[0] == '*')
        {
            assimpTexture = scene.Textures[int.Parse(textureSlot.FilePath.Skip(1).Take(textureSlot.FilePath.Length - 1).ToArray())];
        }
        else if (scene.TextureCount > 0)
        {
            assimpTexture = scene.Textures.FirstOrDefault(texture => texture.Filename == textureSlot.FilePath);
        }
        if (assimpTexture != null)
        {
            if (assimpTexture.IsCompressed == true)
            {
                StbImage.stbi_set_flip_vertically_on_load_thread(1);
                try
                {
                    return TextureLoader.LoadTexture(assimpTexture.CompressedData);

                }
                finally
                {
                    StbImage.stbi_set_flip_vertically_on_load_thread(0);
                }
            }
            else if (assimpTexture.HasNonCompressedData)
            {

                var texture = new Core.Resources.Texture();


                List<byte> data = [];
                for (int i = 0; i < assimpTexture.NonCompressedDataSize; i++)
                {
                    data.Add(assimpTexture.NonCompressedData[i].R);
                    data.Add(assimpTexture.NonCompressedData[i].G);
                    data.Add(assimpTexture.NonCompressedData[i].B);
                    data.Add(assimpTexture.NonCompressedData[i].A);
                }
                texture.SetColorFormat(ColorFormat.RGBA);
                texture.SetLdrData(CollectionsMarshal.AsSpan(data), (uint)assimpTexture.Width, (uint)assimpTexture.Height);
                return texture;
            }
            else
            {
                throw new InvalidDataException("Embedded texture has neither compressed nor non-compressed data.");
            }
        }
        else
        {
            if (loadTextureFunc != null)
            {
                return loadTextureFunc(textureSlot.FilePath);
            }
            else if (path != null)
            {

                var filePath = Path.Combine(path, textureSlot.FilePath);

                StbImage.stbi_set_flip_vertically_on_load_thread(1);
                try
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        return TextureLoader.LoadTexture(stream);
                    }
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                finally
                {

                    StbImage.stbi_set_flip_vertically_on_load_thread(0);
                }
            }
            else
            {
                return null;
            }
        }

    }

    private static unsafe Core.Nodes.Mesh processMesh(Scene scene, Assimp.Mesh assimpMesh, Skeleton? skeleton, Dictionary<int, Core.Resources.Material> materialMap)
    {
        var mesh = new Core.Nodes.Mesh();

        var geometry = new Geometry();

        geometry.PrimitiveType = assimpMesh.PrimitiveType switch
        {
            Assimp.PrimitiveType.Point => Aura3D.Core.Resources.PrimitiveType.Points,
            Assimp.PrimitiveType.Line => Aura3D.Core.Resources.PrimitiveType.Lines,
            _ => Aura3D.Core.Resources.PrimitiveType.Triangles,
        };

        List<float> positions = new List<float>();
        List<float> normals = new List<float>();
        List<float> uvs = new List<float>();
        List<float> bones = new List<float>();
        List<float> boneWeight = new List<float>();
        List<float> colors = new List<float>();

        for (int j = 0; j < assimpMesh.VertexCount; j++)
        {
            var vertex = assimpMesh.Vertices[j];
            positions.Add(vertex.X);
            positions.Add(vertex.Y);
            positions.Add(vertex.Z);

            if (assimpMesh.HasNormals)
            {
                normals.Add(assimpMesh.Normals[j].X);
                normals.Add(assimpMesh.Normals[j].Y);
                normals.Add(assimpMesh.Normals[j].Z);
            }

            if (assimpMesh.HasTextureCoords(0))
            {

                uvs.Add(assimpMesh.TextureCoordinateChannels[0][j].X);
                uvs.Add(assimpMesh.TextureCoordinateChannels[0][j].Y);
            }

            if (assimpMesh.HasVertexColors(0))
            {
                colors.Add(assimpMesh.VertexColorChannels[0][j].X);
                colors.Add(assimpMesh.VertexColorChannels[0][j].Y);
                colors.Add(assimpMesh.VertexColorChannels[0][j].Z);
                colors.Add(assimpMesh.VertexColorChannels[0][j].W);
            }

        }

        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);

        if (assimpMesh.HasTextureCoords(0))
        {
            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);
        }
        if (assimpMesh.HasNormals)
        {
            geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
        }
        if (assimpMesh.HasVertexColors(0))
        {
            geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);
        }

        List<uint> indices = [];

        ;
        foreach (var index in assimpMesh.GetIndices())
        {
            indices.Add((uint)index);
        }
        geometry.SetIndices(indices);


        if (assimpMesh.HasNormals && assimpMesh.HasTextureCoords(0))
        {
            ModelHelper.CalcVerticsTbn(geometry.Indices, normals, uvs, out var tangents, out var bitangents);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);
        }


        mesh.Geometry = geometry;

        mesh.Material = materialMap[assimpMesh.MaterialIndex];


        if (assimpMesh.HasBones && skeleton != null)
        {

            List<float> joints = new List<float>(new float[4 * assimpMesh.VertexCount]);
            List<float> weights = new List<float>(new float[4 * assimpMesh.VertexCount]);
            int[] len = new int[assimpMesh.VertexCount];
            foreach (var bone in assimpMesh.Bones)
            {
                var id = skeleton.GetBoneIndex(bone.Name);
                if (id < 0)
                    throw new InvalidOperationException($"Cannot find skeleton bone: {bone.Name}");

                foreach (var vertexWeight in bone.VertexWeights)
                {
                    joints[vertexWeight.VertexID * 4 + len[vertexWeight.VertexID]] = id;
                    weights[vertexWeight.VertexID * 4 + len[vertexWeight.VertexID]] = vertexWeight.Weight;
                    len[vertexWeight.VertexID]++;
                }
            }
            geometry.SetVertexAttribute(BuildInVertexAttribute.Joints_0, 4, joints);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Weights_0, 4, weights);
        }
        return mesh;
    }


    private static void processBoneNode(Assimp.Node assimpNode, Dictionary<string, Core.Resources.Bone> boneMap)
    {
        if (assimpNode.Parent != null && assimpNode.HasMeshes == false)
        {

            var bone = new Core.Resources.Bone();
            bone.Name = assimpNode.Name;
            bone.Index = boneMap.Count;
            bone.LocalMatrix = Matrix4x4.Transpose(assimpNode.Transform);
            if (boneMap.Count > 0)
            {
                var parentName = assimpNode.Parent.Name;
                var parentNode = boneMap[parentName];
                bone.Parent = parentNode;
                bone.Parent.Children.Add(bone);
            }
            boneMap.Add(assimpNode.Name, bone);

        }

        foreach (var child in assimpNode.Children)
        {
            processBoneNode(child, boneMap);
        }
    }

    private static void processBoneNode2(Assimp.Node assimpNode, Dictionary<string, Core.Resources.Bone> boneMap)
    {
        if (boneMap.TryGetValue(assimpNode.Name, out var bone))
        {

            foreach (var child in assimpNode.Children)
            {
                if (boneMap.TryGetValue(child.Name, out var childBone))
                {
                    bone.Children.Add(childBone);
                    childBone.Parent = bone;
                }
            }
        }

        foreach (var child in assimpNode.Children)
        {
            processBoneNode2(child, boneMap);
        }

    }
    public static Skeleton? processSkeleton(Scene scene)
    {
        Dictionary<string, Core.Resources.Bone> boneMap = [];

        foreach (var mesh in scene.Meshes)
        {
            if (mesh.HasBones)
            {
                foreach (var assiBone in mesh.Bones)
                {
                    if (boneMap.ContainsKey(assiBone.Name))
                        continue;
                    var bone = new Core.Resources.Bone();
                    bone.Name = assiBone.Name;
                    bone.InverseWorldMatrix = Matrix4x4.Transpose(assiBone.OffsetMatrix);
                    bone.WorldMatrix = bone.InverseWorldMatrix.Inverse();
                    bone.Index = boneMap.Count;
                    boneMap.Add(assiBone.Name, bone);
                }
            }
        }

        /*
        processBoneNode(scene.RootNode, boneMap);
        */


        if (boneMap.Count == 0)
            return null;

        processBoneNode2(scene.RootNode, boneMap);

        var skeleton = new Skeleton();

        foreach (var (name, bone) in boneMap)
        {
            if (bone.Parent == null)
            {
                skeleton.Root = bone;
                bone.LocalMatrix = bone.WorldMatrix;
                // bone.WorldMatrix = bone.LocalMatrix;
            }
            else
            {
                bone.LocalMatrix = bone.WorldMatrix * bone.Parent.InverseWorldMatrix;
                //bone.WorldMatrix = bone.LocalMatrix * bone.Parent.WorldMatrix;
            }
            // bone.InverseWorldMatrix = bone.WorldMatrix.Inverse();

            skeleton.Bones.Add(bone);

        }

        return skeleton;
    }


}
