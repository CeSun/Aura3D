using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using System.Drawing;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Serialization;

public class NodeSerializationTests
{
    [Fact]
    public void SaveNodeAndLoadNode_ShouldRoundTripHierarchyAndSharedResources()
    {
        var rootBone = new Bone
        {
            Name = "Root",
            Index = 0,
            LocalMatrix = Matrix4x4.Identity,
            WorldMatrix = Matrix4x4.Identity,
            InverseWorldMatrix = Matrix4x4.Identity
        };
        var skeleton = new Skeleton
        {
            Bones = [rootBone]
        };
        skeleton.Root = rootBone;

        var geometry = new Geometry();
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, [0f, 0f, 0f, 2f, 0f, 0f, 0f, 1f, 0f]);
        geometry.SetIndices([0u, 1u, 2u]);

        var material = new Material();
        material.SetTexture("BaseColor", Texture.CreateFromColor(Color.OrangeRed));
        material.SetParameterValue("roughness", 0.35f);

        var model = new Model
        {
            Name = "SerializedModel",
            Skeleton = skeleton,
            BoundingBoxPadding = 0.75f,
            CustomBoundingBox = new BoundingBox(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f)),
            LocalTransform = Matrix4x4.CreateTranslation(5f, 0f, 0f)
        };
        model.Tags.Add("root");

        var pivot = new Node
        {
            Name = "Pivot",
            LocalTransform = Matrix4x4.CreateTranslation(0f, 2f, 0f)
        };
        pivot.Tags.Add("pivot");

        var body = new Mesh
        {
            Name = "Body",
            Geometry = geometry,
            Material = material,
            LocalTransform = Matrix4x4.CreateTranslation(1f, 0f, 0f)
        };

        var weapon = new Mesh
        {
            Name = "Weapon",
            Geometry = geometry,
            Material = material,
            LocalTransform = Matrix4x4.CreateTranslation(-1f, 0f, 0f),
            Enable = false
        };

        model.AddChild(pivot, AttachToParentRule.KeepLocal);
        pivot.AddChild(body, AttachToParentRule.KeepLocal);
        model.AddChild(weapon, AttachToParentRule.KeepLocal);
        weapon.Enable = false;

        using var stream = new MemoryStream();
        AssetManager.SaveNode(model, stream);
        stream.Position = 0;

        var roundTripped = AssetManager.LoadNode<Model>(stream);

        Assert.Equal("SerializedModel", roundTripped.Name);
        Assert.Contains("root", roundTripped.Tags);
        Assert.Equal(model.LocalTransform, roundTripped.LocalTransform);
        Assert.Equal(0.75f, roundTripped.BoundingBoxPadding, 5);
        Assert.Equal(new BoundingBox(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f)), roundTripped.CustomBoundingBox);
        Assert.NotNull(roundTripped.Skeleton);
        Assert.Equal("Root", roundTripped.Skeleton!.Root.Name);

        var loadedPivot = roundTripped.GetNodesInChildren<Node>().Single(node => node.Name == "Pivot");
        var loadedBody = roundTripped.GetNodesInChildren<Mesh>().Single(mesh => mesh.Name == "Body");
        var loadedWeapon = roundTripped.GetNodesInChildren<Mesh>().Single(mesh => mesh.Name == "Weapon");

        Assert.Contains("pivot", loadedPivot.Tags);
        Assert.Equal(Matrix4x4.CreateTranslation(0f, 2f, 0f), loadedPivot.LocalTransform);
        Assert.Same(loadedPivot, loadedBody.Parent);
        Assert.Same(roundTripped, loadedWeapon.Parent);
        Assert.Same(roundTripped, loadedBody.Model);
        Assert.Same(roundTripped, loadedWeapon.Model);
        Assert.False(loadedWeapon.Enable);

        Assert.NotNull(loadedBody.Geometry);
        Assert.NotNull(loadedBody.Material);
        Assert.Same(loadedBody.Geometry, loadedWeapon.Geometry);
        Assert.Same(loadedBody.Material, loadedWeapon.Material);

        var loadedTexture = Assert.IsType<Texture>(loadedBody.Material!.GetTexture("BaseColor"));
        Assert.Equal(Color.OrangeRed.ToArgb(), Color.FromArgb(
            loadedTexture.LdrData[3],
            loadedTexture.LdrData[0],
            loadedTexture.LdrData[1],
            loadedTexture.LdrData[2]).ToArgb());

        Assert.True(loadedBody.Material.TryGetParameterValue("roughness", out float roughness));
        Assert.Equal(0.35f, roughness, 5);
    }
}
