using Aura3D.Core.Scenes;
using Aura3D.Core.Resources;
using System.Numerics;
using Aura3D.Core.Math;
using Aura3D.Core.Renderers;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 表示场景中的节点对象，支持变换（位置、旋转、缩放）及层级关系管理。
/// </summary>
public partial class Node
{
    /// <summary>
    /// 获取或设置节点名称。
    /// </summary>
    public string Name { get; set; } = "Node";

    /// <summary>
    /// 获取节点的标签集合。
    /// </summary>
    public HashSet<string> Tags { get; } = new HashSet<string>();

    #region Transform

    /// <summary>
    /// 节点的位置。
    /// </summary>
    private Vector3 _position;

    /// <summary>
    /// 获取或设置节点的位置。设置相同值时不触发变换更新。
    /// 连续修改多个属性时建议使用 <see cref="BeginTransformUpdate"/> 包裹。
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
    /// 节点的欧拉角（弧度）表示。
    /// </summary>
    private Vector3 _rotation;

    /// <summary>
    /// 获取或设置节点的旋转（弧度）。设置相同值时不触发变换更新。
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
    /// 节点的欧拉角（度数）表示。
    /// </summary>
    private Vector3 _rotationDegrees;

    /// <summary>
    /// 获取或设置节点的旋转（度数）。
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
    /// 节点的旋转四元数表示。
    /// </summary>
    private Quaternion _rotationQuaternion;

    /// <summary>
    /// 获取或设置节点的旋转（四元数）。
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
    /// 获取或设置节点的缩放。缩放值必须为正数。
    /// </summary>
    /// <summary>
    /// 获取或设置节点的缩放。设置相同值时不触发变换更新。
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
    /// 获取节点的本地变换矩阵。
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
    /// 获取节点的世界变换矩阵（包含父节点变换）。
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
    /// 开始变换更新作用域，在此期间禁用自动变换更新。
    /// </summary>
    /// <param name="updateTransformMode">需要更新的变换模式。</param>
    /// <returns>变换更新作用域。</returns>
    public IDisposable BeginTransformUpdate(UpdateTransformMode updateTransformMode = UpdateTransformMode.All)
    {
        _autoUpdateTransform = false;

        return new TransformUpdateScope(this, updateTransformMode);
    }

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
    /// 获取节点的前方方向向量。
    /// </summary>
    public Vector3 Forward => WorldTransform.ForwardVector();

    /// <summary>
    /// 获取节点的后方方向向量。
    /// </summary>
    public Vector3 Backward => -1 * Forward;

    /// <summary>
    /// 获取节点的上方方向向量。
    /// </summary>
    public Vector3 Up => WorldTransform.UpVector();

    /// <summary>
    /// 获取节点的下方方向向量。
    /// </summary>
    public Vector3 Down => -1 * Up;

    /// <summary>
    /// 获取节点的右方方向向量。
    /// </summary>
    public Vector3 Right => WorldTransform.RightVector();

    /// <summary>
    /// 获取节点的左方方向向量。
    /// </summary>
    public Vector3 Left => -1 * Right;

    /// <summary>
    /// 初始化 <see cref="Node"/> 类的新实例。
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
    /// 获取或设置当前节点所在的场景。
    /// </summary>
    public Scene? CurrentScene { get; internal set; }

    /// <summary>
    /// 获取节点的父节点。
    /// </summary>
    public Node? Parent { get; private set; }


    protected HashSet<Node> _children = new HashSet<Node>();

    /// <summary>
    /// 获取节点的所有子节点（只读）。
    /// </summary>
    public IReadOnlySet<Node> Children => _children;

    /// <summary>
    /// 将指定子节点添加到当前节点，并更新其变换，使其在世界空间中的位置保持不变。
    /// </summary>
    /// <param name="child">要添加的子节点。</param>
    public void AddChild(Node child, AttachToParentRule attachToParentRule)
    {
        // 检查子节点是否已存在，若存在则不重复添加
        if (_children.Contains(child))
            throw new InvalidOperationException("子节点已存在");

        if (child == this) 
            throw new InvalidOperationException("不能将自身作为子节点");

        if (checkCircle(child) == true)
            throw new InvalidOperationException("不能将父节点添加为子节点，形成循环引用");

        // 将子节点加入集合
        _children.Add(child);

        if (attachToParentRule == AttachToParentRule.KeepWorld)
        {
            var tempWorldTransform = child.WorldTransform;

            // 设置子节点的父节点为当前节点
            child.Parent = this;

            // 更新子节点的本地变换，使其世界空间位置保持不变
            child.WorldTransform = tempWorldTransform;
        }
        else
        {
            child.Parent = this;

            child.updateWorldTransform();
        }
       

        if (Enable == false)
            child.Enable = false;
        else 
            child.Enable = true;

        if (CurrentScene != null)
        {
            CurrentScene.AddNode(child);
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
    /// 从当前节点移除指定子节点，并将其变换恢复为世界空间变换，保持其在场景中的位置不变。
    /// </summary>
    /// <param name="child">要移除的子节点。</param>
    public void RemoveChild(Node child, AttachToParentRule attachToParentRule)
    {
        // 检查子节点是否存在，若不存在则不处理
        if (_children.Contains(child) == false)
        {
            throw new InvalidOperationException("子节点不存在");
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
    /// 获取或设置节点是否启用，同时会级联影响所有子节点。
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
    /// 递归获取当前节点及其子节点中指定类型的节点列表。
    /// </summary>
    /// <typeparam name="T">节点类型。</typeparam>
    /// <returns>匹配的节点列表。</returns>
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

    public Dictionary<string, IGpuState> _pipelineGpuResources = new Dictionary<string, IGpuState>();

    /// <summary>
    /// 按名称获取渲染管线中的 GPU 资源。
    /// </summary>
    /// <typeparam name="T">GPU 状态类型。</typeparam>
    /// <param name="name">资源名称。</param>
    /// <returns>匹配类型的 GPU 状态，若不存在则返回默认值。</returns>
    public T? GetPipelineGpuResource<T>(string name) where T : class, IGpuState
    {
        if (_pipelineGpuResources.TryGetValue(name, out var resource))
        {
            if (resource is T typedResource)
            {
                return typedResource;
            }
            else
            {
                throw new InvalidCastException($"GPU资源 '{name}' 的类型不匹配，无法转换为 {typeof(T).Name}");
            }
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// 移除渲染管线中指定名称的 GPU 资源。
    /// </summary>
    /// <param name="name">资源名称。</param>
    public void RemovePipelineGpuResource(string name)
    {
        _pipelineGpuResources.Remove(name);
    }

    /// <summary>
    /// 查询渲染管线中的所有 GPU 资源。
    /// </summary>
    /// <returns>GPU 状态的可查询集合。</returns>
    public IQueryable<IGpuState> QueryPipelineGpuResources()
    {
        return _pipelineGpuResources.Values.AsQueryable();
    }

    /// <summary>
    /// 清空渲染管线中的所有 GPU 资源。
    /// </summary>
    public void ClearPipelineGpuResources()
    {
        _pipelineGpuResources.Clear();
    }


    /// <summary>
    /// 设置渲染管线中的 GPU 资源。
    /// </summary>
    /// <param name="name">资源名称。</param>
    /// <param name="resource">GPU 状态。</param>
    public void SetPipelineGpuResource(string name, IGpuState resource)
    {
        _pipelineGpuResources[name] = resource;
    }

    /// <summary>
    /// 更新节点状态。
    /// </summary>
    /// <param name="delta">时间增量（秒）。</param>
    public virtual void Update(double delta)
    {

    }
}

/// <summary>
/// 定义子节点附加到父节点时的变换规则。
/// </summary>
public enum AttachToParentRule
{
    KeepWorld,
    KeepLocal
}

/// <summary>
/// 变换更新模式。
/// </summary>
public enum UpdateTransformMode
{
    Local = 1 << 0,
    World = 1 << 1,
    ChildrenWorld = 1 << 2,
    All = Local | World | ChildrenWorld
}
