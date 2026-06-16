using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using System.Drawing;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Serialization;

public class AssetManagerSerializationTests
{
    [Fact]
    public void SaveResourceAndLoadResource_ShouldRoundTripTexture()
    {
        var texture = new Texture()
            .SetHdrData([0.1f, 0.2f, 0.3f], 1, 1)
            .SetColorFormat(ColorFormat.RGB)
            .SetIsGammaSpace(true)
            .SetMinFilter(TextureFilterMode.Linear)
            .SetMagFilter(TextureFilterMode.Nearest)
            .SetWarpS(TextureWrapMode.MirroredRepeat)
            .SetWarpT(TextureWrapMode.ClampToBorder);
        texture.TextureId = 7;
        texture.NeedsUpload = false;

        var roundTripped = RoundTrip(texture);

        Assert.True(roundTripped.IsHdr);
        Assert.Equal((uint)1, roundTripped.Width);
        Assert.Equal((uint)1, roundTripped.Height);
        Assert.Equal(texture.HdrData, roundTripped.HdrData);
        Assert.Empty(roundTripped.LdrData);
        Assert.Equal(ColorFormat.RGB, roundTripped.ColorFormat);
        Assert.True(roundTripped.IsGammaSpace);
        Assert.Equal(TextureFilterMode.Linear, roundTripped.MinFilter);
        Assert.Equal(TextureFilterMode.Nearest, roundTripped.MagFilter);
        Assert.Equal(TextureWrapMode.MirroredRepeat, roundTripped.WrapS);
        Assert.Equal(TextureWrapMode.ClampToBorder, roundTripped.WrapT);
        Assert.Equal(0u, roundTripped.TextureId);
        Assert.True(roundTripped.NeedsUpload);
    }

    [Fact]
    public void SaveResourceAndLoadResource_ShouldRoundTripMaterialParameters()
    {
        var material = new Material();
        material.SetParameterValue("enabled", true);
        material.SetParameterValue("layer", 2);
        material.SetParameterValue("flags", 3u);
        material.SetParameterValue("roughness", 0.42f);
        material.SetParameterValue("exposure", 1.25d);
        material.SetParameterValue("seed", 1234567890123L);
        material.SetParameterValue("mask", 1234567890123UL);
        material.SetParameterValue("label", "serialized");
        material.SetParameterValue("uvOffset", new Vector2(1.5f, 2.5f));
        material.SetParameterValue("normalScale", new Vector3(0.1f, 0.2f, 0.3f));
        material.SetParameterValue("tint", new Vector4(0.9f, 0.8f, 0.7f, 1f));
        material.SetParameterValue("albedo", Color.Coral);

        var roundTripped = RoundTrip(material);

        Assert.True(roundTripped.TryGetParameterValue("enabled", out bool enabled));
        Assert.True(enabled);
        Assert.True(roundTripped.TryGetParameterValue("layer", out int layer));
        Assert.Equal(2, layer);
        Assert.True(roundTripped.TryGetParameterValue("flags", out uint flags));
        Assert.Equal(3u, flags);
        Assert.True(roundTripped.TryGetParameterValue("roughness", out float roughness));
        Assert.Equal(0.42f, roughness, 5);
        Assert.True(roundTripped.TryGetParameterValue("exposure", out double exposure));
        Assert.Equal(1.25d, exposure, 10);
        Assert.True(roundTripped.TryGetParameterValue("seed", out long seed));
        Assert.Equal(1234567890123L, seed);
        Assert.True(roundTripped.TryGetParameterValue("mask", out ulong mask));
        Assert.Equal(1234567890123UL, mask);
        Assert.True(roundTripped.TryGetParameterValue("label", out string? label));
        Assert.Equal("serialized", label);
        Assert.True(roundTripped.TryGetParameterValue("uvOffset", out Vector2 uvOffset));
        Assert.Equal(new Vector2(1.5f, 2.5f), uvOffset);
        Assert.True(roundTripped.TryGetParameterValue("normalScale", out Vector3 normalScale));
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), normalScale);
        Assert.True(roundTripped.TryGetParameterValue("tint", out Vector4 tint));
        Assert.Equal(new Vector4(0.9f, 0.8f, 0.7f, 1f), tint);
        Assert.True(roundTripped.TryGetParameterValue("albedo", out Color albedo));
        Assert.Equal(Color.Coral.ToArgb(), albedo.ToArgb());
    }

    [Fact]
    public void SaveResourceAndLoadResource_ShouldRoundTripMaterialTextureReferencesAndShaders()
    {
        var sharedTexture = Texture.CreateFromColor(Color.Blue);
        var material = new Material
        {
            BlendMode = BlendMode.Translucent,
            DoubleSided = true,
            AlphaCutoff = 0.25f
        };
        material.SetTexture("BaseColor", sharedTexture);
        material.SetTexture("Normal", sharedTexture);
        material.SetShaderSource("forward", ShaderType.Vertex, "vertex shader");
        material.SetShaderSource("forward", ShaderType.Fragment, "fragment shader");

        var roundTripped = RoundTrip(material);

        Assert.Equal(BlendMode.Translucent, roundTripped.BlendMode);
        Assert.True(roundTripped.DoubleSided);
        Assert.Equal(0.25f, roundTripped.AlphaCutoff, 5);
        Assert.True(roundTripped.HasShader);
        Assert.Equal(("vertex shader", "fragment shader"), roundTripped.GetShaderSource("forward"));

        var baseColor = Assert.IsType<Texture>(roundTripped.GetTexture("BaseColor"));
        var normal = Assert.IsType<Texture>(roundTripped.GetTexture("Normal"));

        Assert.Same(baseColor, normal);
        Assert.Equal(sharedTexture.LdrData, baseColor.LdrData);
        Assert.Equal(sharedTexture.Width, baseColor.Width);
        Assert.Equal(sharedTexture.Height, baseColor.Height);
    }

    [Fact]
    public void SaveResourceAndLoadResource_ShouldRoundTripSkeletonHierarchy()
    {
        var root = new Bone
        {
            Name = "Root",
            Index = 0,
            LocalMatrix = Matrix4x4.Identity,
            WorldMatrix = Matrix4x4.Identity,
            InverseWorldMatrix = Matrix4x4.Identity
        };
        var child = new Bone
        {
            Name = "Arm",
            Index = 1,
            LocalMatrix = Matrix4x4.CreateTranslation(1f, 2f, 3f),
            WorldMatrix = Matrix4x4.CreateTranslation(1f, 2f, 3f),
            InverseWorldMatrix = Matrix4x4.CreateTranslation(-1f, -2f, -3f)
        };
        child.Parent = root;
        root.Children.Add(child);

        var skeleton = new Skeleton
        {
            Bones = [root, child]
        };
        skeleton.Root = root;

        var roundTripped = RoundTrip(skeleton);

        Assert.Equal(0, roundTripped.RootIndex);
        Assert.Equal("Root", roundTripped.Root.Name);
        Assert.Single(roundTripped.Root.Children);
        Assert.Equal("Arm", roundTripped.Root.Children[0].Name);
        Assert.Same(roundTripped.Root, roundTripped.Bones[1].Parent);
        Assert.Equal(1, roundTripped.GetBoneIndex("Arm"));
        Assert.Equal(Matrix4x4.CreateTranslation(1f, 2f, 3f), roundTripped.Bones[1].LocalMatrix);
    }

    [Fact]
    public void SaveResourceAndLoadResource_ShouldRoundTripAnimation()
    {
        var skeleton = new Skeleton
        {
            Bones =
            [
                new Bone
                {
                    Name = "Hip",
                    Index = 0,
                    LocalMatrix = Matrix4x4.CreateTranslation(0f, 1f, 0f),
                    WorldMatrix = Matrix4x4.CreateTranslation(0f, 1f, 0f),
                    InverseWorldMatrix = Matrix4x4.CreateTranslation(0f, -1f, 0f)
                },
                new Bone
                {
                    Name = "Spine",
                    Index = 1,
                    ParentIndex = 0,
                    LocalMatrix = Matrix4x4.CreateTranslation(0f, 2f, 0f),
                    WorldMatrix = Matrix4x4.CreateTranslation(0f, 3f, 0f),
                    InverseWorldMatrix = Matrix4x4.CreateTranslation(0f, -3f, 0f)
                }
            ]
        };
        skeleton.Root = skeleton.Bones[0];

        var animation = new Animation
        {
            Name = "Walk",
            Duration = 2f,
            Skeleton = skeleton,
            Channels =
            {
                ["Hip"] = new AnimationChannel
                {
                    PositionKeyframes =
                    [
                        new Keyframe<Vector3> { Time = 0f, Value = Vector3.Zero },
                        new Keyframe<Vector3> { Time = 2f, Value = new Vector3(4f, 0f, 0f) }
                    ],
                    RotationKeyframes =
                    [
                        new Keyframe<Quaternion> { Time = 0f, Value = Quaternion.Identity },
                        new Keyframe<Quaternion> { Time = 2f, Value = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI) }
                    ],
                    ScaleKeyframes =
                    [
                        new Keyframe<Vector3> { Time = 0f, Value = Vector3.One },
                        new Keyframe<Vector3> { Time = 2f, Value = new Vector3(3f, 3f, 3f) }
                    ]
                }
            }
        };

        var roundTrippedAnimation = RoundTrip(animation);

        Assert.Equal("Walk", roundTrippedAnimation.Name);
        Assert.Equal(2f, roundTrippedAnimation.Duration, 5);
        Assert.NotNull(roundTrippedAnimation.Skeleton);
        Assert.Equal("Hip", roundTrippedAnimation.Skeleton!.Root.Name);
        Assert.Single(roundTrippedAnimation.Channels);
        var animatedSample = roundTrippedAnimation.Sample("Hip", 1f);
        Assert.Equal(2f, animatedSample.M41, 5);
        Assert.Equal(0f, animatedSample.M42, 5);
        Assert.Equal(0f, animatedSample.M43, 5);

        var fallbackSample = roundTrippedAnimation.Sample("Spine", 0f);
        Assert.Equal(Matrix4x4.CreateTranslation(0f, 2f, 0f), fallbackSample);
    }

    [Fact]
    public void SaveResourceAndLoadResource_ShouldRoundTripGeometry()
    {
        var geometry = new Geometry
        {
            PrimitiveType = PrimitiveType.TriangleStrip
        };
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, [0f, 0f, 0f, 2f, 0f, 0f, 2f, 3f, 0f]);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, [0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f]);
        geometry.SetIndices([0u, 1u, 2u]);

        var roundTrippedGeometry = RoundTrip(geometry);

        Assert.Equal(PrimitiveType.TriangleStrip, roundTrippedGeometry.PrimitiveType);
        Assert.Equal(3, roundTrippedGeometry.VertexCount);
        Assert.Equal(3, roundTrippedGeometry.IndicesCount);
        Assert.Equal(new List<uint> { 0u, 1u, 2u }, roundTrippedGeometry.Indices);
        Assert.Equal(new List<float> { 0f, 0f, 0f, 2f, 0f, 0f, 2f, 3f, 0f }, roundTrippedGeometry.GetAttributeData(BuildInVertexAttribute.Position));
        Assert.Equal(new List<float> { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f }, roundTrippedGeometry.GetAttributeData(BuildInVertexAttribute.Normal));
        Assert.Equal(new BoundingBox(Vector3.Zero, new Vector3(2f, 3f, 0f)), roundTrippedGeometry.BoundingBox);
    }

    private static T RoundTrip<T>(T resource) where T : class
    {
        using var stream = new MemoryStream();
        AssetManager.SaveResource(resource, stream);
        stream.Position = 0;
        return AssetManager.LoadResource<T>(stream);
    }
}
