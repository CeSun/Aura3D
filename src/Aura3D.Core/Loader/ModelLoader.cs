using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using SharpGLTF.Schema2;
using System.IO;
using System.Numerics;
using Material = Aura3D.Core.Resources.Material;
using Mesh = Aura3D.Core.Nodes.Mesh;
using Node = Aura3D.Core.Nodes.Node;
using Texture = Aura3D.Core.Resources.Texture;
using TextureWrapMode = Aura3D.Core.Resources.TextureWrapMode;

namespace Aura3D.Core;

public static class ModelLoader
{

    public static (SkinnedModel, List<Resources.Animation>) LoadGlbModelAndAnimations(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        var model = processModelRoot(modelRoot);

        if (model is not SkinnedModel skinnedModel)
            throw new Exception("The model is not a skinned model.");

        var animations = processAnimations(modelRoot);

        foreach(var animation in animations)
        {
            animation.Skeleton = skinnedModel.Skeleton;
        }
        return (skinnedModel, animations);
    }


    public static (SkinnedModel, List<Resources.Animation>) LoadGlbModelAndAnimations(string filePath)
    {
        using (var streamReader = new StreamReader(filePath))
        {
            return LoadGlbModelAndAnimations(streamReader.BaseStream);
        }
    }

    public static (SkinnedModel, List<Resources.Animation>) LoadGltfModelAndAnimations(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        var model = processModelRoot(modelRoot);

        if (model is not SkinnedModel skinnedModel)
            throw new Exception("The model is not a skinned model.");

        var animations = processAnimations(modelRoot);

        foreach (var animation in animations)
        {
            animation.Skeleton = skinnedModel.Skeleton;
        }
        return (skinnedModel, animations);
    }

    public static Model LoadGlbModel(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        return processModelRoot(modelRoot);
    }


    public static Model LoadGlbModel(string filePath)
    {
        using (var streamReader = new StreamReader(filePath))
        {
            return LoadGlbModel(streamReader.BaseStream);
        }
    }

    public static Model LoadGltfModel(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        return processModelRoot(modelRoot);

    }

    public static List<Resources.Animation> LoadGltfAnimations(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        return processAnimations(modelRoot);
    }


    public static List<Resources.Animation> LoadGlbAnimations(string filePath)
    {
        using (var sr = new StreamReader(filePath))
        {
            return LoadGlbAnimations(sr.BaseStream);
        }
    }
    public static List<Resources.Animation> LoadGlbAnimations(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        return processAnimations(modelRoot);

    }


    private static List<Resources.Animation> processAnimations(ModelRoot modelRoot)
    {
        var list = new List<Resources.Animation>();

        foreach (var gltfAnimation in modelRoot.LogicalAnimations)
        {
            var animation = new Resources.Animation();

            animation.Name = gltfAnimation.Name;
            animation.Duration = gltfAnimation.Duration;

            foreach (var channel in gltfAnimation.Channels)
            {
                if (animation.Channels.TryGetValue(channel.TargetNode.Name, out var animationChannel) == false)
                {
                    animationChannel = new Resources.AnimationChannel();
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
    private static Model processModelRoot(ModelRoot modelRoot)
    {
        Model? model = null;

        var skeletonMap = processSkeleton(modelRoot);

        if (skeletonMap.Count > 0)
        {
            var skinnedModel = new SkinnedModel();

            skinnedModel.Skeleton = skeletonMap.Values.First();

            model = skinnedModel;
        }
        else
        {
            model = new Model();
        }

        model.Name = modelRoot.DefaultScene.Name;

        Dictionary<SharpGLTF.Schema2.Texture, Texture> textureMap = new();

        Dictionary<SharpGLTF.Schema2.Material, Material> materialMap = new();

        Dictionary<MaterialChannel, Channel> channelMap = new();

        foreach (var texture in modelRoot.LogicalTextures)
        {
            if (texture.PrimaryImage != null)
            {
                var data = texture.PrimaryImage.Content.Content;
                var tex = TextureLoader.LoadTexture(data.ToArray());
                if (tex != null)
                {
                    textureMap[texture] = tex;
                }
            }
        }

        foreach (var material in modelRoot.LogicalMaterials)
        {
            if (materialMap.ContainsKey(material))
                continue;
            var mat = new Material();

            mat.AlphaCutoff = material.AlphaCutoff;
            mat.DoubleSided = material.DoubleSided;
            mat.BlendMode = material.Alpha switch
            {
                AlphaMode.OPAQUE => BlendMode.Opaque,
                AlphaMode.BLEND => BlendMode.Translucent,
                AlphaMode.MASK => BlendMode.Masked,
                _ => BlendMode.Opaque,
            };

            foreach (var gltfChannel in material.Channels)
            {
                if (channelMap.TryGetValue(gltfChannel, out var channel))
                {
                    mat.Channels.Add(channel);
                    continue;
                }
                channel = new Channel();
                channel.Name = gltfChannel.Key;
                try
                {
                    channel.Color = gltfChannel.Color.ToColor();
                }
                catch
                {
                }
                if (gltfChannel.Texture != null && textureMap.ContainsKey(gltfChannel.Texture))
                {
                    channel.Texture = textureMap[gltfChannel.Texture];
                    if (channel.Name == "BaseColor")
                    {
                        var texture = (Texture)
                        channel.Texture;

                        texture.SetIsGammaSpace(true);
                    }

                    if (gltfChannel.TextureSampler != null)
                    {
                        if (channel.Texture != null && channel.Texture is Texture texture)
                        {
                            texture.SetWarpS(gltfChannel.TextureSampler.WrapS switch
                            {
                                SharpGLTF.Schema2.TextureWrapMode.REPEAT => TextureWrapMode.Repeat,
                                SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => TextureWrapMode.ClampToEdge,
                                SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => TextureWrapMode.MirroredRepeat,
                                _ => TextureWrapMode.Repeat,
                            });

                            texture.SetWarpT(gltfChannel.TextureSampler.WrapT switch
                            {
                                SharpGLTF.Schema2.TextureWrapMode.REPEAT => TextureWrapMode.Repeat,
                                SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => TextureWrapMode.ClampToEdge,
                                SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => TextureWrapMode.MirroredRepeat,
                                _ => TextureWrapMode.Repeat,
                            });

                            texture.SetMinFilter(gltfChannel.TextureSampler.MinFilter switch
                            {
                                TextureMipMapFilter.NEAREST => TextureFilterMode.Nearest,
                                TextureMipMapFilter.LINEAR => TextureFilterMode.Linear,
                                _ => TextureFilterMode.Linear,
                            });


                            texture.SetMagFilter(gltfChannel.TextureSampler.MagFilter switch
                            {
                                TextureInterpolationFilter.NEAREST => TextureFilterMode.Nearest,
                                TextureInterpolationFilter.LINEAR => TextureFilterMode.Linear,
                                _ => TextureFilterMode.Linear,
                            });
                        }

                    }
                }
                mat.Channels.Add(channel);
                channelMap[gltfChannel] = channel;
            }

            materialMap[material] = mat;

        }

        foreach (var node in modelRoot.DefaultScene.VisualChildren)
        {
            processNode(node, model, materialMap, skeletonMap);
        }


        foreach(var mesh in model.Meshes)
        {
            mesh.Model = model;

            if (mesh is SkinnedMesh skinnedMesh && model is SkinnedModel skinnedModel)
            {
                skinnedMesh.SkinnedModel = skinnedModel;
            }
        }

        return model;
    }


    private static Dictionary<SharpGLTF.Schema2.Node, Skeleton> processSkeleton(ModelRoot modelRoot)
    {
        var dict = new Dictionary<SharpGLTF.Schema2.Node, Skeleton>();


        foreach (var skin in modelRoot.LogicalSkins)
        {
            if (skin.Skeleton == null)
                continue;

            if (dict.ContainsKey(skin.Skeleton))
                continue;

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
            processBone(skin.Skeleton, boneMap);

            foreach(var bone in skeleton.Bones)
            {
                bone.WorldMatrix = GetWorldMatrix(bone);
                bone.InverseWorldMatrix = bone.WorldMatrix.Inverse();
            }
            skeleton.Root = boneMap[skin.Skeleton.Name];

            dict[skin.Skeleton] = skeleton;
        }

        return dict;
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
    private static void processNode(SharpGLTF.Schema2.Node node, Node parent, Dictionary<SharpGLTF.Schema2.Material, Material> materialMap, Dictionary<SharpGLTF.Schema2.Node, Skeleton> skeletonMap)
    {
        Node? currentNode = new Node();

        currentNode.Name = node.Name;

        parent.AddChild(currentNode);

        currentNode.Transform = node.LocalMatrix;

        if (node.Mesh != null)
        {
            foreach (var primitive in node.Mesh.Primitives)
            {
                Mesh? mesh = null;

                if (node.Skin != null && node.Skin.Skeleton != null)
                {
                    var skinnedMesh = new SkinnedMesh();

                    if (skeletonMap.TryGetValue(node.Skin.Skeleton, out var skeleton))
                    {
                        skinnedMesh.Skeleton = skeleton;
                    }
                    mesh = skinnedMesh;
                }
                else
                {
                    mesh = new Mesh();
                }

                currentNode.AddChild(mesh);

                mesh.Transform = Matrix4x4.Identity;

                mesh.Name = node.Name;

                mesh.Geometry = new Geometry();



                foreach (var (name, accessor) in primitive.VertexAccessors)
                {
                    switch (name)
                    {
                        case "POSITION":
                            mesh.Geometry.SetVertexAttribute("position", 0, 3, primitive.GetVertexColumns().Positions.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                            break;
                        case "TEXCOORD_0":
                            mesh.Geometry.SetVertexAttribute("texcoord", 1, 2, primitive.GetVertexColumns().TexCoords0.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                            break;
                        case "NORMAL":
                            mesh.Geometry.SetVertexAttribute("normal", 2, 3, primitive.GetVertexColumns().Normals.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                            break;
                        case "JOINTS_0":
                            if (skeletonMap.Count == 0)
                                break;
                            mesh.Geometry.SetVertexAttribute("joints", 5, 4, primitive.GetVertexColumns().Joints0.SelectMany(v => {
                                if (node.Skin == null)
                                    return new float[] { v.X, v.Y, v.Z, v.W };
                                var x = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var y = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var z = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var w = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                return new float[]{ x, y, z, w};
                            }).ToList());
                            break;
                        case "WEIGHTS_0":
                            if (skeletonMap.Count == 0)
                                break;
                            mesh.Geometry.SetVertexAttribute("weights", 6, 4, primitive.GetVertexColumns().Weights0.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                            break;
                    }
                }

                mesh.Geometry.SetIndices(primitive.GetIndices().ToList());

                var normal = mesh.Geometry.GetAttributeData("normal");
                var uv = mesh.Geometry.GetAttributeData("texcoord");
                if (normal != null && uv != null)
                {
                    InitMeshTbn(mesh.Geometry.Indices, normal, uv, out var tangents, out var bitangents);
                    mesh.Geometry.SetVertexAttribute("tangents", 3, 3, tangents);
                    mesh.Geometry.SetVertexAttribute("bitangents", 4, 3, bitangents);
                }

                if (primitive.Material != null)
                {
                    materialMap.TryGetValue(primitive.Material, out var material);
                    mesh.Material = material;
                }
                else
                {

                }
            }
        }




        foreach (var child in node.VisualChildren)
        {
            processNode(child, currentNode, materialMap, skeletonMap);
        }

    }


    public static void InitMeshTbn(List<uint> indices, List<float> vertexNormals, List<float> uvs, out List<float> tangents, out List<float> bitangents)
    {
        tangents = [.. new float[vertexNormals.Count]];

        bitangents = [.. new float[vertexNormals.Count]];

        for (int i = 0; i < indices.Count - 2; i += 3)
        {
            var p1Index = (int)indices[i] * 3;
            var p2Index = (int)indices[i + 1] * 3;
            var p3Index = (int)indices[i + 2] * 3;

            var p1 = new Vector3(vertexNormals[p1Index], vertexNormals[p1Index + 1], vertexNormals[p1Index + 2]);
            var p2 = new Vector3(vertexNormals[p2Index], vertexNormals[p2Index + 1], vertexNormals[p2Index + 2]);
            var p3 = new Vector3(vertexNormals[p3Index], vertexNormals[p3Index + 1], vertexNormals[p3Index + 2]);

            var uv1 = new Vector2(uvs[p1Index / 3 * 2], uvs[p1Index / 3 * 2 + 1]);
            var uv2 = new Vector2(uvs[p2Index / 3 * 2], uvs[p2Index / 3 * 2 + 1]);
            var uv3 = new Vector2(uvs[p3Index / 3 * 2], uvs[p3Index / 3 * 2 + 1]);


            Vector3 edge1 = p2 - p1;
            Vector3 edge2 = p3 - p1;

            Vector2 deltaUv1 = uv2 - uv1;
            Vector2 deltaUv2 = uv3 - uv1;

            float f = 1.0f / (deltaUv1.X * deltaUv2.Y - deltaUv2.X * deltaUv1.Y);

            Vector3 tangent1;
            Vector3 bitangent1;

            tangent1.X = f * (deltaUv2.Y * edge1.X - deltaUv1.Y * edge2.X);
            tangent1.Y = f * (deltaUv2.Y * edge1.Y - deltaUv1.Y * edge2.Y);
            tangent1.Z = f * (deltaUv2.Y * edge1.Z - deltaUv1.Y * edge2.Z);
            tangent1 = Vector3.Normalize(tangent1);

            bitangent1.X = f * (-deltaUv2.X * edge1.X + deltaUv1.X * edge2.X);
            bitangent1.Y = f * (-deltaUv2.X * edge1.Y + deltaUv1.X * edge2.Y);
            bitangent1.Z = f * (-deltaUv2.X * edge1.Z + deltaUv1.X * edge2.Z);
            bitangent1 = Vector3.Normalize(bitangent1);

            tangents[(int)indices[i] * 3] = tangent1.X;
            tangents[(int)indices[i] * 3 + 1] = tangent1.Y;
            tangents[(int)indices[i] * 3 + 2] = tangent1.Z;

            tangents[(int)indices[i + 1] * 3] = tangent1.X;
            tangents[(int)indices[i + 1] * 3 + 1] = tangent1.Y;
            tangents[(int)indices[i + 1] * 3 + 2] = tangent1.Z;

            tangents[(int)indices[i + 2] * 3] = tangent1.X;
            tangents[(int)indices[i + 2] * 3 + 1] = tangent1.Y;
            tangents[(int)indices[i + 2] * 3 + 2] = tangent1.Z;

            bitangents[(int)indices[i] * 3] = bitangent1.X;
            bitangents[(int)indices[i] * 3 + 1] = bitangent1.Y;
            bitangents[(int)indices[i] * 3 + 2] = bitangent1.Z;
            bitangents[(int)indices[i + 1] * 3] = bitangent1.X;

            bitangents[(int)indices[i + 1] * 3 + 1] = bitangent1.Y;
            bitangents[(int)indices[i + 1] * 3 + 2] = bitangent1.Z;

            bitangents[(int)indices[i + 2] * 3] = bitangent1.X;
            bitangents[(int)indices[i + 2] * 3 + 1] = bitangent1.Y;
            bitangents[(int)indices[i + 2] * 3 + 2] = bitangent1.Z;
        }
    }

}
