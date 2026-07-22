using Aura3D.Core.Nodes;

namespace Aura3D.Core.Exceptions;

/// <summary>
/// Identifies a stable scene-graph failure independently of its display message.
/// </summary>
public enum SceneGraphError
{
    /// <summary>The child is already attached to the parent.</summary>
    ChildAlreadyExists,

    /// <summary>A node was added as its own child.</summary>
    CannotAddNodeAsOwnChild,

    /// <summary>The operation would create a hierarchy cycle.</summary>
    CircularHierarchy,

    /// <summary>The child already has another parent.</summary>
    ChildAlreadyHasParent,

    /// <summary>The parent and child belong to different scenes.</summary>
    ParentAndChildBelongToDifferentScenes,

    /// <summary>A scene-owned node was attached to a detached parent.</summary>
    CannotAttachSceneNodeToDetachedParent,

    /// <summary>The specified node is not a child of the parent.</summary>
    ChildNotFound,

    /// <summary>The parent and child have inconsistent scene ownership.</summary>
    SceneOwnershipMismatch,

    /// <summary>The node's parent is not registered in the scene.</summary>
    ParentDoesNotBelongToScene,

    /// <summary>The subtree contains a cycle or repeated node reference.</summary>
    SubtreeContainsCycleOrDuplicate,

    /// <summary>The node already belongs to a scene.</summary>
    NodeAlreadyBelongsToScene,

    /// <summary>The subtree and scene registry disagree.</summary>
    SubtreeSceneRegistrationMismatch,

    /// <summary>A non-root node was removed directly from a scene.</summary>
    NonRootNodeRemoval,
}

/// <summary>
/// Represents the scene graph exception type.
/// </summary>
public sealed class SceneGraphException : InvalidOperationException
{
    internal SceneGraphException(
        SceneGraphError code,
        string message,
        Node? node = null,
        Node? relatedNode = null)
        : base(message)
    {
        Code = code;
        Node = node;
        RelatedNode = relatedNode;
    }

    /// <summary>
    /// Gets the code.
    /// </summary>
    public SceneGraphError Code { get; }

    /// <summary>
    /// Gets the node.
    /// </summary>
    public Node? Node { get; }

    /// <summary>
    /// Gets the related node.
    /// </summary>
    public Node? RelatedNode { get; }
}

internal static class SceneGraphErrors
{
    private const string ChildAlreadyExistsMessage =
        "The child node is already attached to this parent.";

    private const string CannotAddNodeAsOwnChildMessage =
        "A node cannot be added as its own child.";

    private const string CircularHierarchyMessage =
        "Adding this child would create a cycle in the node hierarchy.";

    private const string ChildAlreadyHasParentMessage =
        "The child node already has a parent.";

    private const string ParentAndChildBelongToDifferentScenesMessage =
        "The parent and child nodes belong to different scenes.";

    private const string CannotAttachSceneNodeToDetachedParentMessage =
        "A node that belongs to a scene cannot be attached to a parent that is not in a scene.";

    private const string ChildNotFoundMessage =
        "The specified node is not a child of this parent.";

    private const string SceneOwnershipMismatchMessage =
        "The parent and child nodes have inconsistent scene ownership.";

    private const string ParentDoesNotBelongToSceneMessage =
        "The node's parent does not belong to this scene.";

    private const string SubtreeContainsCycleOrDuplicateMessage =
        "The node subtree contains a cycle or duplicate reference.";

    private const string NodeAlreadyBelongsToSceneMessage =
        "The node already belongs to a scene.";

    private const string SubtreeSceneRegistrationMismatchMessage =
        "The node subtree is inconsistent with the scene registry.";

    private const string NonRootNodeRemovalMessage =
        "A non-root node must be removed through its parent node.";

    public static SceneGraphException ChildAlreadyExists(Node parent, Node child) =>
        Create(SceneGraphError.ChildAlreadyExists, ChildAlreadyExistsMessage, parent, child);

    public static SceneGraphException CannotAddNodeAsOwnChild(Node node) =>
        Create(SceneGraphError.CannotAddNodeAsOwnChild, CannotAddNodeAsOwnChildMessage, node, node);

    public static SceneGraphException CircularHierarchy(Node parent, Node child) =>
        Create(SceneGraphError.CircularHierarchy, CircularHierarchyMessage, parent, child);

    public static SceneGraphException ChildAlreadyHasParent(Node parent, Node child) =>
        Create(SceneGraphError.ChildAlreadyHasParent, ChildAlreadyHasParentMessage, parent, child);

    public static SceneGraphException ParentAndChildBelongToDifferentScenes(Node parent, Node child) =>
        Create(
            SceneGraphError.ParentAndChildBelongToDifferentScenes,
            ParentAndChildBelongToDifferentScenesMessage,
            parent,
            child);

    public static SceneGraphException CannotAttachSceneNodeToDetachedParent(Node parent, Node child) =>
        Create(
            SceneGraphError.CannotAttachSceneNodeToDetachedParent,
            CannotAttachSceneNodeToDetachedParentMessage,
            parent,
            child);

    public static SceneGraphException ChildNotFound(Node parent, Node child) =>
        Create(SceneGraphError.ChildNotFound, ChildNotFoundMessage, parent, child);

    public static SceneGraphException SceneOwnershipMismatch(Node parent, Node child) =>
        Create(SceneGraphError.SceneOwnershipMismatch, SceneOwnershipMismatchMessage, parent, child);

    public static SceneGraphException ParentDoesNotBelongToScene(Node node) =>
        Create(SceneGraphError.ParentDoesNotBelongToScene, ParentDoesNotBelongToSceneMessage, node, node.Parent);

    public static SceneGraphException SubtreeContainsCycleOrDuplicate(Node node) =>
        Create(SceneGraphError.SubtreeContainsCycleOrDuplicate, SubtreeContainsCycleOrDuplicateMessage, node);

    public static SceneGraphException NodeAlreadyBelongsToScene(Node node) =>
        Create(SceneGraphError.NodeAlreadyBelongsToScene, NodeAlreadyBelongsToSceneMessage, node);

    public static SceneGraphException SubtreeSceneRegistrationMismatch(Node node) =>
        Create(
            SceneGraphError.SubtreeSceneRegistrationMismatch,
            SubtreeSceneRegistrationMismatchMessage,
            node);

    public static SceneGraphException NonRootNodeRemoval(Node node) =>
        Create(SceneGraphError.NonRootNodeRemoval, NonRootNodeRemovalMessage, node, node.Parent);

    private static SceneGraphException Create(
        SceneGraphError code,
        string message,
        Node? node = null,
        Node? relatedNode = null) =>
        new(code, message, node, relatedNode);
}
