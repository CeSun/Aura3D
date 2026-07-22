using Aura3D.Core.Scenes;
using Aura3D.Core.Resources;
using System.Numerics;
using Aura3D.Core.Math;
using Aura3D.Core.Renderers;
using Aura3D.Core.Exceptions;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the node type.
/// </summary>
public partial class Node
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = "Node";

    /// <summary>
    /// Gets the tags.
    /// </summary>
    public HashSet<string> Tags { get; } = new HashSet<string>();

    #region Transform

    /// <summary>
    /// Gets the position.
    /// </summary>
    private Vector3 _position;

    /// <summary>
    /// Gets the position.
    /// </summary>
    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position == value)
                return;

            _position = value;

            if (_autoUpdateTransform)
            {
                updateLocalTransform();
                updateWorldTransform();
                updateChildrenWorldTransform();
            }
        }
    }


    /// <summary>
    /// Gets the rotation.
    /// </summary>
    private Vector3 _rotation;

    /// <summary>
    /// Gets the rotation.
    /// </summary>
    public Vector3 Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
                return;

            _rotation = value;

            _rotationDegrees = new Vector3(value.X.RadiansToDegree(), value.Y.RadiansToDegree(), value.Z.RadiansToDegree());

            _rotationQuaternion = Quaternion.CreateFromYawPitchRoll(value.Y, value.X, value.Z);

            if (_autoUpdateTransform)
            {
                updateLocalTransform();
                updateWorldTransform();
                updateChildrenWorldTransform();
            }
        }
    }


    /// <summary>
    /// Gets the rotation degrees.
    /// </summary>
    private Vector3 _rotationDegrees;

    /// <summary>
    /// Gets the rotation degrees.
    /// </summary>
    public Vector3 RotationDegrees
    {
        get => _rotationDegrees;
        set
        {
            if (_rotationDegrees == value)
                return;

            _rotationDegrees = value;

            _rotation = new Vector3(value.X.DegreeToRadians(), value.Y.DegreeToRadians(), value.Z.DegreeToRadians());

            _rotationQuaternion = Quaternion.CreateFromYawPitchRoll(_rotation.Y, _rotation.X, _rotation.Z);

            if (_autoUpdateTransform)
            {
                updateLocalTransform();
                updateWorldTransform();
                updateChildrenWorldTransform();
            }
        }
    }

    /// <summary>
    /// Gets the rotation quaternion.
    /// </summary>
    private Quaternion _rotationQuaternion;

    /// <summary>
    /// Gets the rotation quaternion.
    /// </summary>
    public Quaternion RotationQuaternion
    {
        get => _rotationQuaternion;
        set
        {
            if (_rotationQuaternion == value)
                return;

            _rotationQuaternion = value;

            _rotation = _rotationQuaternion.ToEulerAngles();

            _rotationDegrees = new Vector3(_rotation.X.RadiansToDegree(), _rotation.Y.RadiansToDegree(), _rotation.Z.RadiansToDegree());

            if (_autoUpdateTransform)
            {
                updateLocalTransform();
                updateWorldTransform();
                updateChildrenWorldTransform();
            }
        }
    }


    private Vector3 _scale;

    /// <summary>
    /// Gets the scale.
    /// </summary>
    public Vector3 Scale
    {
        get => _scale;
        set
        {
            if (_scale == value)
                return;

            _scale = value;

            if (_autoUpdateTransform)
            {
                updateLocalTransform();
                updateWorldTransform();
                updateChildrenWorldTransform();
            }
        }
    }



    private Matrix4x4 _localTransform;
    /// <summary>
    /// Gets the local transform.
    /// </summary>
    public Matrix4x4 LocalTransform 
    { 
        get => _localTransform;
        set
        {
            _localTransform = value;

            using (BeginTransformUpdate(UpdateTransformMode.World | UpdateTransformMode.ChildrenWorld))
            {
                Position = _localTransform.Translation;

                RotationQuaternion = _localTransform.Rotation();

                Scale = _localTransform.Scale();
            }
        }
    }

    private Matrix4x4 _worldTransform;
    /// <summary>
    /// Gets the world transform.
    /// </summary>
    public Matrix4x4 WorldTransform
    {
        get 
        {
            return _worldTransform;
        }
        set
        {
            _worldTransform = value;

            Matrix4x4 localTransform = default;

            if (Parent != null)
            {
                localTransform = _worldTransform * Parent.WorldTransform.Inverse();
            }
            else
            {
                localTransform = _worldTransform;
            }

            using (BeginTransformUpdate(UpdateTransformMode.Local | UpdateTransformMode.ChildrenWorld))
            {
                Position = localTransform.Translation;

                RotationQuaternion = localTransform.Rotation();

                Scale = localTransform.Scale();
            }

            OnWorldTransformChanged();
        }
    }

    
    private bool _autoUpdateTransform = true;


    private class TransformUpdateScope(Node node, UpdateTransformMode updateTransformMode) : IDisposable
    {
        UpdateTransformMode updateTransformMode = updateTransformMode;
        public void Dispose()
        {
            node.EndTransformUpdate(updateTransformMode);
        }
    }

    /// <summary>
    /// Performs the begin transform update operation.
    /// </summary>
    public IDisposable BeginTransformUpdate(UpdateTransformMode updateTransformMode = UpdateTransformMode.All)
    {
        _autoUpdateTransform = false;

        return new TransformUpdateScope(this, updateTransformMode);
    }

    /// <summary>
    /// Performs the end transform update operation.
    /// </summary>
    protected void EndTransformUpdate(UpdateTransformMode updateTransformMode)
    {
        _autoUpdateTransform = true;

        if (updateTransformMode.HasFlag(UpdateTransformMode.Local))
            updateLocalTransform();
        if (updateTransformMode.HasFlag(UpdateTransformMode.World))
            updateWorldTransform();
        if (updateTransformMode.HasFlag(UpdateTransformMode.ChildrenWorld))
            updateChildrenWorldTransform();
    }


    private void updateWorldTransform()
    {
        if (Parent != null)
        {
            _worldTransform = _localTransform * Parent.WorldTransform;
        }
        else
        {
            _worldTransform = _localTransform;
        }
        OnWorldTransformChanged();
    }

    /// <summary>
    /// Performs the on world transform changed operation.
    /// </summary>
    protected virtual void OnWorldTransformChanged()
    {

    }

    private void updateChildrenWorldTransform()
    {
        foreach (var child in Children)
        {
            child.updateWorldTransform();
            child.updateChildrenWorldTransform();
        }
    }

    private void updateLocalTransform()
    {
        _localTransform = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);
    }

    /// <summary>
    /// Gets the forward.
    /// </summary>
    public Vector3 Forward => WorldTransform.ForwardVector();

    /// <summary>
    /// Gets the backward.
    /// </summary>
    public Vector3 Backward => -1 * Forward;

    /// <summary>
    /// Gets the up.
    /// </summary>
    public Vector3 Up => WorldTransform.UpVector();

    /// <summary>
    /// Gets the down.
    /// </summary>
    public Vector3 Down => -1 * Up;

    /// <summary>
    /// Gets the right.
    /// </summary>
    public Vector3 Right => WorldTransform.RightVector();

    /// <summary>
    /// Gets the left.
    /// </summary>
    public Vector3 Left => -1 * Right;

    /// <summary>
    /// Initializes a new instance of the node type.
    /// </summary>
    public Node()
    {

        _rotationDegrees = new Vector3(0, 0, 0);

        _rotation = new Vector3(_rotationDegrees.X.DegreeToRadians(), _rotationDegrees.Y.DegreeToRadians(), _rotationDegrees.Z.DegreeToRadians());

        _rotationQuaternion = Quaternion.CreateFromYawPitchRoll(_rotation.Y, _rotation.X, _rotation.Z);

        _scale = new Vector3(1.0f, 1.0f, 1.0f);

        updateLocalTransform();

        updateWorldTransform();
    }

    #endregion

    #region Hierarchy

    /// <summary>
    /// Gets or sets the current scene.
    /// </summary>
    public Scene? CurrentScene { get; internal set; }

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public Node? Parent { get; private set; }


    /// <summary>
    /// Determines whether h set.
    /// </summary>
    protected HashSet<Node> _children = new HashSet<Node>();

    /// <summary>
    /// Gets the children.
    /// </summary>
    public IReadOnlySet<Node> Children => _children;

    /// <summary>
    /// Adds the child.
    /// </summary>
    public void AddChild(Node child, AttachToParentRule attachToParentRule)
    {
        // 检查子节点是否已存在，若存在则不重复添加
        if (_children.Contains(child))
            throw SceneGraphErrors.ChildAlreadyExists(this, child);

        if (child == this) 
            throw SceneGraphErrors.CannotAddNodeAsOwnChild(this);

        if (checkCircle(child) == true)
            throw SceneGraphErrors.CircularHierarchy(this, child);
        
        if (child.Parent != null)
            throw SceneGraphErrors.ChildAlreadyHasParent(this, child);

        if (CurrentScene == null)
        {
            EnsureSubtreeDetached(child);
        }
        else if (child.CurrentScene == null)
        {
            CurrentScene.ValidateSubtreeCanBeAdded(child);
        }
        else if (!ReferenceEquals(CurrentScene, child.CurrentScene))
        {
            throw SceneGraphErrors.ParentAndChildBelongToDifferentScenes(this, child);
        }
        else
        {
            CurrentScene.ValidateSubtreeBelongsToScene(child);
        }

        var childWorldTransform = child.WorldTransform;
        // 将子节点加入集合
        _children.Add(child);

        // 设置子节点的父节点为当前节点
        child.Parent = this;

        if (attachToParentRule == AttachToParentRule.KeepWorld)
        {
            // 更新子节点的本地变换，使其世界空间位置保持不变
            child.WorldTransform = childWorldTransform;
        }
        else
        {
            // 更新子节点的世界变换，使其相对于父节点的位置保持不变
            child.updateWorldTransform();
        }
       

        if (Enable == false)
            child.Enable = false;
        else 
            child.Enable = true;

        if (CurrentScene != null && child.CurrentScene == null)
        {
            CurrentScene.AddNode(child);
        }
    }

    private void EnsureSubtreeDetached(Node node)
    {
        if (node.CurrentScene != null)
            throw SceneGraphErrors.CannotAttachSceneNodeToDetachedParent(this, node);

        foreach (var child in node.Children)
        {
            EnsureSubtreeDetached(child);
        }
    }

    private bool checkCircle(Node child)
    {
        if (Parent == null)
            return false;
        if (Parent == child)
            return true;
        return Parent.checkCircle(child);
    }

    /// <summary>
    /// Removes the child.
    /// </summary>
    public void RemoveChild(Node child, AttachToParentRule attachToParentRule)
    {
        // 检查子节点是否存在，若不存在则不处理
        if (_children.Contains(child) == false)
        {
            throw SceneGraphErrors.ChildNotFound(this, child);
        }

        if (CurrentScene != null)
        {
            CurrentScene.ValidateSubtreeBelongsToScene(child);
        }
        else if (child.CurrentScene != null)
        {
            throw SceneGraphErrors.SceneOwnershipMismatch(this, child);
        }

        // 从集合中移除子节点
        _children.Remove(child); 

        if (attachToParentRule == AttachToParentRule.KeepWorld)
        {
            // 记录子节点当前的世界变换
            var lastWorldTransform = child.WorldTransform;

            // 清除子节点的父节点引用
            child.Parent = null;

            // 将子节点的本地变换设置为其世界变换，保持位置不变
            child.LocalTransform = lastWorldTransform;
        }
        else
        {
            child.Parent = null;
        }

        if (CurrentScene != null)
        {
            CurrentScene.RemoveNode(child);
        }
    }

    /// <summary>
    /// Gets the enable.
    /// </summary>
    public bool Enable 
    {
        get => _enable; 
        set
        {
            _enable = value;
            foreach (var child in Children)
            {
                child.Enable = value;
            }
        }
    }

    private bool _enable = true;

    /// <summary>
    /// Performs the list operation.
    /// </summary>
    public List<T> GetNodesInChildren<T>() where T : Node
    {
        var list = new List<T>();
        if (this is T t)
        {
            list.Add(t);
        }
        foreach (var child in Children)
        {
            list.AddRange(child.GetNodesInChildren<T>());
        }
        return list;
    }

    #endregion

    private readonly Dictionary<string, IRuntimeGpuState> _pipelineGpuStates = new Dictionary<string, IRuntimeGpuState>();

    /// <summary>
    /// Represents the get pipeline gpu state type.
    /// </summary>
    public T? GetPipelineGpuState<T>(string name) where T : class, IRuntimeGpuState
    {
        if (_pipelineGpuStates.TryGetValue(name, out var resource))
        {
            if (resource is T typedResource)
            {
                return typedResource;
            }
            else
            {
                throw RendererErrors.GpuStateTypeMismatch(name, typeof(T));
            }
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Removes the pipeline gpu state.
    /// </summary>
    public void RemovePipelineGpuState(string name)
    {
        _pipelineGpuStates.Remove(name);
    }

    /// <summary>
    /// Performs the query pipeline gpu states operation.
    /// </summary>
    public IQueryable<IRuntimeGpuState> QueryPipelineGpuStates()
    {
        return _pipelineGpuStates.Values.AsQueryable();
    }

    /// <summary>
    /// Clears the pipeline gpu states.
    /// </summary>
    public void ClearPipelineGpuStates()
    {
        _pipelineGpuStates.Clear();
    }


    /// <summary>
    /// Sets the pipeline gpu state.
    /// </summary>
    public void SetPipelineGpuState(string name, IRuntimeGpuState resource)
    {
        _pipelineGpuStates[name] = resource;
    }

    /// <summary>
    /// Updates the associated data.
    /// </summary>
    public virtual void Update(double delta)
    {

    }
}

/// <summary>
/// Specifies values for attach to parent rule.
/// </summary>
public enum AttachToParentRule
{
    /// <summary>
    /// Specifies keep world.
    /// </summary>
    KeepWorld,
    /// <summary>
    /// Specifies keep local.
    /// </summary>
    KeepLocal
}

/// <summary>
/// Specifies values for update transform mode.
/// </summary>
public enum UpdateTransformMode
{
    /// <summary>
    /// Gets the local.
    /// </summary>
    Local = 1 << 0,
    /// <summary>
    /// Gets the world.
    /// </summary>
    World = 1 << 1,
    /// <summary>
    /// Gets the children world.
    /// </summary>
    ChildrenWorld = 1 << 2,
    /// <summary>
    /// Gets the all.
    /// </summary>
    All = Local | World | ChildrenWorld
}
