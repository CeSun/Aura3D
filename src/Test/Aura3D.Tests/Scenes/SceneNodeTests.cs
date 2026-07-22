using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Xunit;

namespace Aura3D.Tests.Scenes;

public class SceneNodeTests
{
    [Fact]
    public void AddChild_ShouldRegisterDetachedSubtreeInParentScene()
    {
        var scene = CreateScene();
        var parent = new Node();
        var child = new Node();
        var grandchild = new Node();
        child.AddChild(grandchild, AttachToParentRule.KeepLocal);
        scene.AddNode(parent);

        parent.AddChild(child, AttachToParentRule.KeepLocal);

        Assert.Same(parent, child.Parent);
        Assert.Same(scene, child.CurrentScene);
        Assert.Same(scene, grandchild.CurrentScene);
        Assert.Contains(child, scene.Nodes);
        Assert.Contains(grandchild, scene.Nodes);
    }

    [Fact]
    public void AddChild_ShouldAttachExistingRootFromSameSceneWithoutReregistering()
    {
        var scene = CreateScene();
        var parent = new Node();
        var child = new Node();
        scene.AddNode(parent);
        scene.AddNode(child);

        parent.AddChild(child, AttachToParentRule.KeepLocal);

        Assert.Same(parent, child.Parent);
        Assert.Same(scene, child.CurrentScene);
        Assert.Contains(child, scene.Nodes);
    }

    [Fact]
    public void AddChild_ShouldRejectCrossSceneAttachmentWithoutMutation()
    {
        var parentScene = CreateScene();
        var childScene = CreateScene();
        var parent = new Node();
        var child = new Node();
        parentScene.AddNode(parent);
        childScene.AddNode(child);

        Assert.Throws<InvalidOperationException>(
            () => parent.AddChild(child, AttachToParentRule.KeepLocal));

        Assert.Null(child.Parent);
        Assert.DoesNotContain(child, parent.Children);
        Assert.Same(childScene, child.CurrentScene);
        Assert.Contains(child, childScene.Nodes);
    }

    [Fact]
    public void RemoveNode_ShouldRejectNonRootWithoutMutation()
    {
        var scene = CreateScene();
        var parent = new Node();
        var child = new Node();
        parent.AddChild(child, AttachToParentRule.KeepLocal);
        scene.AddNode(parent);

        Assert.Throws<InvalidOperationException>(() => scene.RemoveNode(child));

        Assert.Same(parent, child.Parent);
        Assert.Contains(child, parent.Children);
        Assert.Same(scene, child.CurrentScene);
        Assert.Contains(child, scene.Nodes);
    }

    [Fact]
    public void RemoveChild_ShouldUnregisterEntireSubtree()
    {
        var scene = CreateScene();
        var parent = new Node();
        var child = new Node();
        var grandchild = new Node();
        child.AddChild(grandchild, AttachToParentRule.KeepLocal);
        parent.AddChild(child, AttachToParentRule.KeepLocal);
        scene.AddNode(parent);

        parent.RemoveChild(child, AttachToParentRule.KeepWorld);

        Assert.Null(child.Parent);
        Assert.Null(child.CurrentScene);
        Assert.Null(grandchild.CurrentScene);
        Assert.DoesNotContain(child, scene.Nodes);
        Assert.DoesNotContain(grandchild, scene.Nodes);
        Assert.Same(scene, parent.CurrentScene);
    }

    private static Scene CreateScene()
    {
        return new Scene(scene => new TestRenderPipeline(scene));
    }

    private sealed class TestRenderPipeline(Scene scene) : RenderPipeline(scene)
    {
    }
}
