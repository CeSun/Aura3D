using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using SharpGLTF.IO;
using SharpGLTF.Schema2;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Material = Aura3D.Core.Resources.Material;
using Mesh = Aura3D.Core.Nodes.Mesh;
using Node = Aura3D.Core.Nodes.Node;
using Texture = Aura3D.Core.Resources.Texture;
using TextureWrapMode = Aura3D.Core.Resources.TextureWrapMode;


namespace Aura3D.Model;

public static class ModelLoader
{

    static Dictionary<Type, Func<MaterialExtensionLoaderBase>> _materialExtensionFactories = new();

    public static void RegisterMaterialExtension<T1>(Func<MaterialExtensionLoaderBase> factory) where T1: JsonSerializable
    {
        _materialExtensionFactories[typeof(T1)] = factory;
    }

    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadGlbModelAndAnimations(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        var model = processModelRoot(modelRoot);

        if (model.Skeleton == null)
            return (model, []);

        var animations = processAnimations(modelRoot);

        foreach(var animation in animations)
        {
            animation.Skeleton = model.Skeleton;
        }
        return (model, animations);
    }


    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadGlbModelAndAnimations(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        {
            return LoadGlbModelAndAnimations(stream);
        }
    }

    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadGltfModelAndAnimations(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        var model = processModelRoot(modelRoot);

        if (model.Skeleton == null)
            return (model, []);

        var animations = processAnimations(modelRoot);

        foreach (var animation in animations)
        {
            animation.Skeleton = model.Skeleton;
        }
        return (model, animations);
    }

    public static Core.Nodes.Model LoadGlbModel(Stream stream)
    {
        
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        return processModelRoot(modelRoot);
    }


    public static Core.Nodes.Model LoadGlbModel(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        {
            return LoadGlbModel(stream);
        }
    }

    public static Core.Nodes.Model LoadGltfModel(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        return processModelRoot(modelRoot);

    }

    public static List<Core.Resources.Animation> LoadGltfAnimations(string filePath, Skeleton? skeleton = null)
    {
        var modelRoot = ModelRoot.Load(filePath);

        if (skeleton == null)
            skeleton = processSkeleton(modelRoot);

        if (skeleton == null)
            return [];

        var animations = processAnimations(modelRoot);

        foreach(var animation in animations)
        {
            animation.Skeleton = skeleton;
        }

        return animations;
    }


    public static List<Core.Resources.Animation> LoadGlbAnimations(string filePath, Skeleton? skeleton = null)
    {
        using (var stream = File.OpenRead(filePath))
        {
            return LoadGlbAnimations(stream);
        }
    }
    public static List<Core.Resources.Animation> LoadGlbAnimations(Stream stream, Skeleton? skeleton = null)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        if (skeleton == null)
            skeleton = processSkeleton(modelRoot);

        if (skeleton == null)
            return [];

        var animations = processAnimations(modelRoot);

        foreach (var animation in animations)
        {
            animation.Skeleton = skeleton;
        }

        return animations;

    }


    private static List<Core.Resources.Animation> processAnimations(ModelRoot modelRoot)
    {
        var list = new List<Core.Resources.Animation>();

        foreach (var gltfAnimation in modelRoot.LogicalAnimations)
        {
            var animation = new Core.Resources.Animation();

            animation.Name = gltfAnimation.Name;
            animation.Duration = gltfAnimation.Duration;

            foreach (var channel in gltfAnimation.Channels)
            {
                if (animation.Channels.TryGetValue(channel.TargetNode.Name, out var animationChannel) == false)
                {
                    animationChannel = new Core.Resources.AnimationChannel();
                    animation.Channels[channel.TargetNode.Name] = animationChannel;

                }


                switch (channel.TargetNodePath)
                {
                    case PropertyPath.translation:
                        {

                            var keys = channel.GetTranslationSampler().GetLinearKeys();

                            foreach (var key in keys)
                            {
                                animationChannel.PositionKeyframes.Add(new Keyframe<Vector3>
                                {
                                    Time = key.Key,
                                    Value = key.Value,
                                });
                            }
                        }
                        break;
                    case PropertyPath.rotation:
                        {
                            var keys = channel.GetRotationSampler().GetLinearKeys();

                            foreach (var key in keys)
                            {
                                animationChannel.RotationKeyframes.Add(new Keyframe<Quaternion>
                                {
                                    Time = key.Key,
                                    Value = key.Value,
                                });
                            }
                        }
                        break;
                    case PropertyPath.scale:
                        {
                            var keys = channel.GetScaleSampler().GetLinearKeys();
                            foreach (var key in keys)
                            {
                                animationChannel.ScaleKeyframes.Add(new Keyframe<Vector3>
                                {
                                    Time = key.Key,
                                    Value = key.Value,
                                });
                            }
                        }
                        break;

                    default:
                        break;
                }

            }
        
            list.Add(animation);
        }

        return list;
    }
    private static Core.Nodes.Model processModelRoot(ModelRoot modelRoot)
    {
        var skeleton = processSkeleton(modelRoot);
        var model = new Core.Nodes.Model
        {
            Skeleton = skeleton,
            Name = modelRoot.DefaultScene.Name
        };

        var textureMap = LoadTextures(modelRoot);
        var materialMap = LoadMaterials(modelRoot, textureMap);

        foreach (var node in modelRoot.DefaultScene.VisualChildren)
        {
            processNode(node, model, materialMap, skeleton);
        }

        foreach (var mesh in model.Meshes)
        {
            mesh.Model = model;
        }

        return model;
    }

    private static Dictionary<SharpGLTF.Schema2.Texture, Texture> LoadTextures(ModelRoot modelRoot)
    {
        var textureMap = new Dictionary<SharpGLTF.Schema2.Texture, Texture>();

        foreach (var texture in modelRoot.LogicalTextures)
        {
            if (texture.PrimaryImage != null)
            {
                var data = texture.PrimaryImage.Content.Content;
                var tex = Core.TextureLoader.LoadTexture(data.ToArray());
                if (tex != null)
                {
                    textureMap[texture] = tex;
                }
            }
        }

        return textureMap;
    }

    private static Dictionary<SharpGLTF.Schema2.Material, Material> LoadMaterials(
        ModelRoot modelRoot,
        Dictionary<SharpGLTF.Schema2.Texture, Texture> textureMap)
    {
        var materialMap = new Dictionary<SharpGLTF.Schema2.Material, Material>();
        var channelMap = new Dictionary<MaterialChannel, Channel>();

        foreach (var material in modelRoot.LogicalMaterials)
        {
            if (materialMap.ContainsKey(material))
                continue;

            var mat = CreateMaterialFromGltf(material);
            LoadMaterialExtensions(modelRoot, material, mat);
            LoadMaterialChannels(material, mat, textureMap, channelMap);

            materialMap[material] = mat;
        }

        return materialMap;
    }

    private static Material CreateMaterialFromGltf(SharpGLTF.Schema2.Material material)
    {
        return new Material
        {
            AlphaCutoff = material.AlphaCutoff,
            DoubleSided = material.DoubleSided,
            BlendMode = material.Alpha switch
            {
                AlphaMode.OPAQUE => BlendMode.Opaque,
                AlphaMode.BLEND => BlendMode.Translucent,
                AlphaMode.MASK => BlendMode.Masked,
                _ => BlendMode.Opaque,
            }
        };
    }

    private static void LoadMaterialExtensions(
        ModelRoot modelRoot,
        SharpGLTF.Schema2.Material material,
        Material mat)
    {
        foreach (var ext in material.Extensions)
        {
            _materialExtensionFactories.TryGetValue(ext.GetType(), out var factory);
            if (factory == null)
                continue;

            MaterialExtensionLoaderBase materialExtension = factory();
            if (materialExtension == null)
                continue;

            materialExtension.LoadMaterialExtension(modelRoot, material, mat);
        }
    }

    private static void LoadMaterialChannels(
        SharpGLTF.Schema2.Material material,
        Material mat,
        Dictionary<SharpGLTF.Schema2.Texture, Texture> textureMap,
        Dictionary<MaterialChannel, Channel> channelMap)
    {
        foreach (var gltfChannel in material.Channels)
        {
            if (channelMap.TryGetValue(gltfChannel, out var existingChannel))
            {
                mat.SetChannel(existingChannel);
                continue;
            }

            var channel = CreateChannelFromGltf(gltfChannel, textureMap);
            mat.SetChannel(channel);
            channelMap[gltfChannel] = channel;
        }
    }

    private static Channel CreateChannelFromGltf(
        MaterialChannel gltfChannel,
        Dictionary<SharpGLTF.Schema2.Texture, Texture> textureMap)
    {
        Texture? texture = null;
        if (gltfChannel.Texture != null && textureMap.TryGetValue(gltfChannel.Texture, out var mappedTexture))
        {
            texture = mappedTexture;
            ConfigureTextureForChannel(gltfChannel.Key, texture, gltfChannel);
        }
        else
        {
            texture = TryCreateFallbackTexture(gltfChannel);
        }

        return new Channel
        {
            Name = gltfChannel.Key,
            Texture = texture
        };
    }

    private static void ConfigureTextureForChannel(
        string channelName,
        Texture texture,
        MaterialChannel gltfChannel)
    {
        if (channelName == "BaseColor")
        {
            texture.SetIsGammaSpace(true);
        }

        if (channelName == "MetallicRoughness")
        {
            ApplyMetallicRoughnessFactors(texture, gltfChannel);
        }

        if (gltfChannel.TextureSampler != null)
        {
            ConfigureTextureSampler(texture, gltfChannel.TextureSampler);
        }
    }

    private static void ApplyMetallicRoughnessFactors(Texture texture, MaterialChannel gltfChannel)
    {
        var metallicFactor = gltfChannel.GetFactor("MetallicFactor");
        var roughnessFactor = gltfChannel.GetFactor("RoughnessFactor");

        int step = texture.ColorFormat == ColorFormat.RGB ? 3 : 4;
        if (texture.IsHdr == true)
        {
            var data = texture.AsHdrData().ToArray();
            for (int i = 0; i < texture.Width * texture.Height * step; i += step)
            {
                var r = data[i + 2];
                data[i + 2] = r * metallicFactor;
                var g = data[i + 1];
                data[i + 1] = g * roughnessFactor;
            }
            texture.SetHdrData(data, texture.Width, texture.Height);
        }
        else
        {
            var data = texture.AsLdrData().ToArray();
            for (int i = 0; i < texture.Width * texture.Height * step; i += step)
            {
                var r = data[i + 2];
                data[i + 2] = (byte)(r * metallicFactor);

                var g = data[i + 1];
                data[i + 1] = (byte)(g * roughnessFactor);
            }
            texture.SetLdrData(data, texture.Width, texture.Height);
        }
    }

    private static void ConfigureTextureSampler(Texture texture, TextureSampler sampler)
    {
        texture.SetWarpS(sampler.WrapS switch
        {
            SharpGLTF.Schema2.TextureWrapMode.REPEAT => TextureWrapMode.Repeat,
            SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => TextureWrapMode.ClampToEdge,
            SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => TextureWrapMode.MirroredRepeat,
            _ => TextureWrapMode.Repeat,
        });

        texture.SetWarpT(sampler.WrapT switch
        {
            SharpGLTF.Schema2.TextureWrapMode.REPEAT => TextureWrapMode.Repeat,
            SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => TextureWrapMode.ClampToEdge,
            SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => TextureWrapMode.MirroredRepeat,
            _ => TextureWrapMode.Repeat,
        });

        texture.SetMinFilter(sampler.MinFilter switch
        {
            TextureMipMapFilter.NEAREST => TextureFilterMode.Nearest,
            TextureMipMapFilter.LINEAR => TextureFilterMode.Linear,
            _ => TextureFilterMode.Linear,
        });

        texture.SetMagFilter(sampler.MagFilter switch
        {
            TextureInterpolationFilter.NEAREST => TextureFilterMode.Nearest,
            TextureInterpolationFilter.LINEAR => TextureFilterMode.Linear,
            _ => TextureFilterMode.Linear,
        });
    }

    private static Texture? TryCreateFallbackTexture(MaterialChannel gltfChannel)
    {
        try
        {
            return Texture.CreateFromColor(gltfChannel.Color.ToColor());
        }
        catch
        {
            // 忽略颜色创建失败的情况
            return null;
        }
    }


    private static Skeleton? processSkeleton(ModelRoot modelRoot)
    {
        var dict = new Dictionary<SharpGLTF.Schema2.Node, Skeleton>();


        foreach (var skin in modelRoot.LogicalSkins)
        {
            if (skin.JointsCount <= 0)
                continue;

            var root = skin.Joints[0];

            var skeleton = new Skeleton();

            Dictionary<string, Bone> boneMap = new();

            for (int i = 0; i < skin.Joints.Count; i++)
            {
                var joint = skin.Joints[i];
                skeleton.Bones.Add(new Bone
                {
                    Name = joint.Name,
                    Index = i,
                    InverseWorldMatrix = joint.WorldMatrix.Inverse(),
                    LocalMatrix = joint.LocalMatrix,
                    WorldMatrix = joint.WorldMatrix,
                });

                boneMap.Add(joint.Name, skeleton.Bones.Last());
            }

            processBone(skin.Joints[0], boneMap);

            foreach(var bone in skeleton.Bones)
            {
                bone.WorldMatrix = GetWorldMatrix(bone);
                bone.InverseWorldMatrix = bone.WorldMatrix.Inverse();
            }
            skeleton.Root = boneMap[skin.Joints[0].Name];

            dict[skin.Joints[0]] = skeleton;
        }

        if (dict.Count > 0)
        {
            return dict.Values.First();
        }
        return null;
    }

    private static Matrix4x4 GetWorldMatrix(Bone bone)
    {
        if (bone.Parent == null)
            return bone.LocalMatrix;
        return bone.LocalMatrix * GetWorldMatrix(bone.Parent);
    }
    private static void processBone(SharpGLTF.Schema2.Node node, Dictionary<string, Bone> boneMap)
    {
        if (boneMap.TryGetValue(node.Name, out var bone))
        {
            foreach (var child in node.VisualChildren)
            {
                if (boneMap.TryGetValue(child.Name, out var childBone))
                {
                    bone.Children.Add(childBone);

                    childBone.Parent = bone;
                }
            }
        }

        foreach (var child in node.VisualChildren)
        {
            processBone(child, boneMap);
        }

    }
    private static void processNode(SharpGLTF.Schema2.Node node, Node parent, Dictionary<SharpGLTF.Schema2.Material, Material> materialMap, Skeleton? skeleton)
    {
        var currentNode = CreateNodeFromGltf(node);
        parent.AddChild(currentNode, AttachToParentRule.KeepLocal);

        if (node.Mesh != null)
        {
            foreach (var primitive in node.Mesh.Primitives)
            {
                ProcessMeshPrimitive(primitive, currentNode, node, materialMap, skeleton);
            }
        }

        foreach (var child in node.VisualChildren)
        {
            processNode(child, currentNode, materialMap, skeleton);
        }
    }

    private static Node CreateNodeFromGltf(SharpGLTF.Schema2.Node node)
    {
        return new Node
        {
            Name = node.Name,
            LocalTransform = node.LocalMatrix
        };
    }

    private static void ProcessMeshPrimitive(
        MeshPrimitive primitive,
        Node parentNode,
        SharpGLTF.Schema2.Node gltfNode,
        Dictionary<SharpGLTF.Schema2.Material, Material> materialMap,
        Skeleton? skeleton)
    {
        var mesh = new Mesh
        {
            LocalTransform = Matrix4x4.Identity,
            Name = parentNode.Name
        };

        parentNode.AddChild(mesh, AttachToParentRule.KeepLocal);

        var geometry = BuildGeometryFromPrimitive(primitive, gltfNode, skeleton);
        mesh.Geometry = geometry;

        if (primitive.Material != null)
        {
            materialMap.TryGetValue(primitive.Material, out var material);
            mesh.Material = material;
        }
    }

    private static Geometry BuildGeometryFromPrimitive(MeshPrimitive primitive, SharpGLTF.Schema2.Node node, Skeleton? skeleton)
    {
        var geometry = new Geometry();

        // SharpGLTF 不直接暴露 DrawMode，通过 Get*Indices 方法推断原始图元类型
        var pointIndices = primitive.GetPointIndices();
        var lineIndices = primitive.GetLineIndices();
        if (pointIndices.Any())
            geometry.PrimitiveType = Aura3D.Core.Resources.PrimitiveType.Points;
        else if (lineIndices.Any())
            geometry.PrimitiveType = Aura3D.Core.Resources.PrimitiveType.Lines;
        // 默认为 Triangles

        foreach (var (name, accessor) in primitive.VertexAccessors)
        {
            ProcessVertexAttribute(geometry, name, primitive, node, skeleton);
        }

        geometry.SetIndices(primitive.GetIndices().ToList());
        CalculateTangentsAndBitangents(geometry);

        return geometry;
    }

    private static void ProcessVertexAttribute(Geometry geometry, string name, MeshPrimitive primitive, SharpGLTF.Schema2.Node node, Skeleton? skeleton)
    {
        var columns = primitive.GetVertexColumns();

        switch (name)
        {
            case "POSITION":
                geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, columns.Positions.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                break;
            case "TEXCOORD_0":
                geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, columns.TexCoords0.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                break;
            case "TEXCOORD_1":
                geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_1, 2, columns.TexCoords1.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                break;
            case "TEXCOORD_2":
                geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_2, 2, columns.TexCoords2.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                break;
            case "TEXCOORD_3":
                geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_3, 2, columns.TexCoords3.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                break;
            case "COLOR_0":
                geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, columns.Colors0.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                break; 
            case "NORMAL":
                geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, columns.Normals.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                break;
            case "JOINTS_0":
                ProcessJointsAttribute(geometry, BuildInVertexAttribute.Joints_0, columns.Joints0, node, skeleton);
                break;
            case "JOINTS_1":
                ProcessJointsAttribute(geometry, BuildInVertexAttribute.Joints_1, columns.Joints1, node, skeleton);
                break;
            case "WEIGHTS_0":
                geometry.SetVertexAttribute(BuildInVertexAttribute.Weights_0, 4, columns.Weights0.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                break;
            case "WEIGHTS_1":
                geometry.SetVertexAttribute(BuildInVertexAttribute.Weights_1, 4, columns.Weights1.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                break;
        }
    }

    private static void ProcessJointsAttribute(Geometry geometry, BuildInVertexAttribute attribute, IEnumerable<Vector4> joints, SharpGLTF.Schema2.Node node, Skeleton? skeleton)
    {
        if (skeleton == null)
            return;

        // 预建 GLTF 骨骼名称到 Skeleton 索引的映射，避免重复 LINQ 查询
        var skinJointToSkeletonIndex = new Dictionary<string, int>(node.Skin!.Joints.Count);
        foreach (var joint in node.Skin.Joints)
        {
            skinJointToSkeletonIndex[joint.Name] = skeleton.GetBoneIndex(joint.Name);
        }

        geometry.SetVertexAttribute(attribute, 4, joints.SelectMany(v =>
        {
            if (node.Skin == null)
                return new float[] { v.X, v.Y, v.Z, v.W };

            // 使用预建的映射，O(1) 查找
            skinJointToSkeletonIndex.TryGetValue(node.Skin.Joints[(int)v.X].Name, out var x);
            skinJointToSkeletonIndex.TryGetValue(node.Skin.Joints[(int)v.Y].Name, out var y);
            skinJointToSkeletonIndex.TryGetValue(node.Skin.Joints[(int)v.Z].Name, out var z);
            skinJointToSkeletonIndex.TryGetValue(node.Skin.Joints[(int)v.W].Name, out var w);

            return new float[] { x, y, z, w };
        }).ToList());
    }

    private static void CalculateTangentsAndBitangents(Geometry geometry)
    {
        var normals = geometry.GetAttributeData(BuildInVertexAttribute.Normal);
        var uvs = geometry.GetAttributeData(BuildInVertexAttribute.TexCoord_0);

        if (normals != null && uvs != null)
        {
            ModelHelper.CalcVerticsTbn(geometry.Indices, normals, uvs, out var tangents, out var bitangents);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);
        }
    }

}
