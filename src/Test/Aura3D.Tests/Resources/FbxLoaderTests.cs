using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Aura3D.Model.Exceptions;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Resources;

/// <summary>
/// Covers the ufbx-backed FBX import path: the geometry build (triangulation, unit conversion,
/// generated tangent frame, material fallback) is exercised against a self-contained ASCII FBX,
/// while skeleton, skinning and clip baking need the real rigged mannequin under gallery/assets.
/// </summary>
public class FbxLoaderTests
{
    // A self-contained geometry document for the paths a rigged asset cannot isolate: the quad is a
    // four-index polygon, so triangulation is what turns it into the two triangles the renderer wants;
    // no unit is declared, so ufbx's centimetre default must land as 0.01m; and no material is
    // declared, so the fallback material has to appear.
    //
    // FBX 6.1 ASCII because that is the smallest document ufbx builds a mesh from - the version on
    // the first line decides the parse, and the same text under a 7.x header yields no mesh.
    // AssimpLoader cannot open this file at all: it rejects everything before FBX 2011.
    private const string QuadFbx = """
        ; FBX 6.1.0 project file
        FBXHeaderExtension:  {
        	FBXHeaderVersion: 1003
        	FBXVersion: 6100
        	Creator: "Aura3D test"
        }
        Definitions:  {
        	Version: 100
        	Count: 2
        	ObjectType: "Model" {
        		Count: 1
        	}
        	ObjectType: "NodeAttribute" {
        		Count: 1
        	}
        }
        Objects:  {
        	NodeAttribute: "NodeAttribute::Quad", "Mesh" {
        	}
        	Model: "Model::Quad", "Mesh", "" {
        		Version: 232
        		Vertices: 0,0,0, 1,0,0, 0,1,0, 1,1,0
        		PolygonVertexIndex: 0,1,3,-1
        		GeometryVersion: 124
        		LayerElementNormal: 0 {
        			Version: 101
        			Name: ""
        			MappingInformationType: "ByPolygonVertex"
        			ReferenceInformationType: "Direct"
        			Normals: 0,0,-1, 0,0,-1, 0,0,-1, 0,0,-1
        		}
        		LayerElementUV: 0 {
        			Version: 101
        			Name: "UVMap"
        			MappingInformationType: "ByPolygonVertex"
        			ReferenceInformationType: "Direct"
        			UV: 0,0, 1,0, 1,1, 0,1
        		}
        		Layer: 0 {
        			Version: 100
        			LayerElement:  {
        				Type: "LayerElementNormal"
        				TypedIndex: 0
        			}
        			LayerElement:  {
        				Type: "LayerElementUV"
        				TypedIndex: 0
        			}
        		}
        	}
        }
        Connections:  {
        	C: "OO", "NodeAttribute::Quad", "Model::Quad"
        }
        """;

    private static string WriteTempFbx(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aura3d-fbx-test-{Guid.NewGuid():N}.fbx");

        File.WriteAllText(path, content);

        return path;
    }

    private static string Asset(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "fbx", name);

    // xUnit has no per-component precision overload for Vector3 / Matrix4x4.
    private static void AssertClose(Vector3 expected, Vector3 actual, int precision)
    {
        Assert.Equal((double)expected.X, actual.X, precision);
        Assert.Equal((double)expected.Y, actual.Y, precision);
        Assert.Equal((double)expected.Z, actual.Z, precision);
    }

    private static void AssertClose(Matrix4x4 expected, Matrix4x4 actual, int precision)
    {
        static float[] Flat(Matrix4x4 m) =>
            [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
             m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

        var expectedValues = Flat(expected);
        var actualValues = Flat(actual);

        for (int i = 0; i < expectedValues.Length; i++)
            Assert.Equal((double)expectedValues[i], actualValues[i], precision);
    }

    // Tolerance rather than digit rounding: two loaders agreeing to float noise still land on a
    // rounded digit boundary from time to time.
    private static void AssertNearlyEqual(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
    {
        static float[] Flat(Matrix4x4 m) =>
            [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
             m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

        var expectedValues = Flat(expected);
        var actualValues = Flat(actual);

        for (int i = 0; i < expectedValues.Length; i++)
            Assert.True(System.Math.Abs(expectedValues[i] - actualValues[i]) <= tolerance,
                $"[{i / 4},{i % 4}] expected {expectedValues[i]} actual {actualValues[i]}");
    }

    [Fact]
    public void Load_ShouldTriangulateAndScaleToMetres()
    {
        var path = WriteTempFbx(QuadFbx);

        try
        {
            var model = FbxLoader.Load(path);

            var mesh = Assert.Single(model.Meshes);

            var geometry = mesh.Geometry;

            Assert.NotNull(geometry);
            Assert.Equal(PrimitiveType.Triangles, geometry!.PrimitiveType);
            Assert.Equal(4, geometry.VertexCount);
            Assert.Equal(6, geometry.Indices.Count);

            var positions = geometry.GetAttributeData(BuildInVertexAttribute.Position);

            Assert.NotNull(positions);
            Assert.Equal(12, positions!.Count);

            // Bounding box is in metres: the authored 1x1 quad becomes 0.01x0.01.
            AssertClose(new Vector3(0f, 0f, 0f), geometry.BoundingBox!.Min, 5);
            AssertClose(new Vector3(0.01f, 0.01f, 0f), geometry.BoundingBox!.Max, 5);

            Assert.Null(model.Skeleton);
            Assert.False(model.IsSkinnedModel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ShouldKeepAuthoredNormalsAndBuildTangentFrame()
    {
        var path = WriteTempFbx(QuadFbx);

        try
        {
            var geometry = Assert.Single(FbxLoader.Load(path).Meshes).Geometry;

            var normals = geometry!.GetAttributeData(BuildInVertexAttribute.Normal);

            Assert.NotNull(normals);
            Assert.Equal(12, normals!.Count);

            for (int i = 0; i < 4; i++)
                AssertClose(new Vector3(0f, 0f, -1f), new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]), 5);

            // The file carries no tangents, so the loader derives them from the UV gradient.
            var tangents = geometry.GetAttributeData(BuildInVertexAttribute.Tangent);
            var bitangents = geometry.GetAttributeData(BuildInVertexAttribute.Bitangent);

            Assert.NotNull(tangents);
            Assert.NotNull(bitangents);
            Assert.Equal(12, tangents!.Count);
            Assert.Equal(12, bitangents!.Count);

            for (int i = 0; i < 4; i++)
            {
                var t = new Vector3(tangents[i * 3], tangents[i * 3 + 1], tangents[i * 3 + 2]);
                var b = new Vector3(bitangents[i * 3], bitangents[i * 3 + 1], bitangents[i * 3 + 2]);

                Assert.Equal(1f, t.Length(), 4);
                Assert.Equal(1f, b.Length(), 4);
                Assert.Equal(0f, Vector3.Dot(t, new Vector3(0f, 0f, -1f)), 4);
            }

            Assert.Null(geometry.GetAttributeData(BuildInVertexAttribute.Color_0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ShouldFallBackToDefaultMaterial_WhenFileDefinesNone()
    {
        var path = WriteTempFbx(QuadFbx);

        try
        {
            var material = Assert.Single(FbxLoader.Load(path).Meshes).Material;

            Assert.NotNull(material);

            var names = material!.Channels.Select(channel => channel.Name).ToArray();

            Assert.Equal(["BaseColor", "Normal"], names);
            Assert.NotNull(material.Channels[0].Texture);
            Assert.NotNull(material.Channels[1].Texture);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ShouldBuildOneMaterialPerFbxMaterial_WithBaseColorAndNormal()
    {
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));

        // Two FBX materials on the mannequin's one mesh: each becomes its own Aura3D material, so
        // editing or picking a part cannot leak into the other.
        Assert.Equal(2, model.Meshes.Count);

        var materials = model.Meshes.Select(mesh => mesh.Material!).ToArray();

        Assert.NotSame(materials[0], materials[1]);

        foreach (var material in materials)
        {
            Assert.False(material.DoubleSided);
            Assert.Equal(["BaseColor", "Normal"], material.Channels.Select(channel => channel.Name));

            var baseColor = material.Channels[0].Texture!;
            var normal = material.Channels[1].Texture!;

            // No image is referenced, so the authored diffuse factor (0.216 -> 55/255) becomes a flat
            // colour texture - proof this is the file's material and not the loader's white fallback.
            var basePixels = baseColor.AsLdrData();

            Assert.Equal((byte)55, basePixels[0]);
            Assert.Equal((byte)55, basePixels[2]);
            Assert.Equal((byte)255, basePixels[3]);
            Assert.True(baseColor.IsGammaSpace);

            // The file has no normal map either: the flat +Z placeholder stands in.
            var normalPixels = normal.AsLdrData();

            Assert.Equal((byte)128, normalPixels[0]);
            Assert.Equal((byte)255, normalPixels[2]);
            Assert.False(normal.IsGammaSpace);
        }
    }

    [Fact]
    public void Load_FromStream_ShouldMatchLoad_FromPath()
    {
        var path = WriteTempFbx(QuadFbx);

        try
        {
            var fromPath = FbxLoader.Load(path);
            using var file = File.OpenRead(path);
            var fromStream = FbxLoader.Load(file);

            Assert.Equal(
                fromPath.Meshes.Single().Geometry!.VertexCount,
                fromStream.Meshes.Single().Geometry!.VertexCount);
            Assert.Equal(
                fromPath.Meshes.Single().Geometry!.Indices.Count,
                fromStream.Meshes.Single().Geometry!.Indices.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ShouldThrowStableErrorCode_WhenFileIsNotFbx()
    {
        var path = WriteTempFbx("this is not an FBX file");

        try
        {
            var exception = Assert.Throws<FbxImportException>(() => FbxLoader.Load(path));

            Assert.Equal(FbxImportError.FailedToLoadFbx, exception.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ShouldBuildSkeletonFromSkinClustersAndAttachJoints()
    {
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));

        var skeleton = model.Skeleton;

        Assert.NotNull(skeleton);
        Assert.True(model.IsSkinnedModel);
        Assert.Equal(68, skeleton!.Bones.Count);
        Assert.Equal("root", skeleton.Root.Name);

        var pelvis = skeleton.Bones.Single(bone => bone.Name == "pelvis");

        var parent = pelvis.Parent;

        Assert.NotNull(parent);
        Assert.Equal("root", parent!.Name);
        Assert.Equal(skeleton.Bones.Count, skeleton.Bones.Select(bone => bone.Name).Distinct().Count());

        // InverseWorldMatrix comes from the cluster's geometry-to-bone bind transform.
        Assert.Equal(-0.96746f, pelvis.InverseWorldMatrix.M41, 4);
        Assert.Equal(-0.01410f, pelvis.InverseWorldMatrix.M42, 4);

        // Local * parent world has to reproduce that world matrix, which is what AnimationSampler composes.
        AssertClose(pelvis.WorldMatrix, pelvis.LocalMatrix * parent.WorldMatrix, 4);
        AssertClose(pelvis.WorldMatrix, pelvis.InverseWorldMatrix.Inverse(), 4);

        foreach (var mesh in model.Meshes)
        {
            var joints = mesh.Geometry!.GetAttributeData(BuildInVertexAttribute.Joints_0);
            var weights = mesh.Geometry!.GetAttributeData(BuildInVertexAttribute.Weights_0);

            Assert.NotNull(joints);
            Assert.NotNull(weights);
            Assert.Equal(4 * mesh.Geometry!.VertexCount, joints!.Count);
            Assert.Equal(4 * mesh.Geometry!.VertexCount, weights!.Count);

            for (int v = 0; v < mesh.Geometry!.VertexCount; v++)
            {
                Assert.Equal(1f, weights[v * 4] + weights[v * 4 + 1] + weights[v * 4 + 2] + weights[v * 4 + 3], 3);

                for (int slot = 0; slot < 4; slot++)
                    Assert.InRange(joints[v * 4 + slot], 0f, (float)skeleton.Bones.Count - 1);
            }
        }
    }

    [Fact]
    public void Load_ShouldSplitMeshPerMaterialAndKeepWorldBounds()
    {
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));

        // The mannequin uses two materials, so the single ufbx mesh becomes two Aura3D meshes.
        Assert.Equal(2, model.Meshes.Count);
        Assert.All(model.Meshes, mesh => Assert.Equal("SK_Mannequin", mesh.Name));

        Assert.Equal(123156, model.Meshes.Sum(mesh => mesh.Geometry!.Indices.Count));

        var bounds = model.BoundingBox;

        Assert.Equal(-0.6936f, bounds.Min.X, 3);
        Assert.Equal(1.8253f, bounds.Max.Y, 3);

        foreach (var mesh in model.Meshes)
            Assert.Same(model, mesh.Model);
    }

    [Fact]
    public void LoadAnimations_ShouldBindClipsToTheModelSkeleton()
    {
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));

        var clips = FbxLoader.LoadAnimations(Asset("Idle_Rifle_Hip.FBX"), model.Skeleton);

        var clip = Assert.Single(clips);

        Assert.Same(model.Skeleton, clip.Skeleton);
        Assert.Equal(4.0f, clip.Duration, 3);

        // One channel per animated bone, keyed by bone name the sampler looks up.
        Assert.Equal(68, clip.Channels.Count);
        Assert.All(model.Skeleton!.Bones, bone => Assert.True(clip.Channels.ContainsKey(bone.Name)));

        Assert.Equal(121, clip.Channels["pelvis"].PositionKeyframes.Count);
        Assert.Equal(121, clip.Channels["pelvis"].RotationKeyframes.Count);

        // Root motion: the clip translates the pelvis, and sampling at 0 must land on the first key.
        var rest = model.Skeleton!.Bones.Single(bone => bone.Name == "pelvis");
        var atZero = clip.Sample("pelvis", 0f);

        Assert.Equal(0.01056f, atZero.M42, 4);
        Assert.Equal(0.91383f, atZero.M43, 4);
        Assert.NotEqual(rest.LocalMatrix.M43, atZero.M43);

        // Feed the sampler the way the renderer does and the posed bone matrix must move.
        var sampler = new AnimationSampler(clip);

        sampler.Update(0.0);

        Assert.NotEqual(0f, sampler.BonesTransform[rest.Index].M43);
    }

    [Fact]
    public void LoadAnimations_ShouldPoseTheSkeletonInTheBindSpace()
    {
        // The motion files carry the exporter's axis conversion on their top node while the model
        // file's bind space does not. A baked clip that is not rebased tips the whole character over
        // by 90 degrees, so the posed matrices have to land where AssimpLoader lands them.
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));
        var reference = AssimpLoader.Load(Asset("SK_Mannequin.FBX"));

        var clip = Assert.Single(FbxLoader.LoadAnimations(Asset("Idle_Rifle_Hip.FBX"), model.Skeleton));
        var referenceClip = Assert.Single(AssimpLoader.LoadAnimations(Asset("Idle_Rifle_Hip.FBX"), reference.Skeleton));

        var sampler = new AnimationSampler(clip);
        var referenceSampler = new AnimationSampler(referenceClip);

        for (var frame = 0; frame < 5; frame++)
        {
            sampler.Update(0.2);
            referenceSampler.Update(0.2);
        }

        foreach (var bone in model.Skeleton!.Bones)
        {
            var other = reference.Skeleton!.Bones[bone.Index];

            AssertNearlyEqual(
                other.InverseWorldMatrix * referenceSampler.BonesTransform[other.Index],
                bone.InverseWorldMatrix * sampler.BonesTransform[bone.Index],
                1e-4f);
        }

        // The same invariant without Assimp in the room: what the renderer applies to the root bone is
        // the clip's root motion, a small delta on top of bind rather than a quarter turn.
        var root = model.Skeleton.Root!;
        var rootDelta = root.InverseWorldMatrix * sampler.BonesTransform[root.Index];

        Assert.True(rootDelta.Translation.Length() < 0.05f, $"root moved {rootDelta.Translation.Length()}.");
        Assert.True(System.Math.Abs(rootDelta.M11) > 0.9f, $"root not upright: M11={rootDelta.M11}.");
    }

    [Fact]
    public void LoadAnimations_FromStreams_ShouldAttachTwoMotionFilesToOneSkeleton()
    {
        // What the gallery's FBX page does: one model file plus two motion files, read through the
        // stream overload AssetBatch hands it, all landing on the Skeleton the model was built with.
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));

        List<Animation> clips = [];

        foreach (var file in new[] { "Idle_Rifle_Hip.FBX", "Jog_Fwd_Rifle.FBX" })
        {
            using var stream = File.OpenRead(Asset(file));

            clips.AddRange(FbxLoader.LoadAnimations(stream, model.Skeleton));
        }

        Assert.Equal(["Unreal Take", "Unreal Take"], clips.Select(clip => clip.Name));
        Assert.Equal(2, clips.Count);

        // The take name is what ufbx reports, and both motion files carry the same one, so a clip's
        // identity has to come from the file it came from - the durations and the poses are what say
        // the two are different clips.
        Assert.Equal(4.0f, clips[0].Duration, 3);
        Assert.Equal(1.3333f, clips[1].Duration, 3);

        Assert.All(clips, clip => Assert.Same(model.Skeleton, clip.Skeleton));
        Assert.All(clips, clip => Assert.Equal(68, clip.Channels.Count));

        var pelvis = model.Skeleton!.Bones.Single(bone => bone.Name == "pelvis");

        Assert.NotEqual(clips[0].Sample(pelvis.Name, 0.5f), clips[1].Sample(pelvis.Name, 0.5f));
    }

    [Fact]
    public void LoadAnimations_ShouldPoseEveryBoneWhenTheSamplerRuns()
    {
        var model = FbxLoader.Load(Asset("SK_Mannequin.FBX"));

        var clip = Assert.Single(FbxLoader.LoadAnimations(Asset("Idle_Rifle_Hip.FBX"), model.Skeleton));

        var skeleton = model.Skeleton!;

        HashSet<Bone> reachable = [];

        void Walk(Bone bone)
        {
            if (!reachable.Add(bone))
                return;

            foreach (var child in bone.Children)
                Walk(child);
        }

        // The sampler poses the hierarchy by walking Skeleton.Root, so a bone it cannot reach stays
        // in bind pose forever and the model looks frozen rather than wrong.
        Walk(skeleton.Root);

        Assert.Equal(skeleton.Bones.Count, reachable.Count);

        var sampler = new AnimationSampler(clip);
        var atStart = sampler.BonesTransform.ToArray();

        for (int i = 0; i < 5; i++)
            sampler.Update(0.2);

        var moved = 0;

        for (int i = 0; i < atStart.Length; i++)
        {
            if (atStart[i] != sampler.BonesTransform[i])
                moved++;
        }

        Assert.Equal(skeleton.Bones.Count, atStart.Length);
        Assert.True(moved >= skeleton.Bones.Count / 2, $"only {moved} of {skeleton.Bones.Count} bones changed");
    }

    [Fact]
    public void LoadAnimations_ShouldReturnNothing_WithoutASkeletonOrSkins()
    {
        // An animation-only FBX has no skin clusters, so it cannot produce a skeleton by itself.
        var clips = FbxLoader.LoadAnimations(Asset("Idle_Rifle_Hip.FBX"));

        Assert.Empty(clips);
    }

    [Fact]
    public void LoadModelAndAnimations_ShouldShareOneSkeleton()
    {
        var (model, clips) = FbxLoader.LoadModelAndAnimations(Asset("SK_Mannequin.FBX"));

        Assert.NotNull(model.Skeleton);
        Assert.All(clips, clip => Assert.Same(model.Skeleton, clip.Skeleton));

        var (jogModel, jogClips) = FbxLoader.LoadModelAndAnimations(Asset("Jog_Fwd_Rifle.FBX"));

        Assert.Null(jogModel.Skeleton);
        Assert.Empty(jogClips);
        Assert.Empty(jogModel.Meshes);
    }
}
