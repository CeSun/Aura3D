using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Core.Exceptions;
using OneOf;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Scenes;

/// <summary>
/// Represents the scene type.
/// </summary>
public class Scene
{
    /// <summary>
    /// Gets the nodes.
    /// </summary>
    public IReadOnlySet<Node> Nodes => _nodes;

    private readonly HashSet<Node> _nodes = [];

    private readonly HashSet<Node> _dirtyNodes = [];

    /// <summary>
    /// Gets the nodes snapshot.
    /// </summary>
    private readonly List<Node> _nodesSnapshot = [];

    /// <summary>
    /// Gets or sets the main camera.
    /// </summary>
    public Camera MainCamera { get; private set; }

    /// <summary>
    /// Gets or sets the main directional light.
    /// </summary>
    public DirectionalLight? MainDirectionalLight { get; set; }

    /// <summary>
    /// Gets or sets the mesh octree.
    /// </summary>
    public Octree<Mesh> MeshOctree { get; set; }

    /// <summary>
    /// Gets or sets the render pipeline.
    /// </summary>
    public RenderPipeline RenderPipeline { get; set; }

    /// <summary>
    /// Gets the background.
    /// </summary>
    public OneOf<CubeTexture, Texture> Background
    {
        get => _background;
        set
        {
            _background = value;
        }
    }

    private OneOf<CubeTexture, Texture> _background;

    /// <summary>
    /// Gets the pipeline settings.
    /// </summary>
    public PipelineSettings PipelineSettings { get; }

    /// <summary>
    /// Gets the default output surface.
    /// </summary>
    public RenderSurface? DefaultOutputSurface { get; }

    /// <summary>
    /// Initializes a new instance of the scene type.
    /// </summary>
    public Scene(Func<Scene, RenderPipeline> createRenderPipeline,
                PipelineSettings? pipelineSettings = null,
                RenderSurface? defaultOutputSurface = null)
    {
        PipelineSettings = pipelineSettings ?? new PipelineSettings();
        DefaultOutputSurface = defaultOutputSurface;
        RenderPipeline = createRenderPipeline(this);

        MeshOctree = new Octree<Mesh>(new System.Numerics.Vector3(100, 100, 100), 5);

        MainCamera = new Camera();

        Background = Texture.CreateFromColor(Color.AliceBlue);

        AddNode(MainCamera);

        // 内置的调试可视化配置（默认隐藏）
        AxisGizmo = new AxisGizmo();
        Grid = new Grid();
    }

    /// <summary>
    /// Gets or sets the axis gizmo.
    /// </summary>
    public AxisGizmo AxisGizmo { get; private set; }

    /// <summary>
    /// Gets or sets the grid.
    /// </summary>
    public Grid Grid { get; private set; }

    /// <summary>
    /// Gets the show axis gizmo.
    /// </summary>
    public bool ShowAxisGizmo
    {
        get => AxisGizmo.Enable;
        set => AxisGizmo.Enable = value;
    }

    /// <summary>
    /// Gets the show grid.
    /// </summary>
    public bool ShowGrid
    {
        get => Grid.Enable;
        set => Grid.Enable = value;
    }

    /// <summary>
    /// Adds the node.
    /// </summary>
    public void AddNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Parent != null && !ReferenceEquals(node.Parent.CurrentScene, this))
            throw SceneGraphErrors.ParentDoesNotBelongToScene(node);

        ValidateSubtreeCanBeAdded(node);
        AddNodeCore(node);
    }

    internal void ValidateSubtreeCanBeAdded(Node node)
    {
        ValidateSubtreeCanBeAdded(node, []);
    }

    private void ValidateSubtreeCanBeAdded(Node node, HashSet<Node> visited)
    {
        if (!visited.Add(node))
            throw SceneGraphErrors.SubtreeContainsCycleOrDuplicate(node);

        if (node.CurrentScene != null || _nodes.Contains(node))
            throw SceneGraphErrors.NodeAlreadyBelongsToScene(node);

        foreach (var child in node.Children)
        {
            ValidateSubtreeCanBeAdded(child, visited);
        }
    }

    internal void ValidateSubtreeBelongsToScene(Node node)
    {
        ValidateSubtreeBelongsToScene(node, []);
    }

    private void ValidateSubtreeBelongsToScene(Node node, HashSet<Node> visited)
    {
        if (!visited.Add(node))
            throw SceneGraphErrors.SubtreeContainsCycleOrDuplicate(node);

        if (!ReferenceEquals(node.CurrentScene, this) || !_nodes.Contains(node))
            throw SceneGraphErrors.SubtreeSceneRegistrationMismatch(node);

        foreach (var child in node.Children)
        {
            ValidateSubtreeBelongsToScene(child, visited);
        }
    }

    private void AddNodeCore(Node node)
    {

        _nodes.Add(node);

        node.CurrentScene = this;

        RenderPipeline.AddNode(node);

        if (node is IOctreeObject otreeObject)
        {
            otreeObject.OnBoundingBoxChanged += OnBoundingBoxChanged;
        }

        if (node is Mesh mesh)
        {
            MeshOctree.Add(mesh);
        }

        foreach (var child in node.Children)
        {
            AddNodeCore(child);
        }
    }

    /// <summary>
    /// Removes the node.
    /// </summary>
    public void RemoveNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Parent != null)
            throw SceneGraphErrors.NonRootNodeRemoval(node);

        ValidateSubtreeBelongsToScene(node);
        RemoveNodeCore(node);
    }

    private void RemoveNodeCore(Node node)
    {

        _nodes.Remove(node);

        _dirtyNodes.Remove(node);

        node.CurrentScene = null;

        RenderPipeline.RemoveNode(node);


        if (node is IOctreeObject otreeObject)
        {
            otreeObject.OnBoundingBoxChanged -= OnBoundingBoxChanged;
        }


        if (node is Mesh mesh)
        {
            MeshOctree.Remove(mesh);
        }

        foreach (var child in node.Children)
        {
            RemoveNodeCore(child);
        }
        node.ClearPipelineGpuStates();
    }

    /// <summary>
    /// Adds the node transform dirty.
    /// </summary>
    public void AddNodeTransformDirty(Node node)
    {
        if (_nodes.Contains(node) == false)
            return;
        if (_dirtyNodes.Contains(node) == true)
            return;
        _dirtyNodes.Add(node);
    }

    /// <summary>
    /// Performs the on bounding box changed operation.
    /// </summary>
    private void OnBoundingBoxChanged(IOctreeObject otreeObject)
    {
        if (otreeObject is not Node node)
            return;
        AddNodeTransformDirty(node);
    }

    /// <summary>
    /// Updates the associated data.
    /// </summary>
    public void Update(double deltaTime)
    {

        // 快照避免节点 Update 过程中增删子节点导致集合被修改
        _nodesSnapshot.Clear();
        _nodesSnapshot.AddRange(_nodes);
        foreach (var node in _nodesSnapshot)
        {
            if (!_nodes.Contains(node))
                continue;

            node.Update(deltaTime);
        }

        foreach (var node in _dirtyNodes)
        {
            if (_nodes.Contains(node) == false)
                continue;
            if (node is Mesh mesh)
            {
                MeshOctree.Update(mesh);
            }
        }
        _dirtyNodes.Clear();
    }

    /// <summary>
    /// Performs the pick operation.
    /// </summary>
    public List<PickResult> Pick(float screenX, float screenY, Camera? camera = null)
    {
        camera ??= MainCamera;

        var results = new List<PickResult>();

        // 将屏幕坐标转换为世界空间射线
        var ray = ScreenToRay(screenX, screenY, camera);
        if (ray == null)
            return results;

        // 拾取所有 Mesh（包括 Model 的子 Mesh）
        foreach (var node in _nodes)
        {
            if (node.Enable == false)
                continue;

            if (node is Mesh mesh && IsPickable(mesh))
            {
                PickMesh(mesh, ray.Value, results);
            }
            else if (node is InstancedMesh instancedMesh)
            {
                PickInstancedMesh(instancedMesh, ray.Value, results);
            }
        }

        // 按距离排序（由近到远）
        results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return results;
    }

    /// <summary>
    /// Performs the pick closest operation.
    /// </summary>
    public PickResult? PickClosest(float screenX, float screenY, Camera? camera = null)
    {
        var results = Pick(screenX, screenY, camera);
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Performs the screen to ray operation.
    /// </summary>
    private static Ray? ScreenToRay(float screenX, float screenY, Camera camera)
    {
        float width = camera.Width;
        float height = camera.Height;

        if (width <= 0 || height <= 0)
            return null;

        // 屏幕坐标 → NDC（-1 到 1）
        float ndcX = (2.0f * screenX) / width - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / height;

        // 视口空间中的近平面和远平面点
        Vector4 nearClip = new(ndcX, ndcY, -1.0f, 1.0f);
        Vector4 farClip = new(ndcX, ndcY, 1.0f, 1.0f);

        // 逆视图投影矩阵
        var viewProj = camera.View * camera.Projection;
        Matrix4x4.Invert(viewProj, out Matrix4x4 invViewProj);

        // 变换到世界空间
        Vector4 nearWorld = Vector4.Transform(nearClip, invViewProj);
        Vector4 farWorld = Vector4.Transform(farClip, invViewProj);

        // 透视除法
        if (MathF.Abs(nearWorld.W) > float.Epsilon)
            nearWorld /= nearWorld.W;
        if (MathF.Abs(farWorld.W) > float.Epsilon)
            farWorld /= farWorld.W;

        Vector3 origin = new(nearWorld.X, nearWorld.Y, nearWorld.Z);
        Vector3 farPoint = new(farWorld.X, farWorld.Y, farWorld.Z);

        return new Ray(origin, farPoint - origin);
    }

    /// <summary>
    /// Determines whether pickable.
    /// </summary>
    private static bool IsPickable(Mesh mesh)
    {
        if (mesh.Geometry == null)
            return false;
        if (mesh.BoundingBox == null)
            return false;

        return true;
    }

    /// <summary>
    /// Performs the pick mesh operation.
    /// </summary>
    private static void PickMesh(Mesh mesh, Ray ray, List<PickResult> results)
    {
        var wbb = mesh.BoundingBox;
        if (wbb == null)
            return;

        // 先做 AABB 快速剔除
        float? aabbT = ray.Intersects(wbb);
        if (!aabbT.HasValue)
            return;

        float bestT = aabbT.Value;
        bool hit = true;

        // 对三角形几何体进行精确的逐三角形检测
        if (mesh.Geometry != null
            && mesh.Geometry.PrimitiveType == Resources.PrimitiveType.Triangles
            && mesh.Geometry.VertexCount >= 3)
        {
            // 将射线变换到网格的局部空间
            Matrix4x4.Invert(mesh.WorldTransform, out Matrix4x4 invWorld);
            var localRay = TransformRay(ray, invWorld);

            // 对骨骼动画网格，使用 CPU 蒙皮后的顶点位置
            var positions = GetSkinnedPositions(mesh) ?? mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Position);

            float? triT = RayIntersectTriangles(localRay, mesh.Geometry, positions);
            if (triT.HasValue)
            {
                Vector3 localHit = localRay.GetPoint(triT.Value);
                Vector3 worldHit = Vector3.Transform(localHit, mesh.WorldTransform);
                bestT = (worldHit - ray.Origin).Length();
            }
            else
            {
                hit = false;
            }
        }

        if (hit)
        {
            Node pickNode = mesh.Parent is Model model ? model : mesh;
            results.Add(new PickResult
            {
                Node = pickNode,
                InstanceIndex = null,
                Distance = bestT,
                WorldPosition = ray.GetPoint(bestT)
            });
        }
    }

    /// <summary>
    /// Performs the pick instanced mesh operation.
    /// </summary>
    private static void PickInstancedMesh(InstancedMesh instancedMesh, Ray ray, List<PickResult> results)
    {
        bool hasTriangles = instancedMesh.PrimitiveType == Resources.PrimitiveType.Triangles
            && instancedMesh.VertexCount >= 3;

        int instanceCount = instancedMesh.InstanceCount;
        for (int i = 0; i < instanceCount; i++)
        {
            var wbb = instancedMesh.GetInstanceWorldBoundingBox(i);
            if (wbb == null)
                continue;

            // 先做 AABB 快速剔除
            float? aabbT = ray.Intersects(wbb);
            if (!aabbT.HasValue)
                continue;

            float bestT = aabbT.Value;
            bool hit = true;

            // 获取实例的世界变换矩阵
            var instanceTransform = instancedMesh.GetInstanceTransform(i);
            if (hasTriangles && instanceTransform.HasValue)
            {
                Matrix4x4.Invert(instanceTransform.Value, out Matrix4x4 invTransform);
                var localRay = TransformRay(ray, invTransform);

                // 在局部空间进行三角形检测
                var geometry = instancedMesh.GetGeometry();
                if (geometry != null)
                {
                    float? triT = RayIntersectTriangles(localRay, geometry);
                    if (triT.HasValue)
                    {
                        Vector3 localHit = localRay.GetPoint(triT.Value);
                        Vector3 worldHit = Vector3.Transform(localHit, instanceTransform.Value);
                        bestT = (worldHit - ray.Origin).Length();
                    }
                    else
                    {
                        hit = false;
                    }
                }
            }

            if (hit)
            {
                results.Add(new PickResult
                {
                    Node = instancedMesh,
                    InstanceIndex = i,
                    Distance = bestT,
                    WorldPosition = ray.GetPoint(bestT)
                });
            }
        }
    }

    /// <summary>
    /// Transforms the ray.
    /// </summary>
    private static Ray TransformRay(Ray ray, Matrix4x4 inverseTransform)
    {
        Vector3 localOrigin = Vector3.Transform(ray.Origin, inverseTransform);
        Vector3 localDir = Vector3.TransformNormal(ray.Direction, inverseTransform);
        return new Ray(localOrigin, localDir);
    }

    /// <summary>
    /// Performs the ray intersect triangles operation.
    /// </summary>
    private static float? RayIntersectTriangles(Ray localRay, Resources.Geometry geometry, IReadOnlyList<float>? positions = null)
    {
        positions ??= geometry.GetAttributeData(BuildInVertexAttribute.Position);
        if (positions == null || positions.Count < 9)
            return null;

        float closestT = float.MaxValue;
        bool anyHit = false;

        if (geometry.IndicesCount >= 3)
        {
            // 带索引的几何体
            var indices = geometry.Indices;
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                ReadVertex(positions, indices[i], out var v0);
                ReadVertex(positions, indices[i + 1], out var v1);
                ReadVertex(positions, indices[i + 2], out var v2);

                float? t = localRay.IntersectsTriangle(v0, v1, v2);
                if (t.HasValue && t.Value < closestT)
                {
                    closestT = t.Value;
                    anyHit = true;
                }
            }
        }
        else
        {
            // 无索引的几何体：顺序每 3 个顶点构成一个三角形
            int triCount = positions.Count / 9;
            for (int i = 0; i < triCount; i++)
            {
                int baseIdx = i * 9;
                var v0 = new Vector3(positions[baseIdx], positions[baseIdx + 1], positions[baseIdx + 2]);
                var v1 = new Vector3(positions[baseIdx + 3], positions[baseIdx + 4], positions[baseIdx + 5]);
                var v2 = new Vector3(positions[baseIdx + 6], positions[baseIdx + 7], positions[baseIdx + 8]);

                float? t = localRay.IntersectsTriangle(v0, v1, v2);
                if (t.HasValue && t.Value < closestT)
                {
                    closestT = t.Value;
                    anyHit = true;
                }
            }
        }

        return anyHit ? closestT : null;
    }

    private static void ReadVertex(IReadOnlyList<float> positions, uint index, out Vector3 vertex)
    {
        int i = (int)index * 3;
        vertex = new Vector3(positions[i], positions[i + 1], positions[i + 2]);
    }

    /// <summary>
    /// Gets the skinned positions.
    /// </summary>
    private static List<float>? GetSkinnedPositions(Mesh mesh)
    {
        if (!mesh.IsSkinnedMesh)
            return null;

        var skeleton = mesh.Skeleton;
        var sampler = mesh.AnimationSampler;
        if (skeleton == null)
            return null;

        var bindPositions = mesh.Geometry?.GetAttributeData(BuildInVertexAttribute.Position);
        var joints = mesh.Geometry?.GetAttributeData(BuildInVertexAttribute.Joints_0);
        var weights = mesh.Geometry?.GetAttributeData(BuildInVertexAttribute.Weights_0);

        if (bindPositions == null || joints == null || weights == null)
            return null;

        int vertexCount = bindPositions.Count / 3;
        int boneCount = skeleton.Bones.Count;
        if (boneCount == 0)
            return null;

        // 预计算每根骨骼的蒙皮矩阵（与 shader 中的 BoneMatrices 一致）
        Span<Matrix4x4> boneMatrices = boneCount <= 256
            ? stackalloc Matrix4x4[boneCount]
            : new Matrix4x4[boneCount];

        for (int i = 0; i < boneCount; i++)
        {
            if (sampler != null && i < sampler.BonesTransform.Count)
            {
                // 动画骨骼矩阵 = InverseWorldMatrix * AnimatedTransform
                boneMatrices[i] = skeleton.Bones[i].InverseWorldMatrix * sampler.BonesTransform[i];
            }
            else
            {
                // 无动画时使用绑定姿态：InverseWorldMatrix * WorldMatrix = Identity
                boneMatrices[i] = skeleton.Bones[i].InverseWorldMatrix * skeleton.Bones[i].WorldMatrix;
            }
        }

        var skinned = new List<float>(bindPositions.Count);

        for (int v = 0; v < vertexCount; v++)
        {
            // 读取 4 个骨骼索引和权重
            float w0 = v * 4 + 0 < weights.Count ? weights[v * 4 + 0] : 0;
            float w1 = v * 4 + 1 < weights.Count ? weights[v * 4 + 1] : 0;
            float w2 = v * 4 + 2 < weights.Count ? weights[v * 4 + 2] : 0;
            float w3 = v * 4 + 3 < weights.Count ? weights[v * 4 + 3] : 0;

            float sum = w0 + w1 + w2 + w3;
            if (sum < 0.0001f)
            {
                // 无有效权重，使用原始位置
                skinned.Add(bindPositions[v * 3]);
                skinned.Add(bindPositions[v * 3 + 1]);
                skinned.Add(bindPositions[v * 3 + 2]);
                continue;
            }

            // 归一化权重（与 shader 一致）
            w0 /= sum; w1 /= sum; w2 /= sum; w3 /= sum;

            int j0 = v * 4 + 0 < joints.Count ? (int)joints[v * 4 + 0] : 0;
            int j1 = v * 4 + 1 < joints.Count ? (int)joints[v * 4 + 1] : 0;
            int j2 = v * 4 + 2 < joints.Count ? (int)joints[v * 4 + 2] : 0;
            int j3 = v * 4 + 3 < joints.Count ? (int)joints[v * 4 + 3] : 0;

            var position = new Vector3(bindPositions[v * 3], bindPositions[v * 3 + 1], bindPositions[v * 3 + 2]);

            Vector3 skinnedPos = Vector3.Zero;
            if (w0 > 0 && j0 < boneCount)
                skinnedPos += w0 * Vector3.Transform(position, boneMatrices[j0]);
            if (w1 > 0 && j1 < boneCount)
                skinnedPos += w1 * Vector3.Transform(position, boneMatrices[j1]);
            if (w2 > 0 && j2 < boneCount)
                skinnedPos += w2 * Vector3.Transform(position, boneMatrices[j2]);
            if (w3 > 0 && j3 < boneCount)
                skinnedPos += w3 * Vector3.Transform(position, boneMatrices[j3]);

            // 如果所有骨骼权重都为 0（全部关节索引无效），回退到原始位置
            if (w0 <= 0 && w1 <= 0 && w2 <= 0 && w3 <= 0)
                skinnedPos = position;

            skinned.Add(skinnedPos.X);
            skinned.Add(skinnedPos.Y);
            skinned.Add(skinnedPos.Z);
        }

        return skinned;
    }
}
