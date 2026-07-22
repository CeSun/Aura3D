using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Defines the contract for render pipeline create instance.
/// </summary>
public interface IRenderPipelineCreateInstance
{
    /// <summary>
    /// Creates the instance.
    /// </summary>
    public abstract static RenderPipeline CreateInstance(Scene scene);
}

/// <summary>
/// Represents the render pipeline type.
/// </summary>
public abstract partial class RenderPipeline
{
    /// <summary>
    /// Initializes a new instance of the render pipeline type.
    /// </summary>
    public RenderPipeline(Scene scene)
    {
        this.Scene = scene;
        this.Settings = scene.PipelineSettings;
    }

    /// <summary>
    /// Gets the settings.
    /// </summary>
    public PipelineSettings Settings { get; }

    /// <summary>
    /// Gets the supports csm.
    /// </summary>
    public virtual bool SupportsCSM => false;

    /// <summary>
    /// Gets the enable frustum culling.
    /// </summary>
    public bool EnableFrustumCulling
    {
        get => Settings.EnableFrustumCulling;
        set => Settings.EnableFrustumCulling = value;
    }

    /// <summary>
    /// Gets or sets the scene.
    /// </summary>
    public Scene Scene { get; private set; }

    /// <summary>
    /// Gets the meshes.
    /// </summary>
    public List<Mesh> Meshes { get; } = new List<Mesh>();


    /// <summary>
    /// Gets the instanced meshes.
    /// </summary>
    public List<InstancedMesh> InstancedMeshes { get; } = new List<InstancedMesh>();

    /// <summary>
    /// Gets the particle systems.
    /// </summary>
    public List<ParticleSystem> ParticleSystems { get; } = new List<ParticleSystem>();

    /// <summary>
    /// Gets the cameras.
    /// </summary>
    public List<Camera> Cameras { get; } = new List<Camera>();

    /// <summary>
    /// Gets the point lights.
    /// </summary>
    public List<PointLight> PointLights { get; } = new List<PointLight>();

    /// <summary>
    /// Gets the spot lights.
    /// </summary>
    public List<SpotLight> SpotLights { get; } = new List<SpotLight>();

    /// <summary>
    /// Gets the directional lights.
    /// </summary>
    public List<DirectionalLight> DirectionalLights { get; } = new List<DirectionalLight>();

    /// <summary>
    /// Gets or sets the gl.
    /// </summary>
    public GL? gl { get; protected set; }


    /// <summary>
    /// Gets the every camera render passes.
    /// </summary>
    public List<RenderPass> EveryCameraRenderPasses { get; } = new List<RenderPass>();

    /// <summary>
    /// Gets the once render passes.
    /// </summary>
    public List<RenderPass> OnceRenderPasses { get; } = new List<RenderPass>();

    private HashSet<IGpuState> GpuStates { get; } = new HashSet<IGpuState>();

    private ConditionalWeakTable<Material, MaterialGpuState> materialGpuStates = new ConditionalWeakTable<Material, MaterialGpuState>();
    private ConditionalWeakTable<BoneMatrixBuffer, BoneMatrixBufferGpuState> boneMatrixBufferGpuStates = new ConditionalWeakTable<BoneMatrixBuffer, BoneMatrixBufferGpuState>();
    private ConditionalWeakTable<Geometry, GeometryGpuState> geometryGpuStates = new ConditionalWeakTable<Geometry, GeometryGpuState>();
    private ConditionalWeakTable<InstancedGeometry, InstancedGeometryGpuState> instancedGeometryGpuStates = new ConditionalWeakTable<InstancedGeometry, InstancedGeometryGpuState>();

    private ConditionalWeakTable<Resources.Texture, TextureGpuState> textureGpuStates = new ConditionalWeakTable<Resources.Texture, TextureGpuState>();
    private ConditionalWeakTable<WritableTexture, WritableTextureGpuState> writableTextureGpuStates = new ConditionalWeakTable<WritableTexture, WritableTextureGpuState>();
    private ConditionalWeakTable<CubeTexture, CubeTextureGpuState> cubeTextureGpuStates = new ConditionalWeakTable<CubeTexture, CubeTextureGpuState>();

    private RenderTargetHandle? debugOutputHandle;

    /// <summary>
    /// Gets the directional light limit.
    /// </summary>
    public int DirectionalLightLimit
    {
        get => Settings.DirectionalLightLimit;
        set => Settings.DirectionalLightLimit = value;
    }

    /// <summary>
    /// Gets the point light limit.
    /// </summary>
    public int PointLightLimit
    {
        get => Settings.PointLightLimit;
        set => Settings.PointLightLimit = value;
    }

    /// <summary>
    /// Gets the spot light limit.
    /// </summary>
    public int SpotLightLimit
    {
        get => Settings.SpotLightLimit;
        set => Settings.SpotLightLimit = value;
    }

    private int lastDirectionalLightLimit;

    private int lastPointLightLimit;

    private int lastSpotLightLimit;

    /// <summary>
    /// Occurs when light limit changed event is raised.
    /// </summary>
    protected event Action<int, int, int>? LightLimitChangedEvent;

    /// <summary>
    /// Gets the visible meshes in camera.
    /// </summary>
    public IReadOnlyList<Mesh> VisibleMeshesInCamera => _visibleMeshesInCamera;
    private readonly List<Mesh> _visibleMeshesInCamera = [];

    /// <summary>
    /// Gets the visible instanced meshes in camera.
    /// </summary>
    public IReadOnlyList<InstancedMesh> VisibleInstancedMeshesInCamera => _visibleInstancedMeshesInCamera;
    private readonly List<InstancedMesh> _visibleInstancedMeshesInCamera = [];


    /// <summary>
    /// Performs the register render pass operation.
    /// </summary>
    protected void RegisterRenderPass(RenderPass renderPass, RenderPassGroup renderPassGroup)
    {
        if (renderPassGroup == RenderPassGroup.EveryCamera)
            EveryCameraRenderPasses.Add(renderPass);
        else if (renderPassGroup == RenderPassGroup.Once)
            OnceRenderPasses.Add(renderPass);
    }

    /// <summary>
    /// Performs the register debug pass operation.
    /// </summary>
    protected void RegisterDebugPass(RenderTargetHandle? depthRenderTarget = null)
    {
        debugOutputHandle ??= RegisterRenderTarget("DebugOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderPass(
            new DebugDrawPass(this, debugOutputHandle, depthRenderTarget).SetOutput(CameraOutput),
            RenderPassGroup.EveryCamera);
    }

    /// <summary>
    /// Specifies values for render pass group.
    /// </summary>
    public enum RenderPassGroup
    {
        /// <summary>
        /// Specifies once.
        /// </summary>
        Once,
        /// <summary>
        /// Specifies every camera.
        /// </summary>
        EveryCamera,
    }

    /// <summary>
    /// Initializes the associated data.
    /// </summary>
    public void Initialize(Func<string, nint> getProcAddressFunctionPtr)
    {
        gl = GL.GetApi(getProcAddressFunctionPtr);

        Setup();

        foreach (var renderPass in EveryCameraRenderPasses)
        {
            renderPass.Setup();
        }
        foreach (var renderPass in OnceRenderPasses)
        {
            renderPass.Setup();
        }
    }

    /// <summary>
    /// Sets the up.
    /// </summary>
    public virtual void Setup()
    {

    }

    /// <summary>
    /// Ensures the synced.
    /// </summary>
    public void EnsureSynced(IGpuState resource)
    {
        GpuStates.Add(resource);

        if (resource.SyncedVersion != resource.Version)
        {
            resource.Upload(gl!);
        }
    }

    internal void RemoveGpuState(IGpuState gpuState)
    {
        GpuStates.Remove(gpuState);
    }

    /// <summary>
    /// Gets the material gpu state.
    /// </summary>
    public MaterialGpuState GetMaterialGpuState(Material material)
    {
        if (materialGpuStates.TryGetValue(material, out var gpuState) == false)
        {
            gpuState = new MaterialGpuState(material);
            materialGpuStates.Add(material, gpuState);
            GpuStates.Add(gpuState);
        }

        if (gl != null && gpuState.SyncedVersion != material.Version)
        {
            gpuState.Destroy(gl);
            gpuState.Upload(gl);
        }

        return gpuState;
    }

    internal BoneMatrixBufferGpuState GetBoneMatrixBufferGpuState(BoneMatrixBuffer boneMatrixBuffer)
    {
        if (boneMatrixBufferGpuStates.TryGetValue(boneMatrixBuffer, out var gpuState) == false)
        {
            gpuState = new BoneMatrixBufferGpuState(boneMatrixBuffer);
            boneMatrixBufferGpuStates.Add(boneMatrixBuffer, gpuState);
            GpuStates.Add(gpuState);
        }

        return gpuState;
    }

    internal GeometryGpuState GetGeometryGpuState(Geometry geometry)
    {
        if (geometryGpuStates.TryGetValue(geometry, out var gpuState) == false)
        {
            gpuState = new GeometryGpuState(geometry);
            geometryGpuStates.Add(geometry, gpuState);
            GpuStates.Add(gpuState);
        }

        return gpuState;
    }
    internal InstancedGeometryGpuState GetInstancedGeometryGpuState(InstancedGeometry geometry)
    {
        if (instancedGeometryGpuStates.TryGetValue(geometry, out var gpuState) == false)
        {
            gpuState = new InstancedGeometryGpuState(geometry);
            instancedGeometryGpuStates.Add(geometry, gpuState);
            GpuStates.Add(gpuState);
        }

        return gpuState;
    }


    internal TextureGpuState GetTextureGpuState(Resources.Texture texture)
    {
        if (texture is WritableTexture writableTexture)
        {
            return GetWritableTextureGpuState(writableTexture);
        }

        if (texture is RenderTexture renderTexture)
        {
            return GetRenderTargetTextureGpuState(renderTexture);
        }

        if (textureGpuStates.TryGetValue(texture, out var gpuState) == false)
        {
            gpuState = new TextureGpuState(texture);
            textureGpuStates.Add(texture, gpuState);
            GpuStates.Add(gpuState);
        }

        return gpuState;
    }

    internal TextureGpuState GetRenderTargetTextureGpuState(RenderTexture texture)
    {
        texture.CachedGpuState ??= new RenderTargetTextureGpuState(texture);
        return texture.CachedGpuState;
    }

    internal WritableTextureGpuState GetWritableTextureGpuState(WritableTexture texture)
    {
        if (writableTextureGpuStates.TryGetValue(texture, out var gpuState) == false)
        {
            gpuState = new WritableTextureGpuState(texture, Settings.DepthFormat);
            writableTextureGpuStates.Add(texture, gpuState);
            GpuStates.Add(gpuState);
        }

        return gpuState;
    }

    internal CubeTextureGpuState GetCubeTextureGpuState(CubeTexture texture)
    {
        if (texture is RenderCubeTexture renderTexture)
        {
            return GetRenderTargetCubeTextureGpuState(renderTexture);
        }

        if (cubeTextureGpuStates.TryGetValue(texture, out var gpuState) == false)
        {
            gpuState = new CubeTextureGpuState(texture);
            cubeTextureGpuStates.Add(texture, gpuState);
            GpuStates.Add(gpuState);
        }

        return gpuState;
    }

    internal CubeTextureGpuState GetRenderTargetCubeTextureGpuState(RenderCubeTexture texture)
    {
        texture.CachedGpuState ??= new CubeRenderTargetTextureGpuState(texture);
        return texture.CachedGpuState;
    }

    /// <summary>
    /// Performs the collect unused gpu states operation.
    /// </summary>
    public void CollectUnusedGpuStates()
    {
        if (gl == null)
            return;

        List<IGpuState> unusedGpuStates = [];

        foreach (var gpuState in GpuStates)
        {
            if (gpuState is IResourceGpuState resourceGpuState)
            {
                if (resourceGpuState.IsAlive == false)
                {
                    unusedGpuStates.Add(gpuState);
                }
            }
        }

        foreach (var gpuState in unusedGpuStates)
        {
            gpuState.Destroy(gl);
            GpuStates.Remove(gpuState);
        }
    }

    /// <summary>
    /// Ensures the synced.
    /// </summary>
    public TextureGpuState EnsureSynced(Resources.Texture texture)
    {
        var gpuState = GetTextureGpuState(texture);

        if (gpuState.TextureId == 0 || gpuState.SyncedVersion != texture.Version)
        {
            gpuState.Upload(gl!);
        }

        return gpuState;
    }

    /// <summary>
    /// Ensures the synced.
    /// </summary>
    public CubeTextureGpuState EnsureSynced(CubeTexture texture)
    {
        var gpuState = GetCubeTextureGpuState(texture);

        if (gpuState.TextureId == 0 || gpuState.SyncedVersion != texture.Version)
        {
            gpuState.Upload(gl!);
        }

        return gpuState;
    }

    internal GeometryGpuState EnsureSynced(Geometry geometry)
    {
        var gpuState = GetGeometryGpuState(geometry);

        if (gpuState.Vao == 0 || gpuState.SyncedVersion != geometry.Version)
        {
            gpuState.Upload(gl!);
        }

        return gpuState;
    }
    /// <summary>
    /// Ensures the synced.
    /// </summary>
    internal InstancedGeometryGpuState EnsureSynced(InstancedMesh instancedMesh)
    {
        var geometry = instancedMesh.GetGeometry() as InstancedGeometry;
        if (geometry == null)
            throw Aura3D.Core.Exceptions.RendererErrors.MissingInstancedGeometry();

        var gpuState = GetInstancedGeometryGpuState(geometry);

        if (gpuState.Vao == 0 || gpuState.SyncedVersion != geometry.Version)
        {
            gpuState.Upload(gl!);
        }

        return gpuState;
    }


    /// <summary>
    /// Performs the sync and bind bone matrix buffer operation.
    /// </summary>
    public void SyncAndBindBoneMatrixBuffer(BoneMatrixBuffer boneMatrixBuffer)
    {
        var gpuState = GetBoneMatrixBufferGpuState(boneMatrixBuffer);

        if (gpuState.BufferId == 0 || gpuState.SyncedVersion != boneMatrixBuffer.Version)
        {
            gpuState.Upload(gl!);
        }

        gpuState.Bind(gl!);
    }

    /// <summary>
    /// Adds the node.
    /// </summary>
    public void AddNode(Node node)
    {
        switch (node)
        {
            case Mesh mesh:
                Meshes.Add(mesh);
                break;
            case InstancedMesh instancedMesh:
                InstancedMeshes.Add(instancedMesh);
                break;
            case Camera camera:
                Cameras.Add(camera);
                break;
            case PointLight pointLight:
                PointLights.Add(pointLight);
                break;
            case SpotLight spotLight:
                SpotLights.Add(spotLight);
                break;
            case DirectionalLight directionalLight:
                DirectionalLights.Add(directionalLight);
                break;
            case ParticleSystem particleSystem:
                ParticleSystems.Add(particleSystem);
                break;
        }
    }

    /// <summary>
    /// Removes the node.
    /// </summary>
    public void RemoveNode(Node node)
    {
        switch (node)
        {
            case Mesh mesh:
                Meshes.Remove(mesh);
                break;
            case InstancedMesh instancedMesh:
                InstancedMeshes.Remove(instancedMesh);
                break;
            case Camera camera:
                Cameras.Remove(camera);
                break;
            case PointLight pointLight:
                PointLights.Remove(pointLight);
                break;
            case SpotLight spotLight:
                SpotLights.Remove(spotLight);
                break;
            case DirectionalLight directionalLight:
                DirectionalLights.Remove(directionalLight);
                break;
            case ParticleSystem particleSystem:
                ParticleSystems.Remove(particleSystem);
                break;
        }
    }

    private void UpdateLightLimit()
    {
        if (lastPointLightLimit != PointLightLimit || lastSpotLightLimit != SpotLightLimit || lastDirectionalLightLimit != DirectionalLightLimit)
        {
            lastPointLightLimit = PointLightLimit;
            lastSpotLightLimit = SpotLightLimit;
            lastDirectionalLightLimit = DirectionalLightLimit;
            LightLimitChangedEvent?.Invoke(lastDirectionalLightLimit, lastPointLightLimit, lastSpotLightLimit);
        }
    }

    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public virtual void Render()
    {
        UpdateRenderTargetsLRU();
        UpdateLightLimit();

        BeforeRender();
        foreach (var renderPass in OnceRenderPasses)
        {
            renderPass.BeforeRender();
            renderPass.Render();
            renderPass.AfterRender();
        }

        foreach (var camera in Cameras)
        {
            if (camera.Enable == false)
                continue;

            _visibleMeshesInCamera.Clear();
            _visibleInstancedMeshesInCamera.Clear();
            if (EnableFrustumCulling == true)
            {
                UpdateVisibleMeshesInCamera(camera.View, camera.Projection, _visibleMeshesInCamera);
                UpdateVisibleInstancedMeshesInCamera(camera.View, camera.Projection, _visibleInstancedMeshesInCamera);
            }
            else
            {
                _visibleMeshesInCamera.AddRange(Meshes);
                _visibleInstancedMeshesInCamera.AddRange(InstancedMeshes);
            }

            BeforeCameraRender(camera);
            foreach (var renderPass in EveryCameraRenderPasses)
            {
                renderPass.BeforeRender(camera);
                renderPass.Render(camera);
                renderPass.AfterRender(camera);
            }
            AfterCameraRender(camera);
            PresentCameraOutput(camera);
        }
        AfterRender();
    }

    private Plane[] planes = new Plane[6];

    /// <summary>
    /// Updates the visible meshes in camera.
    /// </summary>
    public void UpdateVisibleMeshesInCamera(Matrix4x4 view, Matrix4x4 projection, List<Mesh> meshes)
    {
        var viewProjection = view * projection;

        Matrix4x4.Invert(viewProjection, out Matrix4x4 invViewProj);

        Span<Vector3> ndcCorners = stackalloc Vector3[]
        {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1),
            new Vector3(-1, 1,-1), new Vector3(1, 1,-1),
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1),
            new Vector3(-1, 1, 1), new Vector3(1, 1, 1)
        };

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var c in ndcCorners)
        {
            Vector4 p = new Vector4(c, 1.0f);
            Vector4 world = Vector4.Transform(p, invViewProj);
            world /= world.W;

            Vector3 wpos = new Vector3(world.X, world.Y, world.Z);
            min = Vector3.Min(min, wpos);
            max = Vector3.Max(max, wpos);
        }

        var cameraBoundingBox = new BoundingBox(min, max);

        MatrixHelper.ExtractPlanes(viewProjection, planes);

        // 八叉树查询（所有网格统一走八叉树）
        this.Scene.MeshOctree.Query(boundingBox =>
        {
            if (cameraBoundingBox.Intersects(boundingBox))
            {
                if (boundingBox.IsBoxInsideFrustum(planes))
                {
                    return true;
                }
            }
            return false;

        }, meshes);
    }

    /// <summary>
    /// Updates the visible instanced meshes in camera.
    /// </summary>
    public void UpdateVisibleInstancedMeshesInCamera(Matrix4x4 view, Matrix4x4 projection, List<InstancedMesh> instancedMeshes)
    {
        var viewProjection = view * projection;
        MatrixHelper.ExtractPlanes(viewProjection, planes);

        foreach (var im in InstancedMeshes)
        {
            if (im.Enable == false)
                continue;
            if (im.InstanceCount == 0)
                continue;
            if (im.EnableFrustumCulling == false)
            {
                instancedMeshes.Add(im);
                continue;
            }
            if (im.IsInsideFrustum(planes))
            {
                instancedMeshes.Add(im);
            }
        }
    }

    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public virtual void BeforeRender()
    {

    }

    /// <summary>
    /// Performs the after render operation.
    /// </summary>
    public virtual void AfterRender()
    {

    }

    /// <summary>
    /// Performs the before camera render operation.
    /// </summary>
    public virtual void BeforeCameraRender(Camera camera)
    {

    }

    /// <summary>
    /// Performs the after camera render operation.
    /// </summary>
    public virtual void AfterCameraRender(Camera camera)
    {
    }

    /// <summary>
    /// Gets the camera framebuffer id.
    /// </summary>
    public unsafe uint GetCameraFramebufferId(Camera camera)
    {
        if (gl == null)
            return 0;

        if (camera.OutputTexture == null)
        {
            return Scene.DefaultOutputSurface?.FrameBufferId ?? 0;
        }

        var outputTexture = EnsureCameraOutputTexture(camera);
        var gpuState = (WritableTextureGpuState)GetTextureGpuState(outputTexture);
        if (gpuState.TextureId == 0)
        {
            gpuState.Upload(gl!);
        }
        if (gpuState.FramebufferId == 0)
        {
            throw Aura3D.Core.Exceptions.RendererErrors.FramebufferNotCreated(nameof(WritableTexture));
        }

        return gpuState.FramebufferId;
    }

    private WritableTexture EnsureCameraOutputTexture(Camera camera)
    {
        if (camera.OutputTexture == null)
        {
            throw Aura3D.Core.Exceptions.RendererErrors.CameraOutputTextureNotSet();
        }

        if (camera.OutputTexture.Width == 0 || camera.OutputTexture.Height == 0)
        {
            var outputSurface = Scene.DefaultOutputSurface
                ?? throw Aura3D.Core.Exceptions.RendererErrors.DefaultOutputSurfaceNotSet();

            camera.OutputTexture.SetSize(outputSurface.Width, outputSurface.Height);
        }

        return camera.OutputTexture;
    }

    private void PresentCameraOutput(Camera camera)
    {
        if (gl == null)
            return;

        if (camera.OutputTexture == null)
            return;

        var outputSurface = Scene.DefaultOutputSurface;

        if (outputSurface == null)
            return;

        uint width = camera.Width;
        uint height = camera.Height;

        if (width == 0 || height == 0)
            return;

        gl.BindFramebuffer(GLEnum.ReadFramebuffer, GetCameraFramebufferId(camera));
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, outputSurface.FrameBufferId);
        gl.BlitFramebuffer(
            0, 0, (int)width, (int)height,
            0, 0, (int)outputSurface.Width, (int)outputSurface.Height,
            ClearBufferMask.ColorBufferBit,
            GLEnum.Nearest);
    }

    /// <summary>
    /// Performs the sort meshes operation.
    /// </summary>
    public virtual void SortMeshes(IReadOnlyList<Mesh> Meshes, Camera camera)
    {
        var m = camera.View;

        if (Meshes is not List<Mesh> list)
            return;
        list.Sort((mesh1, mesh2) =>
        {
            var location1 = Vector3.Transform(mesh1.Position, mesh1.WorldTransform * m);

            var location2 = Vector3.Transform(mesh2.Position, mesh2.WorldTransform * m);

            var l1 = location1.Length();

            var l2 = location2.Length();

            return (l1).CompareTo(l2);

        });
    }

    private InternalCube? _internalCube;

    private InternalQuad? _internalQuad;

    /// <summary>
    /// Renders the cube.
    /// </summary>
    public void RenderCube()
    {
        if (gl == null)
            return;
        if (_internalCube == null)
        {
            _internalCube = new InternalCube();
        }
        GpuStates.Add(_internalCube);
        if (_internalCube.Vao == 0)
        {
            _internalCube.Upload(gl);
        }
        gl.BindVertexArray(_internalCube.Vao);
        gl.DrawArrays(GLEnum.Triangles, 0, 36);
    }

    /// <summary>
    /// Renders the quad.
    /// </summary>
    public unsafe void RenderQuad()
    {
        if (gl == null)
            return;
        if (_internalQuad == null)
        {
            _internalQuad = new InternalQuad();
        }
        GpuStates.Add(_internalQuad);
        if (_internalQuad.Vao == 0)
        {
            _internalQuad.Upload(gl);
        }
        gl.BindVertexArray(_internalQuad.Vao);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
    }

    /// <summary>
    /// Destroys the associated data.
    /// </summary>
    public virtual void Destroy()
    {
        foreach (var pass in OnceRenderPasses)
        {
            pass.Destroy();
        }
        foreach (var pass in EveryCameraRenderPasses)
        {
            pass.Destroy();
        }

        foreach (var gpuState in GpuStates)
        {
            gpuState.Destroy(gl!);
        }
        GpuStates.Clear();
        materialGpuStates = new ConditionalWeakTable<Material, MaterialGpuState>();
        boneMatrixBufferGpuStates = new ConditionalWeakTable<BoneMatrixBuffer, BoneMatrixBufferGpuState>();
        geometryGpuStates = new ConditionalWeakTable<Geometry, GeometryGpuState>();
        textureGpuStates = new ConditionalWeakTable<Resources.Texture, TextureGpuState>();
        writableTextureGpuStates = new ConditionalWeakTable<WritableTexture, WritableTextureGpuState>();
        cubeTextureGpuStates = new ConditionalWeakTable<CubeTexture, CubeTextureGpuState>();

        Meshes.Clear();

        Cameras.Clear();

        PointLights.Clear();

        SpotLights.Clear();
    }
}
