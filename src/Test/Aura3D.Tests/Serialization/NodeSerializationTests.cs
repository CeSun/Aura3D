using Aura3D.Core;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
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

    [Fact]
    public void SaveNodeAndLoadNode_ShouldRoundTripVariousNodeTypesAndNodeReferences()
    {
        var previousControlRenderTarget = Camera.ControlRenderTarget;
        Camera.ControlRenderTarget = new ControlRenderTarget
        {
            Width = 1280,
            Height = 720,
            Scale = 1f
        };

        try
        {
            var texture = Texture.CreateFromColor(Color.CornflowerBlue);
            var material = new Material();
            material.SetTexture("BaseColor", texture);
            material.SetParameterValue("metallic", 0.15f);

            var geometry = new Geometry();
            geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f]);
            geometry.SetIndices([0u, 1u, 2u]);

            var root = new Node
            {
                Name = "Root",
                LocalTransform = Matrix4x4.CreateTranslation(2f, 0f, 0f)
            };

            var attachment = new BoneAttachment
            {
                Name = "A_Attachment",
                BoneName = "WeaponSocket",
                LocalOffset = Matrix4x4.CreateTranslation(0.5f, 0.25f, -0.5f)
            };

            var body = new Mesh
            {
                Name = "Z_Body",
                Geometry = geometry,
                Material = material,
                LocalTransform = Matrix4x4.CreateTranslation(1f, 2f, 3f)
            };
            attachment.Mesh = body;

            var particleSystem = new ParticleSystem
            {
                Name = "Particles",
                MaxParticles = 2048,
                EnableVisibilityCulling = true,
                CustomBoundingBox = new BoundingBox(new Vector3(-2f, -1f, -2f), new Vector3(2f, 3f, 2f))
            };
            particleSystem.Emitters.Add(new ParticleEmitter
            {
                EmissionRate = 42f,
                Shape = EmissionShape.Cone,
                ShapeSize = new Vector3(1f, 2f, 3f),
                ConeAngle = 22.5f,
                Looping = false,
                Duration = 5f,
                Lifetime = new RangeFloat(1.5f, 2.5f),
                Velocity = new RangeVector3(new Vector3(-1f, 2f, -3f), new Vector3(4f, 5f, 6f)),
                StartSize = new RangeFloat(0.2f, 0.4f),
                EndSize = new RangeFloat(0.05f, 0.15f),
                StartColor = Color.Orange,
                EndColor = Color.FromArgb(32, 255, 64, 0),
                Rotation = new RangeFloat(0f, 1f),
                AngularVelocity = new RangeFloat(-0.5f, 0.75f),
                Gravity = new Vector3(0f, -3f, 0f),
                Damping = 0.12f,
                Texture = texture,
                FlipbookTiles = new Vector2(4f, 8f),
                BlendMode = BlendMode.Masked,
                Mesh = body,
                Material = material,
                MaxParticles = 128,
                MeshScale = 1.75f
            });

            var directionalLight = new DirectionalLight
            {
                Name = "Sun",
                CastShadow = true,
                LightColor = Color.AliceBlue,
                Irradiance = 54321f,
                ShadowConfig = new DirectionalLightShadowMapConfig
                {
                    Width = 256,
                    Height = 512,
                    NearPlane = 0.25f,
                    FarPlane = 88f
                }
            };

            var pointLight = new PointLight
            {
                Name = "Lamp",
                CastShadow = true,
                LightColor = Color.Gold,
                ShadowConfig = new ShadowConfig
                {
                    NearPlane = 0.5f,
                    FarPlane = 33f
                },
                AttenuationRadius = 9f,
                SoftRatio = 0.45f,
                LuminousIntensity = 1800f
            };

            var spotLight = new SpotLight
            {
                Name = "Flashlight",
                CastShadow = false,
                LightColor = Color.WhiteSmoke,
                ShadowConfig = new ShadowConfig
                {
                    NearPlane = 1f,
                    FarPlane = 77f
                },
                InnerConeAngleDegree = 12f,
                OuterAngleDegree = 18f,
                LuminousIntensity = 2200f,
                AttenuationRadius = 14f,
                SoftRatio = 0.6f
            };

            var camera = new Camera
            {
                Name = "EditorCamera",
                NearPlane = 0.3f,
                FarPlane = 500f,
                FieldOfView = 60f,
                OrthographicSize = 9f,
                ProjectionType = ProjectionType.Orthographic,
                IsRenderBackground = false
            };

            var instancedMesh = InstancedMesh.FromMesh(body);
            instancedMesh.Name = "DecorInstances";
            instancedMesh.EnableFrustumCulling = false;
            instancedMesh.SetInstances(
            [
                Matrix4x4.CreateTranslation(10f, 0f, 0f),
                Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(-3f, 1f, 5f)
            ]);

            var instancedGroup = new InstancedMeshGroup(body)
            {
                Name = "FoliageGroup",
                MaxInstancesPerGroup = 32,
                MaxDepth = 4
            };
            instancedGroup.SetInstances(
            [
                Matrix4x4.CreateTranslation(2f, 0f, 2f),
                Matrix4x4.CreateTranslation(4f, 0f, 2f),
                Matrix4x4.CreateTranslation(8f, 0f, -1f)
            ]);

            root.AddChild(attachment, AttachToParentRule.KeepLocal);
            root.AddChild(body, AttachToParentRule.KeepLocal);
            root.AddChild(particleSystem, AttachToParentRule.KeepLocal);
            root.AddChild(directionalLight, AttachToParentRule.KeepLocal);
            root.AddChild(pointLight, AttachToParentRule.KeepLocal);
            root.AddChild(spotLight, AttachToParentRule.KeepLocal);
            root.AddChild(camera, AttachToParentRule.KeepLocal);
            root.AddChild(instancedMesh, AttachToParentRule.KeepLocal);
            root.AddChild(instancedGroup, AttachToParentRule.KeepLocal);

            using var stream = new MemoryStream();
            AssetManager.SaveNode(root, stream);
            stream.Position = 0;

            var roundTripped = AssetManager.LoadNode<Node>(stream);

            var loadedAttachment = roundTripped.GetNodesInChildren<BoneAttachment>().Single();
            var loadedBody = roundTripped.GetNodesInChildren<Mesh>().Single(mesh => mesh.Name == "Z_Body");
            var loadedParticleSystem = roundTripped.GetNodesInChildren<ParticleSystem>().Single();
            var loadedDirectionalLight = roundTripped.GetNodesInChildren<DirectionalLight>().Single();
            var loadedPointLight = roundTripped.GetNodesInChildren<PointLight>().Single();
            var loadedSpotLight = roundTripped.GetNodesInChildren<SpotLight>().Single();
            var loadedCamera = roundTripped.GetNodesInChildren<Camera>().Single();
            var loadedInstancedMesh = roundTripped.GetNodesInChildren<InstancedMesh>().Single(instanced => instanced.Name == "DecorInstances");
            var loadedInstancedGroup = roundTripped.GetNodesInChildren<InstancedMeshGroup>().Single();

            Assert.Equal(Matrix4x4.CreateTranslation(2f, 0f, 0f), roundTripped.LocalTransform);

            Assert.Same(loadedBody, loadedAttachment.Mesh);
            Assert.Equal("WeaponSocket", loadedAttachment.BoneName);
            Assert.Equal(Matrix4x4.CreateTranslation(0.5f, 0.25f, -0.5f), loadedAttachment.LocalOffset);

            Assert.Equal(2048, loadedParticleSystem.MaxParticles);
            Assert.True(loadedParticleSystem.EnableVisibilityCulling);
            Assert.Equal(new BoundingBox(new Vector3(-2f, -1f, -2f), new Vector3(2f, 3f, 2f)), loadedParticleSystem.CustomBoundingBox);
            var loadedEmitter = Assert.Single(loadedParticleSystem.Emitters);
            Assert.Equal(42f, loadedEmitter.EmissionRate, 5);
            Assert.Equal(EmissionShape.Cone, loadedEmitter.Shape);
            Assert.Equal(new Vector3(1f, 2f, 3f), loadedEmitter.ShapeSize);
            Assert.Equal(22.5f, loadedEmitter.ConeAngle, 5);
            Assert.False(loadedEmitter.Looping);
            Assert.Equal(5f, loadedEmitter.Duration, 5);
            Assert.Equal(1.5f, loadedEmitter.Lifetime.Min, 5);
            Assert.Equal(2.5f, loadedEmitter.Lifetime.Max, 5);
            Assert.Equal(new Vector3(-1f, 2f, -3f), loadedEmitter.Velocity.Min);
            Assert.Equal(new Vector3(4f, 5f, 6f), loadedEmitter.Velocity.Max);
            Assert.Equal(Color.Orange.ToArgb(), loadedEmitter.StartColor.ToArgb());
            Assert.Equal(Color.FromArgb(32, 255, 64, 0).ToArgb(), loadedEmitter.EndColor.ToArgb());
            Assert.Same(loadedBody, loadedEmitter.Mesh);
            Assert.Same(loadedBody.Material, loadedEmitter.Material);
            Assert.Same(loadedBody.Material!.GetTexture("BaseColor"), loadedEmitter.Texture);
            Assert.Equal(128, loadedEmitter.MaxParticles);
            Assert.Equal(1.75f, loadedEmitter.MeshScale, 5);
            Assert.Equal(BlendMode.Masked, loadedEmitter.BlendMode);

            Assert.True(loadedDirectionalLight.CastShadow);
            Assert.Equal(Color.AliceBlue.ToArgb(), loadedDirectionalLight.LightColor.ToArgb());
            Assert.Equal(54321f, loadedDirectionalLight.Irradiance, 5);
            Assert.Equal(256, loadedDirectionalLight.ShadowConfig.Width);
            Assert.Equal(512, loadedDirectionalLight.ShadowConfig.Height);
            Assert.Equal(0.25f, loadedDirectionalLight.ShadowConfig.NearPlane, 5);
            Assert.Equal(88f, loadedDirectionalLight.ShadowConfig.FarPlane, 5);

            Assert.True(loadedPointLight.CastShadow);
            Assert.Equal(Color.Gold.ToArgb(), loadedPointLight.LightColor.ToArgb());
            Assert.Equal(0.5f, loadedPointLight.ShadowConfig.NearPlane, 5);
            Assert.Equal(33f, loadedPointLight.ShadowConfig.FarPlane, 5);
            Assert.Equal(9f, loadedPointLight.AttenuationRadius, 5);
            Assert.Equal(0.45f, loadedPointLight.SoftRatio, 5);
            Assert.Equal(1800f, loadedPointLight.LuminousIntensity, 5);

            Assert.False(loadedSpotLight.CastShadow);
            Assert.Equal(Color.WhiteSmoke.ToArgb(), loadedSpotLight.LightColor.ToArgb());
            Assert.Equal(12f, loadedSpotLight.InnerConeAngleDegree, 5);
            Assert.Equal(18f, loadedSpotLight.OuterAngleDegree, 5);
            Assert.Equal(2200f, loadedSpotLight.LuminousIntensity, 5);
            Assert.Equal(14f, loadedSpotLight.AttenuationRadius, 5);
            Assert.Equal(0.6f, loadedSpotLight.SoftRatio, 5);

            Assert.Equal(0.3f, loadedCamera.NearPlane, 5);
            Assert.Equal(500f, loadedCamera.FarPlane, 5);
            Assert.Equal(60f, loadedCamera.FieldOfView, 5);
            Assert.Equal(9f, loadedCamera.OrthographicSize, 5);
            Assert.Equal(ProjectionType.Orthographic, loadedCamera.ProjectionType);
            Assert.False(loadedCamera.IsRenderBackground);

            Assert.False(loadedInstancedMesh.EnableFrustumCulling);
            Assert.Equal(2, loadedInstancedMesh.InstanceCount);
            Assert.NotNull(loadedInstancedMesh.Material);
            Assert.Equal(Matrix4x4.CreateTranslation(10f, 0f, 0f), loadedInstancedMesh.GetInstanceTransform(0));
            Assert.Equal(Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(-3f, 1f, 5f), loadedInstancedMesh.GetInstanceTransform(1));

            Assert.Same(loadedBody, loadedInstancedGroup.SourceMesh);
            Assert.Equal(32, loadedInstancedGroup.MaxInstancesPerGroup);
            Assert.Equal(4, loadedInstancedGroup.MaxDepth);
            Assert.Equal(3, loadedInstancedGroup.InstanceCount);
        }
        finally
        {
            Camera.ControlRenderTarget = previousControlRenderTarget;
        }
    }
}
