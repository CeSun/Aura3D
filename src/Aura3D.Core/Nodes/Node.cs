using Aura3D.Core.Scenes;
using Aura3D.Core.Resources;
using System.Numerics;
using Aura3D.Core.Math;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 表示场景中的节点对象，支持变换（位置、旋转、缩放）及层级关系管理。
/// </summary>
public partial class Node
{
    public string Name { get; set; } = "Node";
    public HashSet<string> Tags { get; } = new HashSet<string>();

    #region Transform

    /// <summary>
    /// 节点的旋转四元数表示。
    /// </summary>
    private Quaternion _rotationQuaternion;

    /// <summary>
    /// 节点的欧拉角（度数）表示。
    /// </summary>
    private Vector3 _rotationDegrees;

    /// <summary>
    /// 节点的欧拉角（弧度）表示。
    /// </summary>
    private Vector3 _rotation;

    /// <summary>
    /// 节点的位置。
    /// </summary>
    private Vector3 _position;

    /// <summary>
    /// 节点的缩放，默认为 (1,1,1)。
    /// </summary>
    private Vector3 _scale = new Vector3(1.0f, 1.0f, 1.0f);

    /// <summary>
    /// 节点的所有子节点集合。
    /// </summary>
    private HashSet<Node> _children = new HashSet<Node>();


    private Matrix4x4 _transform;

    private Matrix4x4 _worldtransform;


    /// <summary>
    /// 获取或设置节点的位置。
    /// </summary>
    public Vector3 Position 
    { 
        get => _position;
        set 
        {
            _position = value;

            _transform = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);

            if (Parent == null)
            {
                _worldtransform = _transform;
            }
            else
            {
                _worldtransform = _transform * Parent.WorldTransform;
            }

            updateChildrenWorldTransform();
        }
    }

    /// <summary>
    /// 获取或设置节点的旋转（弧度）。
    /// </summary>
    public Vector3 Rotation 
    { 
        get => _rotation;
        set
        {
            _rotation = value;

            _rotationDegrees = new Vector3(value.X.RadiansToDegree(), value.Y.RadiansToDegree(), value.Z.RadiansToDegree());

            _rotationQuaternion = Quaternion.CreateFromYawPitchRoll(value.Y, value.X, value.Z);

            _transform = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);

            if (Parent == null)
            {
                _worldtransform = _transform;
            }
            else
            {
                _worldtransform = _transform * Parent.WorldTransform;
            }

            updateChildrenWorldTransform();
        }
    }

    /// <summary>
    /// 获取或设置节点的旋转（度数）。
    /// </summary>
    public Vector3 RotationDegrees 
    { 
        get => _rotationDegrees;
        set
        {
            _rotationDegrees = value;

            _rotation = new Vector3(value.X.DegreeToRadians(), value.Y.DegreeToRadians(), value.Z.DegreeToRadians());

            _rotationQuaternion = Quaternion.CreateFromYawPitchRoll(_rotation.Y, _rotation.X, _rotation.Z);

            _transform = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);

            if (Parent == null)
            {
                _worldtransform = _transform;
            }
            else
            {
                _worldtransform = _transform * Parent.WorldTransform;
            }

            updateChildrenWorldTransform();
        }
    }

    /// <summary>
    /// 获取或设置节点的旋转（四元数）。
    /// </summary>
    public Quaternion RotationQuaternion 
    { 
        get => _rotationQuaternion;
        set
        {
            _rotationQuaternion = value;

            _rotation = _rotationQuaternion.ToEulerAngles();

            _rotationDegrees = new Vector3(_rotation.X.RadiansToDegree(), _rotation.Y.RadiansToDegree(), _rotation.Z.RadiansToDegree());

            _transform = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);

            if (Parent == null)
            {
                _worldtransform = _transform;
            }
            else
            {
                _worldtransform = _transform * Parent.WorldTransform;
            }

            updateChildrenWorldTransform();
        }
    }

    /// <summary>
    /// 获取或设置节点的缩放。缩放值必须为正数。
    /// </summary>
    public Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;

            _transform = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);

            if (Parent == null)
            {
                _worldtransform = _transform;
            }
            else
            {
                _worldtransform = _transform * Parent.WorldTransform;
            }


            updateChildrenWorldTransform();
        }
    }


    /// <summary>
    /// 获取节点的本地变换矩阵。
    /// </summary>
    public Matrix4x4 Transform 
    { 
        get => _transform;
        set
        {

            _position = value.Translation;

            _rotationQuaternion = value.Rotation();

            _rotation = _rotationQuaternion.ToEulerAngles();

            _rotationDegrees = new Vector3(_rotation.X.RadiansToDegree(), _rotation.Y.RadiansToDegree(), _rotation.Z.RadiansToDegree());

            _scale = value.Scale();

            _transform = value;

            if (Parent == null)
            {
                _worldtransform = _transform;
            }
            else
            {
                _worldtransform = _transform * Parent.WorldTransform;
            }


            updateChildrenWorldTransform();
        }
    }

    /// <summary>
    /// 获取节点的世界变换矩阵（包含父节点变换）。
    /// </summary>
    public Matrix4x4 WorldTransform
    {   
        
        set
        {
            _worldtransform = value;

            Matrix4x4 transform;

            if (Parent != null)
            {

                Matrix4x4.Invert(Parent.WorldTransform, out var inverseWorldTransform);

                transform = _worldtransform * inverseWorldTransform;

            }
            else
            {
                transform = _worldtransform;
            }


            _position = transform.Translation;

            _rotationQuaternion = transform.Rotation();

            _rotation = _rotationQuaternion.ToEulerAngles();

            _rotationDegrees = new Vector3(_rotation.X.RadiansToDegree(), _rotation.Y.RadiansToDegree(), _rotation.Z.RadiansToDegree());

            _scale = transform.Scale();

            _transform = transform;

            updateChildrenWorldTransform();

        }
        get => _worldtransform;


    }

    public Vector3 Forward => WorldTransform.ForwardVector();

    public Vector3 Backward => -1 * Forward;

    public Vector3 Up => WorldTransform.UpVector();

    public Vector3 Down => -1 * Up;

    public Vector3 Right => WorldTransform.RightVector();

    public Vector3 Left => -1 * Right;

    public Node()
    {
        _worldtransform  = MatrixHelper.CreateTransform(_position, _rotationQuaternion, _scale);

        _transform = _worldtransform;

        _rotation = new Vector3(_rotationDegrees.X.DegreeToRadians(), _rotationDegrees.Y.DegreeToRadians(), _rotationDegrees.Z.DegreeToRadians());

        _rotationQuaternion = Quaternion.CreateFromYawPitchRoll(_rotation.Y, _rotation.X, _rotation.Z);

        updateChildrenWorldTransform();
    }
    protected void updateChildrenWorldTransform()
    {

        foreach (var child in Children)
        {
            child._worldtransform = child.Transform * _worldtransform;

            child.updateChildrenWorldTransform();
        }

    }
    #endregion

    #region Hierarchy

    public Scene? CurrentScene { get; internal set; }

    /// <summary>
    /// 获取节点的父节点。
    /// </summary>
    public Node? Parent { get; private set; }

    /// <summary>
    /// 获取节点的所有子节点（只读）。
    /// </summary>
    public IReadOnlySet<Node> Children => _children;

    /// <summary>
    /// 将指定子节点添加到当前节点，并更新其变换，使其在世界空间中的位置保持不变。
    /// </summary>
    /// <param name="child">要添加的子节点。</param>
    public void AddChild(Node child)
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

        var tempWorldTransform = child.WorldTransform;

        // 设置子节点的父节点为当前节点
        child.Parent = this; 

        // 更新子节点的本地变换，使其世界空间位置保持不变
        child.WorldTransform = tempWorldTransform;

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
    public void RemoveChild(Node child)
    {
        // 检查子节点是否存在，若不存在则不处理
        if (_children.Contains(child) == false)
        {
            throw new InvalidOperationException("子节点不存在");
        }

        // 从集合中移除子节点
        _children.Remove(child); 

        // 记录子节点当前的世界变换
        var lastWorldTransform = child.WorldTransform;

        // 清除子节点的父节点引用
        child.Parent = null;

        // 将子节点的本地变换设置为其世界变换，保持位置不变
        child.Transform = lastWorldTransform;

        if (CurrentScene != null)
        {
            CurrentScene.RemoveNode(child);
        }
    }
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


    public virtual List<IGpuResource> GetGpuResources()
    {
        return [];
    }

    public virtual void Update(double delta)
    {

    }
}