using Aura3D.Core;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model.Exceptions;
using StbImageSharp;
using System.Drawing;
using System.Numerics;
using Ufbx.NET;

namespace Aura3D.Model;

/// <summary>
/// Imports FBX assets through ufbx. Unlike <c>AssimpLoader</c> this has no native
/// dependency, so it also runs on browser/wasm targets.
/// </summary>
public static class FbxLoader
{
    /// <summary>
    /// Loads the model at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">File path of the FBX asset.</param>
    /// <param name="loadTextureFunc">Optional hook for resolving external texture references.</param>
    public static Core.Nodes.Model Load(string path, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        var scene = LoadSceneFile(path);

        try
        {
            return ProcessScene(scene, Path.GetDirectoryName(path), loadTextureFunc);
        }
        finally
        {
            UfbxApi.FreeScene(scene);
        }
    }

    /// <summary>
    /// Loads the model from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Stream holding the FBX asset; ufbx sniffs the format from the content.</param>
    /// <param name="extension">Unused, kept for signature parity with the other loaders.</param>
    /// <param name="loadTextureFunc">Optional hook for resolving external texture references.</param>
    public static Core.Nodes.Model Load(Stream stream, string? extension = null, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        var scene = LoadSceneStream(stream);

        try
        {
            return ProcessScene(scene, null, loadTextureFunc);
        }
        finally
        {
            UfbxApi.FreeScene(scene);
        }
    }

    /// <summary>
    /// Loads only the animation clips at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">File path of the FBX asset.</param>
    /// <param name="skeleton">
    /// Skeleton the clips should bind to. Animation-only FBX files carry no skin clusters, so a
    /// skeleton taken from the matching model file has to be passed in for those.
    /// </param>
    public static List<Core.Resources.Animation> LoadAnimations(string path, Skeleton? skeleton = null)
    {
        var scene = LoadSceneFile(path);

        try
        {
            return BuildAnimations(scene, skeleton);
        }
        finally
        {
            UfbxApi.FreeScene(scene);
        }
    }

    /// <summary>
    /// Loads only the animation clips from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Stream holding the FBX asset.</param>
    /// <param name="skeleton">Skeleton the clips should bind to; see the path overload.</param>
    /// <param name="extension">Unused, kept for signature parity with the other loaders.</param>
    public static List<Core.Resources.Animation> LoadAnimations(Stream stream, Skeleton? skeleton = null, string? extension = null)
    {
        var scene = LoadSceneStream(stream);

        try
        {
            return BuildAnimations(scene, skeleton);
        }
        finally
        {
            UfbxApi.FreeScene(scene);
        }
    }

    /// <summary>
    /// Loads the model and its animation clips from <paramref name="path"/> in one pass.
    /// </summary>
    /// <param name="path">File path of the FBX asset.</param>
    /// <param name="loadTextureFunc">Optional hook for resolving external texture references.</param>
    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadModelAndAnimations(string path, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        var scene = LoadSceneFile(path);

        try
        {
            return ProcessSceneAndAnimations(scene, Path.GetDirectoryName(path), loadTextureFunc);
        }
        finally
        {
            UfbxApi.FreeScene(scene);
        }
    }

    /// <summary>
    /// Loads the model and its animation clips from <paramref name="stream"/> in one pass.
    /// </summary>
    /// <param name="stream">Stream holding the FBX asset.</param>
    /// <param name="extension">Unused, kept for signature parity with the other loaders.</param>
    /// <param name="loadTextureFunc">Optional hook for resolving external texture references.</param>
    public static (Core.Nodes.Model, List<Core.Resources.Animation>) LoadModelAndAnimations(Stream stream, string? extension = null, Func<string, Core.Resources.Texture>? loadTextureFunc = null)
    {
        var scene = LoadSceneStream(stream);

        try
        {
            return ProcessSceneAndAnimations(scene, null, loadTextureFunc);
        }
        finally
        {
            UfbxApi.FreeScene(scene);
        }
    }

    // ModifyGeometry bakes the source unit scale and the axis conversion into the node transforms and
    // the geometry, which is the same shape AssimpLoader produces from an FBX: geometry stays in its
    // authored local space in meters, and the -90° X rotation lands on the model node.
    private static UfbxLoadOpts CreateLoadOpts() => new()
    {
        SpaceConversion = UfbxSpaceConversion.ModifyGeometry,
        TargetAxes = UfbxCoordinateAxes.RightHandedYUp,
        TargetUnitMeters = 1.0,
    };

    private static UfbxScene LoadSceneFile(string path)
    {
        var error = new UfbxError();
        return LoadScene(UfbxApi.LoadFile(path, CreateLoadOpts(), error), error);
    }

    private static UfbxScene LoadSceneStream(Stream stream)
    {
        var error = new UfbxError();
        return LoadScene(UfbxApi.LoadStream(new UfbxStreamAdapter(stream), CreateLoadOpts(), error), error);
    }

    private static UfbxScene LoadScene(UfbxScene? scene, UfbxError error) =>
        scene ?? throw FbxImportErrors.FailedToLoadFbx(error.Description ?? error.Type.ToString());

    private static List<Core.Resources.Animation> BuildAnimations(UfbxScene scene, Skeleton? skeleton)
    {
        skeleton ??= ProcessSkeleton(scene);

        if (skeleton is null)
            return [];

        var animations = ProcessAnimations(scene);

        foreach (var animation in animations)
            animation.Skeleton = skeleton;

        return animations;
    }

    private static (Core.Nodes.Model, List<Core.Resources.Animation>) ProcessSceneAndAnimations(
        UfbxScene scene, string? directory, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {
        var model = ProcessScene(scene, directory, loadTextureFunc);

        return (model, BuildAnimations(scene, model.Skeleton));
    }

    private static Core.Nodes.Model ProcessScene(UfbxScene scene, string? directory, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {
        var model = new Core.Nodes.Model
        {
            Skeleton = ProcessSkeleton(scene),
        };

        var context = new SceneContext
        {
            Materials = ProcessMaterials(scene, directory, loadTextureFunc),
            Skeleton = model.Skeleton,
        };

        var roots = scene.RootNode?.Children;

        if (roots is not null)
        {
            foreach (var child in roots)
                ProcessNode(child, model, context);
        }

        foreach (var mesh in context.Meshes)
            mesh.Model = model;

        return model;
    }

    private static void ProcessNode(UfbxNode ufbxNode, Node parent, SceneContext context)
    {
        var node = new Node
        {
            Name = ufbxNode.Name,
            LocalTransform = ToMatrix(ufbxNode.NodeToParent),
        };

        parent.AddChild(node, AttachToParentRule.KeepLocal);

        if (ufbxNode.Mesh is not null)
        {
            var meshes = FbxMeshBuilder.Build(
                ufbxNode.Mesh,
                ToMatrix(ufbxNode.GeometryToNode),
                context.Materials,
                context.DefaultMaterial,
                context.Skeleton);

            foreach (var mesh in meshes)
            {
                node.AddChild(mesh, AttachToParentRule.KeepLocal);
                context.Meshes.Add(mesh);
            }
        }

        var children = ufbxNode.Children;

        if (children is null)
            return;

        foreach (var child in children)
            ProcessNode(child, node, context);
    }

    private static Skeleton? ProcessSkeleton(UfbxScene scene)
    {
        var meshes = scene.Meshes;

        if (meshes is null)
            return null;

        List<Bone> ordered = [];
        Dictionary<string, Bone> byName = [];

        foreach (var mesh in meshes)
        {
            var skins = mesh.SkinDeformers;

            if (skins is null)
                continue;

            foreach (var skin in skins)
            {
                var clusters = skin.Clusters;

                if (clusters is null)
                    continue;

                foreach (var cluster in clusters)
                {
                    var boneNode = cluster.BoneNode;

                    if (boneNode is null || byName.ContainsKey(boneNode.Name))
                        continue;

                    var bone = new Bone
                    {
                        Name = boneNode.Name,
                        Index = ordered.Count,
                        InverseWorldMatrix = ToMatrix(cluster.GeometryToBone),
                    };

                    bone.WorldMatrix = bone.InverseWorldMatrix.Inverse();

                    ordered.Add(bone);
                    byName[bone.Name] = bone;
                }
            }
        }

        if (ordered.Count == 0)
            return null;

        // A cluster's bone node can sit behind plain group nodes, so the bone hierarchy follows the
        // nearest ancestor that is itself a bone rather than the raw node parenting.
        LinkBones(scene.RootNode, byName, []);

        var skeleton = new Skeleton();
        Bone? root = null;

        foreach (var bone in ordered)
        {
            if (bone.Parent == null)
            {
                root ??= bone;
                bone.LocalMatrix = bone.WorldMatrix;
            }
            else
            {
                bone.LocalMatrix = bone.WorldMatrix * bone.Parent.InverseWorldMatrix;
            }

            skeleton.Bones.Add(bone);
        }

        skeleton.Root = root ?? new Bone();

        return skeleton;
    }

    private static void LinkBones(UfbxNode? node, Dictionary<string, Bone> byName, HashSet<Bone> processed, Bone? nearest = null)
    {
        if (node is null)
            return;

        var nextNearest = nearest;

        if (byName.TryGetValue(node.Name, out var bone))
        {
            if (!processed.Add(bone))
                return;

            if (nearest != null && !ReferenceEquals(nearest, bone))
            {
                bone.Parent = nearest;
                nearest.Children.Add(bone);
            }

            nextNearest = bone;
        }

        var children = node.Children;

        if (children is null)
            return;

        foreach (var child in children)
            LinkBones(child, byName, processed, nextNearest);
    }

    private static List<Core.Resources.Animation> ProcessAnimations(UfbxScene scene)
    {
        List<Core.Resources.Animation> animations = [];

        var stacks = scene.AnimStacks;

        if (stacks is null)
            return animations;

        Dictionary<uint, string> nodeNames = [];

        foreach (var node in scene.Nodes ?? [])
            nodeNames[node.TypedId] = node.Name;

        // With ModifyGeometry, ufbx folds a top-level node's own axis-conversion rotation out of the
        // baked transforms and into the geometry, so a motion file whose armature top node carries the
        // exporter's rotation (Unreal writes Rx(+90) on "root") bakes one rotation short. The model
        // file's bind space never had that rotation, so the two only line up once it is put back.
        Dictionary<uint, Matrix4x4> topLevelRest = [];

        foreach (var node in scene.RootNode?.Children ?? [])
        {
            if (node is not null)
                topLevelRest[node.TypedId] = ToMatrix(node.NodeToParent);
        }

        foreach (var stack in stacks)
        {
            if (stack.Anim is null)
                continue;

            var bakeError = new UfbxError();
            var baked = UfbxBakeApi.BakeAnim(scene, stack.Anim, new UfbxBakeOpts(), bakeError);

            if (baked?.Nodes is not { Length: > 0 } bakedNodes)
                continue;

            var animation = new Core.Resources.Animation { Name = stack.Name };

            float maxTime = 0;

            foreach (var bakedNode in bakedNodes)
            {
                if (!nodeNames.TryGetValue(bakedNode.TypedId, out var name))
                    continue;

                var channel = new AnimationChannel();

                var rebase = false;
                var restShift = Matrix4x4.Identity;

                if (topLevelRest.TryGetValue(bakedNode.TypedId, out var rest))
                    rebase = Matrix4x4.Invert(rest, out restShift);

                var restRotate = restShift;
                restRotate.Translation = Vector3.Zero;

                foreach (var key in bakedNode.TranslationKeys ?? [])
                {
                    var translation = new Vector3((float)key.Value.X, (float)key.Value.Y, (float)key.Value.Z);

                    if (rebase)
                        translation = Vector3.Transform(translation, restShift);

                    channel.PositionKeyframes.Add(new Keyframe<Vector3>
                    {
                        Time = (float)key.Time,
                        Value = translation,
                    });
                    maxTime = System.Math.Max(maxTime, (float)key.Time);
                }

                foreach (var key in bakedNode.RotationKeys ?? [])
                {
                    var rotation = new Quaternion((float)key.Value.X, (float)key.Value.Y, (float)key.Value.Z, (float)key.Value.W);

                    if (rebase &&
                        Matrix4x4.Decompose(Matrix4x4.CreateFromQuaternion(rotation) * restRotate, out _, out var rebased, out _))
                    {
                        rotation = rebased;
                    }

                    channel.RotationKeyframes.Add(new Keyframe<Quaternion>
                    {
                        Time = (float)key.Time,
                        Value = rotation,
                    });
                    maxTime = System.Math.Max(maxTime, (float)key.Time);
                }

                foreach (var key in bakedNode.ScaleKeys ?? [])
                {
                    channel.ScaleKeyframes.Add(new Keyframe<Vector3>
                    {
                        Time = (float)key.Time,
                        Value = new Vector3((float)key.Value.X, (float)key.Value.Y, (float)key.Value.Z),
                    });
                    maxTime = System.Math.Max(maxTime, (float)key.Time);
                }

                animation.Channels[name] = channel;
            }

            if (animation.Channels.Count == 0)
                continue;

            animation.Duration = maxTime;
            animations.Add(animation);
        }

        return animations;
    }

    private static Dictionary<uint, Core.Resources.Material> ProcessMaterials(UfbxScene scene, string? directory, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {
        Dictionary<uint, Core.Resources.Material> materials = [];

        foreach (var ufbxMaterial in scene.Materials ?? [])
        {
            var material = new Core.Resources.Material
            {
                DoubleSided = ufbxMaterial.Features?.DoubleSided.Enabled == true,
            };

            var baseColor = ProcessTexture(ufbxMaterial.Fbx?.DiffuseColor.Texture, directory, loadTextureFunc)
                ?? ProcessTexture(ufbxMaterial.Pbr?.BaseColor.Texture, directory, loadTextureFunc);

            if (baseColor is null)
            {
                var factor = ufbxMaterial.Features?.Pbr.Enabled == true
                    ? ufbxMaterial.Pbr?.BaseColor
                    : ufbxMaterial.Fbx?.DiffuseColor;

                baseColor = Core.Resources.Texture.CreateFromColor(
                    factor?.HasValue == true ? ToColor(factor.ValueVec3) : Color.White);
            }

            baseColor.IsGammaSpace = true;

            material.SetChannel(new Channel { Name = "BaseColor", Texture = baseColor });

            var normal = ProcessTexture(ufbxMaterial.Fbx?.NormalMap.Texture, directory, loadTextureFunc)
                ?? ProcessTexture(ufbxMaterial.Pbr?.NormalMap.Texture, directory, loadTextureFunc)
                ?? Core.Resources.Texture.CreateFromColor(Color.FromArgb(255, 128, 128, 255));

            material.SetChannel(new Channel { Name = "Normal", Texture = normal });

            materials[ufbxMaterial.TypedId] = material;
        }

        return materials;
    }

    /// <summary>
    /// Material for faces that reference none. FBX tolerates unassigned materials, and AssimpLoader
    /// never hands the renderer a null one, so the same guarantee holds here.
    /// </summary>
    private static Core.Resources.Material CreateDefaultMaterial()
    {
        var material = new Core.Resources.Material();

        var baseColor = Core.Resources.Texture.CreateFromColor(Color.White);

        baseColor.IsGammaSpace = true;

        material.SetChannel(new Channel { Name = "BaseColor", Texture = baseColor });
        material.SetChannel(new Channel
        {
            Name = "Normal",
            Texture = Core.Resources.Texture.CreateFromColor(Color.FromArgb(255, 128, 128, 255)),
        });

        return material;
    }

    private static Core.Resources.Texture? ProcessTexture(UfbxTexture? texture, string? directory, Func<string, Core.Resources.Texture>? loadTextureFunc)
    {
        if (texture is null)
            return null;

        var content = texture.Content is { Length: > 0 } embedded ? embedded : texture.Video?.Content;

        if (content is { Length: > 0 })
            return LoadFlipped(() => TextureLoader.LoadTexture(content));

        var filename = FirstNonEmpty(texture.RelativeFilename, texture.Filename, texture.Video?.RelativeFilename, texture.Video?.Filename);

        if (filename.Length == 0)
            return null;

        if (loadTextureFunc != null)
            return loadTextureFunc(filename);

        var absolute = FirstNonEmpty(texture.AbsoluteFilename, texture.Video?.AbsoluteFilename);

        string[] candidates = directory is null
            ? [absolute, filename]
            : [Path.Combine(directory, filename), absolute];

        foreach (var candidate in candidates)
        {
            if (candidate.Length == 0)
                continue;

            try
            {
                return LoadFlipped(() =>
                {
                    using var stream = File.OpenRead(candidate);
                    return TextureLoader.LoadTexture(stream);
                });
            }
            catch (IOException)
            {
            }
        }

        return null;
    }

    // The engine expects images flipped vertically, matching what the other loaders feed it.
    private static Core.Resources.Texture LoadFlipped(Func<Core.Resources.Texture> load)
    {
        StbImage.stbi_set_flip_vertically_on_load_thread(1);
        try
        {
            return load();
        }
        finally
        {
            StbImage.stbi_set_flip_vertically_on_load_thread(0);
        }
    }

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private static Color ToColor(UfbxVec3 value) => Color.FromArgb(
        Clamp255(value.X),
        Clamp255(value.Y),
        Clamp255(value.Z));

    private static int Clamp255(double value) =>
        System.Math.Clamp((int)System.Math.Round(value * 255.0), 0, 255);

    // ufbx hands out column-major 3x4 matrices; System.Numerics is row-vector, so the conversion is a transpose.
    internal static Matrix4x4 ToMatrix(UfbxMatrix m) => new(
        (float)m.M00, (float)m.M10, (float)m.M20, 0,
        (float)m.M01, (float)m.M11, (float)m.M21, 0,
        (float)m.M02, (float)m.M12, (float)m.M22, 0,
        (float)m.M03, (float)m.M13, (float)m.M23, 1);

    private sealed class SceneContext
    {
        public Dictionary<uint, Core.Resources.Material> Materials { get; init; } = [];

        public Core.Resources.Material DefaultMaterial { get; init; } = CreateDefaultMaterial();

        public Skeleton? Skeleton { get; init; }

        public List<Mesh> Meshes { get; } = [];
    }
}
